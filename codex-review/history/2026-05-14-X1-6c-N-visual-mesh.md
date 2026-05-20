# X1-6c-N: SkillProjectile + SkillArea 프리팹 Visual Mesh — 2026-05-14

**Status**: APPLIED. Codex APPROVED WITH CHANGES via MCP. 코드 0.

## Scope

2 prefab EDIT — MeshFilter + MeshRenderer 추가로 smoke test 디버깅 가시성 향상.

## 변경 내용

| Prefab | 추가 컴포넌트 | Mesh | Material |
|--------|--------------|------|----------|
| SkillProjectile.prefab | MeshFilter (7000007) + MeshRenderer (7000008) | Sphere (built-in 10207) | Default-Material (10303) |
| SkillArea.prefab | MeshFilter (8000005) + MeshRenderer (8000006) | Cylinder (built-in 10206) | Default-Material (10303) |

`m_Component` 리스트에 신규 fileID 2개씩 append.

## Codex Decisions Applied

- **Cylinder vs Quad**: Cylinder 유지. SkillArea.cs:76에서 y scale = 0.02 적용해서 thin puck (납작한 디스크) 효과 → ground area처럼 표시. Quad는 vertical default라 부적합.
- **Full MeshRenderer YAML**: Unity 6000.3 표준 필드 모두 포함 (m_CastShadows / m_StaticShadowCaster / m_RayTracingMode / m_RayTraceProcedural / m_RenderingLayerMask / m_AdditionalVertexStreams 등). 최소 블록은 Unity reimport 시 normalization churn 발생.
- **Built-in mesh 검증**: Unity 6000.3.11f1 (`ProjectVersion.txt:1`)에서 Sphere=10207 / Cylinder=10206 / Default-Material=10303 / GUID `0000000000000000e000000000000000` (mesh) / `0000000000000000f000000000000000` (material) 모두 정확.
- **Root placement OK**: SkillArea의 _renderer = GetComponent<Renderer>() 패턴 — 본 라운드 root에 MeshRenderer 추가. 코드 주석에 child Visual GameObject 가정 있으나 root placement도 동작. runtime radius scaling이 NetworkObject root scale 변경하는 점은 알려진 trade-off.
- **Default-Material opaque**: SkillArea._areaColor.a=0.35 transparency 적용 안 됨 (Default-Material은 opaque shader). 색상은 빨강 표시되지만 반투명은 X. 향후 transparent material 필요 시 별도 라운드.

## Files

| Op | Path | LOC delta |
|---|---|---|
| EDIT | `Assets/ArenaCombat/Resources/Skills/Prefabs/SkillProjectile.prefab` | +50 (MeshFilter + MeshRenderer 블록 + m_Component 2 entries) |
| EDIT | `Assets/ArenaCombat/Resources/Skills/Prefabs/SkillArea.prefab` | +50 |

## Verification

1. Unity Asset DB refresh 자동
2. 콘솔 0 신규 에러/경고 확인됨
3. Inspector에서 두 prefab 모두 MeshFilter + MeshRenderer 추가 표시 (사용자 확인 가능)

## Risks (Acknowledged)

1. **Default-Material opaque**: SkillArea 반투명 표현 안 됨. 시각적 디버그에는 무영향 (빨강 cylinder 보임).
2. **Root MeshRenderer + radius scale**: NetworkObject root scale 변경 → spawn 시 NV 동기화 영향 가능성. PNC3D X4-4 패턴과 다름. 후속 polish round에서 child Visual GameObject로 분리 검토.
3. **SortingLayerID/Order=0**: 2D 게임 아니므로 무관.

## X1-6 전체 종합 (2026-05-13 ~ 14)

10 sub-cycles 모두 완료:
- X1-6a Stat .asset
- X1-6b-1 SkillRoleTag enum 9→29
- X1-6b-2 12 SkillDefinition .asset
- X1-6b-3 SkillRegistry._pool 12 GUID
- X1-6b-4a 4 AbilityCard NEW class + skillDefinition
- X1-6b-4b CardManager.allCards 6→4 재바인딩
- X1-6c-1 SkillProjectile + SkillArea prefab
- X1-6c-2 3DScene Pool/Manager 3 GameObject
- X1-6c-3 NetworkPrefabs 등록 (자동)
- X1-6c-N Visual mesh polish

**X3 smoke test 사전 조건 1~6 모두 unblock**. 사용자 Play 모드 host + 2P 진행 가능.

## Spawned Follow-up

- **X3 smoke test 5 verification 사용자 진행** — 통과 시 PHASE X3 COMPLETE 플립 → X4-5c (boss spawn 활성화)
- **X1-6b-4c (선택)** — 3 legacy 3DSceneScript/AbilityCard/ deprecation + 추가 8 AbilityCard 작성 (12 SkillDefinition 풀 활용)
- **X1-6c-N+ (선택)** — Transparent material / child Visual GameObject 분리

## Parallel Workstream

X4 Boss / X3 코드 라운드와 파일 0겹침 유지.
