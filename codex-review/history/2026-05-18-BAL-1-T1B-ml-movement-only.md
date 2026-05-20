# Pending Codex Review — BAL-1 T1B: ML Movement-Only Architecture

## Topic
**아키텍처 명확화**: 보스에서 ML 책임을 **이동에만 한정**. 스킬 선택은 SkillManager auto-cast (서버, BossAdaptiveWeights + variant slotWeights 가중치) 사용. 이렇게 분리하면:
- ML ONNX 파일 drop-in 가능 (이동만 학습된 모델)
- 스킬은 기존 SkillManager 텔레그래프 경로 그대로 사용 → telegraph 정상 작동
- 텔레그래프 정지 게이트가 의미 있게 작동 (T1A에서 미적용한 게이트를 T1B에서 추가)

## Roadmap link
Plan: `C:\Users\paek6\.claude\plans\foamy-baking-melody.md`
사용자 결정: "보스 이동은 mlagent ONNX, 스킬은 서버 가중치 auto-cast"

## Goal
- ML ONNX 추가 시 **drop-in 작동** (이동만 처리, 스킬은 서버가 무관)
- SkillManager auto-cast가 보스에서 **항상 ON** (ML 활성/비활성 무관)
- 텔레그래프 발동 시 (SkillManager가 BNC3D.IsBusy=true) ML 이동 정지
- 코드 정리: BossInferenceAgent에서 스킬 실행 로직 제거 (이동 전용)

## Files to touch

| 파일 | 변경 |
|------|------|
| `Assets/ArenaCombat/Scripts/Core/AI/BossInferenceAgent.cs` | Initialize의 `SetAutoCast(false)` 제거, OnActionReceived에서 스킬 실행 제거, ApplyMovement에 telegraph 게이트, TryExecuteSkill/WriteDiscreteActionMask/Heuristic skill 코드 정리 |
| `Assets/ArenaCombat/Scripts/Core/Network/BossNetworkController3D.cs` | `if (!_mlInferenceActive)` 가드 제거 (`PopulateBossSkills` + `ApplyAIVariant`) — auto-cast 항상 ON |

## Approach

### 1. BossInferenceAgent.cs — 이동 전용으로 단순화

#### 1-1. Initialize에서 auto-cast disable 제거
```diff
 public override void Initialize()
 {
     ...
-    if (_skillManager != null)
-        _skillManager.SetAutoCast(false);
 }
```

이렇게 하면 ML이 활성화돼도 SkillManager.AutoCast는 BNC3D.PopulateBossSkills가 켠 상태 유지.

#### 1-2. OnActionReceived에서 스킬 실행 제거
```diff
 public override void OnActionReceived(ActionBuffers actionBuffers)
 {
     if (!IsServer) return;
     if (_statManager == null || !_statManager.IsAlive) return;

     int moveAction = actionBuffers.DiscreteActions[0];
-    int skillAction = actionBuffers.DiscreteActions[1];

     ApplyMovement(moveAction);
-
-    if (skillAction >= 1 && skillAction <= BossObservationCollector.BossSkillSlots)
-    {
-        int slot = skillAction - 1;
-        TryExecuteSkill(slot);
-    }
 }
```

ONNX 모델이 스킬 action(branch 1)을 출력해도 무시. 모델이 4-action movement만 출력해도 OK.

#### 1-3. ApplyMovement에 telegraph 게이트
```diff
 private void ApplyMovement(int moveAction)
 {
     if (_bnc == null) return;
+
+    // BAL-1 T1B: SkillManager가 텔레그래프 중일 때 보스 이동 정지.
+    // hit 위치 예측 가능 + telegraph readability 유지.
+    if (_bnc.IsBusy)
+    {
+        if (_stateManager != null) _stateManager.NotifyMovementInput(false);
+        return;
+    }

     switch (moveAction) ...
 }
```

#### 1-4. TryExecuteSkill 제거 (이제 호출 안 됨)
```diff
- private void TryExecuteSkill(int slot)
- {
-     if (_skillManager == null || !_skillManager.CanUse(slot)) return;
-     var slots = _skillManager.Slots;
-     if (slot >= slots.Count || slots[slot] == null) return;
-     ICombatant target = _skillManager.FindNearestTarget();
-     SkillContext ctx = _skillManager.BuildSkillContext(target);
-     _skillExecutor.Execute(slots[slot], ctx);
- }
```

#### 1-5. WriteDiscreteActionMask 단순화 (스킬 마스크 제거)
스킬 action은 무시되므로 마스크 불필요. 단, 액션 공간을 그대로 유지해야 기존 ONNX 호환:
```diff
 public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
 {
-    for (int slot = 0; slot < BossObservationCollector.BossSkillSlots; slot++)
-    {
-        int actionIndex = slot + 1;
-        if (_skillManager == null || !_skillManager.CanUse(slot))
-            actionMask.SetActionEnabled(1, actionIndex, false);
-    }
+    // BAL-1 T1B: 스킬 action은 서버 auto-cast가 처리. 마스킹 불필요.
+    // (액션 공간 자체는 호환성 위해 유지)
 }
```

#### 1-6. Heuristic — 스킬 키 제거 (옵션, debug 편의용)
```diff
 public override void Heuristic(in ActionBuffers actionsOut)
 {
     var d = actionsOut.DiscreteActions;
     d[0] = 0;
     d[1] = 0;

     if (Input.GetKey(KeyCode.UpArrow)) d[0] = 1;
     else if (Input.GetKey(KeyCode.LeftArrow)) d[0] = 2;
     else if (Input.GetKey(KeyCode.RightArrow)) d[0] = 3;
-
-    if (Input.GetKey(KeyCode.Alpha1)) d[1] = 1;
-    if (Input.GetKey(KeyCode.Alpha2)) d[1] = 2;
-    if (Input.GetKey(KeyCode.Alpha3)) d[1] = 3;
-    if (Input.GetKey(KeyCode.Alpha4)) d[1] = 4;
-    if (Input.GetKey(KeyCode.Alpha5)) d[1] = 5;
 }
```

### 2. BossNetworkController3D.cs — auto-cast 항상 ON

#### 2-1. PopulateBossSkills (line ~390)
```diff
-            // X4-8: ML inference agent handles skill selection; skip auto-cast.
-            if (!_mlInferenceActive)
-                _skillMgr.SetAutoCast(true);
+            // BAL-1 T1B: ML은 이동만 담당. 스킬은 서버 SkillManager auto-cast가 처리.
+            _skillMgr.SetAutoCast(true);
```

#### 2-2. ApplyAIVariant (line ~426)
```diff
-            if (!_mlInferenceActive)
-                _skillMgr.SetAutoCast(true);
+            // BAL-1 T1B: ML 활성/비활성 무관 auto-cast 항상 ON.
+            _skillMgr.SetAutoCast(true);
```

`_mlInferenceActive` 플래그는 그대로 두기 (다른 코드 영향, ROADMAP 참조). 단, 더 이상 auto-cast 분기에 사용 안 함.

## 행동 결과

- **ML disabled** (현재 운영, ONNX 미할당): 보스 정지 + SkillManager 텔레그래프 자동시전 (현재 작동 그대로)
- **ML enabled** (ONNX 추가 후): 보스 ONNX 이동 + SkillManager 텔레그래프 자동시전. 텔레그래프 발동 시 IsBusy=true → ML 이동 정지 → 텔레그래프 완료 후 ML 이동 재개
- **ML disable→enable 전환**: BNC3D.Awake에서 캐시되므로 런타임 토글은 다음 매치까지 반영 안 됨. 정상 동작.

## Risks / unknowns

1. **action space 호환**: 기존 ONNX가 (movement, skill) 2 branch 출력하면 본 변경 후 skill output은 무시됨. 새 ONNX 학습 시 1 branch (movement only) 또는 2 branch (skill output 무용) 둘 다 OK. **사용자가 ML 학습 측에 전달할 프롬프트에 "skill action은 서버가 처리, 학습 환경에서 학습할 필요 없음" 명시 필요.**

2. **`SetAutoCast(false)` 호출 잔존 여부**: BossInferenceAgent.Initialize 외에 보스에서 SetAutoCast(false) 호출하는 코드 없는지 확인. 만약 있다면 추가 검토.

3. **`_mlInferenceActive` flag dead**: auto-cast 분기에서 사용 안 함. 다른 곳에서 사용한다면 어떤지? 현재 BNC3D 내부에서만 사용 (391, 426줄에서 그것도 제거). T1B 후 사실상 dead flag. 제거? 또는 향후 다른 ML-aware 분기 위해 유지?

4. **텔레그래프 게이트의 NotifyMovementInput(false)**: StateManager가 idle 상태를 받음. 텔레그래프 중에는 보스가 cast 상태 (Casting) 또는 자체 안내. StateManager에서 conflict 없는지?

5. **MaxStep=0 (Boss.prefab의 BossInferenceAgent agentParameters)**: Episode 종료 조건 없음 → 학습 시 종료 안 됨. 학습 측에서 별도 설정 필요. 본 라운드 영향 없음 (운영에서 사용).

6. **Skill action을 마스크하지 않는 이유**: 기존 ONNX가 skill action 출력해도 그냥 무시. 새 ONNX는 4-action movement만 학습. 호환성 양쪽 OK.

## Questions for Codex

1. **`_mlInferenceActive` flag dead code 제거 vs 유지**: T1B 후 use case 사라짐. 제거 권장? 또는 향후 hook을 위해 유지?

2. **WriteDiscreteActionMask 완전 제거 vs 빈 메서드 유지**: 빈 메서드가 명시적이지만 코드 더 길어짐. ML-Agents SDK가 비어있어도 OK인지?

3. **Telegraph 게이트의 NotifyMovementInput(false) 호출**: 텔레그래프 중에는 보스가 cast 상태. NotifyMovementInput(false)가 추가 효과 없을 수도. 코드 단순화 위해 제거 가능?

4. **Skill action을 명시적 마스크해서 모델 학습에 명확히 신호 주는 게 좋은가?** 학습 환경에서 skill action mask가 모든 1-5 차단하면 모델이 skill output 의미 없음 학습 → 4-action 모델과 비슷한 효과. 사용자 학습 측에 권장 사항.

5. **T1B 완료 후 ML 활성화 가능?**: 모든 SkillManager telegraph cancellation 이슈가 해결됐다고 봐도 됨? 다른 경로(예: ApplyAIVariant ClearAll 시 telegraph cancel)에서 의도치 않은 cancel 일어나는지 검증?

## Out of scope for this round
- T2: Player HP / Boss HP / damage scaling
- T3: 페이즈 데미지/cooldown 배율
- ONNX 학습 측 변경 (사용자 별도)
- BTPlayerAgent 변경 (운영 미사용)
- `_mlInferenceActive` 완전 제거 (별도 cleanup 라운드)
