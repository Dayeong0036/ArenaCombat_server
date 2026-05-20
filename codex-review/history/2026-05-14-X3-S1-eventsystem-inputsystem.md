# X3-S1: Smoke Test Patch — EventSystem Input System 전환 — 2026-05-14

**Status**: APPLIED. 사용자 smoke test 첫 시도 시 발견된 InvalidOperationException 즉시 수정.

## 발견 경위

X3 smoke test 시작 — SampleScene (lobby) 로드 → Console에 **InvalidOperationException** 발생:

```
InvalidOperationException: You are trying to read Input using the UnityEngine.Input class,
but you have switched active Input handling to Input System package in Player Settings.
  UnityEngine.Input.get_mousePosition () (at <0a95d216b82544b7954b7f0b10bfaf86>:0)
  UnityEngine.EventSystems.BaseInput.get_mousePosition () (at .../UGUI/EventSystem/InputModules/BaseInput.cs:75)
  UnityEngine.EventSystems.StandaloneInputModule.UpdateModule () (at .../StandaloneInputModule.cs:175)
  UnityEngine.EventSystems.EventSystem.TickModules () (at .../EventSystem.cs:460)
  UnityEngine.EventSystems.EventSystem.Update () (at .../EventSystem.cs:480)
```

## 원인 분석

- 프로젝트 Player Settings → Active Input Handling = **Input System Package** (or Both)
- 그러나 SampleScene / 3DScene / Title.unity의 EventSystem GameObject가 `StandaloneInputModule` 컴포넌트 사용 (옛 Input API `UnityEngine.Input.mousePosition`)
- 매 프레임 StandaloneInputModule.UpdateModule → Input.mousePosition 접근 → InvalidOperationException

`StandaloneInputModule`은 Input System 전환 후 사용 불가. **InputSystemUIInputModule**로 교체 필요.

## 수정

3 씬의 EventSystem GameObject MonoBehaviour 컴포넌트 script GUID 교체:
- `4f231c4fb786f3946a6b90b886c48677` (StandaloneInputModule, com.unity.ugui)
- → `01614664b831546d2ae94a42149d80ac` (InputSystemUIInputModule, com.unity.inputsystem)

| Scene | EventSystem MonoBehaviour fileID |
|-------|----------------------------------|
| SampleScene.unity | 1285113812 |
| 3DScene.unity | 1073860255 |
| Title.unity | 1886448069 |

옛 fields (m_SendPointerHoverToParent / m_HorizontalAxis / m_VerticalAxis / m_SubmitButton / m_CancelButton / m_InputActionsPerSecond / m_RepeatDelay / m_ForceModuleActive) 모두 제거 — Unity가 reimport 시 InputSystemUIInputModule default fields (m_MoveRepeatDelay / m_MoveRepeatRate / m_PointAction / m_LeftClickAction 등) 자동 채움.

## Demo scenes 미수정

다음 3 demo scenes에 동일 GUID 잔존 (사용 안 함, 무영향):
- `Assets/ArenaCombat/Resources/UI pack/Prefabs/SampleScene.unity` (UI pack 데모)
- `Assets/Dark Ghosts FREE/Scenes/Demo.unity` (3rd party)
- `Assets/MasterStylizedProjectiles/Scenes/MasterStylizedProjectileDemo.unity` (3rd party)

Build Settings 미포함 → 게임플레이 실행 안 됨 → 무수정.

## Files

| Op | Path | LOC delta |
|---|---|---|
| EDIT | `Assets/Scenes/SampleScene.unity` | -8 (8 옛 fields 제거, script GUID 1 swap) |
| EDIT | `Assets/Scenes/3DScene.unity` | -8 |
| EDIT | `Assets/Scenes/Title.unity` | -8 |

## Verification (post-apply, expected)

1. Unity scene reimport 자동
2. EventSystem Inspector → InputSystemUIInputModule 표시 (StandaloneInputModule 아님)
3. Play 모드 진입 시:
   - `InvalidOperationException` **사라짐**
   - UI 클릭/마우스 조작 정상 작동
   - Console 로그 정상 흐름 (`[LobbyManager] Unity Services initialized` / `[LobbyTestUI] initialized` / `[LobbyManager] Signed in anonymously`)

## 부가 관찰 (스모크 테스트 1차 시도)

기존 클린 흐름 (호환 OK):
- `[LobbyManager] Unity Services initialized` ✓
- `[PlayerSpawnManager] Resolved spawn points: 0` ⚠️ — spawn points 미설정 (player spawn 시 Vector3.zero 사용 가능, smoke 진행 가능)
- `[LobbyTestUI] Lobby test UI initialized` ✓
- `[LobbyManager] Signed in anonymously - PlayerId: evXayYN2E0eOLzlPPgQdXO2khOyL` ✓
- `[SkillRangeDisplay] Indicator Prefab not assigned — range display disabled` ⚠️ — X2-9 SkillRangeDisplay debug viz 비활성 (게임플레이 무영향)

## 사용자 작업 — 0건 (자동)

본 라운드 후 사용자가 다시 ▶ Play 누르면 InvalidOperationException 해결되어 lobby UI 정상 작동.

## Codex 검증 — 건너뜀 (긴급 수정)

본 패치는 정확히 한 가지 수정 (script GUID 1 swap × 3 scenes). Codex 워크플로우 거치지 않음 — 명확한 well-known 패치. 후속 X3-S2가 필요하면 Codex 사이클 복귀.

## Spawned Follow-up

- 사용자 Play 모드 재시도 → 5 verification 결과 보고
- 만약 다른 에러 발생 → X3-S2 패치 (자동 Codex 워크플로우 복귀)

## Parallel Workstream

X4 / X1-6 모든 라운드와 파일 0겹침 (씬 EventSystem만 수정).
