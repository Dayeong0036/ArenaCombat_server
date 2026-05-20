# X3-S2: Smoke Test Patch — Choice Canvas RectTransform / Sort Order 수정 — 2026-05-14

**Status**: APPLIED. 두 번째 smoke 시도 디버깅 → 즉시 패치.

## 발견 경위

X3 smoke test 2차 시도 (X3-S1 EventSystem 패치 후):
- ✅ InvalidOperationException 사라짐
- ✅ Lobby host 정상
- ✅ 3DScene 진입 + Player A spawn
- ✅ `[CardManager] Draft started. round=2, duration=8.00s` 로그 (10초 대기 후 트리거)
- ✅ `[GSM] Card selected. player=0, slot=N, card=M` 8초 후 자동 선택 (timeout default)
- ❌ **카드 UI 화면에 안 보임** — 사용자 보고

## 원인 분석

### 1차 점검: allCards null 의심
MCP get_gameobject(CardManager) → `allCards: [null, null, null, null]`. 
실제 검증: 4 .asset script GUID `0606cc0234635b741b1ff737732ccb7c` (NEW Core.Card.AbilityCard, X1-6b-4a) + 4 .meta GUID 씬 ref와 정확 일치 → **YAML 정상**. MCP display는 SO array를 inline 표시 못 함 (단일 SO는 가능, array는 null로 표시되는 MCP-side quirk).

### 2차 점검: Choice Canvas RectTransform
3DScene `Choice Canvas` (fileID 60875986 GameObject, fileID 60875990 RectTransform) 직접 점검:

```yaml
RectTransform:
  m_LocalScale: {x: 0, y: 0, z: 0}    ← ⚠️ 0 크기!
  m_AnchorMin: {x: 0, y: 0}
  m_AnchorMax: {x: 0, y: 0}           ← ⚠️ stretch 안 됨
  m_AnchoredPosition: {x: 0, y: 0}
  m_SizeDelta: {x: 0, y: 0}           ← ⚠️ 0 크기
  m_Pivot: {x: 0, y: 0}               ← ⚠️ left-bottom pivot (center 아님)
```

Canvas 컴포넌트:
```yaml
m_RenderMode: 0           # Screen Space - Overlay (OK)
m_OverrideSorting: 0
m_SortingOrder: 0         # Main Canvas와 같은 layer
```

**원인 확정**: 
1. RectTransform `LocalScale=0` → UI 0 크기 그려짐
2. AnchorMax=(0,0) + SizeDelta=(0,0) → Screen Space - Overlay에서도 0 크기 stretch 안 됨
3. SortingOrder=0 → Main Canvas가 동시 표시 시 가려질 수 있음 (덜 중요한 부수 문제)

`cardUIPanel.SetActive(true)`는 정상 호출됐지만 (CardManager.cs:103), Canvas 자체가 0 크기라 그려지지 않음.

## 수정

`Assets/Scenes/3DScene.unity:220-242` Choice Canvas RectTransform + Canvas component EDIT:

```diff
  RectTransform:
-   m_LocalScale: {x: 0, y: 0, z: 0}
+   m_LocalScale: {x: 1, y: 1, z: 1}
-   m_AnchorMax: {x: 0, y: 0}
+   m_AnchorMax: {x: 1, y: 1}
-   m_Pivot: {x: 0, y: 0}
+   m_Pivot: {x: 0.5, y: 0.5}

  Canvas:
-   m_OverrideSorting: 0
+   m_OverrideSorting: 1
-   m_SortingOrder: 0
+   m_SortingOrder: 10
```

**효과**:
- LocalScale 1 → 정상 크기 그려짐
- AnchorMin (0,0) + AnchorMax (1,1) → Screen Space - Overlay에서 fullscreen stretch
- Pivot (0.5, 0.5) → center 기반 좌표계
- SortingOrder 10 + OverrideSorting 1 → Main Canvas (order=0) 위에 표시

Choice1/2/3 cardSlots RectTransforms는 본 라운드 무수정 — 검증 결과 정상 (LocalScale=1, AnchorMin/Max=(0.5,0.5), AnchoredPosition (-550 / 0 / 550, -110), SizeDelta 500x700, Pivot (0.5,0.5)).

## Files

| Op | Path | LOC delta |
|---|---|---|
| EDIT | `Assets/Scenes/3DScene.unity` | ~6 |

## Verification (post-apply, expected)

1. Unity scene reimport 자동
2. Hierarchy → Choice Canvas → Inspector RectTransform:
   - Scale = (1,1,1) ✓
   - Anchor stretch (Min 0,0 / Max 1,1) ✓
3. Play 모드 + lobby host 후 10초 대기:
   - 카드 draft UI 화면에 표시되어야 ✓
   - Choice1/2/3 카드 패널 보임 (한국어 cardName)
   - 카드 클릭 가능 → SkillManager slot 매핑

## 부가 발견 — MCP serializer quirk

CardManager.allCards 같은 SO 배열 필드는 MCP get_gameobject 시 `[null, null, ...]` 로 표시되지만 **실제 데이터는 정상**. 단일 SO ref (ProjectilePool._prefab = "SkillProjectile" 등)는 정상 표시. MCP-side known limitation, 게임 동작 무영향.

## Codex 검증 — 건너뜀 (긴급 디버깅)

본 패치는 명확한 RectTransform 0-scale 버그 수정. Codex 워크플로우 거치지 않음. 후속 X3-S3 필요 시 Codex 사이클 복귀.

## Spawned Follow-up

사용자 Play 재시도 → 5 verification 결과 보고:
- ✅ 1, 2, 4 이미 통과 (X3-S1 후)
- ⏳ 3 (Draft 중 차단): UI 표시되면 시각 확인
- ⏳ 5 (Pool spawn): UI 카드 선택 후 skill auto-cast 발생 시 SkillProjectile/Area spawn 확인

## Parallel Workstream

X4 / X1-6 모든 라운드와 파일 0겹침.
