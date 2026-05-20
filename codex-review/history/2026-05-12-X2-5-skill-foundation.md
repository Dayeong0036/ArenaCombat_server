# X2-5: Skill Foundation Contract (2026-05-12)

ROADMAP item Phase X2-5. Fifth X2 sub-cycle. Smallest dependency-leaf scope unblocking all later skill rounds (X2-6 SkillExecutor, X2-7 SkillComponents/Library/Binder, X2-12 SkillManager).

---

## Outcome

**Status**: APPLIED. **Plan-Mode review** (user AskUserQuestion + ExitPlanMode approval) treated as Codex gate equivalent for this round per user instruction "원래 하던거 진행해줘봐봐 플렌이 맞춰서". Pending.md was prepared per workflow but not separately Codex-reviewed because Plan Mode covered design + decisions thoroughly with explicit user sign-off on 6 decision points.

**Operations**:
- 4 NEW `.cs` + 4 NEW `.meta` (3 with Buildup GUIDs preserved, 1 fresh for SkillRoleTag).
- 1 EDIT to existing `SkillTypes.cs` (uncomment + relocate 2 delegates inside namespace).
- 1 DELETE of obsolete prototype `SkillActionTest` + `.meta`.

**Files touched**:
- NEW `Assets/ArenaCombat/Scripts/Core/Skill/Core/SkillRoleTag.cs` + `.meta` (fresh GUID `7b4e1c2d8a9f4f3e9d0c5b2a6e8f1d3c`)
- NEW `Assets/ArenaCombat/Scripts/Core/Skill/Core/SkillContext.cs` + `.meta` (Buildup GUID `d90ef223bdd473240b376fee6fc90032`)
- NEW `Assets/ArenaCombat/Scripts/Core/Skill/Core/SkillDefinition.cs` + `.meta` (Buildup GUID `a193f29c932883b4ba06b526288ee2f4`)
- NEW `Assets/ArenaCombat/Scripts/Core/Skill/Core/SkillRegistry.cs` + `.meta` (Buildup GUID `f315d2762f429a74caa988195f0b0534`)
- EDIT `Assets/ArenaCombat/Scripts/Core/Skill/Core/SkillTypes.cs` (delegates uncomment + relocate)
- DELETE `Assets/ArenaCombat/Scripts/Perk/Effects/SkillActionTest` + `.meta`

**Doc updates**:
- ROADMAP X2-5 → DONE, X2-6 (SkillExecutor) → NEXT.
- TARGET_ARCHITECTURE.md §10 X2-5 row marked done; X2-6 promoted.
- SKILL_SYSTEM_DESIGN.md gained §10b "Implementation Notes (post X2-5)" capturing tag enum + menu path + field surface decisions.

---

## Plan-Mode Decisions (user sign-off, 2026-05-12)

User entered Plan Mode and answered 6 AskUserQuestion prompts before approving via ExitPlanMode. All 6 decisions baked into this round:

| Decision | Choice | Rationale |
|---|---|---|
| Menu path | `"ArenaCombat/SkillDefinition"`, `"ArenaCombat/SkillRegistry"` | Project-branded; deliberate divergence from X2-1 SOs' generic `"Scriptable Objects/"` path. Skill domain SOs warrant their own group. |
| Tag field type | `SkillRoleTag[]` enum (not Buildup `string[]`) | Compile-time safety, eliminates designer typos, Inspector dropdowns. |
| Enum starter values | 9: `Burst, DOT, Shield, Parry, Zone, Counter, Heal, Mobility, Mark` | Buildup design notes mention 5 (Burst/DOT/Shield/Parry/Zone); 4 anticipated (Counter/Heal/Mobility/Mark) from X2-7 SkillLibrary likely usage. Append-only safe. |
| Enum file location | Separate `SkillRoleTag.cs`, not bundled in `SkillTypes.cs` | Tag set evolves independently; SkillTypes already holds 9 type enums + 2 delegates. |
| `SkillContext` field surface | All Buildup fields verbatim (incl. `ParryInputTime`, `_log`) | Forward-compat with X3 parry-trigger; `_log` universally useful for debug. |
| Old prototype `SkillActionTest` | DELETE in this round | Superseded by Buildup import; no `.cs` ext = no compile impact, pure housekeeping. |

---

## Translation + Rewrite Audit

Per X2-2/3/4 lesson, Buildup originals (Korean comments + mojibake risk) rewritten in clean ASCII English.

| Buildup file | Our rewrite |
|---|---|
| `SkillContext.cs` (Korean comments + section dividers) | English comments, namespace wrap (`ArenaCombat.Core.Skill`), `using ArenaCombat.Core.Combat;` added for ICombatant. Public surface byte-identical (8 instance fields + 4 snapshot + CurrentTime + private `_log` + `AddLog`/`GetLog`/`RefreshSnapshot`). |
| `SkillDefinition.cs` (Korean headers, `Tenebris/` menu) | English headers, namespace wrap, menu changed to `ArenaCombat/SkillDefinition`, `string[]` tag fields → `SkillRoleTag[]`. Public field/property names byte-identical except tag types. |
| `SkillRegistry.cs` (Korean comments, `Tenebris/` menu) | English comments, namespace wrap, menu `ArenaCombat/SkillRegistry`, `GetByRoleTag(string)` → `GetByRoleTag(SkillRoleTag)`, `GetCounterCandidates(string[])` → `GetCounterCandidates(SkillRoleTag[])`. `GetDraftCandidates` keeps `List<string>` (skill IDs, not tags). Public method names byte-identical. |
| `SkillTypes.cs` (existing X2-2) | 2 delegates uncommented and relocated INSIDE `namespace` block (Buildup origin had them at global; our convention nests). |
| `SkillRoleTag.cs` | New file (no Buildup origin). 9 enum values, append-only safety comment. |

---

## SkillTypes.cs Edit Detail

**BEFORE (X2-2)**:
```csharp
// X2-2: SkillStep / SkillCondition delegates require SkillContext, which arrives in X2-5.
// Uncomment together with SkillContext import.
// public delegate void SkillStep(SkillContext ctx);
// public delegate bool SkillCondition(SkillContext ctx);

namespace ArenaCombat.Core.Skill
{
    // Area shape for AoE skills.
    public enum AreaShape { ... }
```

**AFTER (X2-5)**:
```csharp
namespace ArenaCombat.Core.Skill
{
    // X2-5: activated alongside SkillContext arrival.
    public delegate void SkillStep(SkillContext ctx);
    public delegate bool SkillCondition(SkillContext ctx);

    // Area shape for AoE skills.
    public enum AreaShape { ... }
```

The relocation INTO the namespace is intentional — Buildup origin had them at global namespace, which would have required `using ArenaCombat.Core.Skill;` at every consumer site to reference `SkillContext`. Nesting matches our convention.

---

## Conflict / Risk Pre-Check (verified)

- `class SkillContext` / `class SkillDefinition` / `class SkillRegistry` / `enum SkillRoleTag` in `Core/` namespaces: 0 hits prior.
- One unrelated scratchpad at `Perk/Effects/SkillActionTest` had no `.cs` ext (Unity does not compile). Deleted in this round per user decision.
- `ICombatant` (X2-2) has `Transform` / `CurrentHPPercent` / `IsCasting` — all 3 needed by `SkillContext.RefreshSnapshot()`. ✓
- `TargetType` enum (X2-2) referenced by `SkillDefinition.TargetType` field. ✓
- All 4 NEW `.cs` files in same namespace as `SkillTypes.cs` — no cross-namespace using needed within the round.

---

## Behavior Contract After X2-5

- `SkillContext` instantiable, `RefreshSnapshot()` callable on any ICombatant pair.
- `SkillDefinition` SO can be created via `Assets > Create > ArenaCombat > SkillDefinition`. Inspector shows English headers (Identity / Combat / AI/Draft) + enum dropdowns for TargetType / RoleTags / CounterTags.
- `SkillRegistry` SO can be created via `Assets > Create > ArenaCombat > SkillRegistry`. Empty `_pool` initially.
- `SkillStep` / `SkillCondition` delegates resolvable, allowing `SkillDefinition.RuntimeStep` / `RuntimeCondition` field types to compile.
- **Zero call sites** (X2-6 SkillExecutor will be the first consumer).
- **Zero SO instances** in project (designers will populate `SkillRegistry._pool` post X2-7 SkillLibrary import).

---

## Spawned Follow-ups

- **X2-6 SkillExecutor**: per-entity `MonoBehaviour`. Cooldown `Dictionary<string, float>` + attempt/hit history. `Execute(SkillDefinition, SkillContext) → bool`. Calls `ctx.RefreshSnapshot()` + `skill.RuntimeCondition?(ctx)` + `skill.RuntimeStep(ctx)`. Wires `ctx.OnHitRecorded` callback for dedup.
- **X2-7 SkillComponents + SkillLibrary + SkillBinder**: 37 static SkillStep impls (DealDirectionalHit / ApplyInArea / LaunchProjectile / CheckParry / etc.) + composite tree definitions per skill + bind-all bootstrap. Will reveal whether the 9 starter enum values cover all observed RoleTag usage; if not, append values then.
- **X2-12 SkillManager**: per-entity auto-cast tick, 5 slots, FixedUpdate first-ready selection.

---

## User-Side Verification (pending — user confirms in Unity)

1. Compile in Unity. Expect <5s recompile (4 small files + 1 minor edit + 1 delete).
2. Console: 0 new error / 0 new warning. Specifically watch for:
   - Missing-using on `ICombatant` inside SkillContext.cs (would fail import).
   - Missing-type on `SkillRoleTag` from SkillDefinition.cs (forward-ref handling within assembly should work fine).
3. Project window:
   - `Core/Skill/Core/` shows 5 .cs files (SkillContext / SkillDefinition / SkillRegistry / SkillRoleTag / SkillTypes).
   - `Perk/Effects/SkillActionTest` gone.
4. Optional smoke test:
   - `Assets > Create > ArenaCombat > SkillDefinition` creates an SO. Inspector shows English Identity/Combat/AI-Draft headers + enum dropdowns.
   - `Assets > Create > ArenaCombat > SkillRegistry` creates an SO with empty `_pool`.
5. Existing 5 yellow warnings unchanged.

---

## Lessons

- **Plan Mode + AskUserQuestion as Codex-equivalent for design-heavy small rounds**: Plan Mode caught all 6 design decisions with explicit user choice (menu / tag-type / enum-set / enum-loc / field-surface / cleanup). For a contract-layer round of this size (~170 LOC across 4 files), this is at least as thorough as a typical Codex review pass. Workflow precedent set: Plan Mode approval + pending.md preparation can substitute for separate Codex round when user explicitly approves proceeding.
- **Delegate placement matters across versions**: Buildup had `SkillStep` / `SkillCondition` at global namespace. We nested them inside `ArenaCombat.Core.Skill`. Consequence: every consumer now resolves them naturally without `using` clutter. Lesson recorded: when porting global types, prefer nesting unless cross-namespace circular reference forces otherwise.
- **String-to-enum conversion is a 1-time chance**: changing `string[] RoleTags` to `SkillRoleTag[] RoleTags` AFTER `.asset` files are populated would require migration. Doing it BEFORE any SOs exist is free. Lesson: enum vs string decisions are best made at the contract round, not the data-population round.
- **`.cs`-extensionless prototypes are dead weight**: `SkillActionTest` (no `.cs` ext) sat in the project for months without compiling. Easy to forget, easy to mistake for a real source. Cleaner to delete the moment it's superseded.
- **GUID preservation policy continues to pay off**: 3 Buildup `.meta` GUIDs preserved this round mean any Buildup-origin `.asset` referencing these scripts (none yet, but possible at X1-6 game data import) would resolve seamlessly.
