# X2-10: SkillLibrary + SkillBinder Paired (2026-05-12)

ROADMAP item Phase X2-10. Tenth X2 sub-cycle. Paired full import (2 files, ~475 LOC). SkillLibrary defines composite tree recipes for 29 skills (22 implemented + 7 UNIMPLEMENTED null returns). SkillBinder is the one-shot bootstrap that injects RuntimeStep / RuntimeCondition into SkillDefinition SOs.

---

## Outcome

**Status**: APPLIED + **Codex Round 1 APPROVED WITH CHANGES** (3 critical fixes applied).

**Operations**:
- 2 NEW `.cs` + 2 NEW `.meta` (both Buildup GUIDs preserved).
- No new folder (both in existing `Core/Skill/Core/`).
- Critical Codex fixes applied: clean rewrite with method declarations on separate lines, namespace wrap, BarrierBreaker XML doc replaced with English one-liner.

**Files touched**:
- NEW `Assets/ArenaCombat/Scripts/Core/Skill/Core/SkillLibrary.cs` + `.meta` (Buildup GUID `11b22318…`)
- NEW `Assets/ArenaCombat/Scripts/Core/Skill/Core/SkillBinder.cs` + `.meta` (Buildup GUID `ff73184f…`)

**Doc updates**:
- ROADMAP X2-10 → DONE, X2-11 (SkillManager auto-cast 272 LOC) → NEXT.
- TARGET_ARCHITECTURE.md §10 X2-10 row marked done; X2-11 promoted.

---

## Codex Critical Fixes (3 applied)

**C-1: SkillLibrary verbatim copy forbidden — clean rewrite mandatory.**
Buildup source had mojibake + line-break corruption. Some `public static` declarations attached to preceding comment lines: ExecutionSpike, HuntingMark, CollapseRoar, OverchargeModeCondition, PiercingShot, RuptureMagazine, CounterSlash. Our rewrite places each `public static SkillStep MethodName() =>` on its own clean line with proper indentation. Manual structural reconstruction, not text copy.

**C-2: Both files in `namespace ArenaCombat.Core.Skill`.**
Buildup origin has no namespace. We wrap both. SkillLibrary uses `using static ArenaCombat.Core.Skill.SkillComponents;` (fully-qualified) to resolve 37 factory names. SkillBinder accesses `SkillLibrary.*` within same namespace (no using needed).

**C-3: Surface verification by `29 SkillStep + 4 SkillCondition = 33 public methods`, not by SkillId count.**
Post-write grep:
- `grep -c "public static SkillStep "` = **29** ✓
- `grep -c "public static SkillCondition "` = **4** ✓
- Total 33 public methods verified.

---

## Codex Non-Blocking Suggestions Addressed (5)

| # | Suggestion | Status |
|---|---|---|
| S-1 | 7 null-returning unimplemented skills kept as null (NotImplementedException would crash BindAll) | ✓ kept null |
| S-2 | BarrierBreaker `/// <summary> / </summary>` truncated XML doc → English one-liner | ✓ replaced: `// BarrierBreaker — shield break + defense down + main damage.` |
| S-3 | `SurvivalPulseCondition` extracted + inline `cond => ...` duplication kept verbatim (Buildup parity) | ✓ both forms preserved |
| S-4 | SkillBinder.BindAll call site deferred to X3 wiring | ✓ noted, no wiring this round |
| S-5 | `Bind` count semantic (returns 1 success / 0 skip) acceptable; future split into missing-SO / null-step / bound separate logs as needed | ✓ kept as-is |

---

## Type Surface Verification (post-write)

### SkillLibrary.cs — 29 SkillStep + 4 SkillCondition = 33 public methods

**Player common (12 SkillStep + 2 SkillCondition)**:
- SkillStep: `ExecutionSpike` / `CrushingBarrage` / `ErosionField` / `HuntingMark` / `SurvivalPulse` / `FortressArmor` / `SealChain` / `CollapseRoar` / `BarrierBreaker` / `OverchargeMode` / `PiercingShot` / `RuptureMagazine`
- SkillCondition: `SurvivalPulseCondition` / `OverchargeModeCondition`

**Player only (6 SkillStep, 5 return null)**:
- `CounterStance` (null) / `ParryEnhance` (impl) / `CounterSlash` (null) / `WireHook` (null) / `RopeShockwave` (null) / `CollapseStrike` (null)

**Boss common (11 SkillStep + 2 SkillCondition)**:
- SkillStep: `ExecutionSpike_Boss` / `CrushingBarrage_Boss` / `ErosionField_Boss` / `SurvivalPulse_Boss` / `FortressArmor_Boss` / `CollapseRoar_Boss` / `OverchargeMode_Boss` / `MarkWave_Boss` / `SealChain_Boss` (null) / `BarrierBreaker_Boss` / `RuptureMagazine_Boss` (null)
- SkillCondition: `SurvivalPulseCondition_Boss` / `OverchargeModeCondition_Boss`

**Total**:
- SkillStep: 12 + 6 + 11 = **29** ✓
- SkillCondition: 2 + 0 + 2 = **4** ✓
- Public methods: **33** ✓
- Implemented (non-null SkillStep): 12 + 1 + 9 = **22**
- UNIMPLEMENTED (null returns): 5 + 2 = **7**

### SkillBinder.cs

- `BindAll(SkillRegistry)` public ✓
- `Bind(SkillRegistry, string skillId, SkillStep step, SkillCondition condition = null) → int` private ✓
- 29 Bind() calls in BindAll (12 player common + 6 player only + 11 boss common) — verified.
- Final log: `$"[SkillBinder] {bound} skills bound"` (English).

---

## ML Preservation Policy Compliance

Per SKILL_SYSTEM_DESIGN.md §10a:

| Item | Status |
|---|---|
| 33 SkillLibrary method names byte-identical | ✓ |
| Composite tree shapes (damage / duration / radius / angle parameters) preserved | ✓ all numeric values byte-identical |
| Null-returning unimplemented skills preserved | ✓ 7 null methods kept |
| SkillBinder.BindAll signature | ✓ `(SkillRegistry)` |
| SkillId strings byte-identical | ✓ all 29 IDs (designer setup expectation) |
| `Bind` private helper signature | ✓ `(registry, skillId, step, condition=null)` |
| GUID preservation | ✓ both Buildup GUIDs |

ML observation impact: zero. SkillLibrary is pure recipe code, no observable runtime state. BossObservationCollector reads SkillExecutor stats — unaffected.

---

## Translation Audit

Comprehensive Korean → English. Examples:

**Header file comments**:
- `스킬 조립 코드 — SkillDefinition.RuntimeStep 에 주입할 SkillStep 을 부품으로 조립` → `Skill recipe code. Composes SkillStep / SkillCondition trees that get injected into SkillDefinition.RuntimeStep / RuntimeCondition by SkillBinder.`
- `SKILL_DESIGN.md 의 조합식을 코드로 변환한다` → `(merged into header — references SkillBinder + Notes section)`
- Notes section translated: ratio param convention / integer param convention / MaxHP-relative runtime calc

**Section dividers**:
- `플레이어 공용 (12종)` → `Player common (12)`
- `플레이어 전용 (6종)` → `Player only (6)`
- `보스 공용 (11종)` → `Boss common (11)`

**Per-skill design intent comments**: 22 implemented skills + 7 null skills — all translated. Examples:
- `처형 송곳 — 좁은 범위 정확 타격, 빗나가면 넓은 범위 재시도` → `ExecutionSpike — narrow precise hit; on miss, retry with wider cone.`
- `분쇄 연타 — 4단 다단 히트, 적중 시 실드 파괴 추가타` → `CrushingBarrage — 4-hit combo; on hit add shield-break extra.`
- `침식 장판 (플레이어) — 투사체 명중 지점에 2중 장판 생성` → `ErosionField (player) — projectile impact spawns double AoE.`

**Null-skill reason comments**: 7 detailed reason blocks translated. Examples:
- `사유: StatManager.BeginParryWindow() 호출부(패링 입력 바인딩)가 없어 IsParrying 이 영원히 false → CheckParry() 가 HitLanded=false → 후속 보상 미발동. 패링 입력 시스템 구축 후 복원.` → `Reason: StatManager.BeginParryWindow() caller (parry input binding) absent; IsParrying stays false; CheckParry returns HitLanded=false. Restore after parry input system lands.`
- (similar translations for WireHook/RopeShockwave/CollapseStrike + boss SealChain_Boss/RuptureMagazine_Boss)

**SkillBinder Debug.Log**:
- `"[SkillBinder] SkillRegistry 가 null"` → `"[SkillBinder] SkillRegistry is null"`
- `$"[SkillBinder] {bound}종 바인딩 완료"` → `$"[SkillBinder] {bound} skills bound"`

**BarrierBreaker XML doc** (Codex S-2):
- BEFORE (Buildup truncated): `/// <summary>\n/// /\n/// </summary>\n/// <returns></returns>`
- AFTER: `// BarrierBreaker — shield break + defense down + main damage.` (English single-line block comment)

---

## Behavior Contract After X2-10

- `SkillLibrary.{29 SkillStep + 4 SkillCondition}` callable, returns ready-to-inject composite tree delegates.
- `SkillBinder.BindAll(registry)` is the entry point. When called:
  - Iterates 29 SkillIds, for each: `registry.Get(id)` → if SO exists, set `RuntimeStep` + `RuntimeCondition`, return 1 if non-null
  - Logs final count: `[SkillBinder] N skills bound` (N expected = 22 after X1-6 imports 29 SkillDefinition .asset files)
- **Zero call sites in our codebase** (X3 wiring will pick the BindAll call point — `GameStateManager.OnNetworkSpawn` or similar)
- Until call site exists: all `SkillDefinition.RuntimeStep` are null → `SkillExecutor.CanUse` returns false → no skill ever fires. **Inert but compileable**.

---

## Spawned Follow-ups

- **X2-11 (NEXT)**: SkillManager (per-entity MonoBehaviour, auto-cast tick with 5-slot priority queue, ~272 LOC). First user of SkillExecutor + SkillLibrary.
- **X3 wiring**: decide where `SkillBinder.BindAll(masterRegistry)` runs. Candidates:
  - `GameStateManager.OnNetworkSpawn` (singleton, server-only, but clients also need RuntimeStep for `IsReady` check)
  - Standalone bootstrap MonoBehaviour on DDOL GameObject (runs once per session, both server + client)
  - `RuntimeInitializeOnLoadMethod` static — guaranteed earliest, no scene dependency
  - Lean: option (b) — explicit `SkillBootstrap` GameObject in scene with `[ExecuteAlways]` opt-out.
- **X1-6 SO import**: Buildup `.asset` files for 29 SkillDefinitions need to be imported with matching SkillId strings. BindAll will start reporting `N > 0` once these exist.

---

## User-Side Verification (pending — user confirms in Unity)

1. Compile in Unity. Expect <10s recompile.
2. Console: 0 new error / 0 new warning. Specifically watch:
   - `using static ArenaCombat.Core.Skill.SkillComponents;` resolves — all 37 factories accessible.
   - `SkillRegistry.Get(string)` from X2-5 resolves.
   - `SkillDefinition.RuntimeStep` / `.RuntimeCondition` field assignment from X2-5 resolves.
3. Project window: `Core/Skill/Core/` now has 10 files (8 from X2-9 + SkillLibrary + SkillBinder).
4. Optional smoke test: temporary script invoking `SkillBinder.BindAll(myEmptyRegistry)` → Console logs `[SkillBinder] 0 skills bound`.
5. Existing 5 yellow warnings unchanged.

---

## Lessons

- **Codex caught mojibake-attached method declarations**: at least 7 method declarations in Buildup origin had `public static` attached to preceding comment line due to encoding-driven line-break corruption. Verbatim copy would have produced "method declarations inside comments" → compile errors. Clean rewrite mandatory. **Future X2 rounds: always reconstruct structurally, never assume `cp` preserves source layout.**
- **`grep -c "public static X "` for surface verification**: faster + more reliable than counting by file scan. 29 + 4 confirmed in 2 commands.
- **Buildup truncated XML doc**: `/// <summary> / </summary>` with single `/` — likely mojibake-eaten content. Replace with English one-liner; don't preserve broken XML.
- **Pattern recognition continued**: X2-7 / X2-8 / X2-9 / X2-10 — each round had Buildup-origin issues (double-enqueue / mojibake / dangling comment / truncated XML). Verbatim policy ≠ blind copy. ML preservation policy says preserve public surface + behavior semantics, not byte stream.
- **Round size scaling steady**: X2-10 (475 LOC) smaller than X2-9 (837 LOC). Codex Round 1 review handled cleanly. ~500-900 LOC paired rounds remain viable upper bound.
- **Codex strict gate adherence held since X2-7**: pending.md → wait Codex → apply pattern continues. X2-6 workflow violation no longer repeating.
