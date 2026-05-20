# X2-8: Area Subsystem Paired (2026-05-12)

ROADMAP item Phase X2-8. Eighth X2 sub-cycle. Paired full import (4 files, ~247 LOC) — IPersistentArea + SkillArea + PersistentAreaPool + PersistentAreaManager. Same architecture pattern as X2-7 (Projectile subsystem).

---

## Outcome

**Status**: APPLIED + **Codex Round 1 APPROVED** (no critical, 6 non-blocking suggestions all addressed).

**Operations**:
- 1 NEW folder (`Core/Skill/Area/`) with fresh GUID `60d77af4d0f9406ba7445e7a650c90d8`.
- 4 NEW `.cs` + 4 NEW `.meta` (all Buildup GUIDs preserved).
- 1 preemptive Codex critical fix applied (PersistentAreaPool.Get double-enqueue — same pattern as X2-7 ProjectilePool).
- IPersistentArea.cs joined existing X2-7 `Core/Skill/Interfaces/` folder.

**Files touched**:
- NEW `Assets/ArenaCombat/Scripts/Core/Skill/Area.meta` (folder)
- NEW `Assets/ArenaCombat/Scripts/Core/Skill/Interfaces/IPersistentArea.cs` + `.meta` (Buildup GUID `4119d693…`)
- NEW `Assets/ArenaCombat/Scripts/Core/Skill/Area/SkillArea.cs` + `.meta` (Buildup GUID `a01c7231…`)
- NEW `Assets/ArenaCombat/Scripts/Core/Skill/Area/PersistentAreaPool.cs` + `.meta` (Buildup GUID `6b9e09b4…`, **Get patched**)
- NEW `Assets/ArenaCombat/Scripts/Core/Skill/Area/PersistentAreaManager.cs` + `.meta` (Buildup GUID `eed28664…`)

**Doc updates**:
- ROADMAP X2-8 → DONE (Codex notes + bug fix logged), X2-9 (SkillComponents 37 parts, 536 LOC) → NEXT.
- TARGET_ARCHITECTURE.md §10 X2-8 row marked done; X2-9 promoted.

---

## Codex Suggestions Addressed (all 6)

| # | Suggestion | Status |
|---|---|---|
| S-1 | PersistentAreaPool.Get() preemptive fix correct, ship in same round | ✓ applied |
| S-2 | PersistentAreaManager warning string Korean original is corrupted → must use clean ASCII rewrite | ✓ `[PersistentAreaManager] PersistentAreaPool not assigned` (clean English) |
| S-3 | SkillArea field set (`_radius`/`_angleDeg`/`_shape`/`_tickEffect`/`_ctx`/`_forward`) must be real code (not mojibake-eaten) | ✓ post-write grep confirms all 10 fields present at lines 24, 26-28, 31-36 |
| S-4 | SkillArea ShouldRunHitDetection hook deferred to X3 (not now) | ✓ no gate added; X3 adds it |
| S-5 | Physics.OverlapSphere NonAlloc deferred to X3+ perf pass | ✓ kept Buildup verbatim |
| S-6 | PersistentAreaPool no static Instance OK (Inspector-managed pattern) | ✓ matches Buildup |

---

## Codex Critical Fix (Preemptive) — PersistentAreaPool.Get()

Identical double-enqueue defect as Buildup `ProjectilePool.Get()` (X2-7 critical). Same fix applied.

**BEFORE (Buildup origin, line 19)**:
```csharp
public SkillArea Get(Vector3 position)
{
    var obj = _available.Count > 0 ? _available.Dequeue() : CreateInstance();
    // ... obj used as active, but still in queue ...
}
// CreateInstance line 49 enqueues
```

**AFTER (X2-8 patched)**:
```csharp
public SkillArea Get(Vector3 position)
{
    if (_available.Count == 0)
        CreateInstance();
    var obj = _available.Dequeue();
    // ... obj exclusively active, no longer in queue ...
}
```

Symmetric pattern: `CreateInstance()` always enqueues, `Get()` always dequeues. DIVERGENCE FROM BUILDUP header noted.

---

## Type Surface Verification

### IPersistentArea.cs ✓
- `Initialize(Vector3, float, AreaShape, float, float, float, SkillStep, SkillContext)` — Buildup byte-identical.
- Inherits `IPoolable` (X2-7).

### SkillArea.cs ✓
- 1 Inspector field (`_areaColor`)
- 9 private fields (`_renderer`, `_pool`, `_routine`, `_radius`, `_angleDeg`, `_shape`, `_tickEffect`, `_ctx`, `_forward`)
- Public API: `SetPool`, `Initialize`, `OnSpawn`, `OnDespawn`
- Private: `Awake`, `ApplyVisual`, `AreaRoutine` (IEnumerator), `TickArea`, `ReturnToPool`
- All Buildup byte-identical (only comments translated to English).
- Codex S-3 mojibake check: 10 fields all confirmed present as real code via grep (lines 24, 26-28, 31-36).

### PersistentAreaPool.cs ✓
- 2 Inspector fields (`_prefab`, `_initialSize`)
- 1 private field (`_available` Queue)
- Public API: `Get`, `Return`, `ReturnAll`
- Private: `Awake`, `CreateInstance`
- **`Get()` empty-path patched** per Codex pattern (only divergence from Buildup).

### PersistentAreaManager.cs ✓
- 1 Inspector field (`_pool`)
- Static `Instance`
- Public API: `Spawn(Vector3, Vector3, float, AreaShape, float, float, float, SkillStep, SkillContext)` — 9 parameters byte-identical.
- Private: `Awake` (singleton enforce)
- Warning string translated to clean ASCII (Codex S-2).

---

## ML Preservation Policy Compliance

Per SKILL_SYSTEM_DESIGN.md §10a:

| Item | Status |
|---|---|
| Public method signatures preserved | ✓ Initialize / OnSpawn / OnDespawn / SetPool / Get / Return / ReturnAll / Spawn |
| Field names preserved | ✓ all 10 SkillArea fields + 3 Pool fields + 1 Manager field |
| Component composition (MonoBehaviour pattern) | ✓ |
| GUID preservation | ✓ all 4 Buildup GUIDs |
| Forward-compat for X3 wiring (NetworkBehaviour conversion) | ✓ deferred, comments mark it |
| Coroutine-based tick loop preserved | ✓ AreaRoutine intact |

Behavioral divergence: only `PersistentAreaPool.Get()` empty-path internal flow. External observers cannot detect (queue internals private, no public read accessor).

---

## Behavior Contract After X2-8

- `IPersistentArea` / `IPoolable` interface contracts available in `ArenaCombat.Core.Skill`.
- `SkillArea` Prefab-attachable component. Tick loop runs unconditionally (X3 adds IsServer gate).
- `PersistentAreaPool` Inspector-wired pool. `Get(pos)` correctly dequeues. `Return` enqueues. `ReturnAll` iterates active children.
- `PersistentAreaManager` scene singleton, holds Inspector reference to pool, delegates `Spawn(...)` to pool + area Initialize.
- **Zero call sites** (X2-9 SkillComponents `SpawnPersistentArea` will be first user).

---

## Architectural Difference: Projectile vs Area

| Aspect | Projectile (X2-7) | Area (X2-8) |
|---|---|---|
| Pool singleton | Static `Instance` enforced | No singleton; Manager holds Inspector reference |
| Forward-compat server gate | `ShouldRunHitDetection() => true` hook in SkillProjectile | No gate; X3 adds `if (!IsServer) return;` in TickArea |
| Hit detection | `Physics.OverlapSphereNonAlloc` (16-buffer) | `Physics.OverlapSphere` (GC-allocating) |
| Tick driver | FixedUpdate (physics tick) | Coroutine with `WaitForSeconds(tickInterval)` |
| Pool initial size | 10 | 5 |
| Composition | `[RequireComponent(Rigidbody)] + [RequireComponent(Collider)]` | No RequireComponent (Inspector designer attaches collider for visualization only) |

Differences are Buildup-origin design decisions, preserved per verbatim policy. No unification attempted at X2-8.

---

## Spawned Follow-ups

- **X2-9 (NEXT)**: SkillComponents (37 SkillStep impls, 536 LOC, single file). All Projectile/Area dependencies now satisfied. Codex Round 1 will verify 37 function bodies for SkillStep signature correctness + StatManager/SkillContext API call sites.
- **X3 wiring**: convert SkillArea MonoBehaviour → NetworkBehaviour. Add `if (!IsServer) return;` at TickArea top. Add NetworkObject to SkillArea prefab. Convert PersistentAreaPool.Get/Return to use NetworkObject.Spawn/Despawn.
- **X3+ perf pass**: SkillArea Physics.OverlapSphere → NonAlloc variant.

---

## User-Side Verification (pending — user confirms in Unity)

1. Compile in Unity. Expect <5s recompile.
2. Console: 0 new error / 0 new warning.
3. Project window:
   - `Core/Skill/Interfaces/` now has 3 files (IPoolable / IProjectile / IPersistentArea).
   - `Core/Skill/Area/` new folder with 3 files (SkillArea / PersistentAreaPool / PersistentAreaManager).
4. Optional smoke test:
   - Scratch GameObject + `Add Component > Skill Area` → no RequireComponent prompts (Buildup design).
   - Scratch GameObject + `Add Component > Persistent Area Pool` → Inspector shows `_prefab` slot + `_initialSize = 5`.
   - Scratch GameObject + `Add Component > Persistent Area Manager` → Inspector shows `_pool` slot.
5. Existing 5 yellow warnings unchanged.

---

## Lessons

- **Pattern recognition pays off**: X2-7 Codex Pool.Get() critical immediately recognizable in X2-8 PersistentAreaPool.Get(). Preemptive fix in pending.md saved a Round 2. Codex confirmed the preemptive fix is appropriate.
- **Mojibake-eaten field check is a real defense**: Codex S-3 reminded to verify all 10 SkillArea fields are real code (not corrupted into comment lines). Post-write grep confirmed. This is the SkillTypes.cs / ICombatant.cs lesson from X2-2 in action.
- **Warning string clean ASCII**: Codex caught that `PersistentAreaManager 미연결` Korean original had corrupted quote characters in Buildup raw bytes. Clean English `[PersistentAreaManager] PersistentAreaPool not assigned` sidesteps the encoding hazard.
- **Architectural diversity preserved**: Projectile / Area subsystems have different patterns (singleton vs Inspector, NonAlloc vs allocating, FixedUpdate vs Coroutine). Buildup verbatim policy means we don't unify these in X2-7/8. X3 wiring round will handle NetworkBehaviour conversion in matched style.
- **Codex gate strict adherence held**: this round did pending.md → wait Codex feedback → apply. X2-6 workflow violation no longer repeating.
