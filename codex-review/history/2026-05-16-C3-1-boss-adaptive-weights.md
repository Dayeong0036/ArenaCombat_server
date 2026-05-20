# Pending Codex Review — C3-1 Boss Adaptive Skill Weights (R2)

## Topic
Phase C3-1: Connect PlayerBiasTracker data to boss skill selection. **Revision 2** — addresses R1 feedback (RoleTags not CounterTags, single CanCast pass, using fix).

## Roadmap link
- **C3. 보스 적응형 가중치 적용**

## Files to touch
1. **NEW** `Assets/ArenaCombat/Scripts/Core/AI/BossAdaptiveWeights.cs` (~100 LOC)
2. **EDIT** `Assets/ArenaCombat/Scripts/Core/AI/PlayerBiasTracker.cs` (+10 lines) — GetAverageBiases()
3. **EDIT** `Assets/ArenaCombat/Scripts/Core/Skill/Core/SkillManager.cs` (+25 lines) — Weighted auto-cast
4. **EDIT** `Assets/ArenaCombat/Scripts/Core/Network/BossNetworkController3D.cs` (+1 line) — Wire adaptive weights

## R1 → R2 Changes

### Fix 1: Use RoleTags, not CounterTags
R1 checked `skill.CounterTags` — that's "what this skill counters", not "what this skill IS". The correct field is `skill.RoleTags` (what the skill IS). Mapping now: when players favor melee → boss prefers skills tagged Ranged (to keep distance), etc.

Bias → preferred boss RoleTag:
```
Bias[0] Melee       → RoleTag Ranged    (keep players at range)
Bias[1] Ranged      → RoleTag Melee     (close gap, pressure)
Bias[2] AttackFocused → RoleTag Shield   (defensive counter)
Bias[3] Survival    → RoleTag Burst     (burst through healing)
Bias[4] Parry       → RoleTag AOE       (unparriable area attacks)
Bias[5] Rope        → RoleTag Zone      (area denial)
Bias[6] SkillFocused → RoleTag Counter  (counter-pick)
Bias[7] TeamClose   → RoleTag AOE      (punish clustering)
Bias[8] TeamSpread  → RoleTag Mark     (isolate spread players)
```

### Fix 2: Single CanCast pass — collect eligible entries once
R1 called CanCast twice (weight sum + selection). CanCast invokes RuntimeCondition (arbitrary delegate, not guaranteed pure). Fix: collect `(slot, ctx, weight)` in one pass, then weighted random from collected list.

### Fix 3: SkillManager `using ArenaCombat.Core.AI;`

## Full Implementation

### PlayerBiasTracker.cs — Add GetAverageBiases()
```csharp
public float[] GetAverageBiases()
{
    if (_data.Count == 0) return null;
    float[] avg = new float[9];
    foreach (var kvp in _data)
    {
        for (int i = 0; i < 9; i++)
            avg[i] += kvp.Value.biases[i];
    }
    for (int i = 0; i < 9; i++)
        avg[i] /= _data.Count;
    return avg;
}
```

### BossAdaptiveWeights.cs (NEW)
```csharp
using UnityEngine;
using ArenaCombat.Core.Skill;

namespace ArenaCombat.Core.AI
{
    [DisallowMultipleComponent]
    public class BossAdaptiveWeights : MonoBehaviour
    {
        public static BossAdaptiveWeights Instance { get; private set; }

        [SerializeField] float _baseWeight = 1f;
        [SerializeField] float _biasMultiplier = 2f;

        static readonly SkillRoleTag[] BiasResponseMap = new SkillRoleTag[]
        {
            SkillRoleTag.Ranged,   // 0 Melee     → prefer ranged skills
            SkillRoleTag.Melee,    // 1 Ranged    → prefer melee skills
            SkillRoleTag.Shield,   // 2 AttackFocused → prefer defensive
            SkillRoleTag.Burst,    // 3 Survival  → prefer burst
            SkillRoleTag.AOE,      // 4 Parry     → prefer area (unparriable)
            SkillRoleTag.Zone,     // 5 Rope      → prefer zone denial
            SkillRoleTag.Counter,  // 6 SkillFocused → prefer counter
            SkillRoleTag.AOE,      // 7 TeamClose → prefer AoE
            SkillRoleTag.Mark,     // 8 TeamSpread → prefer mark
        };

        void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(this); return; }
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public float ComputeWeight(SkillDefinition skill)
        {
            float weight = _baseWeight;
            if (skill.RoleTags == null || skill.RoleTags.Length == 0)
                return weight;

            if (PlayerBiasTracker.Instance == null)
                return weight;

            float[] avgBias = PlayerBiasTracker.Instance.GetAverageBiases();
            if (avgBias == null) return weight;

            for (int b = 0; b < BiasResponseMap.Length && b < avgBias.Length; b++)
            {
                if (avgBias[b] <= 0f) continue;
                if (System.Array.Exists(skill.RoleTags, t => t == BiasResponseMap[b]))
                    weight += avgBias[b] * _biasMultiplier;
            }

            return weight;
        }
    }
}
```

### SkillManager.cs — Weighted auto-cast (single CanCast pass)
```csharp
// New using:
using ArenaCombat.Core.AI;

// New field (after _roundRobinStart):
private bool _useAdaptiveWeights;
public bool UseAdaptiveWeights { get => _useAdaptiveWeights; set => _useAdaptiveWeights = value; }

// Struct for eligible skill cache:
private struct EligibleSkill
{
    public int Slot;
    public SkillContext Ctx;
    public float Weight;
}

// In Update(), BEFORE the existing sequential for-loop, add:
if (_useAdaptiveWeights && BossAdaptiveWeights.Instance != null)
{
    // Single pass: collect all castable skills with weights
    var eligible = new System.Collections.Generic.List<EligibleSkill>(count);
    float totalWeight = 0f;
    for (int n = 0; n < count; n++)
    {
        int i = (start + n) % count;
        if (_slots[i] == null) continue;
        if (!CanCast(_slots[i], out var ctx)) continue;
        float w = BossAdaptiveWeights.Instance.ComputeWeight(_slots[i]);
        eligible.Add(new EligibleSkill { Slot = i, Ctx = ctx, Weight = w });
        totalWeight += w;
    }
    if (eligible.Count > 0 && totalWeight > 0f)
    {
        float roll = UnityEngine.Random.value * totalWeight;
        float cumulative = 0f;
        for (int e = 0; e < eligible.Count; e++)
        {
            cumulative += eligible[e].Weight;
            if (roll <= cumulative)
            {
                var picked = eligible[e];
                if (_stateManager != null) _stateManager.NotifyCastStart();
                bool fired = _executor.Execute(_slots[picked.Slot], picked.Ctx);
                if (_stateManager != null) _stateManager.NotifyCastEnd();
                if (fired)
                {
                    if (_logAutoCast)
                        Debug.Log($"[AutoCast:Adaptive] slot[{picked.Slot}] {_slots[picked.Slot].DisplayName} w={picked.Weight:F2}");
                    if (_roundRobinEnabled) _roundRobinStart = (picked.Slot + 1) % count;
                }
                break;
            }
        }
    }
    return;
}
// ... existing sequential auto-cast below (unchanged)
```

### BossNetworkController3D.cs — Wire
In `PopulateBossSkills()`, after `_skillMgr.SetAutoCast(true)`:
```csharp
_skillMgr.UseAdaptiveWeights = (BossAdaptiveWeights.Instance != null);
```

## Scene setup
- Add BossAdaptiveWeights GO to Chapter1.unity + 3DScene.unity (same pattern as PlayerBiasTracker)
- Boss skill assets' RoleTags already contain relevant tags (AOE, Melee, Ranged, Burst, Shield, Zone, Mark, Counter) — no data edits needed for basic functionality

## Risks / unknowns
1. **List allocation per Update frame** — `List<EligibleSkill>` allocates. For boss tick rate this is fine. Optimize with static buffer if profiling shows GC pressure.
2. **Bias data lag** — biases update every 5s (PlayerBiasTracker._evalInterval). Boss adapts at next cast attempt — natural delay is acceptable.
3. **Dual AOE mapping** — both Parry(4) and TeamClose(7) map to AOE. This means AOE skills get double-weighted when both biases are high. Intentional — area attacks counter both parry timing and clustering.
