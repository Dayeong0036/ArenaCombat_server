# X4-6: BossNetworkController3D Phase Tracking — 2026-05-13

**Status**: APPLIED. Codex Round 1 APPROVED WITH CHANGES (3 critical + 5 suggestion, all applied).

## Scope

1 파일 EDIT (`BossNetworkController3D.cs`). Buildup `BossController.HandlePhase` / `OnPhaseChanged` 좁게 포팅.

## Codex Critical Applied

- **C-1 `NetworkVariable<BossPhase>` 사용 (raw int 아님)**: 프로젝트 공용 `BossPhase` enum (NetworkConstants.cs:81 — `None / Phase1 / Phase2 / Phase3 / Enrage / Defeated`) 재사용. raw int로 새 phase 의미 만들면 X4-7 FSM에서 해석 충돌.
- **C-2 InitializeStatManager 성공 시 `Phase1` 설정**: full HP after init이 `None`에 머무르지 않고 `Phase1`로 진입. 첫 threshold cross 전까지 `Phase1` 유지.
- **C-3 OnBossDefeated에 `Defeated` write**: phase UI/FSM이 마지막 combat phase에 갇히지 않도록. HP/alive만 false로 두면 안 됨.

## Codex Suggestions Applied

- **S-1 BossPhaseThresholds descending HP ratio 명시**: HandlePhase 코멘트에 명시 (예: `[0.7, 0.4, 0.1]`). 디자이너 X4-5b 라운드에서 채울 가이드.
- **S-2 enum 매핑**: thresholds[0] cross → `Phase2`, thresholds[1] → `Phase3`, thresholds[2] → `Enrage`. `_thresholdPhases` static array로 매핑 명시.
- **S-3 4번째 이상 threshold cap + warn**: `MaxPhaseThresholds = 3` const. 4개 이상이면 warn-once + `Mathf.Min(thresholds.Length, MaxPhaseThresholds)`로 무시.
- **S-4 HandlePhase 위치**: FixedUpdate에서 HP mirror 후 / defeat check 전. OnBossDefeated가 최종 phase를 덮어쓰는 순서 보장.
- **S-5 OnPhaseChanged log-only 적절**: behavior wiring (skill 풀 전환, auto-cast 속도) X4-7 FSM 라운드.

## Edits

- 헤더: `X4-4 SCOPE` → `X4-6 SCOPE` + `ARCH STATUS: HP_AUTHORITY + POSITION_CONTROL + PHASE_TRACKING` 갱신.
- `networkCurrentPhase` `NetworkVariable<BossPhase>` 추가 (default `BossPhase.None`).
- `public BossPhase CurrentPhase => networkCurrentPhase.Value;` getter 추가.
- `InitializeStatManager` 성공 분기에 `networkCurrentPhase.Value = BossPhase.Phase1;` 추가 (Codex C-2).
- FixedUpdate: HP mirror 후 `HandlePhase();` 호출, defeat check 전 (Codex S-4).
- `MaxPhaseThresholds = 3` const + `_thresholdPhases` static array.
- `HandlePhase()` private helper 추가 (~25 LOC, thresholds 순회 + enum 매핑 + warn-once overflow).
- `OnPhaseChanged(BossPhase old, BossPhase new)` private helper 추가 (log-only).
- `OnBossDefeated`에 `networkCurrentPhase.Value = BossPhase.Defeated;` 추가 (Codex C-3).

## Surface Verification (intent)

- `using` 추가 없음 — `BossPhase`는 같은 namespace `ArenaCombat.Core.Network` 내 정의 ✓
- `networkCurrentPhase` NetworkVariable 1개 ✓
- `CurrentPhase` public getter 1개 ✓
- `HandlePhase` 호출 1회 (FixedUpdate 내) ✓
- `OnPhaseChanged` 호출 1회 (HandlePhase 내, `newPhase != oldPhase` 시) ✓
- `networkCurrentPhase.Value = BossPhase.Phase1` 1회 (InitializeStatManager 성공 분기) ✓
- `networkCurrentPhase.Value = BossPhase.Defeated` 1회 (OnBossDefeated) ✓
- `MaxPhaseThresholds = 3` cap + warn-once overflow ✓

## Dormant 계약 유지

X4-1..5a 유지 안전망 그대로:
1. NetworkPrefab 미등록 → 런타임 인스턴스 부재 → NV는 로컬 변수.
2. InitializeStatManager BossStatsSO null guard → 미할당 시 inert + phase 영원히 `None`.
3. NV default 0/false/zero/`None` → 서버 prime 시만 live.
4. 모든 mutation IsServer + alive 가드 (X4-3) + position `_rb` 가드 (X4-4).
5. `SkillManager.SetAutoCast(false)` Awake 유지 (X4-2).

X4-6은 dormant 계약 깨지 않음 — phase는 alive 보스에서만 갱신 (FixedUpdate 가드).

## Verification (post-apply, expected)

1. Unity recompile <3s. 0 신규 에러 / 0 신규 경고.
2. Inspector 변화 없음 — NV는 표시 안 됨, public field 없음.
3. Boss 인스턴스 씬에 없음 → 런타임 무영향.
4. X4-5c+에서 spawn 활성화 + 데미지 받으면 BossPhaseThresholds cross 시 로그 출력 (`Phase Phase1 → Phase2 (HP 65.0%)` 등).

## Risks (Acknowledged)

- **`BossPhaseThresholds` 디자이너 채워야 phase 전환 발생**: null/Length=0이면 early return → Phase1 유지. 디자이너 X4-5b에서 `[0.7, 0.4, 0.1]` 권장 채움.
- **OnPhaseChanged behavior 없음**: 의도. X4-7 FSM 라운드에서 wiring.
- **4번째 이상 threshold cap**: 의도. enum 확장이 더 필요하면 NetworkConstants.cs BossPhase에 Phase4/Phase5 추가 + `_thresholdPhases` 확장. 현재 4 combat phase로 충분.

## Spawned Follow-up

- **X4-7 NEXT** (X4-5c 활성화 후 권장): SkillManager auto-cast 활성화 + boss SkillRegistry subset + phase별 skill 풀 / auto-cast 속도 조정. OnPhaseChanged hookup.
- **X4-8**: ML-Agents inference 통합.

## Parallel Workstream

X4-6은 X3 smoke + X4-5a (BossManager) + X4-5b (designer) 모든 흐름과 **파일 충돌 0**. X4-5c가 BossNetworkController3D를 추가로 건드릴 가능성 있으나 spawn 활성화는 Awake/OnNetworkSpawn 기존 로직 활용이라 phase 코드와 충돌 없음.
