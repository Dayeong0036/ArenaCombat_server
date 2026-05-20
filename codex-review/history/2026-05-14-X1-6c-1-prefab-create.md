# X1-6c-1: SkillProjectile + SkillArea 프리팹 생성 — 2026-05-14

**Status**: APPLIED. Codex APPROVED WITH CHANGES via MCP. YAML-direct 채택 (MCP create_prefab 한계).

## Scope

2 NEW `.prefab` + 1 폴더 `.meta`. 코드 0. 사용자 작업 0.

## Codex Critical Applied

- **C-1 RequireComponent 구체 타입 미보장**: MCP create_prefab이 `RequireComponent(Collider)` 만족시 어떤 collider 타입 (Box/Sphere/Mesh) 추가될지 불명. YAML-direct로 SphereCollider 명시.
- **C-2 isKinematic=false 필요**: SkillProjectile.cs Launch()가 `_rb.linearVelocity` 사용 (MovePosition 아님). PNC3D X4-4 패턴 (kinematic) 미러 부적절. Rigidbody.isKinematic=0 명시.
- **C-3 create_prefab 한계**: signature에 path/multi-component 옵션 없음 → YAML-direct가 deterministic.

## Codex Suggestions Applied

- 위치 `Assets/ArenaCombat/Resources/Skills/Prefabs/` 확정
- Rigidbody: useGravity=0, isKinematic=0, interpolation=1 (Interpolate), constraints=80 (FreezeRotation X+Z, decimal 80 = 64+16)
- SphereCollider: isTrigger=1, radius=0.5 (SkillProjectile._detectionRadius=0.5 일치)
- SkillArea visual mesh 부재 OK — `SkillArea._renderer` null 안전 처리 확인 (Codex 검증)

## Files

| Op | Path | Notes |
|---|---|---|
| NEW (folder) | `Assets/ArenaCombat/Resources/Skills/Prefabs/` | folder .meta GUID `16135b039f86493c9781c1942ab3f3cb` |
| NEW | `Assets/ArenaCombat/Resources/Skills/Prefabs/SkillProjectile.prefab` | 5 components: Transform / Rigidbody / SphereCollider / NetworkObject / SkillProjectile |
| Auto | `SkillProjectile.prefab.meta` | Unity auto-generated GUID `5ca17d1eb6e3c2c4dab1a3e2e20bef9e` |
| NEW | `Assets/ArenaCombat/Resources/Skills/Prefabs/SkillArea.prefab` | 3 components: Transform / NetworkObject / SkillArea |
| Auto | `SkillArea.prefab.meta` | Unity auto-generated (next focus) |

## Component Detail (SkillProjectile.prefab)

```yaml
GameObject (m_Name: SkillProjectile)
├─ Transform (default identity)
├─ Rigidbody:
│   m_UseGravity: 0
│   m_IsKinematic: 0
│   m_Interpolate: 1   # Interpolate
│   m_Constraints: 80  # FreezeRotationX + FreezeRotationZ (16 + 64)
├─ SphereCollider:
│   m_IsTrigger: 1
│   m_Radius: 0.5
├─ NetworkObject (script GUID d5a57f76...) — NGO defaults
└─ SkillProjectile (script GUID d5c6fbf5...):
    _color: {r:1, g:0, b:0, a:1}
    _detectionRadius: 0.5
    _targetMask: m_Bits 4294967295 (all layers)
```

## Component Detail (SkillArea.prefab)

```yaml
GameObject (m_Name: SkillArea)
├─ Transform (default identity)
├─ NetworkObject (script GUID d5a57f76...) — NGO defaults
└─ SkillArea (script GUID a01c7231...):
    _areaColor: {r:1, g:0.2, b:0.2, a:0.35}  # semi-transparent red
```

## Verification (post-apply, expected)

1. Unity Asset DB refresh — 2 prefab 인식, 0 import error
2. SkillProjectile.prefab 클릭 → Inspector에 5개 컴포넌트 표시. Rigidbody constraints에 FreezeRotation X+Z 체크
3. SkillArea.prefab → 3개 컴포넌트
4. Console: 0 신규 에러/경고 (확인됨 — 콘솔 클린)

## Risks (Acknowledged)

1. **NetworkObject GlobalObjectIdHash=0**: Unity가 reimport 시 자동 hash 부여. 일부 NGO 환경에서 0이면 경고 발생 가능 — Inspector에서 한 번 열어두면 자동 갱신.
2. **Visual mesh 부재**: SkillArea / SkillProjectile 게임 내 비가시. 디버그 SkillRangeDisplay (X2-9)가 별도 표시. Polish round에서 추가 가능.
3. **Layer 0 (Default)**: Projectile/Area target detection이 모든 layer 검출. 추후 Layer 분리 권장 (Projectile / Area 각각).

## Spawned Follow-up

- **X1-6c-2 NEXT**: 3DScene에 ProjectilePool / PersistentAreaPool / PersistentAreaManager 3 GameObject + 프리팹 슬롯 할당
- **X1-6c-3**: NetworkManager.NetworkConfig.NetworkPrefabs 2 prefab 등록 (SampleScene)
- **X1-6c-N**: Visual mesh / Layer 분리 polish

## Parallel Workstream

X3 / X4 라운드와 파일 0겹침 유지.
