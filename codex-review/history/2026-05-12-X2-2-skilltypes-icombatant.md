# X2-2: SkillTypes Enums + ICombatant Full Surface (2026-05-12)

ROADMAP item Phase X2-2. Second X2 sub-cycle. Brings the contract layer needed by X2-3+ implementations.

---

## Outcome

**Status**: APPLIED. One Codex review round — APPROVED WITH CHANGES (encoding-driven rewrite).

**Operations**:
- 2 folder creates (`Core/Skill/`, `Core/Skill/Core/`) with fresh GUIDs in folder `.meta`.
- 1 NEW `SkillTypes.cs.meta` copied from Buildup (GUID `55154c2436213b64d99691eeeb8bcbd4` preserved for forward .asset references).
- 1 NEW `SkillTypes.cs` written in clean UTF-8 ASCII (English comments).
- 1 REPLACE `ICombatant.cs` written in clean UTF-8 ASCII (English comments), existing `.meta` GUID preserved.

**Files touched**:
- `Assets/ArenaCombat/Scripts/Core/Skill.meta` (NEW folder meta, GUID `b29de5eaeb50469185d75fc98483c22f`)
- `Assets/ArenaCombat/Scripts/Core/Skill/Core.meta` (NEW folder meta, GUID `39d717fac04f47369263e89970353d55`)
- `Assets/ArenaCombat/Scripts/Core/Skill/Core/SkillTypes.cs` + `.meta` (NEW)
- `Assets/ArenaCombat/Scripts/Core/Combat/ICombatant.cs` (REPLACE, .meta untouched)

**Doc updates**: ROADMAP X2-2 → DONE, X2-3 (StateManager + CombatantState) → NEXT.

---

## Review Cycle Summary

### Round 1 — APPROVED WITH CHANGES

Codex caught two **critical encoding traps** that would have shipped a broken contract if I had followed the verbatim-copy approach from X2-1:

**Critical 1 — SkillTypes.cs**:
Source-file Korean comments in CP949-likely encoding bleed into the next token under some readers. Specifically, certain enum closing braces and values land inside `//` comment lines, so a naive copy+wrap would silently drop enum values (notably `ParryRewardType`, `CleanseType`, `DispelType`).

**Critical 2 — ICombatant.cs**:
Worse — `IsParrying` and `ParryWindow` getters sit on the same source line as the `IsCasting` trailing `//` comment. Verbatim port would yield an interface with **fewer members than intended** (members commented out, not present). Verifying by counting `{ get; }` after copy would have missed it because the visible Buildup file looks fine in a clean editor, but the bytes pass through several encoding-sensitive steps.

**Codex fix directive**: stop preserving Korean mojibake for X2-2 onward. Rewrite contract-layer files in clean UTF-8 with English comments. This is a contract round, not a content-import round — "accurate type surface" beats "byte-identical preservation."

Adopted in full. SkillTypes.cs and ICombatant.cs rewritten from scratch with English comments, type surfaces verified against Buildup originals member-by-member.

**Suggestions (all adopted)**:
- S-1: `SkillStep`/`SkillCondition` delegate comment-out (no orphan stub). ✓
- S-2: `Core/Skill/Core/` folder mirroring Buildup, namespace stays flat `ArenaCombat.Core.Skill`. ✓
- S-3: `CurrentHPPercent` replaces `CurrentHP` per Buildup convention. ✓
- S-4: Pivot to English/ASCII comments for ported files from now on. ✓

---

## Type Surface Verification

### SkillTypes.cs — 9 enums

| Enum | Values |
|---|---|
| `AreaShape` | Circle, Cone, Line |
| `TargetType` | Single, Area, Self, Direction |
| `MoveType` | Dash, Charge, Jump, Rope |
| `ParryRewardType` | Counter, HitStun, Invulnerable, Buff |
| `StatusType` | Stunned, HitStun, Slowed, Rooted, Vulnerable, Silence, Invulnerable, Reflecting, HPRegen, DamageOverTime, AntiHeal, Marked (12) |
| `BuffType` | DamageUp, DefenseUp, ParryWindowUp, ParryRewardUp |
| `DebuffType` | DamageDown, DefenseDown, SelfDefenseDown, Mark |
| `CleanseType` | All, Debuff, DamageOverTime |
| `DispelType` | All, DefenseBuff, OffenseBuff |

Delegates:
- `// public delegate void SkillStep(SkillContext ctx);` — restore in X2-5.
- `// public delegate bool SkillCondition(SkillContext ctx);` — restore in X2-5.

### ICombatant.cs — 23 members (9 properties + 14 methods)

Properties: `Transform`, `GameObject`, `MaxHP`, `CurrentHPPercent`, `Shield`, `IsAlive`, `IsCasting`, `IsParrying`, `ParryWindow`.

Methods: `TakeDamage`, `TakeShieldBreakDamage`, `RecoverHP`, `AddShield`, `ApplyStatus`, `HasStatus`, `ApplyBuff`, `ApplyDebuff`, `RemoveStatuses`, `RemoveBuffs`, `Knockback`, `Pull`, `MoveBy`, `NotifyParryReward`.

(pending.md originally said "22 members" — off-by-one count; actual Buildup surface is 23.)

---

## Conflict / Risk Pre-Check (verified)

| Type | Our project hits | Status |
|---|---|---|
| `class SkillContext` | 0 | OK |
| `class SkillStep` | 0 | OK |
| `enum AreaShape` / `TargetType` / `MoveType` / `ParryRewardType` / `StatusType` / `BuffType` / `DebuffType` / `CleanseType` / `DispelType` | 0 each | OK |

Buildup `StatusType` (regular enum, 12 values) vs our `StatusMask` (bitmask): **different names, different types, coexist without conflict**. Reconciliation between them deferred to X3 when PNC3D implements ICombatant.

Existing `DamageType` / `AttackType` (NetworkConstants.cs) not in SkillTypes.cs → no overlap.

---

## Behavior Contract After X2-2

- 9 enums + commented-out delegates defined in `ArenaCombat.Core.Skill`.
- Full 23-member ICombatant contract defined in `ArenaCombat.Core.Combat`.
- **Zero implementers** in codebase (PNC3D / BossNetworkController3D both defer to X3+).
- **Zero callers** of ICombatant beyond Codex-anticipated future SkillExecutor (X2-6).
- Compile risk: minimal — interfaces + enums are pure type declarations; any unmet contract surfaces at first implementer, which doesn't exist yet.

---

## Spawned Follow-ups

- **X2-5 SkillContext arrival**: uncomment `SkillStep` / `SkillCondition` delegates, add `using` import if needed.
- **X3 PNC3D adapter**: implement 23 ICombatant members. Most map to existing PNC3D state (`MaxHP`/`CurrentHPPercent`/`IsAlive`/`TakeDamage`/`Heal→RecoverHP`); positional control + status/buff routing layers atop StatManager (X2-4). Per S-X0-5-R1-4, decide `Heal`→`RecoverHP` rename vs wrapper at that time.

---

## User-Side Verification (pending — user confirms in Unity)

1. Compile in Unity Editor. Expect 5-10s recompile (2 small files + 2 new folders).
2. Console:
   - **Acceptable**: no new warnings.
   - **Unacceptable**: any C# error (especially missing-type errors on enum names, or "interface member already implemented" if any stray implementer surfaced).
3. Project window:
   - New folder: `Assets/ArenaCombat/Scripts/Core/Skill/Core/` with `SkillTypes.cs`.
   - `Assets/ArenaCombat/Scripts/Core/Combat/ICombatant.cs` size grew ~3x.
4. Pre-existing warnings should NOT increase.

---

## Lessons

- **Encoding-driven hazard is silent**. Visible Buildup file looks fine; the rot only surfaces after copy through PowerShell / editors with different default codepages. Type-level damage (dropped enum values, commented-out members) compiles "successfully" but ships a wrong contract.
- **Verbatim-preservation is wrong for contracts.** For asset-class files (X2-1 SOs), byte-level preservation has value (.meta GUID + future Buildup merge clarity). For interface/enum files, **type-surface correctness wins** — rewrite is safer.
- **Codex's source-grep verification > my member counting.** I counted "22 members" by reading pending.md back; the actual Buildup surface is 23. Codex would have caught this too.
- **Folder .meta creation pattern established**: for new directories outside Unity, write folder `.meta` with fresh GUID before Unity reimports. Skill.meta + Skill/Core.meta in this round set the template.
- **Mojibake policy shift recorded**: X2 contract-layer onward uses English comments. X2-1 SO files keep their Korean mojibake (data-class, low surface risk). Mixed-policy is acceptable; the cleanup cost of retro-converting X2-1 isn't justified.
