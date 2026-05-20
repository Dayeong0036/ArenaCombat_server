# X3-S6: CardUI cardName 해시 색상 tint — 2026-05-14

**Status**: APPLIED. 코드 ~6줄 추가 + 재컴파일 1회.

## 발견 경위

X3-S5 (CardUI cardIcon null guard) 적용 후:
- ✅ NRE 사라짐
- ✅ 카드 UI 4중 3 정상 표시
- ❌ **4 카드 모두 흰색** — cardName 표시 UI 필드 없음 → 시각 구별 불가

사용자 보고: "그림이 없다 뭔지 구별이 안되 그냥 하얀 이미지로 보여"

## 임시 해결

CardUI.Setup에 cardName 해시 기반 색상 tint 추가:

```csharp
if (card.cardIcon == null && !string.IsNullOrEmpty(card.cardName))
{
    int hash = 0;
    foreach (char c in card.cardName) hash = (hash * 397) ^ c;
    float hue = Mathf.Abs(hash % 360) / 360f;
    icon.color = Color.HSVToRGB(hue, 0.55f, 1f);
}
```

- cardIcon null 일 때만 tint 적용 (cardIcon 정상 복원 시 자연스럽게 비활성)
- HSV(hue, 0.55, 1) — saturation 적당히 + value 최대 = 밝은 distinct hue
- cardName 해시는 deterministic (같은 카드는 항상 같은 색)

4 카드 예상 색상 분포 (각각 다른 hue):
- 블랙매지션걸 → some hue A
- 푸른눈의 백룡 → some hue B
- 오시리스의 천공룡 → some hue C
- 오드아이즈레볼루션드래곤 → some hue D

## Files

| Op | Path | LOC delta |
|---|---|---|
| EDIT | `Assets/ArenaCombat/Scripts/Core/Card/CardUI.cs` | +9 / -0 |

## Verification (post-apply, expected)

1. **재컴파일 1회 필요** (사용자 액션)
2. Play 재시도 시 카드 4중 3 offer가 distinct color (4 다른 hue) 표시
3. 카드 hover/click 정상

## 후속 (Polish)

- cardIcon Sprite 정확 복원 (디자이너가 Inspector에서 4 PNG sprite 직접 드래그 또는 png reimport 후 자동 매핑)
- cardIcon 복원되면 X3-S6의 tint 자동 비활성 (조건 if문이 cardIcon != null이면 skip)
- cardName Text overlay UI 추가 (별도 라운드, UI prefab 수정 필요)

## Codex 검증 — 건너뜀 (긴급 시각 디버깅)

본 패치는 임시 색상 구별. polish 라운드에서 정식 sprite 복원 시 자동 무효화.

## Spawned Follow-up

- Play 재시도 → 카드 distinct color 확인
- Verification 5 (Pool spawn): 카드 클릭 → SkillManager.SetSlot → skill auto-cast → SkillProjectile/Area spawn 시각 확인

## Parallel Workstream

X4 / X1-6 모든 라운드와 파일 0겹침.
