# X1-6a: Stat SO `.asset` Import (X3 Smoke Preflight Unblocker) — 2026-05-13

**Status**: APPLIED. Codex Round 1 APPROVED WITH CHANGES (2 critical + 5 suggestion, all applied). 코드 변경 0.

## Scope

3 stat .asset (Buildup → ArenaCombat 그대로) + 1 빈 SkillRegistry .asset (신규 작성). 데이터 레이어만, 코드 0.

## Files (NEW × 8)

| Op | Path | Source |
|---|---|---|
| NEW | `Assets/ArenaCombat/Resources/Stats/PlayerStatsSO.asset` | Buildup `Player&Boss/PlayerStatsSO.asset` 그대로 |
| NEW | `Assets/ArenaCombat/Resources/Stats/PlayerStatsSO.asset.meta` | Buildup .meta GUID `9afb3a66c034805419c3a05a4e0e4a8e` 보존 |
| NEW | `Assets/ArenaCombat/Resources/Stats/BossStatsSO.asset` | Buildup `Player&Boss/BossStatsSO.asset` 그대로 |
| NEW | `Assets/ArenaCombat/Resources/Stats/BossStatsSO.asset.meta` | Buildup GUID `d4dd746456a03144a8eff350dc2b343c` 보존 |
| NEW | `Assets/ArenaCombat/Resources/Stats/BaseStatsSO.asset` | Buildup `Player&Boss/BaseStatsSO.asset` 그대로 |
| NEW | `Assets/ArenaCombat/Resources/Stats/BaseStatsSO.asset.meta` | Buildup GUID `5ca4237275f459a43a9c3b31ec22362d` 보존 |
| NEW | `Assets/ArenaCombat/Resources/Skills/SkillRegistry.asset` | 신규 (m_Script `f315d276…`, `_pool: []`) |
| NEW | `Assets/ArenaCombat/Resources/Skills/SkillRegistry.asset.meta` | fresh GUID `de01f1def5374c5d9d40acc2978792e5` |

코드 변경 0. 재컴파일 불필요 — Asset DB refresh만.

## Codex Critical Applied

- **C-1 검증 범위 정확화**: 본 라운드는 "preflight null-registry 제거 + PlayerStatsSO 슬롯 채움"만 unblock. **full skill behavior** (AbilityCard.skillDefinition resolve, slot binding, auto-cast, projectile spawn)는 여전히 X1-6b/c 후. ROADMAP X1-6a entry에 명시.
- **C-2 SkillRegistry.asset YAML 정확성**: m_Script GUID `f315d2762f429a74caa988195f0b0534` (SkillRegistry.cs ArenaCombat 측) + `_pool: []` 직접 검증. Buildup SkillRegistry.asset 헤더 패턴 동일. **수동 작성** 후 사용자 Asset DB refresh 시 "Missing Script" 발생 안 하는지 확인 필요.

## Codex Suggestions Applied

- **S-1 Stat .asset Buildup .meta 보존 OK**: script GUID 양 프로젝트 일치 검증됨. asset GUID도 보존 → scene/prefab/SO 참조 안정성.
- **S-2 BaseStatsSO.asset 포함 OK**: 직접 사용 X — 비용 작음. Buildup 참조 보존 안전.
- **S-3 Resources/Stats / Resources/Skills 경로 OK**: AbilityCard convention과 매치. 장기적으로 Addressables 검토.
- **S-4 BossPhaseThresholds 빈 배열**: 본 라운드 영향 없음 (X3 smoke = player만). **X4-5b designer task 명시 권장** — Boss smoke 전 [0.7, 0.4, 0.1] 같은 값 채워야 X4-6 phase tracking 활성화.
- **S-5 destination pre-flight**: `Glob Resources/Stats/*` + `Resources/Skills/*` 모두 hit 0 확인. 덮어쓰기 0.

## Verification (post-apply, expected)

1. Unity Asset DB refresh (자동) — 신규 8개 파일 (4 .asset + 4 .meta) 인식. 0 import error / 0 "Missing Script" warning.
2. **재컴파일 불필요** (코드 변경 0).
3. Project window:
   - `Assets/ArenaCombat/Resources/Stats/` 폴더 신규 → PlayerStatsSO / BossStatsSO / BaseStatsSO 3개 표시
   - `Assets/ArenaCombat/Resources/Skills/` 폴더 신규 → SkillRegistry 1개 표시
4. 각 .asset 클릭 시 Inspector에서 필드 정상 표시:
   - PlayerStatsSO: MaxHP=200, BaseDamage=10, BaseDefense=5, MoveSpeed=100, ParryWindow=0.3, ShieldMax=50 등
   - BossStatsSO: BossMaxHP=1000, BossBaseDamage=50, BossBaseDefense=20, BossPhaseThresholds=빈 배열
   - BaseStatsSO: 17개 multiplier 1.0 default
   - SkillRegistry: `_pool` 빈 List

## 사용자가 본 라운드 후 해야 할 작업 (Inspector 할당)

본 라운드 적용 직후 Unity 자동 Asset DB refresh되면 다음 단계 진행 가능:

```
1. Project window → Assets/ArenaCombat/Resources/Stats/PlayerStatsSO 클릭
   → Inspector에서 MaxHP=200 등 default 값 정상 표시 확인 (Missing Script 아님 확인)

2. Project window → Assets/ArenaCombat/3DSceneScript/Player/Player A 프리팹 클릭
   → Inspector에서 PlayerNetworkController3D 컴포넌트 펼침
   → "Player Stats SO" 라벨 슬롯에 PlayerStatsSO.asset 드래그 (또는 동그라미 클릭 → 선택)

3. SampleScene을 Hierarchy에 열고 NetworkManager 또는 GameManager 라는 이름의 오브젝트 클릭
   → Inspector에서 GameManager 컴포넌트 (있다면) 펼침
   → "Skill Registry" 라벨 슬롯에 SkillRegistry.asset 드래그

4. Ctrl+S 저장 (씬/프리팹 변경분 모두 저장)
```

이후 Play 모드 host 진입 시 `[SkillBinder] SkillRegistry is null` 경고가 사라져야 정상 (대신 0 skills bound 정도의 정상 로그가 보일 수 있음). 실제 카드 draft / skill 사용은 X1-6b/c 후.

## Risks (Acknowledged)

1. **수동 YAML SkillRegistry.asset의 미세 손상 위험**: Buildup 헤더 패턴 그대로 따라 작성 (`m_ObjectHideFlags: 0` ~ `m_EditorClassIdentifier:`). Codex C-2 권장에 따라 사용자 Asset DB refresh 시 Missing Script 모니터링 필수.
2. **`Resources/Stats` 경로 Bloat**: 장기적으로 Addressables 권장 (Codex S-3). 현재 4 파일만, 무시 가능.
3. **GameManager가 DDOL singleton인지 SampleScene component인지 사용자 확인 필요**: 사용자 작업 안내 step 3에서 두 가지 가능성 모두 제시.
4. **AbilityCard.skillDefinition null 상태**: 6개 AbilityCard는 이미 CardManager.allCards에 assigned되어 있지만 skillDefinition 필드는 미할당. 본 라운드에서 SkillRegistry가 빈 상태이므로 X1-6b 전까지 skill draft가 의미 있는 결과 못 만듦. **smoke verification 4는 "양쪽 클라이언트 null 상태 일치" 정도까지만 검증**.

## Spawned Follow-up

- **X1-6b NEXT**: Buildup 12 PlayerSkill .asset import + RoleTags string→enum 변환 + SkillRegistry._pool 등록 + AbilityCard.skillDefinition 디자이너 매핑.
- **X1-6c**: SkillProjectile / SkillArea 프리팹 생성 + NetworkPrefabs 등록 + Pool 매니저 3DScene 배치. **smoke verification 5 unblock**.

## 본 라운드 끝나면 가능한 검증 범위 (Codex C-1 명확화)

✅ 컴파일 (코드 변경 0이라 자동 통과)
✅ PlayerStatsSO Inspector 할당 후 Player A.prefab 정상 표시
✅ SkillRegistry 슬롯 할당 후 `[SkillBinder] SkillRegistry is null` 경고 제거
🚫 카드 선택 + slot binding (X1-6b 의존)
🚫 auto-cast (X1-6b 의존)
🚫 projectile/area pool lifecycle (X1-6c 의존)
🚫 end-to-end skill 시스템 (X1-6b/c 모두 후)
