# Pending Codex Review — C2-1 BTPlayerAgent Shell + BT Framework (R4)

## Topic
Phase C2-1: BT (Behavior Tree) based simulation player agent for ML-Agents boss training data generation. NOT for real players (WASD + mouse aim via existing PlayerInputHandler). **Revision 4** — addresses R3 feedback (ServerSetMoveIntent movement gates).

## Roadmap link
- **C2. BT 기반 시뮬레이션 플레이어 에이전트 (ML 학습 전용)**

## Files to touch
1. **NEW** `Assets/ArenaCombat/Scripts/Core/AI/BT/BTNode.cs` (~80 LOC) — All BT types in one file (BTStatus enum + BTNode abstract + BTSelector + BTSequence + BTCondition + BTAction)
2. **NEW** `Assets/ArenaCombat/Scripts/Core/AI/BTPlayerAgent.cs` (~200 LOC) — Server-side BT player controller
3. **EDIT** `Assets/ArenaCombat/Scripts/Core/Network/PlayerNetworkController3D.cs` (+20 lines) — Add 3 server-side action methods + `IsBTControlled` flag

## R3 → R4 Changes

### Fix 8: ServerSetMoveIntent movement gates
R3 lacked `StatusHelper.CanMove` and `networkIsRoping` checks that `RequestMoveRpc` enforces (line 781). BT could move while Stunned/Rooted/Frozen. Fixed:
```csharp
public void ServerSetMoveIntent(Vector2 move, float yaw)
{
    if (!IsServer || !IsSpawned) return;
    if (!networkIsAlive.Value || !StatusHelper.CanMove(networkStatusMask.Value) || networkIsRoping.Value)
    {
        serverMoveInput = Vector2.zero;
        return;
    }
    if (float.IsNaN(move.x) || float.IsNaN(move.y) || float.IsInfinity(move.x) || float.IsInfinity(move.y))
        move = Vector2.zero;
    if (float.IsNaN(yaw) || float.IsInfinity(yaw))
        yaw = 0f;
    serverMoveInput = move.sqrMagnitude > 1f ? move.normalized : move;
    serverLookYaw = yaw;
}
```

### Fix 9 (non-blocking): BTPlayerAgent.Awake validation
Add warning if `_pnc.IsBTControlled` is false:
```csharp
void Awake()
{
    _pnc = GetComponent<PlayerNetworkController3D>();
    _self = GetComponent<ICombatant>();
    if (_pnc != null && !_pnc.IsBTControlled)
        Debug.LogWarning($"[BTPlayerAgent] PNC3D.IsBTControlled is false on {gameObject.name} — owner input may conflict with BT");
}
```

---

## R2 → R3 Changes

### Fix 4: Guard all owner-only runtime paths with `_isBTControlled`
`Update()` at line 453 runs `if (IsOwner && autoSendMoveRequests) SendCachedInputToServer()` — this sends `cachedMoveInput`/`cachedLookYaw` via RPC which overwrites `serverMoveInput`/`serverLookYaw`. Fix: add BT guard to both owner paths in Update:
```csharp
// Line 453:
if (IsOwner && !_isBTControlled && autoSendMoveRequests)
{
    SendCachedInputToServer();
}

// Line 458:
if (IsOwner && !_isBTControlled && rebindOwnerCameraWhenMissing &&
    (ownerCamera == null || !ownerCamera.isActiveAndEnabled))
{
    SetupTopDownCamera();
}
```

### Fix 5: Add `using ArenaCombat.Core;` to BTPlayerAgent
`GameManager` is in namespace `ArenaCombat.Core` — child namespace `ArenaCombat.Core.AI` does not auto-import parent.

### Fix 6: Host clientId collision documented
In host mode, host's local player also has clientId 0. C2-1 BT training must run in **dedicated server mode** (no host human player) or with the host human player on a different clientId. Documented as constraint — multi-agent identity abstraction in C2-2.

### Fix 7: Sanitize ServerSetMoveIntent inputs
Mirror `RequestMoveRpc` sanitization:
```csharp
public void ServerSetMoveIntent(Vector2 move, float yaw)
{
    if (!IsServer || !IsSpawned) return;
    if (!networkIsAlive.Value || !StatusHelper.CanMove(networkStatusMask.Value) || networkIsRoping.Value)
    {
        serverMoveInput = Vector2.zero;
        return;
    }
    if (float.IsNaN(move.x) || float.IsNaN(move.y) || float.IsInfinity(move.x) || float.IsInfinity(move.y))
        move = Vector2.zero;
    if (float.IsNaN(yaw) || float.IsInfinity(yaw))
        yaw = 0f;
    serverMoveInput = move.sqrMagnitude > 1f ? move.normalized : move;
    serverLookYaw = yaw;
}
```

---

## R1 → R2 Changes

### Fix 1: Movement fields — write `serverMoveInput` / `serverLookYaw`
R1 wrote `cachedMoveInput`/`cachedLookYaw` (owner-side cache). Server movement is driven by `serverMoveInput`/`serverLookYaw` (set by `RequestMoveRpc` at line 787). Fixed:
```csharp
public void ServerSetMoveIntent(Vector2 move, float yaw)
{
    if (!IsServer || !IsSpawned || !networkIsAlive.Value) return;
    serverMoveInput = move.sqrMagnitude > 1f ? move.normalized : move;
    serverLookYaw = yaw;
}
```

### Fix 2: Queue API — use `TryEnqueueServerAction` + correct field names
R1 used nonexistent `QueueServerAction` and wrong field name `Type`. Fixed to use actual private `TryEnqueueServerAction(QueuedServerAction, out string)` and `ActionType` field + `ReceivedAt = Time.time`. Methods implemented inside PNC3D class (since `QueuedServerAction`/`QueuedActionType` are private nested types):
```csharp
public void ServerSubmitAttack(AttackType type)
{
    if (!IsServer || !IsSpawned || !networkIsAlive.Value) return;
    if (!System.Enum.IsDefined(typeof(AttackType), type)) return;
    localTick++;
    var action = new QueuedServerAction
    {
        ActionType = QueuedActionType.Attack,
        ClientTick = localTick,
        ReceivedAt = Time.time,
        AttackKind = type
    };
    TryEnqueueServerAction(action, out _);
}

public void ServerSubmitParry()
{
    if (!IsServer || !IsSpawned || !networkIsAlive.Value) return;
    localTick++;
    var action = new QueuedServerAction
    {
        ActionType = QueuedActionType.Parry,
        ClientTick = localTick,
        ReceivedAt = Time.time
    };
    TryEnqueueServerAction(action, out _);
}
```

### Fix 3: Identity model — single BT agent constraint
Multiple server-spawned BT players collide on `OwnerClientId` (CombatManager3D `players3D[clientId]` dict, PlayerBiasTracker keyed by clientId, InputValidator registration). 

**R2 approach**: Constrain C2-1 to **exactly 1 BT agent** per training session. The BT agent replaces one of the 2 human players (the other is human or absent). Server-spawned NetworkObject's `OwnerClientId` = server's clientId (0). This works because:
- CombatManager3D: registers `players3D[0] = btPlayer` — unique since real players have clientId >= 1
- PlayerBiasTracker: tracks clientId 0 separately — produces bias data for this agent
- InputValidator: `RegisterClient(0)` — no collision since server clientId is unique

**BT agent spawn path**: NOT via `PlayerSpawnManager.OnClientConnected`. Instead, `BTPlayerAgent` is pre-placed in training scene, finds its PNC3D, and the PNC3D is spawned via `NetworkObject.Spawn()` with server ownership (no `SpawnAsPlayerObject`).

**`IsBTControlled` flag** on PNC3D:
```csharp
[SerializeField] bool _isBTControlled;
public bool IsBTControlled => _isBTControlled;
```
When true:
- `OnNetworkSpawn` skips `inputHandler.enabled` / `SubscribeInputEvents` / camera setup
- `autoSendMoveRequests` treated as false (BT drives movement via `ServerSetMoveIntent`)
- `useBuiltInInputHandler` treated as false

Multi-agent support (distinct `CombatantId` abstraction) deferred to C2-2+.

## BT Framework Design

Single file, namespace `ArenaCombat.Core.AI.BT`. Reactive stateless BT (restarts from child 0 each tick — documented limitation for R1).

```csharp
namespace ArenaCombat.Core.AI.BT
{
    public enum BTStatus { Success, Failure, Running }

    public abstract class BTNode
    {
        public abstract BTStatus Tick();
    }

    public class BTSelector : BTNode
    {
        readonly BTNode[] _children;
        public BTSelector(params BTNode[] children) => _children = children;
        public override BTStatus Tick()
        {
            foreach (var c in _children)
            {
                var s = c.Tick();
                if (s != BTStatus.Failure) return s;
            }
            return BTStatus.Failure;
        }
    }

    public class BTSequence : BTNode
    {
        readonly BTNode[] _children;
        public BTSequence(params BTNode[] children) => _children = children;
        public override BTStatus Tick()
        {
            foreach (var c in _children)
            {
                var s = c.Tick();
                if (s != BTStatus.Success) return s;
            }
            return BTStatus.Success;
        }
    }

    public class BTCondition : BTNode
    {
        readonly System.Func<bool> _check;
        public BTCondition(System.Func<bool> check) => _check = check;
        public override BTStatus Tick() => _check() ? BTStatus.Success : BTStatus.Failure;
    }

    public class BTAction : BTNode
    {
        readonly System.Func<BTStatus> _action;
        public BTAction(System.Func<BTStatus> action) => _action = action;
        public override BTStatus Tick() => _action();
    }
}
```

## BTPlayerAgent Design (~200 LOC)

```csharp
namespace ArenaCombat.Core.AI
{
    using ArenaCombat.Core;
    using ArenaCombat.Core.AI.BT;
    using ArenaCombat.Core.Network;
    using ArenaCombat.Core.Combat;
    using UnityEngine;

    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerNetworkController3D))]
    public class BTPlayerAgent : MonoBehaviour
    {
        [Header("Personality")]
        [SerializeField, Range(0f, 1f)] float _meleeAggression = 0.5f;
        [SerializeField, Range(0f, 1f)] float _parryTendency = 0.3f;
        [SerializeField, Range(0f, 1f)] float _survivalCaution = 0.3f;
        [SerializeField] float _meleeRange = 3f;
        [SerializeField] float _fleeHPThreshold = 0.3f;

        [Header("Tick")]
        [SerializeField] float _tickInterval = 0.2f;

        PlayerNetworkController3D _pnc;
        BTNode _root;
        float _nextTick;
        ICombatant _self;

        bool IsServer => Unity.Netcode.NetworkManager.Singleton != null 
                       && Unity.Netcode.NetworkManager.Singleton.IsServer;

        void Awake()
        {
            _pnc = GetComponent<PlayerNetworkController3D>();
            _self = GetComponent<ICombatant>();
        }

        void OnEnable()
        {
            _root = BuildTree();
        }

        void FixedUpdate()
        {
            if (!IsServer || _root == null) return;
            if (!_self.IsAlive) return;
            if (Time.time < _nextTick) return;
            _nextTick = Time.time + _tickInterval;
            _root.Tick();
        }

        ICombatant FindBoss()
        {
            if (GameManager.Instance == null) return null;
            var bosses = GameManager.Instance.Bosses;
            if (bosses == null || bosses.Count == 0) return null;
            return bosses[0].GetComponent<ICombatant>();
        }

        float BossDistance()
        {
            var boss = FindBoss();
            if (boss == null) return float.MaxValue;
            return Vector3.Distance(transform.position, boss.Transform.position);
        }

        float LookAtBoss()
        {
            var boss = FindBoss();
            if (boss == null) return transform.eulerAngles.y;
            Vector3 dir = boss.Transform.position - transform.position;
            return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        }

        BTNode BuildTree() => new BTSelector(
            // Survival: flee if HP low
            new BTSequence(
                new BTCondition(() => _self.CurrentHPPercent < _fleeHPThreshold),
                new BTAction(() => {
                    var boss = FindBoss();
                    if (boss == null) return BTStatus.Failure;
                    Vector3 away = (transform.position - boss.Transform.position).normalized;
                    _pnc.ServerSetMoveIntent(new Vector2(away.x, away.z), LookAtBoss());
                    return BTStatus.Success;
                })
            ),
            // Parry: if boss in range and random check
            new BTSequence(
                new BTCondition(() => BossDistance() < _meleeRange * 1.5f 
                                   && Random.value < _parryTendency),
                new BTAction(() => {
                    _pnc.ServerSubmitParry();
                    return BTStatus.Success;
                })
            ),
            // Melee attack: if close enough
            new BTSequence(
                new BTCondition(() => BossDistance() < _meleeRange 
                                   && Random.value < _meleeAggression),
                new BTAction(() => {
                    _pnc.ServerSubmitAttack(AttackType.Light);
                    _pnc.ServerSetMoveIntent(Vector2.zero, LookAtBoss());
                    return BTStatus.Success;
                })
            ),
            // Default: approach boss
            new BTAction(() => {
                var boss = FindBoss();
                if (boss == null) return BTStatus.Failure;
                Vector3 toward = (boss.Transform.position - transform.position).normalized;
                _pnc.ServerSetMoveIntent(new Vector2(toward.x, toward.z), LookAtBoss());
                return BTStatus.Success;
            })
        );
    }
}
```

## PNC3D Full Diff

```csharp
// New field (near line 42):
[SerializeField] bool _isBTControlled;
public bool IsBTControlled => _isBTControlled;

// New methods (after ServerSubmitParry, inside class body):
public void ServerSetMoveIntent(Vector2 move, float yaw)
{
    if (!IsServer || !IsSpawned) return;
    if (!networkIsAlive.Value || !StatusHelper.CanMove(networkStatusMask.Value) || networkIsRoping.Value)
    {
        serverMoveInput = Vector2.zero;
        return;
    }
    if (float.IsNaN(move.x) || float.IsNaN(move.y) || float.IsInfinity(move.x) || float.IsInfinity(move.y))
        move = Vector2.zero;
    if (float.IsNaN(yaw) || float.IsInfinity(yaw))
        yaw = 0f;
    serverMoveInput = move.sqrMagnitude > 1f ? move.normalized : move;
    serverLookYaw = yaw;
}

public void ServerSubmitAttack(AttackType type)
{
    if (!IsServer || !IsSpawned || !networkIsAlive.Value) return;
    if (!System.Enum.IsDefined(typeof(AttackType), type)) return;
    localTick++;
    var action = new QueuedServerAction
    {
        ActionType = QueuedActionType.Attack,
        ClientTick = localTick,
        ReceivedAt = Time.time,
        AttackKind = type
    };
    TryEnqueueServerAction(action, out _);
}

public void ServerSubmitParry()
{
    if (!IsServer || !IsSpawned || !networkIsAlive.Value) return;
    localTick++;
    var action = new QueuedServerAction
    {
        ActionType = QueuedActionType.Parry,
        ClientTick = localTick,
        ReceivedAt = Time.time
    };
    TryEnqueueServerAction(action, out _);
}

// OnNetworkSpawn modification — skip owner input for BT:
// In the `if (IsOwner)` block (~line 357), wrap input setup:
if (IsOwner && !_isBTControlled)
{
    // existing inputHandler.enabled, SubscribeInputEvents, SetupTopDownCamera, etc.
}

// Update() modification — guard owner auto-send + camera rebind:
// Line 453:
if (IsOwner && !_isBTControlled && autoSendMoveRequests)
    SendCachedInputToServer();
// Line 458:
if (IsOwner && !_isBTControlled && rebindOwnerCameraWhenMissing && ...)
    SetupTopDownCamera();
```

## Risks / unknowns
1. **Server clientId = 0 assumption**: NGO 2.x host clientId is 0. If relay/dedicated server changes this, registration breaks. Documented constraint.
2. **Rope**: BT agents don't use rope in C2-1. Rope BT action deferred to C2-2.
3. **Card draft**: BT agent will skip card selection (no client to show UI). `IsServerGameplayBlockedByCardDraft` gates will pause BT agent's queued actions naturally. Card auto-pick for BT is C2-2.
4. **Respawn**: PNC3D.Die/Respawn should still work for server-owned objects. Verify.

## Questions for Codex
1. Does `IsOwner` return true for server-spawned objects (no `SpawnAsPlayerObject`)? If not, the `IsOwner && !_isBTControlled` guard needs adjustment.
2. Should `ServerSetMoveIntent` also check `IsServerGameplayBlockedByCardDraft()`?
3. Any issue with `autoSendMoveRequests` being true on a BT agent? (It calls `SendCachedInputToServer` which uses `cachedMoveInput` — separate from `serverMoveInput`, so should be harmless.)
