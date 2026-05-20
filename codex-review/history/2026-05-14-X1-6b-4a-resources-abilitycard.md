# X1-6b-4a: 4 Resources/AbilityCard .asset Missing Script 정정 + skillDefinition 매핑 — 2026-05-14

**Status**: APPLIED. Codex APPROVED via MCP (자동 호출). 코드 0.

## Discovery (라운드 시작 시점)

3 종류 AbilityCard 자산 발견:

| 위치 | 개수 | Script GUID | 클래스 | 상태 (변경 전) |
|------|------|-------------|--------|---------------|
| `Resources/AbilityCard/` | 4 | `0d939ffd…` | **MISSING** (해당 .cs 0개) | 🚫 Missing Script |
| `3DSceneScript/AbilityCard/` | 3 | `c923b417…` | LEGACY `AbilityCard` (global ns, cardName/cardIcon/description) | ⚠️ Legacy (no skillDefinition) |
| `Scripts/Core/Card/AbilityCard.cs` (X2-12) | 0 인스턴스 | `0606cc02…` | NEW `ArenaCombat.Core.Card.AbilityCard` (5 fields incl. skillDefinition + skillObjectName) | ✅ CardManager.allCards 기대 type |

CardManager.cs:32 `public AbilityCard[] allCards` (`using ArenaCombat.Core.Card`) → NEW class 참조. 어느 .asset도 NEW class와 매치 안 됨 → 본 라운드가 4 Resources/ 자산을 NEW class로 정정.

## Scope

`Resources/AbilityCard/*.asset` 4 파일 EDIT:
1. `m_Script` GUID: `0d939ffd…` → `0606cc0234635b741b1ff737732ccb7c`
2. `m_EditorClassIdentifier` 추가: `Assembly-CSharp::ArenaCombat.Core.Card.AbilityCard`
3. `skillDefinition` 필드 추가 (4 SkillDefinition GUID 참조)
4. `skillObjectName:` 필드 추가 (legacy fallback, empty)

`3DSceneScript/AbilityCard/` 3 파일은 미수정 (legacy class용, X1-6b-4c에서 deprecation 검토).

## 4 SkillDefinition 매핑 (다양한 role 분포)

| .asset | cardName | SkillDefinition | Role 카테고리 |
|--------|----------|-----------------|---------------|
| AbilityCard | 블랙매지션걸 | **ExecutionSpike** (`829a3958…`) | Burst+Execute+Melee |
| AbilityCard 1 | 푸른눈의 백룡 | **FortressArmor** (`20d5486f…`) | Shield+Melee |
| AbilityCard 2 | 오시리스의 천공룡 | **ErosionField** (`2d6e5cbb…`) | DOT+Zone+AntiHeal |
| AbilityCard 3 | 오드아이즈레볼루션드래곤 | **SurvivalPulse** (`218197d6…`) | Heal+Cleanse+Survival |

매핑 근거: 4가지 distinct primary role tag (Burst / Shield / DOT / Heal) — draft 변동성 보장. 남은 8 SkillDefinition은 X1-6b-4b 후속 또는 추가 카드 작성 시 사용.

## Codex Suggestions Applied

- **S-1 필드 순서**: AbilityCard.cs:14 정의 순서 그대로 — cardName / cardIcon / description / skillDefinition / skillObjectName
- **S-2 skillObjectName 명시 빈 필드**: `skillObjectName:` 형식 (값 없음) — current class와 일치
- **S-3 skillDefinition 형식**: `{fileID: 11400000, guid: <SkillDefinition guid>, type: 2}` (SkillRegistry.asset 패턴 미러)
- m_EditorClassIdentifier 형식 `Assembly-CSharp::ArenaCombat.Core.Card.AbilityCard` 정확 확인됨 (m_Script GUID가 authoritative binding)
- 4 SkillDefinition GUID 모두 line-by-line 일치 검증

## Files

| Op | Path | LOC delta |
|---|---|---|
| EDIT | `Assets/ArenaCombat/Resources/AbilityCard/AbilityCard.asset` | +2 / ~1 |
| EDIT | `Assets/ArenaCombat/Resources/AbilityCard/AbilityCard 1.asset` | +2 / ~1 |
| EDIT | `Assets/ArenaCombat/Resources/AbilityCard/AbilityCard 2.asset` | +2 / ~1 |
| EDIT | `Assets/ArenaCombat/Resources/AbilityCard/AbilityCard 3.asset` | +2 / ~1 |

`.meta` GUID 변경 없음 (자산 GUID 4개 보존: `11fee131…`, `a5e093a9…`, `b10fb927…`, `4ff7c729…`).

## Verification (post-apply, expected)

1. Unity Asset DB refresh — 4 .asset reimport. **4 Missing Script 경고 → 0**.
2. Inspector에서 4 .asset 클릭:
   - Script slot: `AbilityCard` (Core.Card namespace) ✓
   - Skill Definition slot: 매핑된 SkillDefinition 표시 (ExecutionSpike 등)
   - cardName / cardIcon / description 보존
3. 0 신규 import error / 0 신규 경고.

## CardManager.allCards 후속 (X1-6b-4b 식별됨)

3DScene 안의 CardManager.allCards entries (총 6개) 모두 **3DSceneScript/ LEGACY 자산** 가리킴 (3 GUID × 2번 중복):
- `8c06a47a…` (3DSceneScript AbilityCard) × 2
- `09242b3d…` (3DSceneScript AbilityCard 1) × 2
- `b6b942f9…` (3DSceneScript AbilityCard 2) × 2

→ 4 NEW Resources/ 자산은 현재 CardManager에서 참조 안 됨. **X1-6b-4b가 allCards 재바인딩**: 6 legacy entries → 4 NEW Resources entries (allCards.Length 6 → 4, GSM RegisterCardCatalogSize도 4로 갱신).

## Risks (Acknowledged)

1. **CardManager.allCards 미변경**: 본 라운드는 .asset 자체만 정정. 씬 binding은 X1-6b-4b. 그 전까지 카드 draft는 여전히 LEGACY 자산 가리킴 → skillDefinition null 상태.
2. **3DSceneScript/AbilityCard/ 3 legacy 자산 유지**: 의도. X1-6b-4b 이후 deprecation/삭제 결정.
3. **Korean Unicode escape 보존**: Buildup escape 형식 그대로 (Unity 자동 디코드). 한글 직접 작성과 동일 결과.

## Spawned Follow-up

- **X1-6b-4b NEXT**: 3DScene.unity의 CardManager.allCards 6 entries → 4 NEW Resources/ entries로 교체. allCards.Length 6→4. GSM 카드 카탈로그 크기도 4 인식.
- **X1-6b-4c (선택)**: 3 3DSceneScript/AbilityCard/ legacy .asset 정리 (삭제 vs 유지 결정).

## Parallel Workstream

X3 smoke / X4 Boss 모든 라운드와 파일 0겹침 유지.
