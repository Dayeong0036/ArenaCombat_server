# X1-6c-3: NetworkPrefabs 등록 (자동 적용 확인) — 2026-05-14

**Status**: VERIFIED. Unity Editor 자동 적용. 코드 0. 사용자 작업 0.

## Discovery

X1-6c-3 plan 시작 전 SampleScene의 NetworkManager.NetworkConfig 점검 → `Assets/DefaultNetworkPrefabs.asset` (GUID `2497bd5b18a120347824404cd4ef9ea6`)이 NetworkPrefabsLists로 참조됨을 확인.

해당 .asset 파일 read 시점에 **이미 SkillProjectile + SkillArea 둘 다 등록되어 있음**. Unity Editor가 NetworkBehaviour 컴포넌트가 포함된 prefab을 발견하면 DefaultNetworkPrefabs SO에 자동 추가하는 패턴 (NGO 2.x default 동작).

## NetworkPrefabsList 현재 상태

`Assets/DefaultNetworkPrefabs.asset` List 내용:

```yaml
List:
- Override: 0
  Prefab: {fileID: 4927185194770037111, guid: 97a2ca8133bf54549af9e1f387bd1b90, type: 3}  # Player A.prefab
- Override: 0
  Prefab: {fileID: 382855959541236958, guid: f0be393caba23b04ea7327db2bffe23a, type: 3}   # 기존 (Boss?)
- Override: 0
  Prefab: {fileID: 7000001, guid: 5ca17d1eb6e3c2c4dab1a3e2e20bef9e, type: 3}              # SkillProjectile (X1-6c-1)
- Override: 0
  Prefab: {fileID: 8000001, guid: 1b8d63f802d441f4ab351f9d972e40ee, type: 3}              # SkillArea (X1-6c-1)
```

Prefab 참조 형식: GameObject root fileID + 자산 GUID + type 3.
- SkillProjectile root GameObject = fileID 7000001 (X1-6c-1 작성)
- SkillArea root GameObject = fileID 8000001 (X1-6c-1 작성)

## 영향

NGO 2.x runtime spawn flow 활성화:
- `ProjectilePool.Get()` → `NetworkObject.Spawn()` 호출 가능 (X3-5b 코드 동작)
- `PersistentAreaPool.Get()` → 동일
- 등록 안 됐을 때: `[Netcode] Network Prefab Hash mismatch` 에러로 실패

## Verification (검증 완료)

1. Asset DB 인식 — DefaultNetworkPrefabs.asset 정상
2. List에 2 Skill prefab entry 존재 (X1-6c-1 prefab 작성 후 Unity 자동 발견)
3. 콘솔 0 에러 / 0 경고 (X1-6c-2 시점 검증)

## X1-6c COMPLETE ✅

4단계 모두 완료:
- X1-6c-1: SkillProjectile + SkillArea prefab YAML 직접 작성
- X1-6c-2: 3DScene Pool/Manager 3 GameObject + 슬롯 wiring
- X1-6c-3: NetworkPrefabs List 자동 등록 확인
- X1-6c-N: Visual mesh polish (선택, smoke test 결과 따라)

**X3 smoke test preflight 1~6 모두 unblock**:
1. ✅ PlayerStatsSO assigned (X1-6a)
2. ✅ CardManager.allCards 채워짐 (X1-6b-4b)
3. ✅ AbilityCard.skillDefinition 유효 (X1-6b-4a)
4. ✅ SkillBinder.BindAll runs (X1-6a SkillRegistry + X1-6b-3 12 GUID)
5. ✅ NetworkPrefabs 등록 (X1-6c-3)
6. ✅ Pool 매니저 3DScene 배치 (X1-6c-2)

## 다음 단계 (사용자 검증 가능)

**X3 smoke test 5 verification 시도 가능**:
1. 새 컴파일 에러 0
2. CardManager 이벤트 sub/unsub no NRE
3. Draft 중 PNC3D + auto-cast 차단
4. CardSelectionResolved → 양쪽 클라이언트 slot 일치
5. Pool Spawn → Despawn(false) → re-Spawn 사이클 정상

Play 모드 host + 2P 진행. 결과에 따라:
- 통과: PHASE X3 COMPLETE 선언, X4-5c (boss spawn 활성화) 진행 가능
- 실패: X3-7.1 / X1-6c-N (Visual mesh 등) 패치

## Parallel Workstream

X4 Boss / X1-6b-4c (선택) 라운드와 파일 0겹침 유지.
