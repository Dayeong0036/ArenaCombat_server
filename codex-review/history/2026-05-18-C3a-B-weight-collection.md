# Pending Codex Review — C3a Phase B (Round 2): Weight Collection Wiring

## R1 Verdict
**REJECTED** — 3 critical issues found, all addressed below.

| # | R1 Critical Issue | R2 Resolution |
|---|------------------|---------------|
| 1 | "Phase B is not implemented in the inspected workspace" | Acknowledged — pending.md is a PRE-implementation proposal per `codex-review/README.md` cycle. No change needed. |
| 2 | Forwarding shim would not accumulate — no caller registers clients with `PlayerArchetypeClassifier` | **FIX:** Add Register/Unregister forwarding in `PlayerBiasTracker.RegisterPlayer/UnregisterPlayer` so classifier lifecycle mirrors PlayerBiasTracker. Symmetric, no race. |
| 3 | Skill-cast melee rule was worded as `else if` fallback in R1; the plan table treats each row as independent | **FIX:** Make all three conditions (CC / Ranged / Melee) independent — a single skill cast may credit multiple buckets. Matches plan spec table. |

Plus R1 Suggestion #4 ("returning 0 for missing boss/client fabricates Melee distance"): replaced with `float.NaN` sentinel + explicit `IsNaN` skip in classifier.

## Topic
Wire up weight collection for `PlayerArchetypeClassifier`. Forwarding shim added to existing `PlayerBiasTracker.Register/Unregister/RecordX` methods so action sites do NOT need new call sites. Classifier's `RecordX` method bodies filled in with weight accumulation per spec. No classification logic yet (Phase C). No event firing yet (Phase C).

## Roadmap link
ROADMAP.md → Phase C3a, sub-phase B (skeleton landed in Phase A)
Plan file: `C:\Users\paek6\.claude\plans\foamy-baking-melody.md` (Weight Collection Rules section)

## Goal
After this phase, every melee hit / skill cast / parry that already calls `PlayerBiasTracker.RecordX(...)` will ALSO accumulate into the new archetype weight buckets, with correct rules. Classifier client lifecycle is also auto-synced. Verification = log archetype weights periodically and observe accumulation as players act.

## Files to touch
- **EDIT** `Assets/ArenaCombat/Scripts/Core/AI/PlayerBiasTracker.cs` — forward Register/Unregister + RecordX + private `DistToBoss` helper
- **EDIT** `Assets/ArenaCombat/Scripts/Core/AI/PlayerArchetypeClassifier.cs` — fill in 3 `RecordX` bodies, handle NaN distance sentinel

## Approach

### Lifecycle forwarding (R1 fix #2)
PlayerNetworkController3D already calls `PlayerBiasTracker.Instance?.RegisterPlayer(clientId)` on spawn and `UnregisterPlayer(clientId)` on despawn. We forward both to `PlayerArchetypeClassifier.Instance?.Register/UnregisterPlayer(clientId)` from inside `PlayerBiasTracker`. This guarantees:
- Same registration timing as PlayerBiasTracker
- No new call sites in PNC3D
- Symmetric lifecycle (Register/Unregister both forwarded)
- No race: any `RecordX` that arrives after `RegisterPlayer` returns will find a valid entry

Alternative considered (lazy create on first `RecordX`): rejected because it leaks on disconnect unless `UnregisterPlayer` is still forwarded — same code path, no gain.

### Independent weight conditions (R1 fix #3)
Plan spec table treats each row as independent. R2 implements each condition independently:
```
if (isCC)     weights[CC]     += _ccCastWeight
if (isRanged) weights[Ranged] += 1.0
if (isMelee)  weights[Melee]  += 1.0
```
A `CC + long-range` skill credits both CC and Ranged. A `melee + ranged-tagged` skill (e.g. melee weapon with `Ranged` role tag mis-tag) credits both — acceptable, data anomaly is user's tagging issue.

### NaN distance sentinel (R1 suggestion #3)
`DistToBoss` returns `float.NaN` when boss not spawned OR player object missing. Classifier's `RecordX` checks `float.IsNaN(distToBoss)` and skips weight application. This prevents the "pre-spawn melee bias" inflation that R1 flagged. `RecordParrySuccess` doesn't take distance — always applies (Melee +2).

### Weight rules (unchanged from R1, plan-spec table)
Per-event:
| Event | Condition | Weight applied |
|-------|-----------|----------------|
| Melee hit | dist < `_meleeDistance` (5m) | `weights[Melee] += _meleeHitWeight` (1.0) |
| Melee hit | dist ≥ `_meleeDistance` | `weights[Ranged] += 0.5` |
| Skill cast | skill has `CC` or `Silence` role tag | `weights[CC] += _ccCastWeight` (1.5) |
| Skill cast | `skill.Range > 8.0 || dist > _rangedDistance` (10m) | `weights[Ranged] += 1.0` |
| Skill cast | `dist < _meleeDistance` | `weights[Melee] += 1.0` |
| Parry success | always | `weights[Melee] += _parryWeight` (2.0) |

(Per-frame distance sampling + slot CC bias deferred to Phase C.)

## Diff sketch

### `PlayerBiasTracker.cs` — additions only
```csharp
// Add at top:
using ArenaCombat.Core.Network;  // for BossManager

// Inside RegisterPlayer(ulong clientId), at end:
PlayerArchetypeClassifier.Instance?.RegisterPlayer(clientId);

// Inside UnregisterPlayer(ulong clientId), at end:
PlayerArchetypeClassifier.Instance?.UnregisterPlayer(clientId);

// Inside RecordMelee(ulong clientId), at end:
if (PlayerArchetypeClassifier.Instance != null)
    PlayerArchetypeClassifier.Instance.RecordMeleeHit(clientId, DistToBoss(clientId));

// Inside RecordSkillCast(ulong clientId, SkillDefinition def, SkillContext ctx), at end:
if (PlayerArchetypeClassifier.Instance != null)
{
    float dist = ctx != null ? ctx.TargetDistance : DistToBoss(clientId);
    PlayerArchetypeClassifier.Instance.RecordSkillCast(clientId, def, dist);
}

// Inside RecordParry(ulong clientId), at end:
PlayerArchetypeClassifier.Instance?.RecordParrySuccess(clientId);

// New private helper at bottom of class:
static float DistToBoss(ulong clientId)
{
    var nm = NetworkManager.Singleton;
    if (nm == null || !nm.ConnectedClients.TryGetValue(clientId, out var nc) || nc.PlayerObject == null)
        return float.NaN;
    if (BossManager.Instance == null || BossManager.Instance.CurrentBoss == null)
        return float.NaN;
    return Vector3.Distance(
        nc.PlayerObject.transform.position,
        BossManager.Instance.CurrentBoss.transform.position);
}
```

### `PlayerArchetypeClassifier.cs` — fill in method bodies
```csharp
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

    bool isCC     = skill.RoleTags != null && System.Array.Exists(skill.RoleTags,
                       t => t == SkillRoleTag.CC || t == SkillRoleTag.Silence);
    bool isRanged = skill.Range > 8f || distToBoss > _rangedDistance;
    bool isMelee  = distToBoss < _meleeDistance;

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
```

### Suppress-unused `OnValidate` block — adjusted
Remove the `_ = ...;` lines for fields now USED in Phase B method bodies: `_meleeDistance`, `_rangedDistance`, `_meleeHitWeight`, `_ccCastWeight`, `_parryWeight`. Keep the rest (Phase C fields).

## Risks / unknowns

1. **`SkillContext.TargetDistance`**: existing PlayerBiasTracker (`PlayerBiasTracker.cs:70`) already trusts it. If a skill is cast with a null target (e.g. Self-targeting), `ctx.TargetDistance` may be 0 or stale. We treat 0 the same as "very close" → routes to Melee bucket. For pure Self skills this is mildly incorrect but rare for boss-fight skills; mitigation in Phase C if needed.

2. **Multi-bucket credit per cast**: a single skill can now increment up to 3 buckets (CC + Ranged + Melee impossible since Ranged and Melee are distance-mutually-exclusive; CC + Ranged or CC + Melee possible). Total weight added per cast is capped at `_ccCastWeight + 1.0 = 2.5`. This is intentional and matches the plan's "what is this player signaling" interpretation.

3. **Distance-mutual-exclusion**: `isRanged = skill.Range > 8 || dist > 10`, `isMelee = dist < 5`. At `5 ≤ dist ≤ 10` with a short-range skill, NEITHER fires — that cast credits no Melee/Ranged bucket (only CC if tagged). Mid-range is a "neutral zone". Acceptable per plan.

4. **Boss-only matchup**: `DistToBoss` always measures to boss. If the user later adds non-boss enemies, this measurement becomes misleading. Out of scope.

5. **Cross-namespace `using ArenaCombat.Core.Network` in PlayerBiasTracker**: confirmed acceptable in R1 (PlayerBiasTracker already imports `Core.Skill`).

## Questions for Codex

1. Re-confirm R1 Question #1 acceptance after Round 2 changes: `DistToBoss` helper duplicates the position-lookup logic of `PlayerBiasTracker.SampleTeamDistance`. Keep parallel (no extract) — was previously accepted.

2. `Vector3.Distance` includes the Y axis. Boss and player are roughly on the same Y plane (arena floor) but not guaranteed identical. For melee threshold of 5m, this is a 0~1m noise floor — accept, or use 2D XZ distance? My pick: 3D distance, matching PlayerBiasTracker's `SampleTeamDistance` pattern (which also uses 3D).

3. Should `RecordSkillCast`'s `isMelee` check also gate on "skill IS a melee skill" (e.g. `skill.RoleTags.Contains(Melee)`) instead of just distance? My read of the plan: NO — the rule is "where the player WAS when they cast", not what the skill is. A ranged skill cast point-blank still signals close-range play-style. Confirmed?

4. Forwarding `Register`/`Unregister` from PlayerBiasTracker creates an implicit dependency direction (BiasTracker → Classifier). If a future refactor wants to remove BiasTracker, classifier lifecycle breaks. Acceptable for now, or document in classifier's class header? My pick: add a `// Lifecycle managed via PlayerBiasTracker forwarding — see PlayerBiasTracker.RegisterPlayer` comment at the top of classifier.

5. Any concern about `_data.TryGetValue` returning false silently if `RegisterPlayer` was somehow missed? Currently `RecordX` no-ops in that case. Add a `Debug.LogWarning`? My pick: NO log (would spam on every cast for the brief pre-register window). Trust the forwarding to keep lifecycle synced.

## Out of scope for this round
- Per-frame distance sampling (Phase C)
- Classification math + 3-min tick (Phase C)
- Event firing (`OnPlayerArchetypeChanged`) (Phase C)
- Slot CC bias (Phase C)
- BossNetworkController3D / SkillManager / BossAIPoolManager (Phases D-F)
