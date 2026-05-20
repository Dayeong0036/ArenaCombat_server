# X1-6b-4a: 4 AbilityCard .asset NEW Class GUID 변환 + skillDefinition 매핑 — 2026-05-14

**Status**: APPLIED. Codex APPROVED (no critical) via MCP.

## Discovery

`Assets/ArenaCombat/`에 AbilityCard 자산 3 종류 발견 (X1-6b-4 시작 시 audit):

| 위치 | 개수 | Script GUID | 상태 |
|------|------|-------------|------|
| `Resources/AbilityCard/` | 4 | `0d939ffd9fc6bb24a83fb5e14cbe1309` | **MISSING** (해당 .cs 0개) |
| `3DSceneScript/AbilityCard/` | 3 | `c923b417bab39734a800c06e1a29db54` | LEGACY (3DSceneScript/Scripts/AbilityCard.cs, 3 fields) |
| **NEW (X2-12)** | 0 | `0606cc0234635b741b1ff737732ccb7c` | `Core.Card.AbilityCard` (5 fields incl. skillDefinition) |

CardManager.cs:32 `AbilityCard[] allCards`는 NEW class 기대 → 본 라운드가 4 Resources/ 자산을 NEW class로 변환.

## Scope

4 파일 EDIT. 코드 0.

## Files

| Op | Path | 변경 |
|---|---|---|
| EDIT | `Resources/AbilityCard/AbilityCard.asset` | m_Script GUID + m_EditorClassIdentifier + skillDefinition + skillObjectName |
| EDIT | `Resources/AbilityCard/AbilityCard 1.asset` | (동일) |
| EDIT | `Resources/AbilityCard/AbilityCard 2.asset` | (동일) |
| EDIT | `Resources/AbilityCard/AbilityCard 3.asset` | (동일) |

각 파일 5 LOC 변경: m_Script GUID 교체 / m_EditorClassIdentifier 추가 / skillDefinition 필드 추가 / skillObjectName 필드 추가.

`.meta` GUID 변경 0 (자산 GUID 보존, Inspector 참조 안정성).

## 매핑 (4 of 12 SkillDefinitions)

| AbilityCard | cardName | 매핑 SkillDefinition | Primary Role |
|---|---|---|---|
| AbilityCard | 블랙매지션걸 | ExecutionSpike (`829a3958…`) | Burst (Burst+Execute+Melee) |
| AbilityCard 1 | 푸른눈의 백룡 | FortressArmor (`20d5486f…`) | Shield (Shield+Melee) |
| AbilityCard 2 | 오시리스의 천공룡 | ErosionField (`2d6e5cbb…`) | DOT (DOT+Zone+AntiHeal) |
| AbilityCard 3 | 오드아이즈레볼루션드래곤 | SurvivalPulse (`218197d6…`) | Heal (Heal+Cleanse+Survival) |

매핑 근거: 4 distinct primary role tag → draft 변동성 보장. 남은 8 SkillDefinition (BarrierBreaker / CollapseRoar / CrushingBarrage / HuntingMark / OverchargeMode / PiercingShot / RuptureMagazine / SealChain) 후속 카드 작성 시 사용.

## Codex 검증 결과 (자동 호출)

Codex가 직접 read한 검증:
- `m_EditorClassIdentifier` format `Assembly-CSharp::ArenaCombat.Core.Card.AbilityCard` 정확 — 동일 패턴이 `Player A.prefab:347`의 `SkillManager`에 존재
- 필드 순서 일치 (AbilityCard.cs:14-22): cardName / cardIcon / description / skillDefinition / skillObjectName
- Missing Script recovery 안전 — 기존 cardName/cardIcon/description은 새 클래스 동일 필드명/타입이라 보존됨
- 4 SkillDefinition GUID 모두 .meta 매치
- SkillRoleTag.cs:21 enum 매핑 모두 정확

## Verification (post-apply, expected)

1. Unity Asset DB refresh — 4 .asset reimport. **Missing Script 4 → 0**.
2. 0 신규 import error.
3. Inspector에서 4 .asset 클릭 시:
   - Script slot에 `AbilityCard` (Core.Card namespace) 표시
   - Skill Definition slot에 매핑된 SkillDefinition 표시 (예: AbilityCard.asset → ExecutionSpike)
   - cardName / cardIcon / description 그대로
4. CardManager.allCards Inspector 변화 없음 (allCards 자체는 X1-6b-4b에서 재바인딩).

## Risks (Acknowledged)

1. **CardManager.allCards 미변경**: 3DScene 안의 CardManager가 어떤 자산 가리키는지 점검 필요 (X1-6b-4b). 현재 6 entries 중 어느 것이 4 NEW Resources/ 자산인지 확인 후 정리.
2. **3DSceneScript/AbilityCard/ 3 legacy 자산은 그대로**: legacy class `c923b417…`로 컴파일됨 (3DSceneScript/Scripts/AbilityCard.cs 존재). 본 라운드 미수정. X1-6b-4b deprecation 결정.
3. **`description` 값 그대로 ("11111", "22222", "33333")**: placeholder. 게임 출시 전 실제 텍스트로 교체 필요. 본 라운드 스코프 아님.

## Spawned Follow-up

- **X1-6b-4b NEXT**: CardManager (3DScene) `allCards` 재바인딩. 씬 YAML 점검 → 6 entries 정리하여 4 NEW Resources/ 자산 가리키게.
- **X1-6c**: SkillProjectile / SkillArea 프리팹 + NetworkPrefabs + Pool 매니저 씬 배치 (smoke verification 5 unblock).
- 후속: 8 남은 SkillDefinition 위해 카드 추가 작성 (X1-6b-4c 또는 X1-6b-5).

## Parallel Workstream

X3 smoke / X4 Boss와 파일 0겹침 유지.
