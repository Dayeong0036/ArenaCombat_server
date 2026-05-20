# X2-11: SkillManager + GameManager Paired (2026-05-12)

ROADMAP item Phase X2-11. Eleventh X2 sub-cycle. Paired full import (2 files, ~370 LOC). **Roadmap reorder**: GameManager promoted from X2-13 (last) to X2-11 (paired) — forced by `SkillManager._gameManager.Bosses` compile dep. Sub-cycle count 13 → 12.

---

## Outcome

**Status**: APPLIED + **Codex Round 1 APPROVED WITH CHANGES** (3 critical + 5 suggestion, all applied).

**Operations**:
- 2 NEW `.cs` + 2 NEW `.meta` (both Buildup GUIDs preserved).
- No new folder.

**Files touched**:
- NEW `Assets/ArenaCombat/Scripts/Core/Skill/Core/SkillManager.cs` + `.meta` (Buildup GUID `024d2968…`)
- NEW `Assets/ArenaCombat/Scripts/Core/GameManager.cs` + `.meta` (Buildup GUID `9f0167c7…`)

**Doc updates**:
- ROADMAP X2-11 → DONE (with reorder + 3 critical + M-1 deviation notes); X2-13 entry removed; X2-12 (card draft) → NEXT.
- TARGET_ARCHITECTURE.md §10 X2-11 row done + §2 component catalog SkillManager phase updated; X2-13 row removed.
- SKILL_SYSTEM_DESIGN.md §9 Tick rate row updated to reflect Buildup verbatim Update + X3 future-AutoCastTick possibility.

---

## Codex Critical Fixes Applied (3)

**C-1: Server-only gate at top of `Update()` — not deferred to X3.**
Buildup `Update()` ran unconditionally. Codex argued that even with `_owner == null` (pre-X3), if `_statManager.IsAlive == true` AND `_slots` populated AND `_gameManager.Bosses` has entries, some SkillStep impls (e.g. `DealDamage`) could still execute. Inert-by-config assumption too fragile.

Applied at line 109:
```csharp
private void Update()
{
    // ── Server-only authority gate (X2-11) ──
    // Skip on confirmed client. NM null path = offline / editor — allowed.
    if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
        return;
    // ... rest of Buildup logic
}
```

Codex-recommended exact gate pattern: `NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer`. Offline (NM null) still allowed for editor testing.

**C-2: GameManager namespace `ArenaCombat.Core`.**
Buildup origin had no namespace. Global `GameManager` would collide with Unity sample code / future imports. Wrapped:
```csharp
namespace ArenaCombat.Core { public class GameManager : MonoBehaviour { ... } }
```
SkillManager.cs added `using ArenaCombat.Core;`.

**C-3: Temporary Buildup-compatible registry — documented deviation from BUILDUP_INTEGRATION_PLAN.md.**
The plan says "thin facade, Players/Bosses route through CombatManager3D". Buildup origin has direct `List<GameObject> _players/_bosses` Inspector lists. Compromise: import verbatim for X2-11 SkillManager compile compatibility, BUT explicit header comment + ROADMAP note flag this as **temporary**:
- GameManager.cs header has "TEMPORARY BUILDUP-COMPATIBLE REGISTRY" warning block
- Notes the X3/X4 plan: PNC3D ICombatant + BNC3D boss registry → CombatManager3D routing
- SkillRegistry / ElapsedTime remain on the facade per plan

---

## Codex Non-Blocking Suggestions Addressed (5)

| # | Suggestion | Status |
|---|---|---|
| S-1 | Roadmap reorder GameManager X2-13 → X2-11 paired is correct (`_gameManager.Bosses` type ref forces pair) | ✓ applied |
| S-2 | `PlayerController _owner` → `ICombatant _owner` + `GetComponent<ICombatant>()` is better than shim | ✓ applied (M-1) |
| S-3 | Update vs FixedUpdate: keep Buildup parity Update + server gate; doc note "X2 import keeps Buildup Update; X3 may extract" | ✓ SKILL_SYSTEM_DESIGN.md §9 updated; FixedUpdate origin deferred |
| S-4 | `GameManager.Start` SkillBinder.BindAll NOT server-only (local SO delegate injection; client needs RuntimeStep for IsReady) | ✓ comment added in GameManager explaining no server gate at BindAll |
| S-5 | `using ArenaCombat.Core.Combat/Stats/State/Skill;` + `ArenaCombat.Core;` block organized in SkillManager | ✓ 6 usings imported at top |

---

## M-1 Deviation: `_owner` Type Change

Buildup line 43:
```csharp
[SerializeField] private PlayerController _owner;   // Caster 로 사용
```

Our project has no `PlayerController` type (Buildup-specific). PNC3D (`PlayerNetworkController3D`) is the equivalent but doesn't yet implement ICombatant (X3 wiring work).

Three options weighed (in pending.md §M-1):
- A. Stub PlayerController — X2-3 stub trap.
- B. `[SerializeField] MonoBehaviour _ownerMono; runtime cast as ICombatant` — Inspector drag still required.
- C. Remove Inspector field; auto-resolve `_owner = GetComponent<ICombatant>()` in Awake — matches per-entity component pattern.

**Choice: C**. Field declaration changed:
```csharp
// M-1 deviation from Buildup
private ICombatant _owner;
```
Awake:
```csharp
if (_owner == null) _owner = GetComponent<ICombatant>();
```

ML preservation policy compliance: BuildSkillContext output semantically identical (`Caster = ICombatant ref`). Designer setup simplified. Inspector slot removed (Buildup `.prefab` files referring to PlayerController slot will produce "missing field" warning on import — silent, no runtime issue).

---

## Type Surface Verification (post-write grep)

### SkillManager.cs — 14 public surface members

| Member | Line | Status |
|---|---|---|
| `public class SkillManager : MonoBehaviour` | 42 | ✅ |
| `public const int SlotCount = 5` | 44 | ✅ |
| `public int MaxSlots` | 70 | ✅ |
| `public bool AutoCastEnabled` | 71 | ✅ |
| `public bool RoundRobinEnabled` (get/set) | 72 | ✅ |
| `public IReadOnlyList<SkillDefinition> Slots` | 77 | ✅ |
| `public bool SetSlot(int, SkillDefinition)` | 156 | ✅ |
| `public void ClearSlot(int)` | 163 | ✅ |
| `public void ClearAll()` | 169 | ✅ |
| `public void SetAutoCast(bool)` | 174 | ✅ |
| `public bool TryExecute(int, SkillContext)` | 180 | ✅ |
| `public bool CanUse(int)` | 186 | ✅ |
| `public float GetRemainingCooldown(int)` | 192 | ✅ |
| `public void ResetCooldown(int)` | 198 | ✅ |
| `public SkillContext BuildSkillContext(ICombatant)` | 257 | ✅ |
| `public ICombatant FindNearestTarget()` | 278 | ✅ |

Total: **16 public members** (14 methods + 2 getters explicitly + 4 simple-expression getters). Buildup byte-identical surface.

### GameManager.cs — 13 public members

| Member | Status |
|---|---|
| `static Instance` | ✅ |
| `Players` / `Bosses` / `ElapsedTime` getters | ✅ |
| `Player1` / `Player2` / `Boss` accessors | ✅ |
| `RegisterPlayer` / `RegisterBoss` / `UnregisterPlayer` / `UnregisterBoss` | ✅ |
| `SkillRegistry` getter | ✅ |
| `StartTimer` / `StopTimer` / `ResumeTimer` | ✅ |

All Buildup byte-identical.

---

## ML Preservation Policy Compliance

Per SKILL_SYSTEM_DESIGN.md §10a:

| Item | Status |
|---|---|
| SkillManager public surface (16 members) byte-identical | ✓ |
| `SlotCount = 5` const preserved | ✓ |
| Inspector defaults (`_autoCastEnabled=true`, `_logAutoCast=true`, `_roundRobinEnabled=false`) | ✓ |
| Update loop logic (server gate added but post-gate identical) | ✓ |
| CanCast 7-condition check byte-identical | ✓ |
| BuildSkillContext field population | ✓ |
| FindNearestTarget algorithm | ✓ |
| GameManager public surface | ✓ byte-identical |
| GUID preservation | ✓ both Buildup GUIDs |
| `_owner` type change (M-1) | ⚠ structural deviation, behaviorally identical |
| Server gate `NetworkManager.Singleton`-based | ⚠ added (Codex C-1); pre-X2-11 Buildup ran unconditionally; behavioral equivalence on server, blocks client unwanted execution |

ML observation impact: zero. BossObservationCollector reads SkillExecutor stats, not SkillManager. Server gate skip on client doesn't affect Buildup learning environment (Buildup was non-networked).

---

## Translation Audit

Comprehensive Korean → English. Examples:

**SkillManager headers + section dividers**:
- `자동 시전 스킬 매니저 (Stage 6 / 디버그·학습용 모드)` → `Auto-cast skill manager (per-entity component)`
- `슬롯 index 순서 = 우선순위` → `Slot index = priority (0 = highest)`
- `시전 가능 조건 (CanCast):` → `CanCast (7 conditions):`
- `슬롯 관리 (런타임 변경 API — 필요 시 사용)` → `Slot management (runtime mutation API)`
- `수동 발동 / 조회` → `Manual fire / query`
- `7가지 조건 전체 검사 + ctx 생성` → `Full 7-condition check + ctx build`
- `[Header("슬롯 (index 0 = 최우선, null = 빈 슬롯)")]` → `[Header("Slots (index 0 = priority, null = empty)")]`
- `[Header("설정")]` → `[Header("Settings")]`
- `[Header("참조")]` → `[Header("References")]`

**SkillManager Debug.Log**:
- `Update 차단:` → `Update blocked:`
- `<b>슬롯[{i}]</b> ... 자동 시전 | {name}` → `<b>slot[{i}]</b> ... fired | {name}`
- `실패: IsReady=...` → `failed: IsReady=...`
- `실패: CanCast=false` → `failed: CanCast=false`
- `실패: 타겟 없음` → `failed: no target`
- `실패: 거리 초과` → `failed: out of range`

**SkillManager M-2** (empty XML doc replacement at Awake):
- `/// <summary> / / / </summary>` → removed (replaced with `// Initialization` section divider)

**GameManager**:
- `게임 전체 참조 허브 + 전투 시간 측정` → `Match-wide reference hub + combat elapsed time tracking`
- `[Header("전투 오브젝트")]` → `[Header("Combat objects")]`
- `[Header("스킬 레지스트리")]` → `[Header("Skill registry")]`
- `[Header("시간")]` → `[Header("Time")]`
- `// 외부 참조` → `// External access`
- `// 런타임 등록` → `// Runtime registration`
- `// 타이머 제어` → `// Timer control`
- `// 단건 편의 프로퍼티` → `// Single-element convenience accessors`

---

## Behavior Contract After X2-11

- `SkillManager` attachable to any GameObject (auto-requires SkillExecutor).
- Server-only Update gate active. On confirmed-client: early return.
- Awake resolves `_statManager` / `_stateManager` / `_owner` via GetComponent.
- `_owner` defaults to null until X3 PNC3D / X4 BNC3D implements ICombatant.
- `GameManager.Instance` available scene-wide. `RegisterPlayer/Boss` called during X1-6 / X1-7 / X3 / X4 wiring.
- `GameManager.Start` triggers `SkillBinder.BindAll(_skillRegistry)` → populates 22 implemented skills (when SOs exist; pre-X1-6 logs "0 skills bound").
- **Zero call sites in our codebase** other than internal Awake/Start/Update self-loops. SkillManager Inspector-attachable for designer setup.

---

## Spawned Follow-ups

- **X2-12 (NEXT)**: AbilityCard + CardManager + SelectableUICard + CardUI (~? LOC, card draft system).
- **X3 wiring**:
  - PNC3D implements ICombatant → SkillManager auto-detects on Awake → operational
  - Decide whether to extract `AutoCastTick(dt)` for server FixedUpdate driver (per SKILL_SYSTEM_DESIGN.md §9 future note) or keep Update
  - Route GameManager.Players / Bosses through CombatManager3D / boss registry per BUILDUP_INTEGRATION_PLAN.md (currently temp direct lists)
- **X4**: BNC3D implements ICombatant → boss FindNearestTarget begins returning real targets
- **X1-6 SO import**: 22+ SkillDefinition `.asset` files → `SkillBinder.BindAll` reports >0 bound
- **Buildup `.prefab` field migration**: PlayerController `_owner` slot in any Buildup-origin prefab will silently drop on import (no PlayerController class). Acceptable; PNC3D Awake auto-detects.

---

## User-Side Verification (pending — Unity recompile + MCP attach)

**Step 1 (user)**: focus Unity Editor window once → auto-recompile of new .cs files (5-10s).

**Step 2 (Claude via MCP)**:
- `update_component` SkillManager → TestObject
- `get_gameobject TestObject` → confirm Inspector surface:
  - 5-slot SkillDefinition array (English label "Slots (index 0 = priority, null = empty)")
  - Settings section: AutoCastEnabled / LogAutoCast / RoundRobinEnabled toggles
  - References section: StatManager / StateManager / GameManager slots
  - Console: `0 new error / 0 new warning`
- Optional: scratch GameObject + GameManager component → confirm Players / Bosses / SkillRegistry / Time sections.

---

## Lessons

- **Server gate must be defensive, not config-dependent**: Codex C-1 caught the brittleness of "inert when _owner is null" argument. Even with `_owner == null`, some SkillStep paths can still mutate state via `_gameManager.Bosses` iteration + DealDamage etc. **Lesson**: add network gates at all entity-level components imported pre-X3 wiring, regardless of intended caller. SkillProjectile / SkillArea X2-7/8 have `ShouldRunHitDetection()` forward-compat — SkillManager now joins that pattern with `NetworkManager.Singleton`-based gate.
- **Roadmap entries can have cross-cycle dependencies**: GameManager listed as X2-13 (final), but SkillManager (X2-11) needed it for compile. Re-validating dependencies at each pending.md is essential. The lesson formalized in X2-3 keeps paying.
- **Buildup-vs-our-design conflicts**: BUILDUP_INTEGRATION_PLAN.md said "thin facade route through CombatManager3D"; Buildup origin has direct lists. Compromise: import verbatim + flag as TEMPORARY in header + ROADMAP. Future X3/X4 wiring round handles the routing change.
- **`Inspector` field type changes are silent at runtime**: removing `[SerializeField] PlayerController _owner` doesn't break Buildup `.prefab` files — Unity silently drops unknown serialized fields. No migration script needed.
- **Codex's strict gate adherence pays off**: 3 critical fixes caught in one round, all preventable design errors. Workflow rule held since X2-6 retroactive.
