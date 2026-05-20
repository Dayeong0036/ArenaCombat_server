# Pending Codex Review — BAL-1 R1 (Round 2): Card Draft Cadence + Initial Loadout

## R1 R1 Verdict
**APPROVED WITH CHANGES** — 4 critical 이슈 발견, 모두 본 R2에서 반영.

| # | R1 Critical | R2 Resolution |
|---|------------|---------------|
| 1 | `Chapter1.unity` scene override `cardDraftInterval: 10` 미반영 | scene YAML 직접 수정 (10 → 180) + .cs 디폴트도 동시 변경 |
| 2 | `+1` slot shift가 `SetPlayerCardHistorySlot`을 깨뜨림 (slot 4 거부됨) | shift 위치 변경: GameStateManager는 history index (0~3) 유지, **CardManager.HandleSelectionResolved**에서 SkillManager 슬롯에 +1 적용 |
| 3 | Restart 시 OnNetworkSpawn 안 돌아 초기 스킬 사라짐 | `ApplyInitialLoadoutServer()` helper 추가, `RestartMatch` 내 ClearAll 직후 호출 |
| 4 | Persistent UI는 4-slot 가정 | UI는 historyIndex(0~3) 그대로 받음 → 변경 불필요. SkillManager slot은 별도 컨셉 |

## Topic
Card draft interval 45→180s + 플레이어 초기 스킬 slot 0 자동 장착. 결과: 12분 매치 동안 4번 드래프트가 slot 1~4를 채워서 최종 5슬롯 풀구성. Slot 0은 designer 지정 starter skill, restart 시에도 재적용.

## Roadmap link
Plan: `C:\Users\paek6\.claude\plans\foamy-baking-melody.md` (BAL-1 Tranche A)

## Goal
- 매치 시작 5초 내 `[AutoCast] slot[0] {InitialSkill}` 로그 확인
- 첫 카드 드래프트는 T≈180s에 시작 (45s 아님)
- 4번째 드래프트가 정상 처리되어 slot 4까지 채워짐 (slot index check 통과)
- Restart 후에도 slot 0 starter 그대로 유지

## Files to touch
- **EDIT** `Assets/Scenes/Chapter1.unity` (scene override cardDraftInterval: 10 → 180)
- **EDIT** `Assets/ArenaCombat/Scripts/Core/Network/GameStateManager.cs`:
  - cardDraftInterval 디폴트 45 → 180
  - RestartMatch에서 ApplyInitialLoadoutServer 호출 추가
- **EDIT** `Assets/ArenaCombat/Scripts/Core/Card/CardManager.cs` (line 153 slot index +1 shift)
- **EDIT** `Assets/ArenaCombat/Scripts/Core/Network/PlayerNetworkController3D.cs`:
  - `_initialSkillSO` 필드 추가
  - `ApplyInitialLoadoutServer()` 신규 public 메서드
  - OnNetworkSpawn 서버 블록에서 호출
- **EDIT** `Assets/ArenaCombat/Prefabs/Player/Player A.prefab` (또는 동등) — `_initialSkillSO`에 PiercingShot 어셋 할당

## Approach

### A1. Scene override (CRITICAL FIX)
`Chapter1.unity:3986`:
```yaml
cardDraftInterval: 10
```
→
```yaml
cardDraftInterval: 180
```

Inspector에서 직접 수정 vs YAML 직접 편집 — Edit 모드라면 MCP 또는 YAML 둘 다 가능. 본 라운드에서는 MCP `update_component` 사용 추천 (씬 Is Dirty 플래그 안전).

`.cs` default 45 → 180은 새 씬/프리팹에 적용되는 fallback.

### A2. Slot index +1 shift는 CardManager로 이동 (GameStateManager는 그대로)
**`GameStateManager.cs`는 history index 0~3 유지** (SetPlayerCardHistorySlot 호환).
`CommitCardSelectionServer` (line 1048-1072) 변경 없음 — `slotIndex = GetPlayerCardHistoryCount(playerId)` 그대로.

**`CardManager.cs:153`에서 SkillManager 슬롯 호출 시 +1 shift:**
```csharp
// 기존:
if (skillMgr != null && card.skillDefinition != null && slotIndex >= 0 && slotIndex < skillMgr.MaxSlots)
{
    skillMgr.SetSlot(slotIndex, card.skillDefinition);
}

// 변경:
int targetSlot = slotIndex + 1;  // BAL-1: slot 0 reserved for initial; drafts go 1..4
if (skillMgr != null && card.skillDefinition != null && targetSlot >= 0 && targetSlot < skillMgr.MaxSlots)
{
    skillMgr.SetSlot(targetSlot, card.skillDefinition);
}
```

UI 호출 `ApplyPersistentSelectionIcon(playerId, slotIndex, card)`은 historyIndex (0~3) 그대로 사용 — UI 4슬롯 카드 이력 표시는 변경 없음.

### A3. PlayerNetworkController3D: 초기 스킬 + ApplyInitialLoadoutServer

필드 추가 (다른 [SerializeField] 근처):
```csharp
[Header("Initial Loadout (BAL-1)")]
[Tooltip("매치 시작 + 부활 시 slot 0에 자동 장착. 드래프트는 slots 1..4 채움.")]
[SerializeField] private SkillDefinition _initialSkillSO;
```

새 public 메서드:
```csharp
// BAL-1: 초기 스킬을 slot 0에 장착. OnNetworkSpawn (matchstart) + RestartMatch 모두에서 호출.
public void ApplyInitialLoadoutServer()
{
    if (!IsServer) return;
    if (_initialSkillSO == null)
    {
        Debug.LogWarning($"[PNC3D] _initialSkillSO 미할당 — slot 0 비어있음. 첫 드래프트까지 무력.", this);
        return;
    }
    var skillMgr = GetComponent<SkillManager>();
    if (skillMgr != null) skillMgr.SetSlot(0, _initialSkillSO);
}
```

OnNetworkSpawn IsServer 블록에서 호출 (line 353 직후, PlayerBiasTracker 등록 직전 또는 직후):
```csharp
// BAL-1: 초기 스킬을 slot 0에 자동 장착.
ApplyInitialLoadoutServer();
```

### A4. RestartMatch에서 ApplyInitialLoadoutServer 호출 (CRITICAL FIX)

`GameStateManager.cs:477-514 RestartMatch` 루프 안:
```csharp
foreach (var kvp in snapshot)
{
    var player = kvp.Value;
    if (player == null) continue;

    var skillMgr = player.GetComponent<SkillManager>();
    if (skillMgr != null)
    {
        skillMgr.ClearAll();
        skillMgr.SetAutoCast(true);
    }

    var skillExec = player.GetComponent<SkillExecutor>();
    if (skillExec != null)
        skillExec.ResetAll();

    Vector3 spawnPos = PlayerSpawnManager.Instance != null
        ? PlayerSpawnManager.Instance.GetRespawnPosition(kvp.Key)
        : player.transform.position;
    player.Respawn(spawnPos);

    // BAL-1: 재시작 시 초기 스킬 재적용 (OnNetworkSpawn은 안 돌고 Respawn은 슬롯 안 건드림)
    player.ApplyInitialLoadoutServer();
}
```

### A5. Player prefab inspector 할당
`Player A.prefab`의 PNC3D 컴포넌트 inspector에서 `_initialSkillSO` 필드에 **PiercingShot** SO 드래그 할당.

추후 디자이너가 변경 가능 — 본 라운드는 PiercingShot으로 시작 (Codex R1 추천).

### A6. SkillManager static reference in GameStateManager
`SkillManager.SlotCount` 직접 참조 불필요 — CardManager.cs에 이미 `skillMgr.MaxSlots` (인스턴스 프로퍼티) 사용 중. 기존 패턴 유지.

## Diff sketch

### `Chapter1.unity` line 3986
```diff
-  cardDraftInterval: 10
+  cardDraftInterval: 180
```

### `GameStateManager.cs`
```diff
-        [SerializeField] private float cardDraftInterval = 45f;
+        [SerializeField] private float cardDraftInterval = 180f;
```

RestartMatch 루프 (line 504 직후):
```diff
                 player.Respawn(spawnPos);
+
+                // BAL-1: 초기 스킬 재적용 (Respawn은 슬롯 건드리지 않음)
+                player.ApplyInitialLoadoutServer();
             }
```

### `CardManager.cs:140` (HandleSelectionResolved)
```diff
             var skillMgr = FindSkillManagerForPlayer(playerId);
-            if (skillMgr != null && card.skillDefinition != null && slotIndex >= 0 && slotIndex < skillMgr.MaxSlots)
-            {
-                skillMgr.SetSlot(slotIndex, card.skillDefinition);
-            }
+            // BAL-1: history slot (0..3) → skill manager slot (1..4). Slot 0 is initial loadout.
+            int targetSlot = slotIndex + 1;
+            if (skillMgr != null && card.skillDefinition != null && targetSlot >= 0 && targetSlot < skillMgr.MaxSlots)
+            {
+                skillMgr.SetSlot(targetSlot, card.skillDefinition);
+            }
```

### `PlayerNetworkController3D.cs`
필드 (다른 SerializeField 가까이):
```csharp
[Header("Initial Loadout (BAL-1)")]
[Tooltip("매치 시작/부활 시 slot 0 자동 장착. 드래프트는 slots 1..4.")]
[SerializeField] private SkillDefinition _initialSkillSO;
```

신규 메서드:
```csharp
public void ApplyInitialLoadoutServer()
{
    if (!IsServer) return;
    if (_initialSkillSO == null)
    {
        Debug.LogWarning("[PNC3D] _initialSkillSO 미할당 — slot 0 비어있음.", this);
        return;
    }
    var skillMgr = GetComponent<SkillManager>();
    if (skillMgr != null) skillMgr.SetSlot(0, _initialSkillSO);
}
```

OnNetworkSpawn IsServer 블록 (line 353 ~ PlayerBiasTracker 등록 사이):
```diff
                 PlayerBiasTracker.Instance?.RegisterPlayer(OwnerClientId);
+
+                // BAL-1: 초기 스킬을 slot 0에 장착
+                ApplyInitialLoadoutServer();
```

## Risks / unknowns

1. **scene YAML 직접 편집 vs MCP**: 본 라운드는 .cs default 변경 + scene 변경 둘 다 필요. MCP가 켜져있으면 `mcp__mcp-unity__update_component`로 GSM 인스턴스 필드 변경 가능 (씬 dirty 후 save_scene). YAML 편집은 fallback.

2. **PiercingShot이 draft pool에 중복 등장**: 디자이너가 초기 = PiercingShot, 드래프트에서 또 PiercingShot 픽 시 → `skillMgr.SetSlot(slotIndex + 1, PiercingShot)` 됨. 결과: slot 0과 slot N에 같은 스킬. SkillExecutor cooldown은 SkillId 기준이라 사실상 한 번만 발동. **드래프트 픽 낭비**. R1에서는 그대로 두고, R2 이후 draft pool 필터 추가 가능 (out of scope).

3. **Respawn (단일 사망 → 자동부활) vs RestartMatch**: 단일 플레이어 사망 시 PNC3D 내부 Respawn 흐름. 슬롯은 ClearAll 호출 없이 그대로 유지됨 → 초기 스킬 살아있음. 추가 처리 불필요.

4. **CardManager `MaxSlots` bounds**: `targetSlot < skillMgr.MaxSlots` (= 5) 통과해야 함. slotIndex 0~3 + 1 = 1~4, 모두 통과. 안전.

5. **`_initialSkillSO` null 경고**: prefab에 할당 잊은 경우 warning 1회 출력. 게임은 진행되지만 첫 3분 빈 슬롯 → 사용자가 즉시 알아챔. 디자이너 실수 방지.

6. **Editor에서 cardDraftInterval Inspector 노출**: SerializeField 이미 적용됨. 디버그용으로 30s 등으로 임시 조정 가능.

## Questions for Codex

1. **scene YAML 변경 방법**: MCP `update_component` 호출 vs Edit tool로 scene YAML 직접 수정. 보통 Unity가 scene 변경 감지하고 reload하므로 둘 다 작동. Codex 권장?

2. **Slot 0 vs Slot 4 (last) 초기**: 본 안은 slot 0(highest priority). 만약 designer 의도가 "starter는 마지막 fallback" (낮은 우선순위)이면 slot 4를 잡고 드래프트는 0-3 유지 가능. 어느 게 좋은가?

3. **`ApplyInitialLoadoutServer()` public 여부**: GameStateManager.RestartMatch에서 호출하려면 public 필요. 다른 외부 호출자는 없으므로 internal로 줄여도 됨. 현재 같은 어셈블리(Assembly-CSharp)이라 internal=public 동등. Codex 의견?

4. **drafted PiercingShot duplicate 자동 거부**: 본 라운드 scope 외이지만, 미래에 도입할 가치 있는지?

5. **Designer 미할당 시 fallback**: `_initialSkillSO == null`일 때 자동으로 SkillRegistry에서 첫 PlayerSkill 픽? 또는 그냥 비워두고 경고? 본 안은 후자.

## Out of scope for this round
- 보스 hit rate (R2)
- 플레이어 hit rate (R3)
- 스탯 SO 수치 (R4)
- 페이즈 데미지 배율 (R5)
- Draft pool에서 초기 스킬 제외하는 필터
