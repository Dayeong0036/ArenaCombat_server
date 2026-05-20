# Pending Codex Review — BAL-1 T3: Phase Scaling (R2)

## Round
R2 — R1 FAIL fix: damage scale now flows through SkillContext → SkillComponents.

## R1 Finding & Fix

### FAIL: End-to-end damage scaling bypass
- **Problem**: SkillComponents called `target.TakeDamage(amount)` directly, bypassing `StatManager.DealDamage`. Phase scale had no effect on actual skill damage.
- **Fix**: Added `DamageScale` field to `SkillContext` (default 1f). `SkillManager.BuildSkillContext` injects `StatManager.GetPhaseDamageScale()`. All 6 damage paths in `SkillComponents` now multiply by `ctx.DamageScale`:
  - `DealDamage` (#1)
  - `DealMultiHitDamage` (#2)
  - `ApplyDamageOverTime` (#3) — dps scaled at cast time
  - `DealDirectionalHit` (#35)
  - `DealShieldBreakDamage` (#28)
  - `ExecuteBelowHP` (#29)

### WARN: Phase1 initialization
- **Fix**: `InitializeStatManager` now explicitly calls `_statMgr.SetPhaseDamageScale(1f)` before `PopulateBossSkills(Phase1)`.

## Files changed (cumulative)

### Code (.cs)

| 파일 | 변경 |
|------|------|
| `Core/Stats/StatManager.cs` | `_phaseDamageScale` + `SetPhaseDamageScale`/`GetPhaseDamageScale`. `DealDamage`/`DealShieldBreakDamage`에 `* _phaseDamageScale` (StatManager 자체 경로용, 비사용 중이나 일관성 유지). |
| `Core/Skill/Core/SkillContext.cs` | `DamageScale` 필드 추가 (default 1f) |
| `Core/Skill/Core/SkillManager.cs` | `TelegraphScale` property. `BuildSkillContext`에서 `DamageScale = statMgr.GetPhaseDamageScale()` 주입. `ExecuteOrTelegraph`에서 `* TelegraphScale` 적용. |
| `Core/Skill/Core/SkillComponents.cs` | 6개 damage 경로에 `* ctx.DamageScale` 적용 |
| `Core/Network/BossNetworkController3D.cs` | `OnPhaseChanged`: dmg scale 1.0/1.08/1.16/1.25. `PopulateBossSkills`: telegraph scale 1.0/0.9/0.78/0.7. `InitializeStatManager`: explicit `SetPhaseDamageScale(1f)`. |

## Damage Flow (after fix)

```
BossNetworkController3D.OnPhaseChanged
  → StatManager.SetPhaseDamageScale(1.08)

SkillManager.BuildSkillContext
  → ctx.DamageScale = StatManager.GetPhaseDamageScale()  // 1.08

SkillComponents.DealDamage(76f)
  → target.TakeDamage(76 * ctx.DamageScale, caster)      // 76 * 1.08 = 82.08
```

## Phase Scaling Table

| Axis | Phase1 | Phase2 | Phase3 | Enrage |
|------|--------|--------|--------|--------|
| Cooldown | 1.0 | 0.85 | 0.7 | 0.5 |
| Damage | 1.0 | 1.08 | 1.16 | 1.25 |
| Telegraph | 1.0 | 0.9 | 0.78 | 0.7 |
| Speed | 8.4 | 8.4 | 9.2 | 10.1 |

## Questions for Codex

1. **SkillComponents 6개 경로 완전성**: DealDamage, DealMultiHitDamage, ApplyDamageOverTime, DealDirectionalHit, DealShieldBreakDamage, ExecuteBelowHP — 이 외에 TakeDamage를 직접 호출하는 경로가 있는지?

2. **SkillProjectile.ApplyHit**: `_onHit?.Invoke(_ctx)` — _ctx는 SkillManager.BuildSkillContext로 생성된 원본. DamageScale이 이미 주입되어 있으므로 projectile 경로도 phase scale 적용됨 확인.

3. **Player DamageScale**: Player의 StatManager._phaseDamageScale은 기본 1f 유지. SetPhaseDamageScale 호출자가 BossNetworkController3D뿐이므로 Player 스킬 데미지에 phase scale 미적용 확인.

4. **DoT phase scale 시점**: ApplyDamageOverTime에서 cast-time dps를 곱합니다 (dps * ctx.DamageScale). Phase 전환 후 이미 적용된 DoT의 dps는 변경되지 않음 (의도적 — cast-time snapshot).

## Out of scope
- StatManager.DealDamage의 기존 DamageUpMultiplier가 SkillComponents 경로에서 미적용 (pre-existing, buff system 별도 이슈)
- Variant-specific damage/telegraph scale
- Player phase scaling

## Codex Review Result (R2)

Result: PASS for BAL-1 T3 R2. The R1 end-to-end damage scale bypass is fixed for the requested six SkillComponents damage paths.

Verification:
- `SkillContext` has `public float DamageScale = 1f;`.
- `StatManager` has `_phaseDamageScale`, `SetPhaseDamageScale(float)`, and `GetPhaseDamageScale()`. `DealDamage` and `DealShieldBreakDamage` both multiply by `_phaseDamageScale`.
- `SkillManager.BuildSkillContext` injects `DamageScale = _statManager.GetPhaseDamageScale()`, `TelegraphScale` exists, and `ExecuteOrTelegraph` uses `skill.TelegraphDuration * TelegraphScale`.
- `SkillComponents` scales all six requested paths: `DealDamage`, `DealMultiHitDamage`, `ApplyDamageOverTime`, `DealDirectionalHit`, `DealShieldBreakDamage`, and `ExecuteBelowHP`.
- No additional direct `TakeDamage` or `TakeShieldBreakDamage` calls were found in `SkillComponents`.
- `BossNetworkController3D.InitializeStatManager` explicitly calls `_statMgr.SetPhaseDamageScale(1f)`. `OnPhaseChanged` sets damage scale to `1.0/1.08/1.16/1.25`. `PopulateBossSkills` sets telegraph scale to `1.0/0.9/0.78/0.7`.
- `SkillProjectile` preserves the original context: `LaunchProjectile` calls `proj.SetHitCallback(onImpact, ctx, pierce)`, `SetHitCallback` stores that same reference in `_ctx`, and `ApplyHit` invokes `_onHit?.Invoke(_ctx)`. The `DamageScale` produced by `BuildSkillContext` therefore reaches projectile impact callbacks.

Answers:
1. Yes. The six listed paths are covered, and there are no other direct `TakeDamage` / `TakeShieldBreakDamage` calls in `SkillComponents`.
2. Confirmed. Projectile impact callbacks receive the original `SkillContext` reference, so `DamageScale` carries through projectile skills.
3. Confirmed. Player `StatManager._phaseDamageScale` remains the default `1f`; repo-wide `SetPhaseDamageScale` callers are only in `BossNetworkController3D`, so player damage is not phase-scaled unless a future caller explicitly sets it.
4. Confirmed. DoT scaling is a cast-time snapshot: `ApplyDamageOverTime` passes `dps * ctx.DamageScale` into `ApplyStatus`, so later phase changes do not mutate already-applied DoT DPS.

Note: `ApplyParryReward(ParryRewardType.Counter)` can deal damage indirectly through `StatManager.NotifyParryReward`, but it is not a direct `SkillComponents` `TakeDamage` call and is outside the six BAL-1 T3 damage paths reviewed here.
