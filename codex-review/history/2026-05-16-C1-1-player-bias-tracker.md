# Pending Codex Review — C1-1 PlayerBiasTracker + SkillExecutor Hook + BNC3D Cleanup (R6)

## Topic
Phase C1-1: Player behavior bias tracking + SkillExecutor execution event + BNC3D debug log removal. **Revision 6** — addresses R5 feedback (despawn unsubscribe form, SampleTeamDistance implementation, zero-sample guard).

## Roadmap link
- **C1. 플레이어 행동 편향 로그 수집** — Phase C first item
- **X5 cleanup** — Remove temporary TakeDamage debug log

## Files to touch
1. **NEW** `Assets/ArenaCombat/Scripts/Core/AI/PlayerBiasTracker.cs` (~150 LOC)
2. **EDIT** `Assets/ArenaCombat/Scripts/Core/Skill/Core/SkillExecutor.cs` (+3 lines)
3. **EDIT** `Assets/ArenaCombat/Scripts/Core/Network/PlayerNetworkController3D.cs` (+16 lines)
4. **EDIT** `Assets/ArenaCombat/Scripts/Core/Network/BossNetworkController3D.cs` (-1 line)
5. **(Scene, no codex)** `Chapter1.unity` — add PlayerBiasTracker GO

## R5 → R6 Changes

### Fix 10: Expanded despawn unsubscribe (no null-conditional on event)
The compact `GetComponent<SkillExecutor>()?.OnExecuted -= _biasSkillHandler;` is invalid — C# does not support null-conditional for event unsubscribe assignment. Use expanded form everywhere:
```csharp
if (_biasSkillHandler != null)
{
    var exec = GetComponent<SkillExecutor>();
    if (exec != null) exec.OnExecuted -= _biasSkillHandler;
    _biasSkillHandler = null;
}
PlayerBiasTracker.Instance?.UnregisterPlayer(OwnerClientId);
```

### Fix 11: SampleTeamDistance implementation + zero-sample guard
Replace placeholder with actual implementation using NetworkManager.ConnectedClientsList:
```csharp
void SampleTeamDistance()
{
    var nm = NetworkManager.Singleton;
    if (nm == null) return;
    var clients = nm.ConnectedClientsList;
    foreach (var kvp in _data)
    {
        ulong id = kvp.Key;
        if (!nm.ConnectedClients.TryGetValue(id, out var nc) || nc.PlayerObject == null) continue;
        Vector3 pos = nc.PlayerObject.transform.position;
        foreach (var other in clients)
        {
            if (other.ClientId == id || other.PlayerObject == null) continue;
            float dist = Vector3.Distance(pos, other.PlayerObject.transform.position);
            kvp.Value.teamDistanceSum += dist;
            kvp.Value.teamDistanceSamples++;
        }
    }
}
```

Zero-sample guard in EvaluateAll — produce neutral biases when no samples:
```csharp
if (d.teamDistanceSamples > 0)
{
    float avgDist = d.teamDistanceSum / d.teamDistanceSamples;
    float threshold = Mathf.Max(_teamCloseThreshold, 0.01f);
    d.biases[7] = avgDist < threshold ? 1f - (avgDist / threshold) : 0f;
    d.biases[8] = avgDist > threshold ? Mathf.Min((avgDist - threshold) / threshold, 1f) : 0f;
}
else
{
    d.biases[7] = 0f;
    d.biases[8] = 0f;
}
```

Also add `[Min(0.01f)]` to `_teamCloseThreshold` field to prevent inspector misconfiguration:
```csharp
[SerializeField, Min(0.01f)] float _teamCloseThreshold = 8f;
```

---

## R4 → R5 Changes

### Fix 7: RegisterPlayer / UnregisterPlayer calls in PNC3D
All `Record*` methods no-op if clientId is not in `_data`. Must call `RegisterPlayer` before any recording.
- **PNC3D.OnNetworkSpawn (server branch)** — register before subscribing:
  ```csharp
  if (IsServer)
  {
      PlayerBiasTracker.Instance?.RegisterPlayer(OwnerClientId);
      
      var exec = GetComponent<SkillExecutor>();
      if (exec != null && PlayerBiasTracker.Instance != null)
      {
          ulong clientId = OwnerClientId;
          _biasSkillHandler = (def, ctx) => PlayerBiasTracker.Instance?.RecordSkillCast(clientId, def, ctx);
          exec.OnExecuted += _biasSkillHandler;
      }
  }
  ```
- **PNC3D.OnNetworkDespawn** — unsubscribe then unregister:
  ```csharp
  if (_biasSkillHandler != null)
  {
      var exec = GetComponent<SkillExecutor>();
      if (exec != null) exec.OnExecuted -= _biasSkillHandler;
      _biasSkillHandler = null;
  }
  PlayerBiasTracker.Instance?.UnregisterPlayer(OwnerClientId);
  ```

### Fix 8: Add `using ArenaCombat.Core.AI;` to PNC3D
`PlayerNetworkController3D.cs` is in `ArenaCombat.Core.Network` namespace. Needs `using ArenaCombat.Core.AI;` for unqualified `PlayerBiasTracker` references.

### Fix 9: Local clientId capture + null-conditional Instance access
Capture `OwnerClientId` into a local `ulong clientId` before the lambda, and use `PlayerBiasTracker.Instance?.RecordSkillCast(...)` (null-conditional) to tolerate tracker teardown during late event fires.

---

## R3 → R4 Changes

### Fix 5: RoleTags is SkillRoleTag[] not flags
`SkillRoleTag` is a plain sequential enum, `SkillDefinition.RoleTags` is `SkillRoleTag[]`.
Replace all `def.RoleTags.HasFlag(X)` with `System.Array.Exists(def.RoleTags, t => t == X)`:
```csharp
bool IsRanged(SkillDefinition def) => def.RoleTags != null && System.Array.Exists(def.RoleTags, t => t == SkillRoleTag.Ranged);
bool IsSurvival(SkillDefinition def) => def.RoleTags != null && System.Array.Exists(def.RoleTags, t => t == SkillRoleTag.Heal || t == SkillRoleTag.Shield || t == SkillRoleTag.Survival || t == SkillRoleTag.Regen);
```

### Fix 6: SkillExecutor subscription lifecycle with clientId capture
OnExecuted emits `(SkillDefinition, SkillContext)` — no clientId. Solution:
- **PNC3D.OnNetworkSpawn (server branch)** registers + subscribes with local clientId capture:
  ```csharp
  if (IsServer)
  {
      PlayerBiasTracker.Instance?.RegisterPlayer(OwnerClientId);
      var exec = GetComponent<SkillExecutor>();
      if (exec != null && PlayerBiasTracker.Instance != null)
      {
          ulong clientId = OwnerClientId;
          _biasSkillHandler = (def, ctx) => PlayerBiasTracker.Instance?.RecordSkillCast(clientId, def, ctx);
          exec.OnExecuted += _biasSkillHandler;
      }
  }
  ```
- **PNC3D.OnNetworkDespawn** unsubscribes then unregisters:
  ```csharp
  if (_biasSkillHandler != null)
  {
      var exec = GetComponent<SkillExecutor>();
      if (exec != null) exec.OnExecuted -= _biasSkillHandler;
      _biasSkillHandler = null;
  }
  PlayerBiasTracker.Instance?.UnregisterPlayer(OwnerClientId);
  ```
- **PNC3D field**: `private System.Action<SkillDefinition, SkillContext> _biasSkillHandler;`
- **PNC3D using**: `using ArenaCombat.Core.AI;`
- This keeps PlayerBiasTracker decoupled — it doesn't search for SkillExecutors.

## R2 → R3 Changes

### Fix 1: Event signature with SkillContext
```csharp
// SkillExecutor.cs
public event System.Action<SkillDefinition, SkillContext> OnExecuted;
```
Invoked after `skill.RuntimeStep.Invoke(ctx)` in Execute():
```csharp
// After line ~108 (RuntimeStep.Invoke(ctx)), before return true:
OnExecuted?.Invoke(skill, ctx);
return true;
```
This gives PlayerBiasTracker access to `ctx.TargetDistance` for ranged classification and `skill.RoleTags` for survival/heal classification.

### Fix 2: Correct denominator
`totalActions` = melee + skillCasts + parry + rope (4 atomic categories). Sub-classifications (`rangedSkillCasts`, `survivalSkillCasts`) are derived from `skillCasts` and do NOT add to denominator. A ranged survival skill counts as 1 totalAction (skillCasts++), with rangedSkillCasts++ and survivalSkillCasts++ as separate tracking.

### Fix 3: RoleTag classification
`SkillDefinition.RoleTags` (flags enum) checked at record time:
```csharp
void RecordSkillCast(ulong clientId, SkillDefinition def, SkillContext ctx)
{
    var d = GetData(clientId);
    d.skillCasts++;
    d.totalActions++;
    
    if (def.RoleTags.HasFlag(SkillRoleTag.Ranged) || ctx.TargetDistance > _rangedDistanceThreshold)
        d.rangedSkillCasts++;
    if (def.RoleTags.HasFlag(SkillRoleTag.Heal) || def.RoleTags.HasFlag(SkillRoleTag.Shield) 
        || def.RoleTags.HasFlag(SkillRoleTag.Survival) || def.RoleTags.HasFlag(SkillRoleTag.Regen))
        d.survivalSkillCasts++;
}
```

### Fix 4: event not plain Action
Using `public event Action<SkillDefinition, SkillContext>` — prevents external invoke/overwrite.

## Full Diff Sketch

### SkillExecutor.cs (EDIT, +3 lines)
```csharp
// New field after existing fields:
public event System.Action<SkillDefinition, SkillContext> OnExecuted;

// In Execute(), after skill.RuntimeStep.Invoke(ctx) (line ~108), before return true:
OnExecuted?.Invoke(skill, ctx);
```

### PlayerBiasTracker.cs (NEW, ~150 LOC)
```csharp
using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using ArenaCombat.Core.Skill;

namespace ArenaCombat.Core.AI
{
    [DisallowMultipleComponent]
    public class PlayerBiasTracker : MonoBehaviour
    {
        public static PlayerBiasTracker Instance { get; private set; }

        [SerializeField] float _evalInterval = 5f;
        [SerializeField] float _rangedDistanceThreshold = 5f;
        [SerializeField, Min(0.01f)] float _teamCloseThreshold = 8f;

        class PlayerBiasData
        {
            public int meleeAttempts;
            public int skillCasts;
            public int rangedSkillCasts;
            public int survivalSkillCasts;
            public int parryAttempts;
            public int ropeUses;
            public int totalActions;        // = melee + skill + parry + rope
            public float teamDistanceSum;
            public int teamDistanceSamples;
            public float[] biases = new float[9];
        }

        Dictionary<ulong, PlayerBiasData> _data = new();
        float _nextEvalTime;

        void Awake() { if (Instance == null) Instance = this; else { Destroy(this); return; } }
        void OnDestroy() { if (Instance == this) Instance = null; }

        bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        public void RegisterPlayer(ulong clientId) { if (IsServer) _data.TryAdd(clientId, new PlayerBiasData()); }
        public void UnregisterPlayer(ulong clientId) { if (IsServer) _data.Remove(clientId); }

        public void RecordMelee(ulong clientId)
        {
            if (!IsServer || !_data.TryGetValue(clientId, out var d)) return;
            d.meleeAttempts++;
            d.totalActions++;
        }

        public void RecordSkillCast(ulong clientId, SkillDefinition def, SkillContext ctx)
        {
            if (!IsServer || !_data.TryGetValue(clientId, out var d)) return;
            d.skillCasts++;
            d.totalActions++;
            bool ranged = (def.RoleTags != null && System.Array.Exists(def.RoleTags, t => t == SkillRoleTag.Ranged))
                          || (ctx != null && ctx.TargetDistance > _rangedDistanceThreshold);
            if (ranged) d.rangedSkillCasts++;
            bool survival = def.RoleTags != null && System.Array.Exists(def.RoleTags, t =>
                t == SkillRoleTag.Heal || t == SkillRoleTag.Shield || t == SkillRoleTag.Survival || t == SkillRoleTag.Regen);
            if (survival) d.survivalSkillCasts++;
        }

        public void RecordParry(ulong clientId)
        {
            if (!IsServer || !_data.TryGetValue(clientId, out var d)) return;
            d.parryAttempts++;
            d.totalActions++;
        }

        public void RecordRope(ulong clientId)
        {
            if (!IsServer || !_data.TryGetValue(clientId, out var d)) return;
            d.ropeUses++;
            d.totalActions++;
        }

        public float[] GetBiases(ulong clientId)
        {
            if (_data.TryGetValue(clientId, out var d)) return d.biases;
            return null;
        }

        void FixedUpdate()
        {
            if (!IsServer) return;
            SampleTeamDistance();
            if (Time.time < _nextEvalTime) return;
            _nextEvalTime = Time.time + _evalInterval;
            EvaluateAll();
        }

        void SampleTeamDistance()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            var clients = nm.ConnectedClientsList;
            foreach (var kvp in _data)
            {
                ulong id = kvp.Key;
                if (!nm.ConnectedClients.TryGetValue(id, out var nc) || nc.PlayerObject == null) continue;
                Vector3 pos = nc.PlayerObject.transform.position;
                foreach (var other in clients)
                {
                    if (other.ClientId == id || other.PlayerObject == null) continue;
                    float dist = Vector3.Distance(pos, other.PlayerObject.transform.position);
                    kvp.Value.teamDistanceSum += dist;
                    kvp.Value.teamDistanceSamples++;
                }
            }
        }

        void EvaluateAll()
        {
            foreach (var kvp in _data)
            {
                var d = kvp.Value;
                int t = Mathf.Max(d.totalActions, 1);
                d.biases[0] = (float)d.meleeAttempts / t;         // Melee
                d.biases[1] = (float)d.rangedSkillCasts / t;      // Ranged
                d.biases[2] = (float)(d.meleeAttempts + d.skillCasts) / t; // AttackFocused
                d.biases[3] = (float)d.survivalSkillCasts / t;    // Survival
                d.biases[4] = (float)d.parryAttempts / t;         // Parry
                d.biases[5] = (float)d.ropeUses / t;              // Rope
                d.biases[6] = (float)d.skillCasts / t;            // SkillFocused
                if (d.teamDistanceSamples > 0)
                {
                    float avgDist = d.teamDistanceSum / d.teamDistanceSamples;
                    float threshold = Mathf.Max(_teamCloseThreshold, 0.01f);
                    d.biases[7] = avgDist < threshold ? 1f - (avgDist / threshold) : 0f;
                    d.biases[8] = avgDist > threshold ? Mathf.Min((avgDist - threshold) / threshold, 1f) : 0f;
                }
                else
                {
                    d.biases[7] = 0f;
                    d.biases[8] = 0f;
                }
                Debug.Log($"[Bias] client={kvp.Key} M={d.biases[0]:F2} R={d.biases[1]:F2} AF={d.biases[2]:F2} S={d.biases[3]:F2} P={d.biases[4]:F2} Rp={d.biases[5]:F2} SF={d.biases[6]:F2} TC={d.biases[7]:F2} TS={d.biases[8]:F2}");
                // Reset counters
                d.meleeAttempts = d.skillCasts = d.rangedSkillCasts = d.survivalSkillCasts = 0;
                d.parryAttempts = d.ropeUses = d.totalActions = 0;
                d.teamDistanceSum = 0; d.teamDistanceSamples = 0;
            }
        }
    }
}
```

### PNC3D (EDIT, +16 lines: using + field + 3 record hooks + register/subscribe lifecycle)
```csharp
// New using:
using ArenaCombat.Core.AI;

// New field:
private System.Action<SkillDefinition, SkillContext> _biasSkillHandler;

// OnNetworkSpawn server branch — register + subscribe:
PlayerBiasTracker.Instance?.RegisterPlayer(OwnerClientId);
var exec = GetComponent<SkillExecutor>();
if (exec != null && PlayerBiasTracker.Instance != null)
{
    ulong clientId = OwnerClientId;
    _biasSkillHandler = (def, ctx) => PlayerBiasTracker.Instance?.RecordSkillCast(clientId, def, ctx);
    exec.OnExecuted += _biasSkillHandler;
}

// After TryProcessAttack3D accepted (~line 1078):
PlayerBiasTracker.Instance?.RecordMelee(OwnerClientId);

// After TryProcessParry3D accepted (~line 1121):
PlayerBiasTracker.Instance?.RecordParry(OwnerClientId);

// After rope activation (networkIsRoping.Value = true, ~line 932):
PlayerBiasTracker.Instance?.RecordRope(OwnerClientId);

// OnNetworkDespawn — unsubscribe + unregister:
if (_biasSkillHandler != null)
{
    var exec2 = GetComponent<SkillExecutor>();
    if (exec2 != null) exec2.OnExecuted -= _biasSkillHandler;
    _biasSkillHandler = null;
}
PlayerBiasTracker.Instance?.UnregisterPlayer(OwnerClientId);
```

### BNC3D (EDIT, -1 line)
Delete line 412: `Debug.Log($"[Boss] TakeDamage ...")`.

## Risks / unknowns
1. **SkillRoleTag flags enum**: Need to verify HasFlag works (flags enum requirement). If SkillRoleTag is not [Flags], use == or Contains check on a list.
2. **SkillExecutor subscription lifecycle**: PlayerBiasTracker must subscribe to each player's SkillExecutor.OnExecuted. Subscription timing: on RegisterPlayer, find the player GO's SkillExecutor and subscribe. Unsubscribe on UnregisterPlayer.
3. **AttackFocused bias can exceed 1.0**: (melee + skill) / total can be > 1 if both are present. Should clamp to 1.0 or redesign. Preference: `Mathf.Min(..., 1f)`.

## Questions for Codex
1. Is `SkillRoleTag` a `[Flags]` enum? If not, what's the correct way to check multiple tags on a SkillDefinition?
2. Should we clamp AttackFocused to 1.0, or is (melee+skill)/total semantically the correct offensive ratio?
3. SkillExecutor subscription: should PlayerBiasTracker find SkillExecutors via GameManager.Players list, or should PNC3D call `PlayerBiasTracker.Instance?.SubscribeSkillExecutor(GetComponent<SkillExecutor>(), OwnerClientId)` during OnNetworkSpawn?
