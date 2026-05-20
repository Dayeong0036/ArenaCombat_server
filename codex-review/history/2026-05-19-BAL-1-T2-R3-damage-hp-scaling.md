# Pending Codex Review — BAL-1 T2: Boss Damage + HP/Stat Scaling (R3)

## Round
R3 — R2 PASS. Additional fix: Player CrushingBarrage same-pattern bug + handoff doc channel order fix.

## R1 Findings & Fixes (resolved in R2)

### Critical 1: CrushingBarrage_Boss total damage 100 → 80
- **Fix**: `DealDirectionalHit(20f)` → `DealDirectionalHit(0f)` in SkillLibrary.cs. Detection only.

### Critical 2: SkillProjectile.prefab serialized override
- **Fix**: prefab YAML `_detectionRadius: 0.5` → `1`.

## R2 Result: PASS (2 WARN, 0 FAIL)

## R3 Changes (new this round)

### 1. Player CrushingBarrage — same DealDirectionalHit additive pattern
- **Problem**: R2 WARN #3 — Player `CrushingBarrage()` had `DealDirectionalHit(28f)` + `DealMultiHitDamage(28f, 4)` = 28+112 = 140. Intended: 28×4 = 112.
- **Fix**: `DealDirectionalHit(28f, 13.6f, 38f)` → `DealDirectionalHit(0f, 13.6f, 38f)` in SkillLibrary.cs line 42. Same pattern as Boss R2 fix.

### 2. ML_TRAINING_HANDOFF.md Section 5 channel order
- **Problem**: Section 3 (Boss slots) and Section 5 (Player slots) had different per-slot 7ch order. Boss: remCD/maxCD/range. Player: range/remCD/maxCD. Doc stated "동일 구조" but order conflicted.
- **Fix**: Section 5 unified to match Section 3 (A안: remCD → maxCD → range → coneOrAoE → one-hot3).

## Files changed (cumulative)

### Code (.cs)

| 파일 | 변경 |
|------|------|
| `Core/Skill/Core/SkillLibrary.cs` | Boss 7개 스킬 데미지 + R2: Boss CrushingBarrage DealDirectionalHit 20→0 + R3: Player CrushingBarrage DealDirectionalHit 28→0 |
| `Core/Skill/Projectile/SkillProjectile.cs` | `_detectionRadius` default 0.5 → 1.0 |
| `Core/Network/PlayerNetworkController3D.cs` | `maxHP` 100 → 150 |

### Data (.asset / .prefab)

| 파일 | 변경 |
|------|------|
| `Resources/Stats/BossStatsSO.asset` | BossMaxHP 1000→6000, BossCurrentHP 1000→6000 |
| `Resources/Skills/Prefabs/SkillProjectile.prefab` | `_detectionRadius` 0.5→1 (R2) |
| `3DSceneScript/Player/Player A.prefab` | maxHP 100→150 |

### Documentation

| 파일 | 변경 |
|------|------|
| `ML_TRAINING_HANDOFF.md` | Section 5 per-slot channel order → Section 3과 통일 (R3) |

## Damage Summary

### Boss Skills

| Skill | Total Damage | 구성 |
|-------|-------------|------|
| ExecutionSpike_Boss | 76 | DealDirectionalHit(76f) |
| CrushingBarrage_Boss | 80 | DealDirectionalHit(0f) + DealMultiHitDamage(20f, 4) |
| FortressArmor_Boss | 65 | DealDirectionalHit(65f) |
| CollapseRoar_Boss | 110 | DealDamage(55f) × 2 |
| MarkWave_Boss | 50 | DealDamage(50f) cone 65° |
| BarrierBreaker_Boss | 62 | DealDamage(62f) projectile |
| ErosionField_Boss | 8/s | ApplyDamageOverTime(1f, 8f) |

### Player CrushingBarrage (R3 fix)

| Before | After |
|--------|-------|
| DealDirectionalHit(28f) + DealMultiHitDamage(28f, 4) = 140 | DealDirectionalHit(0f) + DealMultiHitDamage(28f, 4) = 112 |

## Questions for Codex

1. **Player CrushingBarrage R3 fix 검증**: `DealDirectionalHit(0f, 13.6f, 38f)` — Boss R2와 동일 패턴. 0 damage cone detection + multi-hit 112 총 데미지 정합 확인.

2. **ML_TRAINING_HANDOFF.md Section 5**: Boss Section 3과 동일 순서 (remCD, maxCD, range, coneOrAoE, one-hot3) 인지 확인.

## Out of scope
- T3: 페이즈 데미지/telegraph/speed 배율
- ML observation 차원 확장 (학습 Phase 2)
- BossBaseDamage 조정

## Codex Review Result (R3)

PASS (0 FAIL, 0 WARN)

Verified:
- Player `CrushingBarrage()` uses `DealDirectionalHit(0f, 13.6f, 38f)` followed by `DealMultiHitDamage(28f, 4)`, so total damage is `0 + 28*4 = 112`.
- Boss `CrushingBarrage_Boss()` still uses `DealDirectionalHit(0f, 15f, 40f)` followed by `DealMultiHitDamage(20f, 4)`.
- `ML_TRAINING_HANDOFF.md` Section 5 matches Section 3 per-slot order: remCD, maxCD, range, coneOrAoE, directional/aoe/projectile one-hot3.
- `SkillProjectile.prefab` still has `_detectionRadius: 1`.
- `BossStatsSO.asset` still has `BossMaxHP: 6000` and `BossCurrentHP: 6000`.
