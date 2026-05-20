# X2-4: StatManager + StateManager (Paired) (2026-05-12)

ROADMAP item Phase X2-4. Fourth X2 sub-cycle. **Largest round so far** (~877 LOC). Paired full import per X2-3 scope-down decision (StateManager has compile-time hard dep on StatManager).

---

## Outcome

**Status**: APPLIED. One Codex review round — APPROVED with 5 non-blocking suggestions, all adopted.

**Operations**:
- 1 folder create (`Core/Stats/`) with fresh GUID `f3ab8a0ddfc64736b1400b9ed14ed05a` in folder `.meta`.
- 2 NEW `.meta` files with Buildup GUIDs preserved (StatManager `cc3c21c8…`, StateManager `17b9658f…`).
- 2 NEW `.cs` files written in clean UTF-8 ASCII (English comments + Debug.Log strings, public API + field names byte-identical to Buildup).

**Files touched**:
- `Assets/ArenaCombat/Scripts/Core/Stats.meta` (NEW folder)
- `Assets/ArenaCombat/Scripts/Core/Stats/StatManager.cs` + `.meta` (NEW)
- `Assets/ArenaCombat/Scripts/Core/State/StateManager.cs` + `.meta` (NEW — extends X2-3 folder)

**Doc updates**:
- ROADMAP X2-4 → DONE, X2-5 (SkillContext + SkillRegistry + uncomment delegates) → NEXT.
- TARGET_ARCHITECTURE.md §10 X2-4 row marked done; X2-5 promoted.
- TARGET_ARCHITECTURE.md §3 prefaced with **pattern correction note** distinguishing singleton managers vs per-entity component managers. Original framing called all managers "DDOL singletons"; X2-4 reality forced the split.

---

## Review Cycle Summary

### Round 1 — APPROVED (no critical issues)

Codex confirmed local pre-conditions: SkillTypes.cs / full ICombatant.cs / CombatantState.cs all present, no `StatManager` / `StateManager` / `CombatantKind` collisions, both Buildup GUIDs verified.

5 non-blocking suggestions, all applied:

**S-1 — CombatantKind enum placement**: must be top-level inside namespace, NOT nested in `StatManager` class. Sketch in pending.md was ambiguous; rewrite places `enum CombatantKind` at namespace level (line 36) before `class StatManager` (line 47). Matches Buildup origin. ✓

**S-2 — clean ASCII rewrite, not verbatim copy**: applied per X2-2 lesson. Buildup `StatManager.cs` has same Korean-comment mojibake risk as prior rounds. Public API byte-identical, only comments + Debug.Log strings translated. ✓

**S-3 — public surface verification post-apply**: minimum check list (`Initialize`, `BindOwner`, `Tick`, `DealDamage`, `ReceiveDamage`, `ApplyStatus`, `ApplyBuff`, `ApplyDebuff`, `ResetForTraining`, `SetCasting`, `BeginParryWindow`, `EndParryWindow`, `HasStatus`, `protected internal SetHP/SetShield`, `CombatantKind` top-level enum). All present and grep-confirmed at expected line ranges. ✓

**S-4 — server authority disclaimer in StatManager header**: added explicit "SERVER AUTHORITY CONTRACT" block stating the manager does not enforce IsServer; callers must gate mutations. Same disclaimer mirrored in StateManager header (Tick / Notify methods). ✓

**S-5 — `_logCombat = true` default**: kept true for X2-4 import smoke test per Codex approval; flip to false at X3 wiring stage if log spam becomes noise. ✓

---

## Type Surface Verification (post-apply grep)

### StatManager.cs public/protected surface

| Member | Line | Status |
|---|---|---|
| `enum CombatantKind` (top-level, namespace direct) | 36 | ✅ |
| `class StatManager : MonoBehaviour` | 47 | ✅ |
| `Initialize(BaseStatsSO, float, float, float, float, CombatantKind)` | 106 | ✅ |
| `BindOwner(ICombatant)` | 132 | ✅ |
| `GetHP/GetMaxHP/GetHPPercent/GetShield/GetShieldMax/GetHPRegenRate/GetParryWindow` | 138-144 | ✅ |
| `GetMoveControl/GetReflectRatio/GetDamageTakenMultiplier/GetHealingMultiplier/GetDamageUpMultiplier` | 146-150 | ✅ |
| `Kind / Owner` props | 152-153 | ✅ |
| `IsAlive / IsCasting / IsParrying` props | 159-161 | ✅ |
| `SetCasting / SetParrying` | 163-164 | ✅ |
| `Tick(dt)` | 169 | ✅ |
| `DealDamage / DealShieldBreakDamage` | 213, 220 | ✅ |
| `ReceiveDamage / ReceiveShieldBreakDamage` | 232, 265 | ✅ |
| `BeginParryWindow / EndParryWindow` | 309, 320 | ✅ |
| `NotifyParryReward(ParryRewardType, float, float, ICombatant)` | 341 | ✅ |
| `HasStatus / HasBuff / HasDebuff` | 366-368 | ✅ |
| `RecoverHP / AddShield` | 374, 384 | ✅ |
| `ApplyStatus / RemoveStatuses` | 394, 405 | ✅ |
| `ApplyBuff / RemoveBuffs` | 511, 519 | ✅ |
| `ApplyDebuff` | 589 | ✅ |
| `ResetForTraining` | 639 | ✅ |
| `protected internal SetHP / SetShield` | 690, 696 | ✅ |

### StateManager.cs public surface

`BindOwner`, `NotifyMovementInput/CastStart/CastEnd/ParryStart/ParryEnd`, `Tick`, `ForceReset`, `CurrentState/PreviousState/TimeInState` props, `CanAct/CanMove/CanCast/CanParry` derived flags, `OnStateChanged/OnStateEntered/OnStateExited` events. All present.

---

## Translation Audit

Korean → English translations applied:

| Source (Buildup) | Target (our file) |
|---|---|
| `[Combat] <b>패링 성공!</b> ... 에게 ... 반사` | `[Combat] <b>PARRY!</b> ... reflected ... to ...` |
| `[Combat] 반사: ... 에게 ... 반사 피해` | `[Combat] Reflect: ... -> ... ... reflected damage` |
| `피해 수신:` / `사망!` | `took damage:` / `DEAD!` |
| `실드파괴:` | `shield-break:` |
| `회복:` | `recover:` |
| `상태부여:` | `apply status:` |
| `[Header("디버그")]` | `[Header("Debug")]` |
| `[Header("HP / Shield (실시간)")]` | `[Header("HP / Shield (live)")]` |
| `[Header("상태 플래그 (실시간)")]` | `[Header("State Flags (live)")]` |
| `[Header("배율 (실시간)")]` | `[Header("Multipliers (live)")]` |
| `[Header("활성 상태이상 / 버프 / 디버프")]` | `[Header("Active Statuses / Buffs / Debuffs")]` |
| Various block comment headers | Translated; structure preserved |

All `Debug.Log` calls retain `[Combat]` prefix and field-formatted layout (`HP: prev->curr/max`, etc.) for greppability. No semantic behavior change.

---

## Conflict / Risk Pre-Check (verified)

Confirmed by Codex local check + my pre-write grep:
- `class StatManager`, `class StateManager`, `enum CombatantKind`: 0 hits prior to import.
- All cross-namespace deps resolve: `BaseStatsSO/PlayerStatsSO/BossStatsSO` (Core.Combat, X2-1), `ICombatant` (Core.Combat, X2-2), `StatusType/BuffType/DebuffType/CleanseType/DispelType/ParryRewardType` (Core.Skill, X2-2), `CombatantState` (Core.State, X2-3).
- No NetworkBehaviour involvement (per-entity MonoBehaviour pattern).

---

## Behavior Contract After X2-4

- StatManager + StateManager defined in `ArenaCombat.Core.Stats` and `ArenaCombat.Core.State` namespaces.
- **Zero call sites** in our codebase (PNC3D doesn't wire StatManager yet — X3 work).
- **Zero entities use them** (no `[RequireComponent]` consumer yet).
- Manager files compile against existing X2-1/2/3 dependencies cleanly.
- Inspector preview: a scratch GameObject with `StatManager + StateManager` attached should show English `[Header]` labels in Inspector, confirming clean encoding.

---

## Pattern Decisions Recorded

1. **Per-entity component pattern** (vs singleton): one StatManager + StateManager per `ICombatant`, attached to controller GameObject. Pros: clean ownership, no dict lookup, Inspector-friendly. Cons: requires X3 wiring layer to instantiate components.

2. **MonoBehaviour, not NetworkBehaviour**: per-entity managers are not NetworkObjects themselves — they ride on the controller's NetworkObject. Server authority enforced at call sites.

3. **Coroutine pattern preserved**: status/buff/debuff/parry use `StartCoroutine` + `Dictionary<EnumType, Coroutine>` tracking. Coexists with our existing manual-timer patterns (CombatManager3D `attackCooldowns3D`, PNC3D `parryWindowTimer`). Future cleanup deferred — both patterns are correct for their respective use cases (coroutines deterministic for self-contained timer, manual for tick-driven cooldowns).

4. **Pattern correction in TARGET_ARCHITECTURE §3**: the original "all managers are DDOL NetworkBehaviour singletons" framing was wrong. Real shape is two-tier: singleton coordinators + per-entity components. Doc note added prefacing §3.

---

## Spawned Follow-ups

- **X2-5 SkillContext + SkillRegistry**: foundation for skill system. Will uncomment `SkillStep` / `SkillCondition` delegates in SkillTypes.cs (X2-2).
- **X3 PNC3D wiring**: instantiate StatManager + StateManager components on PNC3D, call `Initialize` from `OnNetworkSpawn` (server only), implement ICombatant via delegation to managers, swap `TakeDamage` body to `_statManager.ReceiveDamage`. Major slim-down — PNC3D should drop ~500 LOC of damage / parry timer / status logic that StatManager + StateManager now own.
- **X3 design decision**: `_logCombat` default flip to false once damage smoke test passes. Or expose via NetworkManager debug toggle.
- **X4 BossNetworkController3D**: similar wiring pattern, BNC3D instantiates StatManager (kind=Boss) + StateManager.

---

## User-Side Verification (pending — user confirms in Unity)

1. Compile in Unity. Expect 10-20s recompile (largest round; cross-namespace lookup).
2. Console:
   - **Acceptable**: no new warnings.
   - **Unacceptable**: any C# error. Especially missing-using errors (`StatusType not found`, `ICombatant not found`).
3. Project window:
   - New folder: `Core/Stats/` with `StatManager.cs`.
   - New file: `Core/State/StateManager.cs` (sits next to `CombatantState.cs` from X2-3).
4. Optional smoke test: scratch GameObject + `Add Component` → `StatManager` (auto-adds nothing else) + `StateManager` (should auto-add prompt for StatManager via RequireComponent). Inspector renders English `[Header]` labels under each section.
5. Pre-existing warnings should NOT increase.

---

## Lessons

- **Top-level vs nested enum is a real footgun.** My pending sketch had `enum CombatantKind` indented inside the `namespace { ... }` block but visually next to `class StatManager`, which Codex correctly flagged as ambiguous. C# allows nested enums in classes, and a careless rewrite could place it inside `class StatManager { ... }` instead. Always grep `^public enum` at namespace-direct depth post-write.
- **Public surface check is mandatory for large rewrites.** 700 LOC translation can silently drop a method or rename a field. Codex's S-3 minimum-list grep is the right discipline; should become standard for any rewrite > 200 LOC.
- **Pattern framing in TARGET docs needs reality-check passes.** I wrote §3 calling everything "DDOL NetworkBehaviour singletons" before actually surveying Buildup. Halfway through X2-4 the per-entity component reality forced a correction. Lesson: when adding manager rows to the catalog, label the shape (singleton vs per-entity) explicitly from day 1.
- **Coroutine vs manual timer coexistence is fine.** I'd worried about pattern inconsistency, but the two patterns serve different lifetimes (self-expiring buff vs tick-driven cooldown) and don't conflict. Premature unification would have been over-engineering.
- **Largest single round (877 LOC) passed Round-1 cleanly.** Discipline scaled: clean ASCII rewrite + public surface preservation + pre-checked deps + Codex pre-flight verification all paid off. Pair-import is viable for files this size.
