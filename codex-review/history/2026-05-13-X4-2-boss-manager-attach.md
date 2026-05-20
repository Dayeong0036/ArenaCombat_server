# X4-2: BossNetworkController3D 4 Manager Attach + `_bossStatsSO` SerializeField — 2026-05-13

**Status**: APPLIED. Codex Round 1 APPROVED WITH CHANGES (1 critical + 4 suggestion, all applied).

## Scope

1 파일 EDIT (`BossNetworkController3D.cs`). PNC3D X3-2 패턴 미러 + Boss-specific dormant 보강.

## Codex Critical Applied

- **C-1 `SkillManager.SetAutoCast(false)` Awake 호출**: StatManager Initialize 전에도 `_isAlive=true` 기본값 + `SkillManager.Update`는 `StatManager.IsAlive` 참조. slot이 실수로 채워진 테스트 오브젝트에서 self skill 실행 가능성 차단. X4-3/4에서 실제 boss tick 붙일 때 명시적 재활성화 (Awake 마지막 줄 + 주석).

## Codex Suggestions Applied

- **S-1 `_bossStatsSO` X4-2 시점에 추가 OK**: X4-1 dead field 우려는 X4-3 Initialize 준비 라운드라는 컨텍스트에서 해소. Inspector slot 미리 열어둠.
- **S-2 `TryGetComponent` 방식 유지**: RequireComponent에도 prefab/scene migration 중 null 방어. PNC3D 패턴과 일치.
- **S-3 using 추가**: `ArenaCombat.Core.Stats` + `ArenaCombat.Core.State` 추가. Skill / Combat은 이미 X4-1에서 포함.
- **S-4 헤더 갱신**: "X4-2 attaches managers and binds owner. StatManager.Initialize and live ICombatant routing remain X4-3."

## Edits

`BossNetworkController3D.cs`:
- 헤더: X4-1 SHELL_ONLY → X4-2 SCOPE 갱신 + SetAutoCast 의도 설명
- `using ArenaCombat.Core.State;` + `using ArenaCombat.Core.Stats;` 추가
- `[RequireComponent(typeof(StatManager))]` + `[RequireComponent(typeof(StateManager))]` + `[RequireComponent(typeof(SkillExecutor))]` + `[RequireComponent(typeof(SkillManager))]` 추가
- `[Header("=== Stat Authority Placeholder (X4-3 will wire Initialize) ===")] [SerializeField] private BossStatsSO _bossStatsSO;` 추가
- `private StatManager _statMgr;` 캐시 필드 추가
- `Awake()` 메서드 추가: StatManager.BindOwner / StateManager.BindOwner / **SkillManager.SetAutoCast(false)** (Codex C-1)
- WarnX4Stub 메시지 문구 일반화 ("X4-1 shell stub" → "X4 shell stub", "X4-2+ wiring lands" → "X4-3+ ICombatant routing lands")

## Surface Verification

- `[RequireComponent` × 5 (NetworkObject + 4 manager) ✓
- `BindOwner` 2회 (StatManager + StateManager) ✓
- `SetAutoCast(false)` 1회 ✓
- `_bossStatsSO` SerializeField 1개 + `_statMgr` private cache 1개 ✓
- `using ArenaCombat.Core.Stats;` ✓ / `using ArenaCombat.Core.State;` ✓
- 23 ICombatant explicit impl 변경 없음 (X4-3 대상)

## Why no Boss prefab in this round (confirmed by Codex)

- `Glob Assets/**/Boss*.prefab` → 2 hits 모두 `QuarterView 3D Action BE5/Prefabs/` (서드파티 템플릿, ArenaCombat 시스템과 무관).
- ArenaCombat 네임스페이스 내 Boss 프리팹 부재 → X4-2 마이그레이션 대상 없음.
- Codex 동의: "지금은 ArenaCombat 소유 boss prefab이 없고, spawn path/NetworkPrefabs/scene wiring이 같이 없으면 prefab만 먼저 만드는 게 검증 가능성을 높이지 않습니다."

## Verification (post-apply, expected)

1. Unity recompile <3s. 0 신규 에러 / 0 신규 경고.
2. `Add Component > BossNetworkController3D` → StatManager / StateManager / SkillExecutor / SkillManager 4개 자동 부착. Inspector에 4 슬롯 + BossStatsSO 슬롯 표시.
3. Boss 인스턴스 씬에 없음 → 런타임 무영향. ICombatant impl 여전히 inert defaults.

## Risks (Acknowledged)

- **`_bossStatsSO` 미할당 통과**: 의도. X4-3 InitializeStatManager에서 null 가드 (`baseStats != null` 분기).
- **Boss 프리팹 없음으로 검증 표면 부족**: 의도. X4-5 spawn path 라운드에서 prefab + NetworkPrefabs 등록 한 번에.
- **dormant 계약 의존**: SetAutoCast(false)로 1차 차단 + 23 ICombatant impl이 inert defaults 반환으로 2차 차단. 이중 안전망.

## Spawned Follow-up

- **X4-3 NEXT**: Stat authority swap (PNC3D X3-3 미러). `InitializeStatManager()` 헬퍼 + OnNetworkSpawn(서버) 호출 + `_lastAttackerId` 캐리 + ICombatant 11 mutation/query StatManager 실제 라우팅. `IsAlive` / `CurrentHPPercent` / `MaxHP` / `Shield` / `IsCasting` inert defaults을 StatManager forward로 교체. **`SetAutoCast(true)` 복귀는 X4-3 NOT yet — X4-4 FSM 부착 시점이 안전** (살아있지만 행동 패턴 없는 보스 회피).
