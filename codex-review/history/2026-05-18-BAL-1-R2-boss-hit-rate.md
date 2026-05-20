# Pending Codex Review — BAL-1 R2 (Round 2): Boss Skill Hit Rate ~20%

## R1 R1 Verdict
**APPROVED WITH CHANGES** — 2 critical 발견:

| # | R1 Critical | R2 Resolution |
|---|------------|---------------|
| 1 | SO `Range` field는 `SkillManager.CanCast` cast eligibility gate (단순 cosmetic 아님). 줄이면 보스가 "거리 가까울 때만 시전" → hit-rate-per-cast가 오히려 상승. ML observation에도 노출됨. | **SO `Range`는 변경하지 않음.** SkillLibrary.cs 내 hit geometry 파라미터만 조정. 멀리서 시전했을 때 효과 영역 부족하여 자연스럽게 miss → 의도된 hit rate 감소. |
| 2 | Telegraph 초기 ctx.CastPosition/Direction 표시 vs `CompleteTelegraph()` 시점 재계산. 보스가 windup 중 회전/추적 시 디스플레이-실제 hit 불일치. | 본 라운드 scope 외 (현재 보스는 windup 중 회전 거의 없음). 노트로만 문서화, 별도 이슈로 처리. |

Plus suggestions 채택:
- Wide retry **완전 제거** (절반 축소 아님)
- Cone 각도 차별화 유지 (25° 정밀, 30° 광역)
- Hit rate "정확히 20% 도달"이라고 주장하지 않음 — 첫 라운드 후 로그 측정으로 fine-tune

## Topic
**Tranche B 일부** — 보스 7개 스킬 (directional/AoE/projectile) hit geometry 조정:
1. **Wide retry 완전 제거** (4개 cone 스킬)
2. **Narrow cone 축소** (35-40° → 25-30°)
3. **TelegraphDuration 1.5-2x** (7개 SO)
4. **SO `Range` 필드는 변경하지 않음** (cast gate 보존)

데미지 값은 본 라운드에서 변경 없음 (Tranche C/R4).

## Roadmap link
Plan: `C:\Users\paek6\.claude\plans\foamy-baking-melody.md` (BAL-1 Tranche B)

## Goal
- 보스 cone 스킬 hit rate 첫 시도(narrow)만 의존 → 자연스러운 회피 가능
- 텔레그래프 1.0-1.8s로 늘려 반응 시간 확보
- 측정 가능한 변경: 로그에서 `=>HIT` vs `=>MISS` 비율 추정 (목표 근방 ~20%, 정확한 수치는 R6에서 fine-tune)

## Files to touch
- **EDIT** `Assets/ArenaCombat/Scripts/Core/Skill/Core/SkillLibrary.cs` — 7개 보스 스킬 함수 변경
- **EDIT** `Assets/ArenaCombat/Resources/Skills/BossSkills/*.asset` (7개) — TelegraphDuration 필드만

**변경하지 않음:**
- 모든 BossSkills SO의 `Range` 필드 (cast eligibility gate 보존)
- 모든 보스 스킬 데미지 값 (R4 처리)
- SealChain_Boss / RuptureMagazine_Boss (UNIMPLEMENTED null 반환)
- SurvivalPulse_Boss / OverchargeMode_Boss (Self/조건부)

## Approach

### B-1. SkillLibrary.cs — 4개 cone 스킬 wide retry 제거 + cone 축소

#### ExecutionSpike_Boss (line 237-249)
```diff
         public static SkillStep ExecutionSpike_Boss() =>
             ctx =>
             {
-                DealDirectionalHit(118f, 16.6f, 35f).Invoke(ctx);
+                DealDirectionalHit(118f, 16.6f, 25f).Invoke(ctx);
                 TriggerOnHit(
                     onHit: hit =>
                     {
                         ApplyVulnerability(2.5f, 0.06f).Invoke(hit);
                         ExecuteBelowHP(30f, 26f).Invoke(hit);
-                    },
-                    onMiss: DealDirectionalHit(118f, 20.4f, 75f)
+                    }
                 ).Invoke(ctx);
             };
```

#### CrushingBarrage_Boss (line 252-270)
```diff
         public static SkillStep CrushingBarrage_Boss() =>
             ctx =>
             {
-                DealDirectionalHit(32f, 15f, 40f).Invoke(ctx);
+                DealDirectionalHit(32f, 15f, 30f).Invoke(ctx);
                 TriggerOnHit(
                     onHit: hit =>
                     {
                         DealMultiHitDamage(32f, 4).Invoke(hit);
                         DealShieldBreakDamage(55f, 1.35f).Invoke(hit);
-                    },
-                    onMiss: miss =>
-                    {
-                        DealDirectionalHit(32f, 19f, 80f).Invoke(miss);
-                        TriggerOnHit(
-                            onHit: DealMultiHitDamage(32f, 4)
-                        ).Invoke(miss);
                     }
                 ).Invoke(ctx);
             };
```

#### FortressArmor_Boss (line 307-319)
```diff
         public static SkillStep FortressArmor_Boss() =>
             ctx =>
             {
-                DealDirectionalHit(82f, 16f, 35f).Invoke(ctx);
+                DealDirectionalHit(82f, 16f, 25f).Invoke(ctx);
                 TriggerOnHit(
                     onHit: hit =>
                     {
                         float maxHp = hit.Caster?.MaxHP ?? 0f;
                         GainShield(maxHp * 0.08f).Invoke(hit);
-                    },
-                    onMiss: DealDirectionalHit(82f, 19.6f, 80f)
+                    }
                 ).Invoke(ctx);
             };
```

#### CollapseRoar_Boss (line 322-336)
```diff
         public static SkillStep CollapseRoar_Boss() =>
             ctx =>
             {
-                ApplyInArea(12f, AreaShape.Circle,
+                ApplyInArea(10f, AreaShape.Circle,
                     inner =>
                     {
                         DealDamage(88f).Invoke(inner);
                         ApplyHitStun(0.16f).Invoke(inner);
                         ApplyDefenseDown(3f, 6f).Invoke(inner);
                     }
                 ).Invoke(ctx);
-                TriggerOnHit(
-                    onMiss: ApplyInArea(23.8f, AreaShape.Circle, DealDamage(88f))
-                ).Invoke(ctx);
             };
```

### B-2. SkillLibrary.cs — AoE/projectile 3개 footprint 축소

#### MarkWave_Boss (line 354-362)
```diff
         public static SkillStep MarkWave_Boss() =>
-            ApplyInArea(27f, AreaShape.Cone,
+            ApplyInArea(22f, AreaShape.Cone,
                 inner =>
                 {
                     DealDamage(70f).Invoke(inner);
                     ApplyDebuff(4f, DebuffType.Mark, 1f).Invoke(inner);
                 },
-                angleDeg: 80f
+                angleDeg: 50f
             );
```

#### BarrierBreaker_Boss (line 371-379)
```diff
         public static SkillStep BarrierBreaker_Boss() =>
-            LaunchProjectile(19f, 48f, true,
+            LaunchProjectile(19f, 35f, true,
                 hit =>
                 {
                     DealDamage(90f).Invoke(hit);
                     DealShieldBreakDamage(70f, 1.5f).Invoke(hit);
                     ApplyDefenseDown(4f, 8f).Invoke(hit);
                 }
             );
```

#### ErosionField_Boss (line 273-287)
```diff
         public static SkillStep ErosionField_Boss() =>
             ctx =>
             {
-                SpawnPersistentArea(4f, 9f, AreaShape.Circle, 1f,
+                SpawnPersistentArea(4f, 7f, AreaShape.Circle, 1f,
                     tick =>
                     {
                         ApplyDamageOverTime(1f, 7f).Invoke(tick);
                         ApplyAntiHeal(1f, 0.20f).Invoke(tick);
                     }
                 ).Invoke(ctx);

-                SpawnPersistentArea(4f, 22.2f, AreaShape.Circle, 1f,
+                SpawnPersistentArea(4f, 18f, AreaShape.Circle, 1f,
                     tick => ApplyDamageOverTime(1f, 7f).Invoke(tick)
                 ).Invoke(ctx);
             };
```

### B-3. SkillDefinition SO TelegraphDuration 변경 (7개)

| SO | Old TelegraphDuration | New TelegraphDuration |
|----|---------|---------|
| ExecutionSpike_Boss.asset | 0.8 | **1.5** |
| CrushingBarrage_Boss.asset | 0.6 | **1.2** |
| FortressArmor_Boss.asset | 0.7 | **1.4** |
| CollapseRoar_Boss.asset | 1.0 | **1.8** |
| MarkWave_Boss.asset | 1.0 | **1.6** |
| BarrierBreaker_Boss.asset | 1.0 | **1.6** |
| ErosionField_Boss.asset | 1.2 | **1.8** |

**SO `Range` 필드는 모두 그대로 유지** (cast eligibility gate 보존).

### B-4. 검증 측정 (R2 후 플레이테스트)

R2 적용 후:
- 콘솔 필터 `DealDirectionalHit` → HIT/MISS 카운트 추출
- 보스 분당 effective 데미지 측정 (Combat 로그 합산)
- 텔레그래프 길어진 만큼 CD-aware 캐스팅 빈도 감소 — DPS = damage × (1/CD) × hit_rate. CD 동일하지만 텔레그래프 길어진 만큼 첫 캐스트 지연 → 분당 캐스트 빈도 감소

**ErosionField persistent area는 hit rate 측정 불가** (zone tick은 `OnHitRecorded` 안 호출). 별도 측정: 플레이어가 영역 안에 머문 시간 / 분 (다음 라운드 또는 R6에서 도구화).

## Risks / unknowns

1. **Hit rate가 20% 못 미칠 가능성 (Codex 지적)**: Wide retry 제거 + cone 25-30°는 area 기준으로 보면 옛 wide retry footprint의 ~21-23%. 실제 회피 동작 더하면 20% 이하 가능. R2 적용 후 측정 → R6에서 조정.

2. **TelegraphDuration 1.5-2x로 인한 보스 캐스트 빈도 감소**: 한 사이클 = telegraph + CD. CD 동일하므로 telegraph 추가시간만큼 분당 캐스트 감소. 보스 압박감 약화 가능 → R4 데미지 배율 + R5 페이즈 데미지 배율로 보상.

3. **MarkWave_Boss 변경 폭이 가장 큼**: cone 80°→50° + range 27→22. Codex 의견 "OK for first pass". R6에서 fine-tune.

4. **BarrierBreaker_Boss projectile range만 변경**: pierce=true 유지. cone 개념 없으므로 거리 회피만 가능. 텔레그래프 길어져 회피 시간 확보 + range 짧아 거리 회피도 효과적.

5. **Telegraph 컨텍스트 resample (Codex Critical #2)**: 현재 보스 windup 중 회전 거의 없음 → 영향 작음. 추후 보스 AI movement 추가 시 재방문 필요. Out of scope.

6. **SkillLibrary.cs 변경 후 컴파일**: 모든 변경이 단순 파라미터 수정 또는 인자 제거. 컴파일 안정성 높음.

## Questions for Codex (Round 2)

1. **R1 critical #1 (SO Range 변경 안 함) 반영 적절한가?**
2. **R1 critical #2 (Telegraph 컨텍스트 resample) defer**: 보스 movement이 없는 현 상황에서 R2 적용 시 hit rate에 영향 없는가? (텔레그래프 시 표시되는 CastPosition/Direction과 실제 hit 시 ctx 재계산이 일치?)
3. **Wide retry 제거 후 hit rate 20% 이하로 떨어질 경우 — 즉시 cone 각도 살짝 늘려야 (e.g., 25°→30°)? 또는 R4 진행 후 통합 측정?**
4. **변경 후 ErosionField hit rate 측정 방법**: zone tick이 OnHitRecorded 안 호출 → 별도 telemetry 필요. R6 telemetry pass에서 다룰지?

## Out of scope for this round
- 플레이어 hit rate (R3)
- 보스 스킬 데미지 (R4)
- 페이즈 데미지 배율 (R5)
- SealChain_Boss / RuptureMagazine_Boss 구현
- Telegraph 컨텍스트 resample 디자인 수정
- ErosionField persistent area telemetry
