using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ArenaCombat.Core.Network;

namespace ArenaCombat.Core.AI
{
    // Server-only singleton. Selects and applies BossAIDefinition variants based on
    // PlayerArchetypeClassifier output. See foamy-baking-melody.md (C3a plan) for design.
    //
    // Phase transition: subscribes to BNC3D.OnPhaseSkillsPopulated and re-applies
    // current variant slots immediately after PopulateBossSkills clobbers them.
    [DisallowMultipleComponent]
    public class BossAIPoolManager : MonoBehaviour
    {
        public static BossAIPoolManager Instance { get; private set; }
        public const int ComboCount = 10;

        [Header("Variant Pool")]
        [SerializeField] private BossAIDefinition _defaultAI;
        [SerializeField] private BossAIDefinition[] _combos = new BossAIDefinition[ComboCount];

        [Header("Debug")]
        [SerializeField] private bool _verboseLog = true;

        private readonly Dictionary<(PlayerArchetype, PlayerArchetype), BossAIDefinition> _lookup = new();
        private BossAIDefinition _currentDef;
        private BossAIDefinition _pendingDef;
        private BossNetworkController3D _bossController;
        private GameStateManager _subscribedGSM;
        private PlayerArchetypeClassifier _subscribedClassifier;
        private bool _needsEvaluate;

        public BossAIDefinition CurrentVariant => _currentDef;
        public BossAIDefinition PendingVariant => _pendingDef;

        void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(this); return; }
            BuildLookup();
        }

        void OnValidate()
        {
            if (_combos == null || _combos.Length != ComboCount)
                System.Array.Resize(ref _combos, ComboCount);
        }

        void OnEnable()
        {
            TrySubscribe();
        }

        void OnDisable()
        {
            UnsubscribeAll();
        }

        void OnDestroy()
        {
            UnsubscribeAll();
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            // Update-poll retry pattern (mirrors BossManager.cs:38-54). Handles scene
            // load order where dependencies may come up after this manager.
            if (_subscribedGSM == null || _subscribedClassifier == null)
                TrySubscribe();

            // Boss-available retry: when InProgress fires before BossManager spawns,
            // EvaluateAndSwap sets _needsEvaluate; retry each frame until applied.
            if (_needsEvaluate && IsServer)
                EvaluateAndSwap();
        }

        void BuildLookup()
        {
            _lookup.Clear();
            for (int i = 0; i < _combos.Length; i++)
            {
                var def = _combos[i];
                if (def == null) continue;
                if (def.isDefault)
                {
                    Debug.LogWarning($"[BossAIPool] Combo slot {i} ({def.name}) is flagged isDefault — assign to _defaultAI, not _combos[].", this);
                    continue;
                }
                var key = Norm(def.playerType1, def.playerType2);
                if (_lookup.ContainsKey(key))
                {
                    Debug.LogWarning($"[BossAIPool] Duplicate combo {key} — keeping first, ignoring {def.name}.", this);
                    continue;
                }
                _lookup[key] = def;
            }
            if (_defaultAI == null)
                Debug.LogWarning("[BossAIPool] _defaultAI is null — cold-start and lookup-miss fallback will be null.", this);
        }

        static (PlayerArchetype, PlayerArchetype) Norm(PlayerArchetype a, PlayerArchetype b)
            => (byte)a <= (byte)b ? (a, b) : (b, a);

        static bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        void TrySubscribe()
        {
            if (_subscribedGSM == null && GameStateManager.Instance != null)
            {
                _subscribedGSM = GameStateManager.Instance;
                _subscribedGSM.OnMatchStateChanged += HandleMatchStateChanged;
                if (IsServer && _subscribedGSM.CurrentMatchState == MatchState.InProgress)
                    _needsEvaluate = true;  // catch up if we hooked late
            }
            if (_subscribedClassifier == null && PlayerArchetypeClassifier.Instance != null)
            {
                _subscribedClassifier = PlayerArchetypeClassifier.Instance;
                _subscribedClassifier.OnPlayerArchetypeChanged += HandleArchetypeChanged;
            }
        }

        void UnsubscribeAll()
        {
            if (_subscribedGSM != null)
            {
                _subscribedGSM.OnMatchStateChanged -= HandleMatchStateChanged;
                _subscribedGSM = null;
            }
            if (_subscribedClassifier != null)
            {
                _subscribedClassifier.OnPlayerArchetypeChanged -= HandleArchetypeChanged;
                _subscribedClassifier = null;
            }
            UnsubscribeBoss();
        }

        void UnsubscribeBoss()
        {
            if (_bossController != null)
            {
                _bossController.OnIdleAfterAction -= ApplyPending;
                _bossController.OnPhaseSkillsPopulated -= HandlePhaseSkillsPopulated;
                _bossController = null;
            }
        }

        void HandleMatchStateChanged(MatchState prev, MatchState next)
        {
            if (!IsServer) return;
            if (next == MatchState.WaitingForPlayers)
            {
                ResetState();
                return;
            }
            if (next == MatchState.InProgress)
            {
                _needsEvaluate = true;
                EvaluateAndSwap();
            }
        }

        void HandleArchetypeChanged(ulong clientId, PlayerArchetype oldType, PlayerArchetype newType)
        {
            if (!IsServer) return;
            EvaluateAndSwap();
        }

        void ResetState()
        {
            UnsubscribeBoss();
            _currentDef = null;
            _pendingDef = null;
            _needsEvaluate = false;
        }

        BossNetworkController3D ResolveBossController()
        {
            // Validate cache against authoritative BossManager.CurrentBoss every call.
            if (BossManager.Instance == null) return null;
            var nob = BossManager.Instance.CurrentBoss;
            if (nob == null)
            {
                if (_bossController != null) UnsubscribeBoss();
                _bossController = null;
                return null;
            }
            if (_bossController == null || _bossController.NetworkObject != nob)
            {
                if (_bossController != null) UnsubscribeBoss();
                _bossController = nob.GetComponent<BossNetworkController3D>();
                if (_bossController != null)
                    _bossController.OnPhaseSkillsPopulated += HandlePhaseSkillsPopulated;
            }
            return _bossController;
        }

        (PlayerArchetype, PlayerArchetype) GetCurrentArchetypePair()
        {
            var classifier = PlayerArchetypeClassifier.Instance;
            var nm = NetworkManager.Singleton;
            if (classifier == null || nm == null)
                return (PlayerArchetype.Hybrid, PlayerArchetype.Hybrid);

            var ids = new List<ulong>();
            foreach (var c in nm.ConnectedClientsList) ids.Add(c.ClientId);
            ids.Sort();

            if (ids.Count == 0) return (PlayerArchetype.Hybrid, PlayerArchetype.Hybrid);
            var a1 = classifier.GetArchetype(ids[0]);
            var a2 = ids.Count >= 2 ? classifier.GetArchetype(ids[1]) : a1;
            return (a1, a2);
        }

        BossAIDefinition ResolveVariant(PlayerArchetype a, PlayerArchetype b)
        {
            var key = Norm(a, b);
            if (_lookup.TryGetValue(key, out var def) && def != null) return def;
            return _defaultAI;
        }

        public void EvaluateAndSwap()
        {
            if (!IsServer) return;
            var gsm = GameStateManager.Instance;
            if (gsm == null || gsm.CurrentMatchState != MatchState.InProgress)
            {
                _needsEvaluate = false;
                return;
            }

            var boss = ResolveBossController();
            if (boss == null)
            {
                // Boss not yet spawned; retry next frame.
                _needsEvaluate = true;
                return;
            }

            // Cold start: first apply uses dedicated Default AI explicitly, not lookup
            // (so HH variant can differ from "no data yet" variant if designer assigns
            // both). Subsequent calls fall through to normal lookup.
            BossAIDefinition def;
            if (_currentDef == null && _defaultAI != null)
            {
                def = _defaultAI;
            }
            else
            {
                var (p1, p2) = GetCurrentArchetypePair();
                def = ResolveVariant(p1, p2);
            }

            if (def == null)
            {
                _needsEvaluate = false;
                return;
            }

            // If the resolved variant matches what we already applied, cancel any
            // stale pending entry (covers the case where pending=B but classifier
            // flipped back to current=A before idle fired).
            if (def == _currentDef)
            {
                if (_pendingDef != null && boss != null)
                {
                    boss.OnIdleAfterAction -= ApplyPending;
                    _pendingDef = null;
                    if (_verboseLog) Debug.Log("[BossAI] pending swap cancelled (resolved back to current).", this);
                }
                _needsEvaluate = false;
                return;
            }

            if (boss.IsBusy)
            {
                _pendingDef = def;
                boss.OnIdleAfterAction -= ApplyPending;
                boss.OnIdleAfterAction += ApplyPending;
                _needsEvaluate = false;
                if (_verboseLog) Debug.Log($"[BossAI] swap deferred → {def.name} (boss busy)", this);
                return;
            }

            boss.ApplyAIVariant(def);
            _currentDef = def;
            _pendingDef = null;
            _needsEvaluate = false;
            if (_verboseLog) Debug.Log($"[BossAI] swap applied: {def.name}", this);
        }

        void ApplyPending()
        {
            // Unsubscribe immediately — one-shot semantics.
            if (_bossController != null)
                _bossController.OnIdleAfterAction -= ApplyPending;

            if (!IsServer) { _pendingDef = null; return; }
            if (_pendingDef == null) return;

            var def = _pendingDef;
            _pendingDef = null;

            var boss = ResolveBossController();
            if (boss == null || !boss.IsSpawned)
            {
                // Boss despawned between defer and idle event — requeue.
                _pendingDef = def;
                _needsEvaluate = true;
                return;
            }
            if (def == _currentDef) return;

            // Defensive re-check: another telegraph may have started between idle
            // event and this dispatch (rare but possible). Requeue if busy.
            if (boss.IsBusy)
            {
                _pendingDef = def;
                boss.OnIdleAfterAction -= ApplyPending;
                boss.OnIdleAfterAction += ApplyPending;
                if (_verboseLog) Debug.Log($"[BossAI] re-defer {def.name} (boss became busy again)", this);
                return;
            }

            boss.ApplyAIVariant(def);
            _currentDef = def;
            if (_verboseLog) Debug.Log($"[BossAI] deferred swap applied: {def.name}", this);
        }

        void HandlePhaseSkillsPopulated()
        {
            if (!IsServer || _currentDef == null || _bossController == null) return;
            _bossController.ApplyAIVariantSlots(_currentDef);
        }
    }
}
