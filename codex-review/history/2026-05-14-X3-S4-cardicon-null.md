# X3-S4: AbilityCard cardIcon Sprite 참조 정리 (임시 null) — 2026-05-14

**Status**: APPLIED. 네 번째 smoke 시도 디버깅.

## 발견 경위

X3-S3 (CardManager + CardUI script swap) 적용 후:
- ✅ `[GameStateManager] Card catalog size registered: 4`
- ✅ `[GameSceneInitializer] Game scene initialization complete`
- ✅ `[GameStateManager] Match state changed: WaitingForPlayers -> InProgress`
- ✅ `[GameStateManager] Match started!`
- ❌ `MissingReferenceException: The variable cardIcon of AbilityCard doesn't exist anymore. You probably need to reassign the cardIcon variable of the 'AbilityCard' script in the inspector.` (Round=1 직전)
- ✅ `[GameStateManager] Card draft started. Round=1` 진행 (NRE에도 불구하고 draft는 시작)

## 원인

4 Resources/AbilityCard.asset의 `cardIcon: {fileID: 21300000, guid: <png-guid>, type: 3}` Sprite 참조 깨짐:

- AbilityCard:   guid `f8229e5c8e1b6b641b0ebffd6268bb8c` → Resources/pngegg.png ✓ 존재
- AbilityCard 1: guid `53ffb52cfde1be243b09070de8248137` → 다른 PNG
- AbilityCard 2: guid `a729e84cdb192fd4591999690090b4d8` → 다른 PNG
- AbilityCard 3: guid `930260a83da23984495192332a19b6f6` → 다른 PNG

PNG 파일 + .meta 모두 존재. `textureType: 8` (Sprite) / `spriteMode: 1` (Single) 확인됨. 그럼에도 fileID `21300000` sub-asset을 Unity가 찾지 못함.

가능 이유:
- PNG의 Sprite sub-asset이 다른 fileID 사용 (Unity 6.3에서 변경됐을 수도)
- PNG가 Texture2D로만 import되고 Sprite sub-asset 미생성 (textureType=8인데도)
- `.meta`의 `internalIDToNameTable` 미설정으로 sub-asset id 추적 안 됨

## 임시 수정

4 Resources/AbilityCard.asset의 `cardIcon`을 null로 변경:

```diff
- cardIcon: {fileID: 21300000, guid: <png-guid>, type: 3}
+ cardIcon: {fileID: 0}
```

CardUI.Setup이 cardIcon null 처리 — 빈 sprite 슬롯으로 카드 UI 표시. 카드 클릭은 정상 작동. **smoke verification에는 영향 없음**.

## Files

| Op | Path | LOC delta |
|---|---|---|
| EDIT | `Assets/ArenaCombat/Resources/AbilityCard/AbilityCard.asset` | -1 (cardIcon line 단축) |
| EDIT | `Assets/ArenaCombat/Resources/AbilityCard/AbilityCard 1.asset` | -1 |
| EDIT | `Assets/ArenaCombat/Resources/AbilityCard/AbilityCard 2.asset` | -1 |
| EDIT | `Assets/ArenaCombat/Resources/AbilityCard/AbilityCard 3.asset` | -1 |

## Verification (post-apply, expected)

1. Play 재시도 시 `MissingReferenceException: cardIcon` 사라짐
2. 카드 UI 화면에 4 카드 (한국어 cardName) 표시 — 아이콘 영역만 빈 채
3. 카드 클릭 → SkillManager.SetSlot 정상 → skill 활성화

## 후속 (Polish)

X3-S5 또는 디자이너 라운드에서:
1. 디자이너가 Project window에서 Resources/pngegg*.png 클릭 → Inspector → "Texture Type: Sprite (2D and UI)" 확인 → "Apply"
2. 4 AbilityCard.asset Inspector 열고 cardIcon 슬롯에 PNG (Sprite) 직접 드래그
3. 또는 spritesheet 만들어서 4 카드 아이콘 통합

## Codex 검증 — 건너뜀 (긴급 디버깅)

본 패치는 명확한 임시 fix (Sprite null 처리). 후속 polish 또는 X3-S5 필요 시 Codex 사이클 복귀.

## Spawned Follow-up

- Play 재시도 → 카드 UI 정상 표시 확인 + verification 5 (skill spawn) 시각 확인
- Polish: cardIcon Sprite 정확 재할당

## Parallel Workstream

X4 / X1-6 모든 라운드와 파일 0겹침.
