# X1-6b-4b: 3DScene CardManager.allCards 재바인딩 (LEGACY → NEW Resources/) — 2026-05-14

**Status**: APPLIED. Codex APPROVED via MCP (no critical, 3 suggestion all applied).

## Scope

1 파일 EDIT (`Assets/Scenes/3DScene.unity:573-579`). 코드 0.

## 변경

```diff
  allCards:
- - {fileID: 11400000, guid: 8c06a47a4620f6c42aafb3f95378a483, type: 2}  # LEGACY 3DSceneScript AbilityCard
- - {fileID: 11400000, guid: 09242b3d60dc9ff42b655d1e87ec969b, type: 2}  # LEGACY 3DSceneScript AbilityCard 1
- - {fileID: 11400000, guid: b6b942f9c64a17548b3ba34a0430bacf, type: 2}  # LEGACY 3DSceneScript AbilityCard 2
- - {fileID: 11400000, guid: 8c06a47a4620f6c42aafb3f95378a483, type: 2}  # 중복
- - {fileID: 11400000, guid: 09242b3d60dc9ff42b655d1e87ec969b, type: 2}  # 중복
- - {fileID: 11400000, guid: b6b942f9c64a17548b3ba34a0430bacf, type: 2}  # 중복
+ - {fileID: 11400000, guid: 11fee131cc46bb948876baf7b7f2d738, type: 2}  # NEW Resources AbilityCard (Burst-ExecutionSpike)
+ - {fileID: 11400000, guid: a5e093a9d8abe6e4c94e50add5a15286, type: 2}  # NEW Resources AbilityCard 1 (Shield-FortressArmor)
+ - {fileID: 11400000, guid: b10fb9279fa77454d8666ebfe192e1d7, type: 2}  # NEW Resources AbilityCard 2 (DOT-ErosionField)
+ - {fileID: 11400000, guid: 4ff7c729870b13f43b9684d5c0d5b405, type: 2}  # NEW Resources AbilityCard 3 (Heal-SurvivalPulse)
```

(파일에는 인라인 코멘트 없음 — Codex S-2 미러 X1-6b-3 패턴)

allCards.Length: 6 → 4. CardManager Start에서 `GameStateManager.Instance.RegisterCardCatalogSize(4)` 자동 등록.

## Codex Verification Notes

- **CardManager.allCards 동적 처리 확인**:
  - CardManager.cs:59 `GameStateManager.Instance.RegisterCardCatalogSize(allCards.Length)` 동적
  - CardManager.cs:116, 138 bounds/null check
  - CardManager.cs:184 `Array.IndexOf(allCards, card)` 동적
  - 하드코드 length=6 가정 0개
- **GSM.RegisterCardCatalogSize N=4 처리**:
  - GameStateManager.cs:588 `Mathf.Max(0, cardCount)` — 안전
  - GameStateManager.cs:1079-1090 `BuildOfferFromCatalog()` 0..N-1 풀
  - GameStateManager.cs:1093-1102 OfferChoices=3 unique 선택 — N=4면 4중 3 offer 정상
- **4 Resources GUID 모두 .meta 일치 확인** (Codex line-by-line)
- **인라인 코멘트 제거**: Unity reserialize 시 strip 위험 (X1-6b-3 Codex C-2 미러)
- **YAML 들여쓰기**: allCards 2-space, list items 동일 indent — 정확

## Files

| Op | Path | LOC delta |
|---|---|---|
| EDIT | `Assets/Scenes/3DScene.unity` | -2 (6 → 4 entries) |

## Verification (post-apply, expected)

1. Unity scene reimport 자동
2. 3DScene 열고 CardManager Inspector → allCards Size = 4. 4 NEW Resources/ AbilityCard (한국어 cardName 표시)
3. Play 모드 진입:
   - GSM.RegisterCardCatalogSize(4) 호출
   - 카드 draft trigger 시 4개 중 3개 offer
   - card.skillDefinition 정상 (4 카드 모두 X1-6b-4a 매핑)
   - SkillManager.SetSlot 정상
4. LEGACY 3 3DSceneScript/AbilityCard/ 자산은 그대로 (참조 0)

## Risks (Acknowledged)

1. **MCP 재연결 가능성**: 씬 reimport 시 Editor stall 가능
2. **draft 변동성 감소**: 4 중 3 offer = 거의 항상 비슷한 selection. X1-6b-4c에서 추가 카드 작성하면 12 SkillDefinition 풀 활용 (12 카드 → 3 offer)
3. **LEGACY 3 자산 잔존**: 참조 0이지만 파일 시스템에 존재. X1-6b-4c deprecation

## Spawned Follow-up

- **X1-6b-4c (선택)**: 3 3DSceneScript/AbilityCard/ legacy .asset 정리 (삭제 vs 유지) + 추가 8 AbilityCard 작성하여 12 SkillDefinition 풀 활용
- **X1-6c (별도)**: SkillProjectile / SkillArea 프리팹 생성 + NetworkPrefabs 등록 + Pool 매니저 씬 배치

## Workflow Note

X1-6b-2 ~ 4b 모두 **Codex MCP 자동 워크플로우**로 완료. 4 연속 라운드 사용자 복사붙여넣기 0.

## Parallel Workstream

X3 smoke / X4 Boss 모든 라운드와 파일 0겹침 유지.
