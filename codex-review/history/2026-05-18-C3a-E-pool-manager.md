# Pending Codex Review — C3a Phase E: BossAIPoolManager

## Topic
Add `BossAIPoolManager`: server-only singleton that holds the 11 `BossAIDefinition` SOs (1 Default + 10 archetype-pair combos), builds an order-normalized lookup table, subscribes to `PlayerArchetypeClassifier.OnPlayerArchetypeChanged` and match-state events, and applies variants to the boss via `BossNetworkController3D.ApplyAIVariant`. Defers swaps via `OnIdleAfterAction` when the boss is mid-telegraph.

## Roadmap link
ROADMAP.md → Phase C3a, sub-phase E
Plan file: `C:\Users\paek6\.claude\plans\foamy-baking-melody.md` (file change #4)

## Goal
End-to-end: archetype classified (Phase C) → event fires → pool manager resolves combo → applies variant on boss (Phase D). After E, with 11 placeholder SOs assigned, swapping is observable in console even with empty skill arrays.

## Files to touch
- **NEW** `Assets/ArenaCombat/Scripts/Core/AI/BossAIPoolManager.cs` — server-only `MonoBehaviour`, ~150 lines

## Approach

### Lookup key normalization
2 player archetypes (P1, P2). Order doesn't matter — `(Melee, Ranged)` and `(Ranged, Melee)` map to same SO. Normalize by sorting enum values: smaller byte first. So all 10 combos use `(low, high)` keys.

Implementation: `(PlayerArchetype, PlayerArchetype)` tuple, normalized in a helper:
```csharp
static (PlayerArchetype, PlayerArchetype) Norm(PlayerArchetype a, PlayerArchetype b)
    => (byte)a <= (byte)b ? (a, b) : (b, a);
```

### Lookup table construction
On Awake, iterate `_combos[]` (10 SOs), build `Dictionary<(PlayerArchetype, PlayerArchetype), BossAIDefinition>` with normalized keys. Log warnings for:
- Null entry
- Duplicate key
- A `_combos[i]` flagged as `isDefault` (shouldn't be — Default goes in `_defaultAI` field)

If a combo is missing, lookup falls back to `_defaultAI`.

### Subscriptions (all server-only)
On Start (server only):
1. Subscribe `GameStateManager.Instance.OnMatchStateChanged += HandleMatchStateChanged`
2. Subscribe `PlayerArchetypeClassifier.Instance.OnPlayerArchetypeChanged += HandleArchetypeChanged`

`HandleMatchStateChanged(prev, next)`: if `next == MatchState.InProgress`, call `EvaluateAndSwap()` to apply Default AI (or current classification if any data already exists). Also re-evaluate if `next == MatchState.WaitingForPlayers` (restart) — clears `_currentDef` so next InProgress applies fresh.

`HandleArchetypeChanged(clientId, oldType, newType)`: call `EvaluateAndSwap()`.

### Boss controller reference acquisition
The pool manager doesn't subscribe to `BossManager.OnBossSpawned` (no such event currently — would require BossManager edit). Instead, it looks up the controller lazily at each `EvaluateAndSwap` via `BossManager.Instance.CurrentBoss?.GetComponent<BossNetworkController3D>()`. Caches the result; clears cache when null returned (boss despawned).

### Boss controller's `OnIdleAfterAction` subscription
Each `EvaluateAndSwap` that needs to defer subscribes one-shot to the boss controller's `OnIdleAfterAction`. Idempotent re-subscription protected by `_ -= _` before `_ += _`.

### `EvaluateAndSwap` flow
```
EvaluateAndSwap():
    if !IsServer return
    if classifier or boss-controller missing → return (will retry on next trigger)
    if match state != InProgress → return  (skip during draft / matchend)

    (p1Type, p2Type) = GetCurrentArchetypePair()
    key = Norm(p1Type, p2Type)
    def = _lookup[key] ?? _defaultAI
    if def == _currentDef → return  (no-op)

    if bossController.IsBusy:
        _pendingDef = def
        bossController.OnIdleAfterAction -= ApplyPending
        bossController.OnIdleAfterAction += ApplyPending
        Debug.Log $"[BossAI] swap deferred → {def.name}"
        return

    bossController.ApplyAIVariant(def)
    _currentDef = def
```

`ApplyPending` (one-shot handler):
```
ApplyPending():
    bossController.OnIdleAfterAction -= ApplyPending  (unsubscribe immediately)
    if _pendingDef == null return
    var def = _pendingDef
    _pendingDef = null
    if def == _currentDef return  (changed mind in the meantime — should not happen but guard)
    if !bossController.IsServer || !bossController.IsSpawned return  (controller died)
    bossController.ApplyAIVariant(def)
    _currentDef = def
```

### `GetCurrentArchetypePair`
Query the connected client IDs in deterministic order. Use `NetworkManager.Singleton.ConnectedClientsList` and take the first two (sorted by ClientId for determinism). If 1 player (solo testing), mirror: `p2 = p1`. If 0 players, return `(Hybrid, Hybrid)`.

```csharp
(PlayerArchetype, PlayerArchetype) GetCurrentArchetypePair()
{
    var classifier = PlayerArchetypeClassifier.Instance;
    var nm = NetworkManager.Singleton;
    if (classifier == null || nm == null) return (PlayerArchetype.Hybrid, PlayerArchetype.Hybrid);

    var sorted = new List<ulong>();
    foreach (var c in nm.ConnectedClientsList) sorted.Add(c.ClientId);
    sorted.Sort();

    if (sorted.Count == 0) return (PlayerArchetype.Hybrid, PlayerArchetype.Hybrid);
    var a1 = classifier.GetArchetype(sorted[0]);
    var a2 = sorted.Count >= 2 ? classifier.GetArchetype(sorted[1]) : a1;
    return (a1, a2);
}
```

### Inspector fields
```csharp
[SerializeField] private BossAIDefinition _defaultAI;
[SerializeField] private BossAIDefinition[] _combos = new BossAIDefinition[10];
[SerializeField] private bool _verboseLog = true;
```

`_combos.Length == 10` is enforced via `OnValidate` (Array.Resize) — same pattern as `SkillManager.OnValidate` (`SkillManager.cs:123`).

### Reset on restart
When `MatchState` transitions WaitingForPlayers (from MatchEnd via restart):
- Clear `_currentDef` and `_pendingDef`
- Unsubscribe any pending `OnIdleAfterAction` callback
- Reset boss controller cache

### Lifecycle
- `Awake` — Instance + lookup table build
- `Start` — server-only subscriptions
- `OnDestroy` — unsubscribe from classifier, GSM, boss controller; clear Instance

## Diff sketch (`BossAIPoolManager.cs` — new file)

```csharp
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ArenaCombat.Core.Network;

namespace ArenaCombat.Core.AI
{
    // Server-only singleton. Selects and applies BossAIDefinition variants based on
    // PlayerArchetypeClassifier output. See foamy-baking-melody.md for design.
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

        void BuildLookup()
        {
            _lookup.Clear();
            for (int i = 0; i < _combos.Length; i++)
            {
                var def = _combos[i];
                if (def == null) continue;
                if (def.isDefault)
                {
                    Debug.LogWarning($"[BossAIPool] Combo slot {i} ({def.name}) is flagged isDefault — should be assigned to _defaultAI, not _combos[].", this);
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

        void Start()
        {
            if (!IsServer) return;

            if (GameStateManager.Instance != null)
            {
                _subscribedGSM = GameStateManager.Instance;
                _subscribedGSM.OnMatchStateChanged += HandleMatchStateChanged;
            }
            if (PlayerArchetypeClassifier.Instance != null)
            {
                _subscribedClassifier = PlayerArchetypeClassifier.Instance;
                _subscribedClassifier.OnPlayerArchetypeChanged += HandleArchetypeChanged;
            }
        }

        void OnDestroy()
        {
            if (_subscribedGSM != null)
                _subscribedGSM.OnMatchStateChanged -= HandleMatchStateChanged;
            if (_subscribedClassifier != null)
                _subscribedClassifier.OnPlayerArchetypeChanged -= HandleArchetypeChanged;
            UnsubscribeBoss();
            if (Instance == this) Instance = null;
        }

        static bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        void HandleMatchStateChanged(MatchState prev, MatchState next)
        {
            if (!IsServer) return;
            if (next == MatchState.WaitingForPlayers)
            {
                // Restart: reset variant state so next InProgress applies fresh Default.
                ResetState();
                return;
            }
            if (next == MatchState.InProgress)
            {
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
            _bossController = null;
        }

        void UnsubscribeBoss()
        {
            if (_bossController != null)
                _bossController.OnIdleAfterAction -= ApplyPending;
        }

        BossNetworkController3D ResolveBossController()
        {
            if (_bossController != null) return _bossController;
            if (BossManager.Instance == null) return null;
            var nob = BossManager.Instance.CurrentBoss;
            if (nob == null) return null;
            _bossController = nob.GetComponent<BossNetworkController3D>();
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
            if (gsm == null || gsm.CurrentMatchState != MatchState.InProgress) return;
            var boss = ResolveBossController();
            if (boss == null) return;

            var (p1, p2) = GetCurrentArchetypePair();
            var def = ResolveVariant(p1, p2);
            if (def == null || def == _currentDef) return;

            if (boss.IsBusy)
            {
                _pendingDef = def;
                boss.OnIdleAfterAction -= ApplyPending;
                boss.OnIdleAfterAction += ApplyPending;
                if (_verboseLog)
                    Debug.Log($"[BossAI] swap deferred → {def.name} (boss busy)", this);
                return;
            }

            boss.ApplyAIVariant(def);
            _currentDef = def;
            if (_verboseLog)
                Debug.Log($"[BossAI] swap applied: {def.name} ({p1}+{p2})", this);
        }

        void ApplyPending()
        {
            if (_bossController != null)
                _bossController.OnIdleAfterAction -= ApplyPending;

            if (!IsServer) { _pendingDef = null; return; }
            if (_pendingDef == null) return;

            var def = _pendingDef;
            _pendingDef = null;

            var boss = ResolveBossController();
            if (boss == null || !boss.IsSpawned) return;
            if (def == _currentDef) return;

            boss.ApplyAIVariant(def);
            _currentDef = def;
            if (_verboseLog)
                Debug.Log($"[BossAI] deferred swap applied: {def.name}", this);
        }
    }
}
```

## Risks / unknowns

1. **Boss-controller cache invalidation**: pool manager caches `_bossController`. If boss despawns and respawns mid-match (rare), cache holds the dead reference. Mitigation: `ResolveBossController` returns null if `BossManager.Instance.CurrentBoss == null`, and `ApplyPending` re-resolves before using. Acceptable.

2. **Subscription timing race on first scene load**: `Start` is per-frame timing; if `BossAIPoolManager.Start` runs before `GameStateManager.Awake`, `Instance` is null. Mitigated by `Update`-poll pattern? Currently no — uses one-shot `Start`. Risk: if scene order doesn't guarantee GSM exists by pool manager's Start, the GSM subscription is skipped silently. Mitigation: follow BossManager's pattern (`BossManager.cs:38-54`) — `Update`-driven retry until both `GameStateManager.Instance` and `PlayerArchetypeClassifier.Instance` are non-null.

3. **Restart edge case**: when MatchState goes MatchEnd → WaitingForPlayers (restart), we reset `_currentDef`. But the boss is despawned by `RestartMatch`. When the new InProgress fires, boss is re-spawned by BossManager, then our InProgress hook fires `EvaluateAndSwap`. If we're called before BossManager spawns the new boss, `ResolveBossController` returns null and we silently skip. Subsequent classifier event after re-classification triggers another EvaluateAndSwap, which would then succeed. Acceptable but means a brief window where the new boss has no variant assigned. Mitigation: also re-call `EvaluateAndSwap` after a short delay, or hook BossManager spawn. Simpler: BossManager.cs:130 already calls `Debug.Log("[BossManager] Boss spawned...")` after spawn — we could subscribe to a new `OnBossSpawned` event, but that requires a BossManager edit. Defer to follow-up.

4. **`Norm` correctness**: `(byte)a <= (byte)b ? (a, b) : (b, a)` — for (M=1, R=2) returns (M, R); for (R=2, M=1) returns (M, R). ✓

5. **Same-key duplicates in inspector**: `BuildLookup` warns and skips duplicates. Author sees the warning. ✓

6. **Variant SOs not yet authored**: pool manager works with empty/null entries — lookup-miss falls back to `_defaultAI`, which can itself be null until Phase G placeholder docs are followed. Acceptable for Phase E landing.

## Questions for Codex

1. **Subscription retry pattern**: should pool manager use `Update`-poll subscription (like `BossManager.cs:38-54`) to handle init-order races? My pick: yes — defensive against scene load order.

2. **Manual phase-change recovery**: should pool manager subscribe to boss controller phase change to re-apply variant after `PopulateBossSkills` clobbers? `OnPhaseChanged` is private (per Phase D Codex note). Adding a public event is a BossNetworkController3D edit. For Phase E, my pick: defer the recovery to a follow-up phase; document the issue. Pool manager re-applies on the NEXT archetype change anyway, so a phase-clobber gap is bounded by archetype eval cadence (~3min).

3. **`ApplyPending` should re-check `IsBusy`?**: if a new telegraph fires between defer-queue and idle-event, we shouldn't apply. My current code doesn't re-check. Add `if (boss.IsBusy) { _pendingDef = def; boss.OnIdleAfterAction += ApplyPending; return; }` ? My pick: yes, add defensive re-check.

4. **`_currentDef` comparison with `==`**: reference equality on SO. Two SOs with identical content compare false. That's desired (variant identity = SO asset identity).

5. **`_verboseLog` default**: `true` for now to aid testing. Should be `false` for production? My pick: `true` for Phase E since the system is brand new and observability matters; flip to `false` post-tuning.

## Out of scope for this round
- `SkillManager.SetSlotWeights` + multiplication into adaptive weights (Phase F)
- 11 placeholder variant SOs (Phase G — doc only)
- ROADMAP / placeholder doc (Phase G)
- Phase-change clobber recovery (follow-up, after E lands and we observe in play)
- BossManager.OnBossSpawned event (follow-up)
