# X2-3: CombatantState Enum (Scope-Down) (2026-05-12)

ROADMAP item Phase X2-3. Third X2 sub-cycle. Originally roadmapped as "StateManager + CombatantState"; scope-down to enum only after Buildup `StateManager.cs` dependency analysis.

---

## Outcome

**Status**: APPLIED. One Codex review round — APPROVED WITH CHANGES (priority comment wording).

**Operations**:
- 1 folder create (`Core/State/`) with fresh GUID `e2a27faf07d340538c6e0a5d722d2235` in folder `.meta`.
- 1 NEW `CombatantState.cs.meta` with Buildup GUID `04b911cd207f2db47be37d22579b94d3` preserved.
- 1 NEW `CombatantState.cs` written in clean UTF-8 ASCII (English comments, Codex-approved priority wording).

**Files touched**:
- `Assets/ArenaCombat/Scripts/Core/State.meta` (NEW folder meta)
- `Assets/ArenaCombat/Scripts/Core/State/CombatantState.cs` + `.meta` (NEW)

**Doc updates**:
- ROADMAP X2-3 → DONE, X2-4 → NEXT (paired StatManager + StateManager).
- TARGET_ARCHITECTURE.md §10 migration table: X2-3 reclassified to L3 contract layer, X2-4 becomes paired L4 import.

---

## Review Cycle Summary

### Round 1 — APPROVED WITH CHANGES

Codex approved the scope-down and folder/namespace strategy outright. Single suggestion accepted: priority comment wording.

**Original draft comment** (confusing):
```
// Priority (smaller = stronger):
//   Dead(0) > Stunned(1) > HitStun(2) > Parrying(3) > Casting(4) > Moving(5) > Idle(6)
```

Problem: the parenthetical numbers `Dead(0)..Idle(6)` contradict the actual enum (`Idle=0..Dead=6`). Reader sees the same name with two different numbers and gets confused.

**Codex-recommended wording** (applied verbatim):
```
// State resolution priority, strongest to weakest:
//   Dead > Stunned > HitStun > Parrying > Casting > Moving > Idle
//
// Numeric values are preserved from Buildup for compatibility and do not encode priority.
```

This separates two orthogonal concerns: numeric serialization (preserve Buildup) vs resolution priority (semantic, runtime-only).

**Other Codex confirmations**:
- Clean ASCII rewrite policy correct (X2-2 lesson holds — Buildup source has same mojibake risk).
- `Core/State/` + `ArenaCombat.Core.State` separation is the right placement (future StateManager lands here too).
- `.meta` GUID preservation is policy-consistent though enums aren't typically referenced by MonoScript GUID in `.asset` files.
- Scope-down to enum-only is the correct call given StateManager's hard `RequireComponent(StatManager)` + 4 direct StatManager method calls.

---

## Scope-Down Justification

Buildup `StateManager.cs` dependency surface:
```
StateManager
  ├─ [RequireComponent(typeof(StatManager))]    ← compile-time hard dep
  ├─ GetComponent<StatManager>()                ← required field
  ├─ _stat.HasStatus(StatusType.Rooted/Silence/Stunned/HitStun)
  ├─ _stat.SetCasting(true/false)
  ├─ _stat.BeginParryWindow()
  └─ _stat.EndParryWindow()
```

Without `StatManager`, StateManager produces 7+ compile errors. Stub StatManager would silently return no-op values for `HasStatus` (always false), permanently breaking every state-transition decision the moment a real StatManager replaces it. Pair-import at X2-4 is cleaner.

Roadmap revision recorded: X2-4 explicitly labeled "paired (StatManager + StateManager)".

---

## Type Surface Verification

`CombatantState` — 7 values:
| Name | Value | Priority rank (strongest→weakest) |
|---|---|---|
| Idle | 0 | 7 |
| Moving | 1 | 6 |
| Casting | 2 | 5 |
| Parrying | 3 | 4 |
| HitStun | 4 | 3 |
| Stunned | 5 | 2 |
| Dead | 6 | 1 |

Skill cast gate: `state ∈ {Idle, Moving}` + `!HasStatus(Silence)` (X2-4 enforces the Silence half).

---

## Conflict / Risk Pre-Check (verified)

| Type | Our project hits | Status |
|---|---|---|
| `enum CombatantState` | 0 | OK |
| `class StateManager` | 0 | OK |
| `class StatManager` | 0 | OK |

`CharacterStateId` (existing, used by `PNC3D.networkStateId`) overlaps semantically but has different value set (Idle/Moving/Roping/Skill/Parry/Hit/Dead). **Different names, different taxonomies, coexist without conflict.** Reconciliation deferred to X3 (PNC3D ICombatant impl decides which is the authoritative FSM and how `networkStateId` maps to / from it).

---

## Behavior Contract After X2-3

- 1 enum defined in `ArenaCombat.Core.State` namespace.
- Zero implementers / callers (X2-4 StateManager will be the first consumer).
- No behavior change. No new compile dependencies.

---

## Spawned Follow-ups

- **X2-4 (paired)**: import StatManager + StateManager together. Both compile-tight to each other and to ICombatant (X2-2) + skill enums (X2-2) + Stats SOs (X2-1). Expected size: StatManager ~300+ LOC, StateManager 178 LOC. Largest X2 round so far — may split into Round-1 (StatManager skeleton) + Round-2 (StatManager full + StateManager) if Codex prefers smaller chunks.
- **X3 PNC3D adapter**: decide `CombatantState` vs `CharacterStateId` reconciliation. Likely: `CombatantState` becomes runtime FSM truth, `CharacterStateId` becomes a compact NV wire format mapped from `CombatantState`. Or retire `CharacterStateId` entirely.

---

## User-Side Verification (pending — user confirms in Unity)

1. Compile in Unity. Expect <5s recompile (one tiny enum).
2. Console:
   - **Acceptable**: no new warnings.
   - **Unacceptable**: any C# error.
3. Project window: new `Core/State/` folder with `CombatantState.cs`.
4. Pre-existing warnings should NOT increase.

---

## Lessons

- **Read Buildup dependencies before scoping a roadmap item.** I'd written "X2-3 = StateManager + CombatantState" months ago based on file count. Actual import order is dictated by *compile dependency*, not file count. Lesson: roadmap entries for ported files should be re-validated by `grep`ing `GetComponent` / `RequireComponent` / direct class refs before the round.
- **Scope-down is usually right.** Smaller round = faster Codex pass = lower revert risk. The original X2-3 would have needed either a stub manager (technical debt) or full X2-4 worth of work (massive round). Splitting won.
- **Comment correctness is part of the contract.** My priority comment with `Dead(0)..Idle(6)` numbers contradicted the enum. Codex caught it. Future contract files: never let comment numbers drift from declared values.
- **The pair-import pattern**: when two files are compile-tight, importing them as a unit is safer than splitting and stubbing. X2-4 follows this. Future: SkillExecutor + SkillContext (X2-5/6), SkillProjectile + IProjectile + IPoolable (X2-8), SkillArea + IPersistentArea (X2-10) — all candidate pairings.
