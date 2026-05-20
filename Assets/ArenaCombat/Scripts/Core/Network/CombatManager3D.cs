// ARCH TAG: ACTIVE_3D
// ARCH SCOPE: 3D combat hub — registry (B1-3), Physics.OverlapBox hit logic (B1-4), K/D/A + atomic Die() flip (B1-5).
// ARCH STATUS: B1 COMPLETE. CombatManager3D fully owns 3D combat including K/D/A. Legacy CombatManager.cs untouched.

using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ArenaCombat.Core.Combat;

namespace ArenaCombat.Core.Network
{
    /// <summary>
    /// 3D combat hub. Server-authoritative. After B1-5, owns full 3D combat surface:
    /// - Player registry (B1-3)
    /// - Attack/Parry resolution via Physics.OverlapBox + LayerMask filter + dedup + parry handling (B1-4)
    /// - Game-side cooldown enforcement (AttackData3D.Cooldown) (B1-4)
    /// - K/D/A bookkeeping with assist window + threshold (B1-5)
    /// - PlayerKilled3DRpc for kill feed UI consumers
    ///
    /// Coexists with legacy CombatManager (Instance singleton) without cross-reference.
    /// CombatManager.cs is intentionally untouched per architectural decision D1.
    /// Phase D1 (legacy 2D removal) will delete CombatManager without affecting this class.
    ///
    /// Death contract (B1-5): K/D/A counters move only on combat death (HP-zero via TakeDamage).
    /// Kill-zone fall calls Die(self) → scored death (deaths++ only, no kill credit). BF-1a.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class CombatManager3D : NetworkBehaviour
    {
        public static CombatManager3D Instance { get; private set; }

        [Header("=== Attack Data ===")]
        [Tooltip("Inspector-assigned attack definitions. Each AttackType should have at most one entry. Duplicates are logged and ignored.")]
        [SerializeField] private List<AttackData3D> attackTable = new List<AttackData3D>();

        [Header("=== Hit Detection ===")]
        [Tooltip("LayerMask filter for Physics.OverlapBox. Should match the layer player hurtbox colliders are on. If empty, attacks silently miss.")]
        [SerializeField] private LayerMask playerLayer;

        [Tooltip("Y offset added to attack hitbox center; accounts for player capsule center vs ground origin.")]
        [SerializeField, Min(0f)] private float attackVerticalOffset = 0.5f;

        [Tooltip("Defensive cap on hitTargets payload size to bound RPC wire cost. Damage still applies to all eligible targets.")]
        [SerializeField, Min(1)] private int maxHitTargets = 8;

        [Header("=== K/D/A & Assists ===")]
        [Tooltip("Damage attribution counts as assist if delivered within this many seconds of the victim's death.")]
        [SerializeField, Min(0f)] private float assistWindow3D = 10f;

        [Tooltip("Minimum cumulative damage from one attacker required to count as an assist on the victim.")]
        [SerializeField, Min(0f)] private float assistDamageThreshold = 10f;

        // Per-victim damage attribution: cumulative damage + most-recent timestamp per attacker.
        // Used by ComputeAssisters3D to filter by assistWindow3D + assistDamageThreshold at death.
        private struct DamageAttribution
        {
            public float CumulativeDamage;
            public float LastDamageTime;
        }

        // Player registry (server-only access pattern)
        private readonly Dictionary<ulong, PlayerNetworkController3D> players3D = new Dictionary<ulong, PlayerNetworkController3D>();

        // Game-side per-attacker per-AttackType cooldown tracking (separate from InputValidator rate limit)
        private readonly Dictionary<ulong, Dictionary<AttackType, float>> attackCooldowns3D = new Dictionary<ulong, Dictionary<AttackType, float>>();

        // Per-player K/D/A counters (server-only). Survive UnregisterPlayer3D for session display.
        private readonly Dictionary<ulong, int> kills3D = new Dictionary<ulong, int>();
        private readonly Dictionary<ulong, int> deaths3D = new Dictionary<ulong, int>();
        private readonly Dictionary<ulong, int> assists3D = new Dictionary<ulong, int>();

        // Per-victim damage attribution log: victimId → (attackerId → DamageAttribution)
        private readonly Dictionary<ulong, Dictionary<ulong, DamageAttribution>> recentDamage3D = new Dictionary<ulong, Dictionary<ulong, DamageAttribution>>();

        // Attack table lookup cache, built lazily from attackTable
        private Dictionary<AttackType, AttackData3D> attackLookup;

        private readonly HashSet<AttackType> _warnedAppliedStatusTypes = new HashSet<AttackType>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                BuildAttackLookup();

                if (playerLayer.value == 0)
                {
                    Debug.LogWarning("[CombatManager3D] playerLayer is empty (mask value 0). All 3D attacks will silently miss until you assign a hurtbox layer in the Inspector.");
                }

                Debug.Log($"[CombatManager3D] Server initialized. Attack table entries: {attackTable.Count}");
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            if (Instance == this)
            {
                Instance = null;
            }

            players3D.Clear();
            attackCooldowns3D.Clear();
            kills3D.Clear();
            deaths3D.Clear();
            assists3D.Clear();
            recentDamage3D.Clear();
            attackLookup = null;
        }

        #region Player Registration (Server Only — callers wired in B1-3)

        public void RegisterPlayer3D(ulong clientId, PlayerNetworkController3D player)
        {
            if (!IsServer)
            {
                return;
            }

            if (player == null)
            {
                Debug.LogWarning($"[CombatManager3D] RegisterPlayer3D rejected null for client {clientId}");
                return;
            }

            players3D[clientId] = player;
            EnsurePlayerSessionBuckets3D(clientId);
            Debug.Log($"[CombatManager3D] Player {clientId} registered");
        }

        public void UnregisterPlayer3D(ulong clientId)
        {
            if (!IsServer)
            {
                return;
            }

            if (players3D.Remove(clientId))
            {
                attackCooldowns3D.Remove(clientId);
                // K/D/A counters intentionally kept for session display (mirrors legacy CombatManager pattern).
                // recentDamage3D entries also kept — disconnected attackers are filtered out at assist computation
                // via players3D.ContainsKey check in ComputeAssisters3D.
                Debug.Log($"[CombatManager3D] Player {clientId} unregistered");
            }
        }

        public PlayerNetworkController3D GetPlayer3D(ulong clientId)
        {
            players3D.TryGetValue(clientId, out var player);
            return player;
        }

        public List<KeyValuePair<ulong, PlayerNetworkController3D>> GetAllPlayersSnapshot()
        {
            return new List<KeyValuePair<ulong, PlayerNetworkController3D>>(players3D);
        }

        private bool AreAllPlayersDead()
        {
            if (players3D.Count < 2) return false;
            foreach (var player in players3D.Values)
            {
                if (player != null && player.IsAlive) return false;
            }
            return true;
        }

        private void EnsurePlayerSessionBuckets3D(ulong clientId)
        {
            if (!kills3D.ContainsKey(clientId)) kills3D[clientId] = 0;
            if (!deaths3D.ContainsKey(clientId)) deaths3D[clientId] = 0;
            if (!assists3D.ContainsKey(clientId)) assists3D[clientId] = 0;
            if (!recentDamage3D.ContainsKey(clientId)) recentDamage3D[clientId] = new Dictionary<ulong, DamageAttribution>();
        }

        #endregion

        #region Attack Table Lookup

        private void BuildAttackLookup()
        {
            attackLookup = new Dictionary<AttackType, AttackData3D>();
            foreach (AttackData3D entry in attackTable)
            {
                if (entry == null)
                {
                    continue;
                }

                if (attackLookup.ContainsKey(entry.AttackType))
                {
                    Debug.LogWarning($"[CombatManager3D] Duplicate AttackData3D for type {entry.AttackType} ignored");
                    continue;
                }

                attackLookup[entry.AttackType] = entry;
            }
        }

        /// <summary>
        /// Returns the attack data for the given type, or null if not configured.
        /// Caller must treat null as request rejection, NOT fall back to arbitrary defaults.
        /// Lookup is built lazily on first call after spawn.
        /// </summary>
        public AttackData3D Lookup(AttackType attackType)
        {
            if (attackLookup == null)
            {
                BuildAttackLookup();
            }

            attackLookup.TryGetValue(attackType, out var data);
            return data;
        }

        #endregion

        #region Attack/Parry Resolvers

        /// <summary>
        /// Server-authoritative attack resolution. Filters via Physics.OverlapBox + LayerMask +
        /// self/team/dead exclusion + registry validation + dedup. Any parrier in AOE blocks the
        /// entire attack and stuns the attacker (Decision 5 + 2D mirror).
        /// On true return: result RPC broadcast (Hit/Miss/Parried). On false return: caller emits owner-only reject.
        ///
        /// hitTargets in AttackResultRpc carries IDs of victims who actually took damage (B1-5 refinement).
        /// </summary>
        public bool TryProcessAttack3D(
            ulong attackerId,
            AttackType attackType,
            Vector3 attackerPosition,
            float attackerYaw,
            out string detail)
        {
            detail = "Unknown";

            if (!IsServer)
            {
                detail = "Server only";
                return false;
            }

            if (!players3D.TryGetValue(attackerId, out var attacker) || attacker == null)
            {
                detail = "Invalid attacker";
                return false;
            }

            AttackData3D data = Lookup(attackType);
            if (data == null)
            {
                detail = "AttackDataMissing";
                return false;
            }

            if (IsAttackOnCooldown(attackerId, attackType, data.Cooldown))
            {
                detail = "Cooldown";
                return false;
            }

            // Hitbox geometry: forward from yaw, center = pos + forward*range + Vector3.up*verticalOffset
            Quaternion facing = Quaternion.Euler(0f, attackerYaw, 0f);
            Vector3 forward = facing * Vector3.forward;
            Vector3 hitboxCenter = attackerPosition + forward * data.Range + Vector3.up * attackVerticalOffset;

            Collider[] overlaps = Physics.OverlapBox(
                hitboxCenter,
                data.HitboxHalfExtents,
                facing,
                playerLayer,
                QueryTriggerInteraction.Ignore
            );

            // Build full eligible list FIRST (no per-loop cap), so parry scan sees every potential parrier.
            HashSet<ulong> seenClientIds = new HashSet<ulong>();
            List<PlayerNetworkController3D> eligibleHits = new List<PlayerNetworkController3D>();

            foreach (Collider col in overlaps)
            {
                if (col == null) continue;
                if (col == attacker.PlayerCollider) continue;

                PlayerNetworkController3D target = col.GetComponentInParent<PlayerNetworkController3D>();
                if (target == null) continue;

                // Registry validation: skip stale/unregistered PNC3D objects on the layer
                if (!players3D.TryGetValue(target.OwnerClientId, out var registeredTarget) || registeredTarget != target)
                {
                    continue;
                }
                target = registeredTarget;

                if (!target.IsAlive) continue;
                if (target.OwnerClientId == attackerId) continue;
                if (attacker.Team != TeamId.None && attacker.Team == target.Team) continue;
                if (!seenClientIds.Add(target.OwnerClientId)) continue;

                eligibleHits.Add(target);
            }

            // Parry scan over all eligible (any-parrier-blocks-all per Decision 5 + 2D mirror)
            PlayerNetworkController3D parrier = null;
            foreach (var t in eligibleHits)
            {
                if (t.IsParrying)
                {
                    parrier = t;
                    break;
                }
            }

            if (parrier != null)
            {
                attacker.ApplyParryStun();
                ParrySuccessRpc(parrier.OwnerClientId, attackerId);
                SetAttackCooldown(attackerId, attackType);
                AttackResultRpc(attackerId, (byte)attackType, true, 0, System.Array.Empty<ulong>(), "Parried");
                detail = "Parried";
                return true;
            }

            // B1-5: hitTargets refined to "actually damaged" — TakeDamage returns bool now.
            // CI-B1-5-R1-1: only TrackDamageForAssist3D when target survives the hit. Fatal hits are
            // already credited via Die→OnPlayerDeath3D(killerId), and re-tracking would corrupt the
            // freshly-cleared recentDamage3D log with stale entries.
            List<PlayerNetworkController3D> actuallyDamaged = new List<PlayerNetworkController3D>();
            foreach (var t in eligibleHits)
            {
                if (t.TakeDamage(data.Damage, attackerId, data.DamageType))
                {
                    actuallyDamaged.Add(t);
                    if (t.IsAlive)
                    {
                        TrackDamageForAssist3D(attackerId, t.OwnerClientId, data.Damage);
                    }
                }
            }

            if (data.AppliedStatus != StatusMask.None && _warnedAppliedStatusTypes.Add(attackType))
            {
                Debug.LogWarning($"[CombatManager3D] AttackData3D.appliedStatus={data.AppliedStatus} on {attackType} ignored — status duration model not implemented. (See ROADMAP B1-4 note.)");
            }

            SetAttackCooldown(attackerId, attackType);

            // Cap RPC payload only. hitCount = real damaged count, hitTargets array trimmed.
            int hitCount = actuallyDamaged.Count;
            int payloadCount = Mathf.Min(hitCount, maxHitTargets);
            ulong[] hitTargets = new ulong[payloadCount];
            for (int i = 0; i < payloadCount; i++)
            {
                hitTargets[i] = actuallyDamaged[i].OwnerClientId;
            }

            if (hitCount > maxHitTargets)
            {
                Debug.LogWarning($"[CombatManager3D] hit count {hitCount} exceeded maxHitTargets cap {maxHitTargets}; truncating RPC payload.");
            }

            string resultDetail = hitCount > 0 ? $"Hit {hitCount}" : "Miss";
            AttackResultRpc(attackerId, (byte)attackType, true, hitCount, hitTargets, resultDetail);
            detail = resultDetail;
            return true;
        }

        /// <summary>
        /// Confirms parrier registration, emits ParryStarted broadcast for animation.
        /// Parry window timer is owned by PlayerNetworkController3D (per B1-1 Decision 5).
        /// </summary>
        public bool TryProcessParry3D(ulong defenderId, out string detail)
        {
            detail = "Unknown";

            if (!IsServer)
            {
                detail = "Server only";
                return false;
            }

            if (!players3D.TryGetValue(defenderId, out var defender) || defender == null)
            {
                detail = "Invalid defender";
                return false;
            }

            ParryStartedRpc(defenderId);
            detail = "Started";
            return true;
        }

        #endregion

        #region Cooldown helpers

        private bool IsAttackOnCooldown(ulong attackerId, AttackType attackType, float cooldown)
        {
            if (!attackCooldowns3D.TryGetValue(attackerId, out var byType)) return false;
            if (!byType.TryGetValue(attackType, out float lastTime)) return false;
            return Time.time - lastTime < cooldown;
        }

        private void SetAttackCooldown(ulong attackerId, AttackType attackType)
        {
            if (!attackCooldowns3D.TryGetValue(attackerId, out var byType))
            {
                byType = new Dictionary<AttackType, float>();
                attackCooldowns3D[attackerId] = byType;
            }
            byType[attackType] = Time.time;
        }

        #endregion

        #region K/D/A & Assist Tracking (B1-5)

        /// <summary>
        /// Server-only. Called by PlayerNetworkController3D.Die. Updates K/D/A counters,
        /// computes assist credits from recentDamage3D (within assistWindow3D, above threshold,
        /// excluding killer + skipping unregistered attackers), broadcasts PlayerKilled3DRpc,
        /// clears victim's damage log.
        ///
        /// Edge cases:
        /// - killerId == victimId (self-damage death): no kill credit, death++ only.
        /// - killerId not registered (left game mid-combat): death++ + assists computed; kill credit skipped.
        /// - kill-zone fall: routed through Die(OwnerClientId) → deaths++ (BF-1a). Self-kill, no kills credit.
        /// </summary>
        public void OnPlayerDeath3D(ulong victimId, ulong killerId)
        {
            if (!IsServer) return;

            EnsurePlayerSessionBuckets3D(victimId);

            deaths3D[victimId]++;

            bool killerCounts = killerId != victimId && players3D.ContainsKey(killerId);
            if (killerCounts)
            {
                EnsurePlayerSessionBuckets3D(killerId);
                kills3D[killerId]++;
            }

            List<ulong> assisters = ComputeAssisters3D(victimId, killerId);
            foreach (ulong assisterId in assisters)
            {
                EnsurePlayerSessionBuckets3D(assisterId);
                assists3D[assisterId]++;
            }

            if (recentDamage3D.TryGetValue(victimId, out var log)) log.Clear();

            PlayerKilled3DRpc(killerId, victimId, assisters.ToArray());
            Debug.Log($"[CombatManager3D] Death: victim={victimId} killer={killerId} assists=[{string.Join(",", assisters)}]");

            if (AreAllPlayersDead())
            {
                if (GameStateManager.Instance != null)
                    GameStateManager.Instance.EndMatch(MatchEndReason.AllPlayersDead);
            }
        }

        private List<ulong> ComputeAssisters3D(ulong victimId, ulong killerId)
        {
            List<ulong> assisters = new List<ulong>();
            if (!recentDamage3D.TryGetValue(victimId, out var damagers)) return assisters;

            float now = Time.time;
            foreach (var kvp in damagers)
            {
                ulong attackerId = kvp.Key;
                if (attackerId == killerId) continue;

                // Defensive: disconnected players don't earn assists from stale logs.
                if (!players3D.ContainsKey(attackerId)) continue;

                var attribution = kvp.Value;
                if (attribution.CumulativeDamage < assistDamageThreshold) continue;
                if (now - attribution.LastDamageTime > assistWindow3D) continue;

                assisters.Add(attackerId);
            }

            return assisters;
        }

        private void TrackDamageForAssist3D(ulong attackerId, ulong victimId, float damage)
        {
            EnsurePlayerSessionBuckets3D(victimId);
            var byAttacker = recentDamage3D[victimId];

            if (byAttacker.TryGetValue(attackerId, out var prev))
            {
                byAttacker[attackerId] = new DamageAttribution
                {
                    CumulativeDamage = prev.CumulativeDamage + damage,
                    LastDamageTime = Time.time
                };
            }
            else
            {
                byAttacker[attackerId] = new DamageAttribution
                {
                    CumulativeDamage = damage,
                    LastDamageTime = Time.time
                };
            }
        }

        public int GetKills3D(ulong clientId) => kills3D.TryGetValue(clientId, out int k) ? k : 0;
        public int GetDeaths3D(ulong clientId) => deaths3D.TryGetValue(clientId, out int d) ? d : 0;
        public int GetAssists3D(ulong clientId) => assists3D.TryGetValue(clientId, out int a) ? a : 0;
        public string GetKDAString3D(ulong clientId) => $"{GetKills3D(clientId)}/{GetDeaths3D(clientId)}/{GetAssists3D(clientId)}";

        #endregion

        #region Result RPCs

        [Rpc(SendTo.ClientsAndHost)]
        private void AttackResultRpc(ulong attackerId, byte attackType, bool accepted, int hitCount, ulong[] hitTargets, string detail)
        {
            Debug.Log($"[CombatManager3D] AttackResult attacker={attackerId} type={(AttackType)attackType} accepted={accepted} hits={hitCount} detail={detail}");
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void ParryStartedRpc(ulong defenderId)
        {
            Debug.Log($"[CombatManager3D] ParryStarted defender={defenderId}");
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void ParrySuccessRpc(ulong defenderId, ulong attackerId)
        {
            Debug.Log($"[CombatManager3D] ParrySuccess defender={defenderId} attacker={attackerId}");
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PlayerKilled3DRpc(ulong killerId, ulong victimId, ulong[] assisters)
        {
            string assistStr = assisters.Length > 0 ? $" assists=[{string.Join(",", assisters)}]" : "";
            Debug.Log($"[CombatManager3D] PlayerKilled killer={killerId} victim={victimId}{assistStr}");
        }

        #endregion

        #region Perk Trigger (migrated from legacy CombatManager D1-1)

        [SerializeField, Min(0f)] private float perkTriggerCooldown3D = 0.2f;
        private readonly Dictionary<ulong, Dictionary<int, float>> perkTriggerCooldowns3D = new Dictionary<ulong, Dictionary<int, float>>();

        public bool TryProcessPerkTrigger3D(
            ulong casterId,
            int triggerId,
            Vector3 origin,
            Vector3 targetHint,
            out string detail)
        {
            detail = "Unknown";

            if (!IsServer)
            {
                detail = "Server only";
                return false;
            }

            if (!players3D.TryGetValue(casterId, out var caster) || caster == null)
            {
                detail = "Invalid caster";
                return false;
            }

            if (!caster.IsAlive || !StatusHelper.CanAct(caster.CurrentStatus))
            {
                detail = "Cannot act";
                return false;
            }

            EnsurePlayerSessionBuckets3D(casterId);

            if (!perkTriggerCooldowns3D.TryGetValue(casterId, out var cooldownTable))
            {
                cooldownTable = new Dictionary<int, float>();
                perkTriggerCooldowns3D[casterId] = cooldownTable;
            }

            if (cooldownTable.TryGetValue(triggerId, out float lastTime))
            {
                if (Time.time - lastTime < perkTriggerCooldown3D)
                {
                    detail = "Cooldown";
                    return false;
                }
            }

            cooldownTable[triggerId] = Time.time;
            PerkTriggerAccepted3DRpc(casterId, triggerId, origin, targetHint);
            detail = "Accepted";
            return true;
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PerkTriggerAccepted3DRpc(ulong casterId, int triggerId, Vector3 origin, Vector3 targetHint)
        {
            Debug.Log($"[CombatManager3D] perk trigger accepted: caster={casterId}, trigger={triggerId}");
        }

        #endregion
    }
}
