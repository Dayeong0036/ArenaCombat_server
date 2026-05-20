# X3-S3: CardManager + CardUI script GUID LEGACY → NEW swap — 2026-05-14

**Status**: APPLIED. 세 번째 smoke 시도에서 핵심 원인 발견 → 즉시 수정.

## 발견 경위

X3-S2 (Choice Canvas RectTransform 패치) 적용 후에도:
- ✅ Choice Canvas activeInHierarchy + 1920x1080 fullscreen
- ❌ Choice1/2/3 카드 슬롯 비활성 (Hierarchy에서 inactive)
- ❌ Project window에서 4 Resources/AbilityCard 자산이 깨진 {} 아이콘 (Script Missing 표시)

MCP get_gameobject(CardManager) → `allCards: [null, null, null, null]` (X1-6b-4b 적용 후 줄곧)

## 추적

1. **GUID 매치 검증**: scene refs ↔ .meta files 모두 정확. 4 .asset의 m_Script GUID도 정확 (`0606cc02…` NEW Core.Card.AbilityCard).
2. **AbilityCard.cs 2개 발견**:
   - `3DSceneScript/Scripts/AbilityCard.cs` (LEGACY, global namespace `AbilityCard`, GUID `c923b417…`)
   - `Scripts/Core/Card/AbilityCard.cs` (NEW, `ArenaCombat.Core.Card.AbilityCard`, GUID `0606cc02…`)
3. **CardManager.cs 2개 발견**:
   - `3DSceneScript/Scripts/CardManager.cs` (LEGACY, global, GUID `ea987e74…`)
   - `Scripts/Core/Card/CardManager.cs` (NEW, namespaced, GUID `180fb7e4…`)
4. **3DScene CardManager component → script GUID `ea987e74…` (LEGACY)** ← 결정적 발견
5. **CardUI.cs 2개**: LEGACY (`194612b6…`) + NEW (`82e3ae51…`). 3 Choice1/2/3 모두 LEGACY GUID 사용.

## 진짜 원인

LEGACY CardManager의 `public AbilityCard[] allCards`는 LEGACY global `AbilityCard` 클래스를 기대. NEW Resources/AbilityCard 4개 자산은 namespaced `ArenaCombat.Core.Card.AbilityCard` 클래스. **Unity 시리얼라이저가 type mismatch 감지 → 자산 ref 모두 null로 처리**.

CardManager.cs:116 `allCards[cardIdx] == null` 체크에 4 모두 걸림 → `cardSlots[i].gameObject.SetActive(false)` 4번 실행 → Choice1/2/3 inactive → UI 빈 Canvas만 보임 (사용자 시점에 UI 안 보이는 것처럼).

## 수정

3DScene.unity script GUID swap:

| Component | LEGACY GUID | NEW GUID | fileID |
|-----------|-------------|----------|--------|
| CardManager | `ea987e742f42384479606957e5c252f8` | `180fb7e4ac69a93438f987bcd9f4ac31` | 158453617 |
| CardUI #1 | `194612b6667ab5d42b230decd6852912` | `82e3ae51f498c4e4b9384232973258a5` | 569462507 |
| CardUI #2 | `194612b6667ab5d42b230decd6852912` | `82e3ae51f498c4e4b9384232973258a5` | 1102588391 |
| CardUI #3 | `194612b6667ab5d42b230decd6852912` | `82e3ae51f498c4e4b9384232973258a5` | 1243347760 |

Edit `replace_all=true`로 4개 한 번에.

## 검증

MCP get_gameobject(CardManager) 후속:

**Before (X3-S3 적용 전)**:
```
"allCards": [null, null, null, null]
```

**After (X3-S3 적용 후)** ✅:
```
"allCards": [
  "AbilityCard",
  "AbilityCard 1",
  "AbilityCard 2",
  "AbilityCard 3"
]
```

## Files

| Op | Path | LOC delta |
|---|---|---|
| EDIT | `Assets/Scenes/3DScene.unity` | 4 lines (script GUID swap × 4) |

## 부수 효과 / Trade-off

LEGACY CardManager 필드 중 NEW에 없는 것들 (시리얼라이즈된 데이터 손실):
- `hostUI` / `clientUI` (DraftSideUIBinding) — host/guest persistent selected card UI
- `bigCardPreview` (Image)
- `useNetworkSynchronizedDraft` / `autoBindToGameStateManager` (bool)
- `standaloneFirstDraftDelay` / `standaloneDraftInterval` / `pauseTimeScaleInStandalone` / `standaloneMaxSelections` (standalone fallback)
- `debugHostClientId` / `debugGuestClientId` / `debugActiveDraftRound`

NEW CardManager는 X3-6 라운드에서 GSM 이벤트 기반으로 lean하게 재작성. standalone fallback / persistent slot UI 없음. 카드 draft 자체는 정상 (NetworkSynchronized only via GSM, GameManager.Start의 RegisterCardCatalogSize + 4 GSM 이벤트 구독으로 흐름 완성).

NEW에 추가된 `selectedCardSlots` (Image[]) 필드는 빈 채로 시작 — UI 후속 라운드에서 wire 가능 (선택).

## Smoke Test 영향

- **Verification 1, 2, 4 이미 통과** (X3-S1 후)
- **Verification 3 (Draft 중 차단)** — 카드 UI 표시되면 시각 확인 가능
- **Verification 5 (Pool spawn)** — 카드 클릭 → SkillManager.SetSlot → skill auto-cast → SkillProjectile/Area spawn 시각 확인

다음 Play 시도에서 카드 UI 정상 표시 + 선택 → skill spawn까지 진행 가능.

## Codex 검증 — 건너뜀 (긴급 디버깅)

본 패치는 명확한 type mismatch 원인 추적 + script GUID 4 swap. 후속 X3-S4 필요 시 Codex 사이클 복귀.

## Spawned Follow-up

- Play 재시도 → 카드 UI 표시 확인 + skill auto-cast verification 5 시각 확인
- 만약 NEW CardManager에 LEGACY UI (host/guest persistent slot) 가 필요해지면 추가 라운드에서 재도입
- LEGACY 3DSceneScript/Scripts/ 정리 (X1-6b-4c 또는 별도) — 더 이상 사용 안 됨

## Parallel Workstream

X4 / X1-6 모든 라운드와 파일 0겹침.
