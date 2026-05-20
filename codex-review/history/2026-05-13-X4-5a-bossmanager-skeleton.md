# X4-5a: BossManager Scene-Local Singleton Skeleton — 2026-05-13

**Status**: APPLIED. Codex Round 1 APPROVED WITH CHANGES (1 critical + 5 suggestion, all applied).

## Scope

1 NEW 파일 + 1 NEW .meta. PlayerSpawnManager 패턴 미러 셸 (단일 보스용 단순화) + Codex C-1 scene-local 결정.

## Files

- NEW `Assets/ArenaCombat/Scripts/Core/Network/BossManager.cs` (~80 LOC)
- NEW `Assets/ArenaCombat/Scripts/Core/Network/BossManager.cs.meta` (GUID `53aebc4c1c6948619d3a1b4eabd684f0`)

## Codex Critical Applied

- **C-1 Scene-local (DDOL 제거)**: `DontDestroyOnLoad` 사용 안 함. 이유: serialized scene Transform `_bossSpawnPoint`와 DDOL 조합이 씬 전환 후 dangling reference. Boss arena 씬에 종속되는 manager가 자연스러움. PlayerSpawnManager는 lobby↔game 전환 + client spawn orchestration 때문에 DDOL이 맞지만 BossManager는 그런 역할 없음. 향후 라이프사이클이 씬 너머로 확장되면 name/tag-based spawn-point resolver로 전환 (PlayerSpawnManager 패턴).

## Codex Suggestions Applied

- **S-1 Scene-local 자연스러움**: C-1 결정과 동일 — Boss arena에 종속.
- **S-2 `[DisallowMultipleComponent]`**: 추가.
- **S-3 TrySpawnBoss stub server guard 불필요 (지금은)**: stub은 false만 반환 → server guard 무의미. X4-5c 실제 구현 시 `NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer` 가드 명시. 파일 헤더 deferred list에 함께 명시:
  - GameManager.Instance.RegisterBoss(bossGameObject) on spawn success
  - despawn/death 시 unregister
  - SkillManager.FindNearestTarget가 보스 인식 — auto-cast smoke 의미 부여
- **S-4 `CurrentBoss`는 NetworkObject 타입**: 채택. `CurrentBossController` convenience는 caller 필요 시 추가.
- **S-5 .meta fresh GUID**: `53aebc4c1c6948619d3a1b4eabd684f0`로 수동 생성.

## Surface Verification (intent)

- `public class BossManager : MonoBehaviour` ✓
- `[DisallowMultipleComponent]` ✓
- `public static BossManager Instance { get; private set; }` ✓
- `[SerializeField] GameObject _bossPrefab` + `[SerializeField] Transform _bossSpawnPoint` ✓
- `private NetworkObject _spawnedBoss` + `public NetworkObject CurrentBoss => _spawnedBoss` ✓
- `Awake`: Instance singleton + duplicate Destroy (no DDOL) ✓
- `OnDestroy`: `if (Instance == this) Instance = null` ✓
- `TrySpawnBoss()`: warn-once + return false ✓
- `_warnedOnce` static HashSet (BossNetworkController3D 패턴 미러) ✓
- 호출자 부재 — 본 라운드 zero behavioral surface ✓

## Verification (post-apply, expected)

1. Unity recompile <3s. 0 신규 에러 / 0 신규 경고.
2. `Add Component > BossManager` Inspector 노출 — `_bossPrefab` / `_bossSpawnPoint` SerializeField 슬롯 보임.
3. 씬에 BossManager 인스턴스 없음 → 런타임 무영향.

## Risks (Acknowledged)

- **`_bossSpawnPoint` null 가능**: X4-5b designer 채움 의도. null이면 X4-5c spawn 시 Vector3.zero fallback.
- **TrySpawnBoss 호출자 부재**: 의도. X4-5c에서 GSM 매치 phase 진입 트리거 wiring.
- **Scene-local lifecycle**: 씬 전환 시 BossManager 인스턴스 destroyed. 다음 씬에서 다시 배치 필요. 현재 단일 boss arena 가정 — 향후 멀티 chapter 시 정책 재검토.

## Spawned Follow-up

- **X4-5b NEXT (designer)**: Boss 프리팹 생성 + NetworkPrefabs 등록 + 씬 배치 + 슬롯 할당.
- **X4-5c**: TrySpawnBoss 실제 구현 + GSM 트리거 + GameManager.RegisterBoss/Unregister + SetAutoCast(true) + match-end broadcast. **첫 dormant 해제 + runtime smoke 필수**.
- **X4-6**: Buildup BossController FSM 포팅.
- **X4-7**: ML-Agents inference.

## Parallel Workstream

X4-5a는 X3 smoke와 **여전히** 파일 0겹침 — BossManager.cs 단독 신규 파일, 기존 PNC3D / SkillManager / CardManager / pool 변경 없음.
