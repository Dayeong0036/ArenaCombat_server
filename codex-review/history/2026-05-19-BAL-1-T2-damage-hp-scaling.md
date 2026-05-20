# Pending Codex Review — BAL-1 T2: Player Skills + HP/Damage Scaling

## Topic
운영 측 발란스 수치 일괄 갱신 (ML/observation 관련 변경 모두 제외):

1. **Player 스킬 12개** — wide retry 제거 (멜리 4개) + projectile 속도 ↑ + AoE 반경 ↑ + 데미지 조정
2. **Boss 스킬 데미지 조정** — 핸드오프 §8 표대로 (cone/range는 R2/R8에서 적용 완료)
3. **Projectile detection radius**: 0.5 → 1.0
4. **Player HP**: 100 → 150
5. **Boss HP**: 1000 → 6000

ML observation 관련 (AIHint 필드 / 23 SO 값 / 129 ch / _maxBurstDmg) 모두 **T2 범위 외**. 학습 측 동기화는 별도 일정.

## Roadmap link
Plan: `C:\Users\paek6\.claude\plans\foamy-baking-melody.md` (Tranche 2)
Handoff doc: `ML_TRAINING_HANDOFF.md` §8 (게임 사양 표)

## Goal
- 운영 매치 10.5-11.5분 평균 (보스 HP 6000 / 팀 effective DPM ~580)
- 플레이어 사망 주기 30-45s (보스 effective hit 2.0-2.8 per min × 평균 50 dmg / 150 HP)
- 플레이어 hit rate 자연 분포: 멜리 75-88% / 원거리 55-65% (wide retry 제거 후)

## Files to touch

### Code (.cs)
| 파일 | 변경 |
|------|------|
| `Core/Skill/Core/SkillLibrary.cs` | 12 player skills + 7 boss skill damage 조정 |
| `Core/Skill/Projectile/SkillProjectile.cs` | `_detectionRadius` default 0.5 → 1.0 |

### Stat / Prefab
| 파일 | 변경 |
|------|------|
| `Resources/Stats/BossStatsSO.asset` | BossMaxHP 1000→6000, BossCurrentHP 1000→6000 |
| `3DSceneScript/Player/Player A.prefab` | maxHP 100→150 |

### NOT changed (T2 외부)
- ML obs 차원 / AIHint 필드 / 23 SO 값
- _maxBurstDmg normalization
- cardDraftInterval, episode timing
- 페이즈 damage/telegraph 배율 (T3)

## Approach

### 1. SkillLibrary.cs — Player skills 12개

#### ExecutionSpike — wide retry 제거, narrow cone 30→32, dmg 125→100
```diff
 public static SkillStep ExecutionSpike() =>
     ctx =>
     {
-        DealDirectionalHit(125f, 15f, 30f).Invoke(ctx);
+        DealDirectionalHit(100f, 15f, 32f).Invoke(ctx);
         TriggerOnHit(
             onHit: hit =>
             {
                 ApplyVulnerability(2f, 0.05f).Invoke(hit);
                 ExecuteBelowHP(30f, 20f).Invoke(hit);
-            },
-            onMiss: DealDirectionalHit(125f, 18f, 70f)
+            }
         ).Invoke(ctx);
     };
```

#### CrushingBarrage — wide retry 제거, cone 35→38, dmg 34→28
```diff
 public static SkillStep CrushingBarrage() =>
     ctx =>
     {
-        DealDirectionalHit(34f, 13.6f, 35f).Invoke(ctx);
+        DealDirectionalHit(28f, 13.6f, 38f).Invoke(ctx);
         TriggerOnHit(
             onHit: hit =>
             {
-                DealMultiHitDamage(34f, 4).Invoke(hit);
+                DealMultiHitDamage(28f, 4).Invoke(hit);
                 DealShieldBreakDamage(45f, 1.2f).Invoke(hit);
-            },
-            onMiss: miss =>
-            {
-                DealDirectionalHit(34f, 16.6f, 75f).Invoke(miss);
-                TriggerOnHit(
-                    onHit: DealMultiHitDamage(34f, 4)
-                ).Invoke(miss);
             }
         ).Invoke(ctx);
     };
```

#### FortressArmor — wide retry 제거, cone 35→38, dmg 90→75
```diff
 public static SkillStep FortressArmor() =>
     ctx =>
     {
-        DealDirectionalHit(90f, 14.4f, 35f).Invoke(ctx);
+        DealDirectionalHit(75f, 14.4f, 38f).Invoke(ctx);
         TriggerOnHit(
             onHit: hit =>
             {
                 float maxHp = hit.Caster?.MaxHP ?? 0f;
                 GainShield(maxHp * 0.06f).Invoke(hit);
-            },
-            onMiss: DealDirectionalHit(90f, 17.4f, 75f)
+            }
         ).Invoke(ctx);
     };
```

#### CollapseRoar — wide retry 제거, AoE 10.6→12, dmg 95→80
```diff
 public static SkillStep CollapseRoar() =>
     ctx =>
     {
-        ApplyInArea(10.6f, AreaShape.Circle,
+        ApplyInArea(12f, AreaShape.Circle,
             inner =>
             {
-                DealDamage(95f).Invoke(inner);
+                DealDamage(80f).Invoke(inner);
                 ApplyDefenseDown(2f, 5f).Invoke(inner);
             }
         ).Invoke(ctx);
-        TriggerOnHit(
-            onMiss: ApplyInArea(21.6f, AreaShape.Circle, DealDamage(95f))
-        ).Invoke(ctx);
     };
```

#### ErosionField — projectile 속도 19→28, inner AoE 7.6→9
```diff
 public static SkillStep ErosionField() =>
-    LaunchProjectile(19f, 42f, false,
+    LaunchProjectile(28f, 42f, false,
         impact =>
         {
-            SpawnPersistentArea(4f, 7.6f, AreaShape.Circle, 1f,
+            SpawnPersistentArea(4f, 9f, AreaShape.Circle, 1f,
                 tick =>
                 {
                     ApplyDamageOverTime(1f, 7f).Invoke(tick);
                     ApplyAntiHeal(1f, 0.20f).Invoke(tick);
                 }
             ).Invoke(impact);

-            SpawnPersistentArea(4f, 20.4f, AreaShape.Circle, 1f,
+            SpawnPersistentArea(4f, 20f, AreaShape.Circle, 1f,
                 tick => ApplyDamageOverTime(1f, 7f).Invoke(tick)
             ).Invoke(impact);
         }
     );
```

#### HuntingMark — projectile 22→35, dmg 68→60
```diff
 public static SkillStep HuntingMark() =>
-    LaunchProjectile(22f, 48f, false,
+    LaunchProjectile(35f, 48f, false,
         hit =>
         {
-            DealDamage(68f).Invoke(hit);
+            DealDamage(60f).Invoke(hit);
             ApplyDebuff(4f, DebuffType.Mark, 1f).Invoke(hit);
         }
     );
```

#### SealChain — projectile 20→30, dmg 60→50
```diff
 public static SkillStep SealChain() =>
-    LaunchProjectile(20f, 48f, false,
+    LaunchProjectile(30f, 48f, false,
         hit =>
         {
-            DealDamage(60f).Invoke(hit);
+            DealDamage(50f).Invoke(hit);
             ApplyHitStun(0.08f).Invoke(hit);
             ApplySilence(0.5f).Invoke(hit);
         }
     );
```

#### BarrierBreaker — projectile 19→30, dmg 105→88
```diff
 public static SkillStep BarrierBreaker() =>
-    LaunchProjectile(19f, 42f, false,
+    LaunchProjectile(30f, 42f, false,
         hit =>
         {
             DealShieldBreakDamage(60f, 1.4f).Invoke(hit);
             ApplyDefenseDown(3f, 6f).Invoke(hit);
-            DealDamage(105f).Invoke(hit);
+            DealDamage(88f).Invoke(hit);
         }
     );
```

#### PiercingShot — projectile 25→40, dmg 140→110
```diff
 public static SkillStep PiercingShot() =>
-    LaunchProjectile(25f, 60f, true,
+    LaunchProjectile(40f, 60f, true,
         hit =>
         {
             ApplyDefenseDown(3f, 8f).Invoke(hit);
-            DealDamage(140f).Invoke(hit);
+            DealDamage(110f).Invoke(hit);
         }
     );
```

#### RuptureMagazine — projectile 20→30, dmg 115→95
```diff
 public static SkillStep RuptureMagazine() =>
-    LaunchProjectile(20f, 48f, false,
+    LaunchProjectile(30f, 48f, false,
         hit =>
         {
             ApplyVulnerability(3f, 0.10f).Invoke(hit);
-            DealDamage(115f).Invoke(hit);
+            DealDamage(95f).Invoke(hit);
         }
     );
```

#### SurvivalPulse, OverchargeMode — 변경 없음 (Self trigger)

### 2. SkillLibrary.cs — Boss skills 데미지 조정

| Boss skill | Current | T2 target |
|-----------|---------|-----------|
| ExecutionSpike_Boss | 118 | **76** |
| CrushingBarrage_Boss | 32×4 | **20×4** |
| FortressArmor_Boss | 82 | **65** |
| CollapseRoar_Boss | 88×2 | **55×2** |
| MarkWave_Boss | 70 | **50** |
| BarrierBreaker_Boss | 90 | **62** |
| ErosionField_Boss | 7 DoT/s | **8 DoT/s** (소폭 ↑) |

Diff 예시 (ExecutionSpike_Boss):
```diff
 public static SkillStep ExecutionSpike_Boss() =>
     ctx =>
     {
-        DealDirectionalHit(118f, 18f, 38f).Invoke(ctx);
+        DealDirectionalHit(76f, 18f, 38f).Invoke(ctx);
         TriggerOnHit(...);
     };
```

(나머지 6개 보스 스킬 동일 패턴.)

### 3. SkillProjectile.cs — detection radius

```diff
- [SerializeField] private float _detectionRadius = 0.5f;
+ [SerializeField] private float _detectionRadius = 1.0f;
```

기존 projectile prefab 인스턴스가 SerializeField로 0.5 override해 두었다면, 그것도 갱신 필요. 본 라운드는 default만 변경 → prefab override 검증 별도.

### 4. BossStatsSO.asset

```diff
- BossMaxHP: 1000
- BossCurrentHP: 1000
+ BossMaxHP: 6000
+ BossCurrentHP: 6000
```

### 5. Player A.prefab

PNC3D 컴포넌트 serialized:
```diff
- maxHP: 100
+ maxHP: 150
```

## Risks / unknowns

1. **데미지 산식 변동 폭 큼**: 플레이어 PiercingShot 140→110 (-21%), 보스 ExecutionSpike 118→76 (-36%). 보스 HP 6배 증가가 보상. 매치 길이는 예상 10.3분.

2. **Wide retry 제거가 플레이어 hit rate 50-65%로 자연 감소**: 핸드오프 §8 hit rate 목표 부합. 단 실제 측정은 플레이테스트 필요.

3. **Projectile detection radius 0.5→1.0** 두 배 증가 시 hit 빈도 ↑. 보스 8.4m/s 이동으로 회피 상쇄. 종합적으로 50-65% 원거리 hit rate 목표.

4. **Player HP 150 vs 보스 데미지**: 보스 최대 단발 데미지 76 (ExecutionSpike_Boss) = 150 HP의 51%. 핸드오프 §10 "Damage per effective hit 일반 23-30% / 처형 48-53%" 부합.

5. **SkillProjectile prefab inspector override**: 기존 projectile prefab들에 `_detectionRadius: 0.5` serialized 가능성. 검증 후 prefab도 갱신 (별도 라운드 또는 본 라운드 후행).

6. **OverchargeMode/SurvivalPulse 변경 없음**: 두 스킬은 Self trigger이며 hit rate/데미지 무관. 그대로 유지.

7. **CardManager 카드 풀 / draft 풀 변경 없음**: PiercingShot exclude 등 polish 항목은 본 라운드 외. 사용자 결정 사항.

## Questions for Codex

1. **Boss BaseDamage 50 그대로 유지 vs 30으로 ↓?**: BossStatsSO.BossBaseDamage는 현재 50. 스킬마다 데미지 직접 정의되어 base는 사실상 미사용. 일관성을 위해 동기화 vs 그냥 두기?

2. **Projectile prefab override 검증**: SkillProjectile prefab들 (e.g., `Projectile_PiercingShot.prefab`) 의 serialized `_detectionRadius`가 .cs default를 override하면 1.0 변경 무효. 검증 필요?

3. **CrushingBarrage_Boss 데미지 20×4 = 총 80**: 핸드오프 §8 표가 "20×4"이지만 실제 단발은 20. 최대 burst 80. _maxBurstDmg=80 호환 가능 (saturate 안 됨 일반 시).

4. **Boss 0개 스킬 데미지 변경 안 함**: SealChain_Boss, RuptureMagazine_Boss (UNIMPLEMENTED). SurvivalPulse_Boss (recover), OverchargeMode_Boss (buff). 본 라운드 제외 OK?

5. **MarkWave_Boss 70→50 변경 폭 큼**: 28% 감소. cone 65° 광역에 비해 데미지 낮춤. 의도된 패턴 (광역 + 디버프 위주)?

## Out of scope for this round
- ML observation 차원 / AIHint 필드 (T1C — 별도 일정)
- 페이즈 telegraph / damage 배율 (T3)
- 카드 풀에서 PiercingShot 제외 필터
- CardManager pool 변경
- Projectile prefab _detectionRadius override 일괄 갱신
- A2 cardDraftInterval 조정 (운영은 episode timeout 없음, 무관)
