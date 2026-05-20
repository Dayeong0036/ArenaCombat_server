# X4-4: BossNetworkController3D Position Control Routing — 2026-05-13

**Status**: APPLIED. Codex Round 1 APPROVED WITH CHANGES (1 critical + 5 suggestion, all applied).

## Scope

1 파일 EDIT (`BossNetworkController3D.cs`). PNC3D X3-4 직접 미러. FSM은 X4-5로 분리.

## Codex Critical Applied

- **C-1 `networkPosition` 명시 NV는 server write + client apply 한 세트**: NetworkVariable이 Transform을 자동 이동시키지 않음. `OnNetworkSpawn` 비서버 분기에서 `transform.position = networkPosition.Value` snap + `networkPosition.OnValueChanged += HandlePositionChanged` 구독. `OnNetworkDespawn` 비서버 분기 unsubscribe. `HandlePositionChanged(old, new)` → `transform.position = new` immediate snap. 보간은 X4-N polish.

## Codex Suggestions Applied

- **S-1 NetworkTransform 대신 명시 NV OK**: PNC3D와 동일 권위 패턴 일관성 유지. 보스 FSM 부재 시점에 과한 컴포넌트 의존 회피.
- **S-2 FixedUpdate에서 networkPosition 매-tick 갱신 안 함**: FSM movement 없어 매-tick sync 의미 없음. `ApplyPositionOffset` 호출 시점에 즉시 NV mirror로 충분. 후속 FSM 라운드에서 중앙 movement helper 통해 위치 쓰는 게 더 명확.
- **S-3 threshold 비교 → 미적용**: FixedUpdate에서 networkPosition 갱신 안 함으로 결정 (S-2 채택) → threshold 비교 불필요.
- **S-4 `WarnX4Stub` 제거 + `_x4StubWarned` 리네임**: X4-4 후 position stub 호출자 없음 → WarnX4Stub helper 완전 제거. HashSet 이름 `_x4StubWarned` → `_warnedOnce` 변경 (OnBossDefeated 로그용 1회 사용 유지).
- **S-5 Knockback / Pull / MoveBy IsServer + alive + `_rb` 가드 유지**: 3 메서드 모두 `if (!IsServer || !networkIsAlive.Value || _rb == null) return;`. 죽은 보스 위치 제어 받지 않음.
- **Rigidbody / Collider RequireComponent + Awake config 추가**: PNC3D 패턴 (useGravity=false / FreezeRotationX,Z / Interpolate). isKinematic 정책은 X4-5 FSM movement 라운드 결정.

## Edits

- 헤더: X4-3 SCOPE → X4-4 SCOPE 갱신. 5중 dormant 안전망 + Codex X4-4 C-1 client apply 의무 명시.
- RequireComponent 2개 추가 (Rigidbody / Collider).
- `_rb` private cache 추가 + Awake config (useGravity / constraints / interpolation).
- `_lastValidatedServerPosition` 필드 추가.
- `networkPosition` NetworkVariable 추가 (server-write everyone-read, default zero).
- `OnNetworkSpawn` 서버 분기: `_lastValidatedServerPosition` + `networkPosition` prime + Initialize 호출 유지.
- `OnNetworkSpawn` 비서버 분기: snap + subscribe (Codex C-1).
- `OnNetworkDespawn` override 추가: 비서버 분기 unsubscribe.
- `HandlePositionChanged(old, new)` 추가: immediate snap.
- `ApplyPositionOffset(direction, distance)` private helper 추가 (PNC3D X3-4 그대로).
- 3 stub 교체 → `ApplyPositionOffset` 호출 (3중 가드).
- `WarnX4Stub` helper 제거; `_x4StubWarned` → `_warnedOnce` 리네임.
- FixedUpdate 변경 없음 (HP/alive sync만 유지; networkPosition 갱신 추가 안 함, Codex S-2).
- 헤더 SetAutoCast(false) 의도 코멘트 "X4-4 keeps dormant; X4-5 FSM enables" 갱신.

## Surface Verification

- `RequireComponent` × 7 ([NetworkObject, Rigidbody, Collider, StatManager, StateManager, SkillExecutor, SkillManager]) ✓
- `networkPosition` NetworkVariable + `HandlePositionChanged` + `OnNetworkDespawn` unsubscribe ✓
- `ApplyPositionOffset` 1개 helper + 호출 3회 (Knockback / Pull / MoveBy) ✓
- 3 position-control mutation: `IsServer && networkIsAlive.Value && _rb != null` 가드 ✓
- `WarnX4Stub` 0 hits (제거됨) ✓
- `_warnedOnce.Add("OnBossDefeated")` 1회 (X4-3 로그 유지) ✓
- `Rigidbody.MovePosition` 1회 (helper 내) ✓

## Dormant 계약 유지 (X4-4 시점)

1. NetworkPrefab 미등록 — 런타임 인스턴스 부재.
2. InitializeStatManager BossStatsSO null guard — 미할당 시 inert.
3. NV default 0/false/zero — 서버 prime 시만 live.
4. 모든 mutation IsServer + alive 가드 + position은 추가 `_rb` 가드.
5. `SetAutoCast(false)` 유지 — auto-cast 차단.

X4-5 라운드에서 prefab 생성 + NetworkPrefabs 등록 + FSM + SetAutoCast(true)으로 한 번에 dormant 해제.

## Verification (post-apply, expected)

1. Unity recompile <5s. 0 신규 에러 / 0 신규 경고.
2. `Add Component > BossNetworkController3D` 시 Rigidbody + Collider + 4매니저(X4-2) 자동 부착. Inspector에 BossStatsSO 슬롯 + Rigidbody 설정 노출.
3. Boss 인스턴스 씬에 없음 → 런타임 무영향.

## Risks (Acknowledged)

- **클라이언트 보간 없음 (immediate snap)**: 의도. boss는 능동 움직임 0 (FSM 없음) → 시각적 jitter 없음. FSM 도입 후 visual smoothness 필요 시 X4-N polish.
- **`isKinematic` 결정 미정**: X4-5 FSM movement 방식 (rb.MovePosition vs CharacterController vs NavMeshAgent)에 따라 결정. 현재 Awake에서 isKinematic 설정 안 함 → Unity 기본값 (false).
- **Rigidbody 추가가 prefab 미존재 시 영향 없음**: prefab 없어 미영향. X4-5 prefab 생성 시 RequireComponent로 자동 첨부.

## Spawned Follow-up

- **X4-5 NEXT**: BossManager + Boss 프리팹 생성 + NetworkPrefabs 등록 + spawn path 활성화 + Buildup BossController FSM 포팅 + SetAutoCast(true) + match-end broadcast. **이 라운드부터 dormant 계약 해제** — runtime smoke 필요.
- **X4-N polish**: HandlePositionChanged smooth interpolation, MoveType branching (Dash/Charge/Jump/Rope), Pull duration coroutine.
