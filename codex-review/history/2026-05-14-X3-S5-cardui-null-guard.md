# X3-S5: CardUI cardIcon null guard — 2026-05-14

**Status**: APPLIED. 다섯 번째 smoke 시도 디버깅. 코드 1줄 변경 + 재컴파일 1회 필요.

## 발견 경위

X3-S4 (cardIcon → null) 적용 후 새 에러:
```
UnassignedReferenceException: The variable cardIcon of AbilityCard has not been assigned.
You probably need to assign the cardIcon variable of the AbilityCard script in the inspector.
```

## 원인

`Scripts/Core/Card/CardUI.cs:40`:
```csharp
cardMaterial.SetTexture("_MainTex", card.cardIcon.texture);
```

`card.cardIcon`이 null Sprite (X3-S4에서 의도적으로 null 처리). Unity의 Object override null check:
- `if (sprite == null)` → true (감지)
- 하지만 직접 `sprite.texture` 접근 → UnassignedReferenceException 던짐

CardUI.Setup line 32 `icon.sprite = card.cardIcon` (null OK, slot에 빈 sprite 들어감)는 통과. line 40 `.texture` 접근에서 NRE.

## 수정

`Scripts/Core/Card/CardUI.cs:40` null guard 추가:

```diff
- cardMaterial.SetTexture("_MainTex", card.cardIcon.texture);
+ // X3-S5: cardIcon Sprite slot may be unassigned (X3-S4 null fix). Guard texture access.
+ cardMaterial.SetTexture("_MainTex", card.cardIcon != null ? card.cardIcon.texture : null);
```

cardIcon null이면 `_MainTex`에 null texture 전달. material은 fallback 색상/기본 texture 사용. 카드 UI 시각적 표시는 빈 슬롯이지만 NRE 없음.

## Files

| Op | Path | LOC delta |
|---|---|---|
| EDIT | `Assets/ArenaCombat/Scripts/Core/Card/CardUI.cs` | +1 / -1 |

## Verification (post-apply, expected)

1. **재컴파일 1회 필요** (사용자 액션)
2. Play 재시도 시 `UnassignedReferenceException` 사라짐
3. 카드 UI 정상 표시 (4 카드 중 3 offer, 한국어 cardName) — 아이콘 빈 채
4. 카드 클릭 → CardManager.OnCardSelected → SkillManager.SetSlot

## Codex 검증 — 건너뜀 (긴급 디버깅, 1줄 null guard)

본 패치는 명확한 null safety. 후속 X3-S6 / polish 시 Codex 사이클 복귀.

## Spawned Follow-up

- 재컴파일 후 Play → 카드 UI 정상 표시 확인 + verification 5 (skill spawn) 시각 확인
- Polish: cardIcon Sprite 정확 재할당 (디자이너 라운드)

## Parallel Workstream

X4 / X1-6 모든 라운드와 파일 0겹침.
