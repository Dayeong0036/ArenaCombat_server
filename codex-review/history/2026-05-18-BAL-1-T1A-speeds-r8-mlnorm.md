# Pending Codex Review — BAL-1 Tranche 1A (Round 2): Numerical Balance Update

## R1 Verdict
**FAIL / Not Ready** — 2 P0 + 3 P1 발견. R2에서 Tranche 1 범위 축소 + scope 명확화:

| # | R1 발견 | R2 Resolution |
|---|--------|---------------|
| **P0-1** | ML boss 스킬 path가 telegraph 우회 (`_skillExecutor.Execute` 직접) | **T1A에서 제외, T1B(별도 라운드)로 분리.** 현재 ML inference 비활성 상태라 영향 없음. T1B에서 SkillManager telegraph-aware 경로로 라우팅 + auto-cast 의존성 분리 |
| **P0-2** | `SetAutoCast(false)` 시 SkillManager가 active telegraph 캔슬 | **T1B에서 SkillManager.Update의 telegraph cancel 조건을 `!_statMgr.IsAlive`만으로 좁힘** |
| P1-3 | Player A.prefab `moveSpeed: 7` serialized → NetworkConstants 변경 무의미 | **prefab YAML 직접 변경 (7→14) 추가** |
| P1-4 | Tranche 값들 미적용 | (Codex가 pending.md를 implementation으로 오해. PRE-implementation proposal임. 무관) |
| P1-5 | R8 geometry 미적용 | (위와 동일) |

Plus suggestion 채택:
- 회전 720°/s → **540°/s** (sharp oscillation 위험 감소)
- `SetMoveSpeed` 안에 `Mathf.Max(0, speed)` clamp 추가
- 텔레그래프 정지 게이트는 T1B로 이동 (T1A에서 추가해도 ML 비활성 상태라 의미 없음)

## Topic — Tranche 1A (Round 2)
**순수 numerical balance update** — 코드 아키텍처 변경 없이 값만 변경:

1. Player MoveSpeed 7→14 (NetworkConstants + Player A.prefab)
2. Boss MoveSpeed 5→8.4 (BossInferenceAgent default + Boss.prefab)
3. Boss 회전 200→540°/s (Codex 권장)
4. 페이즈별 보스 속도 자동 갱신 (OnPhaseChanged → SetMoveSpeed)
5. ML 정규화 상수 12→16, 50→80 (BossInferenceAgent + BossObservationCollector)
6. 보스 7스킬 R8 cone/range/AoE 재조정 (R2의 정지 가정에서 이동 가정으로 갱신)
7. BossSkills SOs telegraph duration 재조정 (1.5→1.35 등)

**제외 (T1B로 이관):**
- 텔레그래프 정지 게이트 (ML이 telegraph 안 쓰면 무의미)
- SkillManager telegraph 독립 (auto-cast off에서도 telegraph 유지)
- BossInferenceAgent.TryExecuteSkill → telegraph-aware 경로

## Roadmap link
Plan: `C:\Users\paek6\.claude\plans\foamy-baking-melody.md`

## Goal
- 운영 코드 모든 numerical 값을 새 발란스 도면과 일치
- ML 학습 시점에 사용자에게 넘긴 프롬프트의 값들과 정합성
- 컴파일 안정, 게임 동작 변화는 속도 + cone 변화에 한정

## Files to touch

### .cs 파일 (5개)
| 파일 | 변경 |
|------|------|
| `Core/Network/NetworkConstants.cs` | `DEFAULT_MOVE_SPEED: 7→14` |
| `Core/AI/BossInferenceAgent.cs` | `_moveSpeed: 5→8.4`, `_rotationSpeed: 200→540`, `SetMoveSpeed(float)` API, 정규화 `/12f→/16f`, `/50f→/80f` |
| `Core/AI/BossObservationCollector.cs` | `_maxSpeed: 12→16`, `_maxBurstDmg: 50→80` |
| `Core/Network/BossNetworkController3D.cs` | `_inferenceAgent` 캐시 + `OnPhaseChanged` 속도 갱신 |
| `Core/Skill/Core/SkillLibrary.cs` | 보스 7스킬 cone/range/AoE 재조정 |

### Prefab/Scene (2개)
| 파일 | 변경 |
|------|------|
| `3DSceneScript/Player/Player A.prefab:189` | `moveSpeed: 7→14` |
| `Prefabs/Boss/Boss.prefab:299` | `_moveSpeed: 5→8.4` |
| `Prefabs/Boss/Boss.prefab` (BossInferenceAgent fields) | `_rotationSpeed: 200→540` |
| `Prefabs/Boss/Boss.prefab` (BossObservationCollector fields) | `_maxSpeed: 12→16`, `_maxBurstDmg: 50→80` |

### Boss Skills SO (7개)
| SO | 현재 (R2 후) | T1A 목표 |
|----|----------|----------|
| ExecutionSpike_Boss | TelegraphDuration 1.5 | **1.35** |
| CrushingBarrage_Boss | 1.2 | **1.2** (유지) |
| FortressArmor_Boss | 1.4 | **1.3** |
| CollapseRoar_Boss | 1.8 | **1.6** |
| MarkWave_Boss | 1.6 | **1.4** |
| BarrierBreaker_Boss | 1.6 | **1.4** |
| ErosionField_Boss | 1.8 | **1.6** |

## Approach

### 1. NetworkConstants.cs
```diff
- public const float DEFAULT_MOVE_SPEED = 7f;
+ public const float DEFAULT_MOVE_SPEED = 14f;
```

### 2. Player A.prefab — line 189 (P1-3 fix)
```diff
-  moveSpeed: 7
+  moveSpeed: 14
```
Direct YAML edit. (PlayerStatsSO 안 건드림 — PNC3D 자체 maxHP/moveSpeed 필드를 사용.)

### 3. BossInferenceAgent.cs
```diff
- [SerializeField] private float _moveSpeed = 5f;
- [SerializeField] private float _rotationSpeed = 200f;
+ [SerializeField] private float _moveSpeed = 8.4f;
+ [SerializeField] private float _rotationSpeed = 540f;  // 720→540 (Codex 권장, oscillation 감소)

+ // BAL-1 T1A: 페이즈별 속도 갱신 외부 API. clamp >=0.
+ public void SetMoveSpeed(float speed)
+ {
+     _moveSpeed = Mathf.Max(0f, speed);
+ }
```

정규화 상수 (line 95-97):
```diff
- sensor.AddObservation(p1Alive ? Mathf.Clamp01(_collector.P1AvgSpeed / 12f) : 0f);
- sensor.AddObservation(p2Alive ? Mathf.Clamp01(_collector.P2AvgSpeed / 12f) : 0f);
- sensor.AddObservation(Mathf.Clamp01(_collector.RecentBurstDamage / 50f));
+ // BAL-1 T1A: Player 14 m/s → /16f (여유), burst ~95 max → /80f
+ sensor.AddObservation(p1Alive ? Mathf.Clamp01(_collector.P1AvgSpeed / 16f) : 0f);
+ sensor.AddObservation(p2Alive ? Mathf.Clamp01(_collector.P2AvgSpeed / 16f) : 0f);
+ sensor.AddObservation(Mathf.Clamp01(_collector.RecentBurstDamage / 80f));
```

### 4. BossObservationCollector.cs
```diff
- [SerializeField] private float _maxBurstDmg = 50f;
- [SerializeField] private float _maxSpeed = 12f;
+ [SerializeField] private float _maxBurstDmg = 80f;
+ [SerializeField] private float _maxSpeed = 16f;
```

### 5. BossNetworkController3D.cs

#### 5-1. inference agent 캐시 (Awake 안, line ~135 근방)
```diff
- _mlInferenceActive = TryGetComponent<BossInferenceAgent>(out var agent) && agent.enabled;
+ TryGetComponent<BossInferenceAgent>(out _inferenceAgent);
+ _mlInferenceActive = _inferenceAgent != null && _inferenceAgent.enabled;
```

새 필드:
```csharp
private BossInferenceAgent _inferenceAgent;
```

#### 5-2. `OnPhaseChanged`에 페이즈별 속도 갱신
```diff
 private void OnPhaseChanged(BossPhase oldPhase, BossPhase newPhase)
 {
     Debug.Log($"[BossNetworkController3D] Phase {oldPhase} → {newPhase} ...");
     PopulateBossSkills(newPhase);

+    // BAL-1 T1A: 페이즈별 속도 스케일링 (ML inference agent의 _moveSpeed 갱신)
+    if (_inferenceAgent != null)
+    {
+        float phaseSpeed = newPhase switch
+        {
+            BossPhase.Phase1 => 8.4f,
+            BossPhase.Phase2 => 8.4f,
+            BossPhase.Phase3 => 9.2f,
+            BossPhase.Enrage => 10.1f,
+            _ => 8.4f,
+        };
+        _inferenceAgent.SetMoveSpeed(phaseSpeed);
+    }
 }
```

### 6. SkillLibrary.cs — 보스 7스킬 R8 재조정

```diff
 public static SkillStep ExecutionSpike_Boss() =>
     ctx =>
     {
-        DealDirectionalHit(118f, 16.6f, 25f).Invoke(ctx);
+        DealDirectionalHit(118f, 18f, 38f).Invoke(ctx);  // R8: range 16.6→18, cone 25→38
         ...
     };

 public static SkillStep CrushingBarrage_Boss() =>
     ctx =>
     {
-        DealDirectionalHit(32f, 15f, 30f).Invoke(ctx);
+        DealDirectionalHit(32f, 15f, 40f).Invoke(ctx);  // R8: cone 30→40
         ...
     };

 public static SkillStep FortressArmor_Boss() =>
     ctx =>
     {
-        DealDirectionalHit(82f, 16f, 25f).Invoke(ctx);
+        DealDirectionalHit(82f, 16f, 35f).Invoke(ctx);  // R8: cone 25→35
         ...
     };

 public static SkillStep CollapseRoar_Boss() =>
     ctx =>
     {
-        ApplyInArea(10f, AreaShape.Circle, ...).Invoke(ctx);
+        ApplyInArea(12f, AreaShape.Circle, ...).Invoke(ctx);  // R8: AoE 10→12
         ...
     };

 public static SkillStep MarkWave_Boss() =>
     ApplyInArea(22f, AreaShape.Cone,
         inner => { ... },
-        angleDeg: 50f
+        angleDeg: 65f  // R8: sweep 50→65
     );

 public static SkillStep BarrierBreaker_Boss() =>
-    LaunchProjectile(19f, 35f, true, ...);
+    LaunchProjectile(19f, 40f, true, ...);  // R8: projectile range 35→40

 public static SkillStep ErosionField_Boss() =>
     ctx =>
     {
-        SpawnPersistentArea(4f, 7f, ...);
-        SpawnPersistentArea(4f, 18f, ...);
+        SpawnPersistentArea(4f, 8f, ...);   // R8: inner 7→8
+        SpawnPersistentArea(4f, 20f, ...);  // R8: outer 18→20
     };
```

### 7. BossSkills SO TelegraphDuration (7개)
위 표 그대로 직접 YAML 편집.

## Risks / unknowns

1. **ML inference 비활성 상태 가정**: 본 라운드는 ML이 currently disabled라는 가정. Boss.prefab의 BossInferenceAgent component가 disabled이거나 ONNX 미할당이면 영향 없음. 사용자가 ML을 실 사용으로 켜기 전에 T1B 완료 필요.

2. **회전 540°/s vs 720°/s**: Codex 540 권장. ML 학습 시 동일 값으로 학습해야 정합성. **학습 측에 540 전달 필요** (PART 1 프롬프트에 노트 추가 필요).

3. **player A.prefab 직접 편집**: Unity가 prefab 변경 감지 시 모든 인스턴스에 propagate. Chapter1.unity의 Player 인스턴스에도 영향. 정상 동작.

4. **컴파일 안정**: 본 라운드는 새 메서드 추가(`SetMoveSpeed`) + 필드 추가(`_inferenceAgent`) + 값 변경 + 디폴트 변경. 기존 호출 패턴 깨지지 않음.

5. **새 ONNX 적용 전 ML 비활성**: 사용자가 별도 학습 진행 중. 그 사이 ML이 enabled 상태로 켜져있으면 (1) telegraph 안 됨 (P0-1), (2) 정규화 mismatch로 saturation. 결론: ML disabled 상태 유지. Boss.prefab에서 BossInferenceAgent.enabled를 false로 (현재 상태인지 확인).

6. **MarkWave_Boss cone 65°는 너무 큰가**: 22m × tan(32.5°) = 14m sweep width. 14 m/s 플레이어가 1.4s 텔레그래프 동안 21m 이동 가능. 회피 가능하지만 빠듯. R6 fine-tune.

## Questions for Codex

1. **`SetAutoCast(false)` 시 telegraph cancel 동작을 T1A에서 미리 분리할 가치 있나?** R2 verdict P0-2의 분리는 T1B로 미루는 게 맞나, 또는 T1A에서 같이 처리하는 게 안전한가? 분리하면 SkillManager.cs:154 한 줄 변경 (`if (!_autoCastEnabled || ...)` → `if (!_statMgr.IsAlive || ...)`).

2. **BossInferenceAgent.Initialize 안의 `SetAutoCast(false)` 호출도 T1B로 미루나?** 미루면 ML 비활성 상태에서 BossInferenceAgent가 어쩌다 활성화돼도 auto-cast가 자동으로 끄지지 않음 (수동 disable 필요).

3. **회전 540°/s vs 400°/s**: 540은 Codex 추천 상한. 더 보수적인 400은? ML 학습 안정성 vs visual response 사이 균형.

4. **`_inferenceAgent` null 시 OnPhaseChanged 안전성**: 본 안은 null 체크 했음. 안전.

5. **R8 cone 값 hit rate 예측에 대한 신뢰도**: Codex가 "25-30% 도달 보장 안 함" 응답. 본 라운드 적용 후 R6에서 telemetry 측정해서 fine-tune 필수. 동의?

## Out of scope for this round
- **T1B**: SkillManager telegraph 독립 + BossInferenceAgent telegraph-aware 경로 (별도 라운드)
- **T2**: Player HP / Boss HP / damage (R3+R4)
- **T3**: 페이즈 damage/cooldown 배율 (R5)
- ML 학습 측 변경 (사용자 별도)
- BTPlayerAgent 수정 (운영 미사용)
