# X2-9: SkillComponents + SkillRangeDisplay Paired (2026-05-12)

ROADMAP item Phase X2-9. Ninth X2 sub-cycle. **Largest single round to date** (~837 LOC paired). SkillComponents (537 LOC, 36 SkillStep + 1 SkillCondition factories) + SkillRangeDisplay (300 LOC debug visualization singleton) bundled.

---

## Outcome

**Status**: APPLIED + **Codex Round 1 APPROVED** (no critical, 6 non-blocking suggestions all addressed).

**Operations**:
- 2 NEW `.cs` + 2 NEW `.meta` (both Buildup GUIDs preserved).
- No new folder (both in existing `Core/Skill/Core/`).

**Files touched**:
- NEW `Assets/ArenaCombat/Scripts/Core/Skill/Core/SkillComponents.cs` + `.meta` (Buildup GUID `03ccc585…`)
- NEW `Assets/ArenaCombat/Scripts/Core/Skill/Core/SkillRangeDisplay.cs` + `.meta` (Buildup GUID `eceb2777…`)

**Doc updates**:
- ROADMAP X2-9 → DONE, X2-10 (SkillLibrary + SkillBinder paired, ~450 LOC) → NEXT.
- TARGET_ARCHITECTURE.md §10 X2-9 row marked done; X2-10 promoted.

---

## Codex Suggestions Addressed (all 6)

| # | Suggestion | Status |
|---|---|---|
| S-1 | SkillRangeDisplay bundle correct (7 `Instance?.X()` call sites; shim is X2-3 stub trap) | ✓ bundled |
| S-2 | SkillComponents needs `using ArenaCombat.Core.Combat;` for ICombatant | ✓ added (line 2) |
| S-3 | SkillRangeDisplay original `[Header(...)]` mojibake → clean ASCII rewrite mandatory | ✓ 4 Headers translated to English (Settings / Prefab / Runtime colors / Gizmo colors) |
| S-4 | `Physics.OverlapSphere` + per-call `HashSet<ICombatant>` allocation: keep verbatim, defer to X3+ perf pass | ✓ unchanged |
| S-5 | SkillRangeDisplay `SpawnAt()` pool pattern differs from X2-7/8 (`CreateInstance` does NOT enqueue, so empty path has no double-enqueue bug) | ✓ no fix needed; pattern preserved |
| S-6 | static class in `namespace ArenaCombat.Core.Skill` + X2-10 SkillLibrary uses `using static ArenaCombat.Core.Skill.SkillComponents;` | ✓ namespace wrap done; X2-10 will use static-import |

---

## Type Surface Verification (post-write)

### SkillComponents.cs
- 37 factory functions verified via `grep -c "public static SkillStep\|public static SkillCondition"` = 37 ✓
- Distribution: 36 SkillStep + 1 SkillCondition (CheckTargetDistance #37)
- All categories present:
  - Combat (#1, 2, 3, 35) — 4
  - Survival (#4, 5, 6, 7, 8) — 5
  - Status (#9, 10, 11, 12, 13, 25, 27) — 7
  - Buff/Debuff (#14, 15, 16, 17, 21, 22) — 6
  - Position (#18, 19, 36) — 3
  - Defense/Execute/Cleanse (#26, 28, 29, 30) — 4
  - Parry (#23, 24) — 2
  - Area (#20, 31) — 2
  - Projectile (#32) — 1
  - Control flow (#33, 34) — 2
  - Condition (#37) — 1
  - **Total: 37** ✓

### SkillRangeDisplay.cs
- `Instance` static property ✓
- 4 public Show methods: `ShowCircle` / `ShowCone` / `ShowLine` / `ShowArea` ✓
- 4 `[Header]` Inspector sections (clean English): Settings / Prefab / Runtime colors / Gizmo colors ✓
- Private: `RecordGizmo` / `OnDrawGizmos` / `DrawWireCircle` / `DrawWireCone` / `DrawWireLine` / `SpawnAt` / `SetScale` / `FadeAndReturn` / `ReturnToPool` / `CreateInstance` ✓
- `_pool` Queue<GameObject> ✓
- `_gizmoRecords` List<GizmoRecord> + internal `GizmoShape` enum + `GizmoRecord` struct ✓

---

## ML Preservation Policy Compliance

Per SKILL_SYSTEM_DESIGN.md §10a:

| Item | Status |
|---|---|
| 37 factory function names byte-identical | ✓ |
| Factory signatures byte-identical | ✓ |
| ctx mutation patterns (PrimaryTarget save/restore, HitLanded toggling) | ✓ |
| `ctx.OnHitRecorded?.Invoke()` dedup pattern | ✓ |
| SkillRangeDisplay public surface (Instance + 4 Show methods) | ✓ |
| GUID preservation | ✓ both Buildup GUIDs |
| Composite tree behavior | ✓ byte-identical |

ML observation impact: zero. SkillComponents are pure runtime functions; BossObservationCollector reads SkillExecutor stats not internals. SkillRangeDisplay is debug-only with no observable state contributing to ML inputs.

---

## Translation Audit

Comprehensive Korean → English pass for both files. Highlights:

**SkillComponents header (50+ line catalog)**: full categorize translation, retained `#1..37` ordinal numbering for Buildup design notes cross-reference.

**SkillComponents inline comments**: all 37 function header comments translated. Examples:
- `// 단일 피해` → `// Single damage`
- `// 다단 히트` → `// Multi-hit`
- `// 전방 부채꼴 범위 내 ICombatant 전부 타격, ctx.HitLanded 기록` → `// Directional hit — all ICombatant in front cone, records ctx.HitLanded`
- `// Reflecting 상태 적용 → PlayerController.TakeDamage 에서 반사 처리` → `// Applies Reflecting status; PlayerController.TakeDamage handles reflection routing.`
- `// ratio : 0~1 (0.3 = 30% 감소)` → `// ratio: 0..1 (0.3 = 30% reduction)`

**SkillComponents AddLog format strings**: untouched — all Buildup format strings already use English identifiers and `:F0`/`{value}` format specifiers. Examples:
- `$"DealDamage({amount})"` ✓
- `$"DealDirectionalHit(dmg:{damage},r:{range},a:{angleDeg})=>{(anyHit ? \"HIT\" : \"MISS\")}"` ✓

**SkillComponents Debug.LogWarning**:
- `"[SpawnPersistentArea] PersistentAreaManager 씬에 없음"` → `"[SpawnPersistentArea] PersistentAreaManager not in scene"`
- `"[LaunchProjectile] ProjectilePool 씬에 없음"` → `"[LaunchProjectile] ProjectilePool not in scene"`

**SkillRangeDisplay [Header]**: 4 Korean attributes translated to clean English ASCII (per Codex S-3 mojibake guard).

**SkillRangeDisplay Debug.Log**:
- `"[SkillRangeDisplay] Indicator Prefab 미지정 — 범위 표시 불가"` → `"[SkillRangeDisplay] Indicator Prefab not assigned — range display disabled"`
- `"[SkillRangeDisplay] 초기화 완료 (풀 {_poolSize}개)"` → `"[SkillRangeDisplay] Init complete (pool size {_poolSize})"`

**SkillRangeDisplay [RangeDisplay] runtime log strings**: untouched — already English format with parameter substitution.

`ApplyInArea` log string `"360°"` degree symbol → `"360deg"` ASCII-safe substitution to avoid Unicode in log output (clean ASCII policy).

---

## Behavior Contract After X2-9

- 37 SkillStep / SkillCondition factories callable from `using static ArenaCombat.Core.Skill.SkillComponents;` (X2-10 SkillLibrary will use).
- `SkillRangeDisplay.Instance` accessible scene-wide. Designer attaches GameObject + indicator prefab for visual debug.
- All composite tree primitives ready. SkillLibrary (X2-10) can now compose `SkillStep` returns into multi-step trees.
- **Zero call sites** remain in our codebase (X2-10 SkillLibrary first user).

---

## Spawned Follow-ups

- **X2-10 (NEXT)**: SkillLibrary (~378 LOC) + SkillBinder (~72 LOC) paired. SkillLibrary uses `using static ArenaCombat.Core.Skill.SkillComponents;` to reference factories by name. SkillBinder calls `SkillLibrary.X()` getters at game start to inject `SkillDefinition.RuntimeStep`.
- **X3 wiring**: 
  - SkillComponents `Physics.OverlapSphere` (lines 99, 402 of our rewrite) → consider NonAlloc variant if hot path.
  - SkillProjectile/SkillArea NetworkBehaviour conversion (per X2-7/8 history) affects how SkillComponents `LaunchProjectile` / `SpawnPersistentArea` interact with pools.
- **Designer setup**: SkillRangeDisplay GameObject + indicator prefab + `_poolSize` configuration in scene. Layer mask for `_targetMask` per projectile prefab (Codex X2-7 S-5 note).

---

## User-Side Verification (pending — user confirms in Unity)

1. Compile in Unity. Expect 10-20s recompile (largest round to date).
2. Console: 0 new error / 0 new warning. Specifically watch:
   - Missing-using on `ICombatant` (resolved via `using ArenaCombat.Core.Combat;`).
   - `StatusType` / `BuffType` / `DebuffType` / `CleanseType` / `DispelType` / `ParryRewardType` / `AreaShape` / `MoveType` references (all X2-2 same namespace).
   - `ProjectilePool` (X2-7) / `PersistentAreaManager` (X2-8) references.
   - `using static ArenaCombat.Core.Skill.SkillComponents;` will be exercised in X2-10.
3. Project window: `Core/Skill/Core/` now has 8 files (SkillComponents / SkillContext / SkillDefinition / SkillExecutor / SkillRangeDisplay / SkillRegistry / SkillRoleTag / SkillTypes).
4. Optional smoke test:
   - Scratch GameObject + `Add Component > Skill Range Display` → Inspector shows 4 English `[Header]` sections + indicator prefab slot.
5. Existing 5 yellow warnings unchanged.

---

## Lessons

- **Late dependency discovery pattern**: SkillRangeDisplay was not in ROADMAP; SkillComponents source inspection revealed 7 `Instance?.X()` calls. Discovery → pending.md → Codex approval → bundle in same round. Process worked. **Take-away**: always grep for `Instance?` / `Singleton` / `manager` references in candidate files before final pending.md.
- **Shim vs bundle trade-off applied correctly**: X2-3 stub-trap lesson prevented shim path. Bundle 300 LOC of pure debug code worth avoiding 2-round shim + replacement debt.
- **Pool pattern variations preserved**: SkillRangeDisplay's `CreateInstance()` does NOT enqueue (different from X2-7 ProjectilePool / X2-8 PersistentAreaPool). Codex verified no analogous double-enqueue bug. Each Buildup pool deserves separate review.
- **ML observation surface remains intact**: SkillComponents bodies route mutations through ICombatant interface; no direct StatManager calls (delegated via ICombatant). SkillExecutor stats unchanged. Future BossObservationCollector wiring (X4-N) won't need adjustment for SkillComponents.
- **837 LOC paired passed Round 1**: same discipline (byte-identical surface + comment translation only) scales to ~900 LOC. X2-4 (877) + X2-9 (837) precedent. Likely upper bound — anything beyond should split.
- **Workflow strict adherence held**: pending.md → Codex feedback (`feedback.md` content visible to me) → apply. X2-6 violation pattern not recurring.
