using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ArenaCombat.Core.Skill;
using ArenaCombat.Core.Network;

namespace ArenaCombat.Core.AI
{
    // Lifecycle managed via PlayerBiasTracker forwarding:
    // RegisterPlayer/UnregisterPlayer are called from PlayerBiasTracker shims
    // (see PlayerBiasTracker.RegisterPlayer/UnregisterPlayer) so action sites
    // in PNC3D do not need new call sites. RecordX hooks similarly forward.
    [DisallowMultipleComponent]
    public class PlayerArchetypeClassifier : MonoBehaviour
    {
        public static PlayerArchetypeClassifier Instance { get; private set; }

        [Header("Distance Thresholds (meters)")]
        [SerializeField] float _meleeDistance  = 5.0f;
        [SerializeField] float _rangedDistance = 10.0f;

        [Header("Eval Cycle")]
        [SerializeField] float _evalIntervalSec          = 180f;
        [SerializeField] float _passiveSampleIntervalSec = 0.5f;
        [SerializeField, Range(0f, 1f)] float _weightDecayOnEval = 0.3f;

        [Header("Classification Thresholds (%)")]
        [SerializeField] float _dominantPercent       = 55f;
        [SerializeField] float _semiDominantPercent   = 45f;
        [SerializeField] float _secondaryGuardPercent = 30f;
        [SerializeField] float _minTotalWeight        = 5.0f;

        [Header("Weight Values")]
        [SerializeField] float _meleeHitWeight        = 1.0f;
        [SerializeField] float _ccCastWeight          = 1.5f;
        [SerializeField] float _parryWeight           = 2.0f;
        [SerializeField] float _passiveDistanceWeight = 0.05f;
        [SerializeField] float _slotCCBias            = 0.5f;

        class ArchetypeData
        {
            public float[] weights = new float[3]; // [Melee, Ranged, CC]
            public PlayerArchetype current = PlayerArchetype.Hybrid;
        }
        readonly Dictionary<ulong, ArchetypeData> _data = new();

        float _nextEvalTime;
        float _nextPassiveSampleTime;

        // Batched event payloads — collected during EvaluateAll, fired after the
        // dictionary iteration completes (avoids InvalidOperationException if a
        // subscriber mutates _data).
        readonly List<(ulong clientId, PlayerArchetype oldType, PlayerArchetype newType)> _pendingChanges = new();

        public event Action<ulong, PlayerArchetype, PlayerArchetype> OnPlayerArchetypeChanged;

        void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(this); return; }

            _nextEvalTime          = Time.time + _evalIntervalSec;
            _nextPassiveSampleTime = Time.time;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        public void RegisterPlayer(ulong clientId)
        {
            if (IsServer) _data.TryAdd(clientId, new ArchetypeData());
        }

        public void UnregisterPlayer(ulong clientId)
        {
            if (IsServer) _data.Remove(clientId);
        }

        public void RecordMeleeHit(ulong clientId, float distToBoss)
        {
            if (!IsServer || float.IsNaN(distToBoss)) return;
            if (!_data.TryGetValue(clientId, out var d)) return;
            if (distToBoss < _meleeDistance) d.weights[0] += _meleeHitWeight;
            else                              d.weights[1] += 0.5f;
        }

        public void RecordSkillCast(ulong clientId, SkillDefinition skill, float distToBoss)
        {
            if (!IsServer || skill == null || float.IsNaN(distToBoss)) return;
            if (!_data.TryGetValue(clientId, out var d)) return;

            bool isCC = skill.RoleTags != null && System.Array.Exists(skill.RoleTags,
                t => t == SkillRoleTag.CC || t == SkillRoleTag.Silence);
            bool isRanged = skill.Range > 8f || distToBoss > _rangedDistance;
            bool isMelee = distToBoss < _meleeDistance;

            // Independent conditions — one cast may credit multiple buckets.
            if (isCC)     d.weights[2] += _ccCastWeight;
            if (isRanged) d.weights[1] += 1.0f;
            if (isMelee)  d.weights[0] += 1.0f;
        }

        public void RecordParrySuccess(ulong clientId)
        {
            if (!IsServer || !_data.TryGetValue(clientId, out var d)) return;
            d.weights[0] += _parryWeight;
        }

        public PlayerArchetype GetArchetype(ulong clientId)
            => _data.TryGetValue(clientId, out var d) ? d.current : PlayerArchetype.Hybrid;

        public bool TryGetWeights(ulong clientId, out float melee, out float ranged, out float cc)
        {
            if (_data.TryGetValue(clientId, out var d))
            {
                melee = d.weights[0]; ranged = d.weights[1]; cc = d.weights[2];
                return true;
            }
            melee = ranged = cc = 0f;
            return false;
        }

        void FixedUpdate()
        {
            if (!IsServer) return;

            if (Time.time >= _nextPassiveSampleTime)
            {
                SamplePassiveDistances();
                _nextPassiveSampleTime = Time.time + _passiveSampleIntervalSec;
            }

            if (Time.time >= _nextEvalTime)
            {
                EvaluateAll();
                _nextEvalTime = Time.time + _evalIntervalSec;
            }
        }

        void SamplePassiveDistances()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            if (BossManager.Instance == null || BossManager.Instance.CurrentBoss == null) return;
            Vector3 bossPos = BossManager.Instance.CurrentBoss.transform.position;
            foreach (var kvp in _data)
            {
                ulong id = kvp.Key;
                if (!nm.ConnectedClients.TryGetValue(id, out var nc) || nc.PlayerObject == null) continue;
                float dist = Vector3.Distance(nc.PlayerObject.transform.position, bossPos);
                if (dist < _meleeDistance)        kvp.Value.weights[0] += _passiveDistanceWeight;
                else if (dist > _rangedDistance)  kvp.Value.weights[1] += _passiveDistanceWeight;
            }
        }

        void EvaluateAll()
        {
            var nm = NetworkManager.Singleton;
            _pendingChanges.Clear();

            foreach (var kvp in _data)
            {
                ulong clientId = kvp.Key;
                var d = kvp.Value;

                // Fresh slot CC bias from current loadout state (NOT stored — recomputed each eval).
                float slotCCBonus = ComputeSlotCCBonus(nm, clientId);

                float m = d.weights[0];
                float r = d.weights[1];
                float c = d.weights[2] + slotCCBonus;
                var newType = Classify(m, r, c);

                float total = m + r + c;
                Debug.Log($"[Archetype] client={clientId} M={m:F1} R={r:F1} C={c:F1} (slotCC={slotCCBonus:F1}, total={total:F1}) → {newType}");

                if (newType != d.current)
                {
                    _pendingChanges.Add((clientId, d.current, newType));
                    d.current = newType;
                }

                // Decay stored signals (slot bonus is recomputed, not stored).
                d.weights[0] *= _weightDecayOnEval;
                d.weights[1] *= _weightDecayOnEval;
                d.weights[2] *= _weightDecayOnEval;
            }

            // Fire batched events AFTER iteration so subscribers may safely mutate _data.
            for (int i = 0; i < _pendingChanges.Count; i++)
            {
                var ch = _pendingChanges[i];
                OnPlayerArchetypeChanged?.Invoke(ch.clientId, ch.oldType, ch.newType);
            }
            _pendingChanges.Clear();
        }

        float ComputeSlotCCBonus(NetworkManager nm, ulong clientId)
        {
            if (nm == null) return 0f;
            if (!nm.ConnectedClients.TryGetValue(clientId, out var nc) || nc.PlayerObject == null) return 0f;
            var skillMgr = nc.PlayerObject.GetComponentInChildren<SkillManager>();
            if (skillMgr == null) return 0f;
            float bonus = 0f;
            foreach (var slot in skillMgr.Slots)
            {
                if (slot == null || slot.RoleTags == null) continue;
                if (System.Array.Exists(slot.RoleTags, t => t == SkillRoleTag.CC || t == SkillRoleTag.Silence))
                    bonus += _slotCCBias;
            }
            return bonus;
        }

        PlayerArchetype Classify(float m, float r, float c)
        {
            float total = m + r + c;
            if (total < _minTotalWeight) return PlayerArchetype.Hybrid;

            float mPct = m / total * 100f;
            float rPct = r / total * 100f;
            float cPct = c / total * 100f;

            // Find top and second-place percentages with deterministic tie-break (M > R > C).
            PlayerArchetype topType = PlayerArchetype.Melee;
            float topPct = mPct;
            float secondPct = 0f;

            if (rPct > topPct) { secondPct = topPct; topPct = rPct; topType = PlayerArchetype.Ranged; }
            else                 secondPct = Mathf.Max(secondPct, rPct);

            if (cPct > topPct) { secondPct = topPct; topPct = cPct; topType = PlayerArchetype.CC; }
            else if (cPct > secondPct) secondPct = cPct;

            if (topPct >= _dominantPercent) return topType;
            if (topPct >= _semiDominantPercent && secondPct < _secondaryGuardPercent) return topType;
            return PlayerArchetype.Hybrid;
        }
    }
}
