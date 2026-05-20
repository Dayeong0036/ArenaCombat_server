# X2-7: Projectile Subsystem Paired (2026-05-12)

ROADMAP item Phase X2-7 (reordered from original X2-8 position on 2026-05-12). Seventh X2 sub-cycle. Paired full import (4 files, ~207 LOC) — circular dep between `SkillProjectile` and `ProjectilePool` forces single-round import.

---

## Outcome

**Status**: APPLIED + **Codex APPROVED WITH CHANGES** (1 critical fix applied).

**Operations**:
- 2 NEW folders (`Core/Skill/Interfaces/`, `Core/Skill/Projectile/`) with fresh GUIDs `b90152db…` and `7463dc0c…`.
- 4 NEW `.cs` + 4 NEW `.meta` (all Buildup GUIDs preserved).
- 1 Codex critical fix to `ProjectilePool.Get()` (double-enqueue bug in Buildup origin).

**Files touched**:
- NEW `Assets/ArenaCombat/Scripts/Core/Skill/Interfaces.meta` (folder)
- NEW `Assets/ArenaCombat/Scripts/Core/Skill/Projectile.meta` (folder)
- NEW `Assets/ArenaCombat/Scripts/Core/Skill/Interfaces/IPoolable.cs` + `.meta` (Buildup GUID `89c42833…`)
- NEW `Assets/ArenaCombat/Scripts/Core/Skill/Interfaces/IProjectile.cs` + `.meta` (Buildup GUID `89b4713f…`)
- NEW `Assets/ArenaCombat/Scripts/Core/Skill/Projectile/SkillProjectile.cs` + `.meta` (Buildup GUID `d5c6fbf5…`)
- NEW `Assets/ArenaCombat/Scripts/Core/Skill/Projectile/ProjectilePool.cs` + `.meta` (Buildup GUID `f3ee6d1f…`)

**Doc updates**:
- ROADMAP X2-7 → DONE (with Codex critical fix note), X2-8 (Area subsystem paired) → NEXT.
- TARGET_ARCHITECTURE.md §10 X2-7 row marked done with Pool.Get fix note; X2-8 promoted.

---

## Codex Critical Fix — ProjectilePool.Get() Double-Enqueue Bug

**Buildup origin defect** (line 26-33):
```csharp
public SkillProjectile Get(Vector3 position, Quaternion rotation)
{
    var obj = _available.Count > 0 ? _available.Dequeue() : CreateInstance();
    obj.transform.SetPositionAndRotation(position, rotation);
    obj.gameObject.SetActive(true);
    obj.OnSpawn();
    return obj;
}

private SkillProjectile CreateInstance()
{
    var obj = Instantiate(_prefab, transform);
    obj.SetPool(this);
    obj.gameObject.SetActive(false);
    _available.Enqueue(obj);   // <-- enqueues
    return obj;                // <-- but Get() returns this without dequeueing
}
```

**The bug**: when `_available.Count == 0`:
1. `Get()` calls `CreateInstance()`
2. `CreateInstance()` creates obj, enqueues it, returns it
3. `Get()` activates obj + `OnSpawn` + returns it as active
4. **obj is now active AND in `_available` queue**
5. Next `Get()` dequeues this same active obj, calling `OnSpawn` again — corrupts state
6. Caller now has two references to the same active projectile

This would have caused intermittent "phantom" duplicate-projectile and reset-state bugs at runtime.

**Codex-recommended fix** (applied):
```csharp
public SkillProjectile Get(Vector3 position, Quaternion rotation)
{
    if (_available.Count == 0)
        CreateInstance();
    var obj = _available.Dequeue();
    obj.transform.SetPositionAndRotation(position, rotation);
    obj.gameObject.SetActive(true);
    obj.OnSpawn();
    return obj;
}
// CreateInstance() unchanged — always enqueues.
```

Symmetric: `CreateInstance()` always enqueues, `Get()` always dequeues. ML preservation policy intact (`Get` / `CreateInstance` / `Return` / `ReturnAll` signatures unchanged; only `Get` empty-path body changes).

**DIVERGENCE FROM BUILDUP** explicitly noted in ProjectilePool.cs header comment block.

---

## Codex Non-Blocking Suggestions (all addressed)

1. **Folder structure OK** → confirmed Buildup mirror.
2. **`ShouldRunHitDetection() => true` X2-7 OK** → kept; X3 wiring flips to IsServer.
3. **MonoBehaviour OK** → kept; NetworkBehaviour conversion at X3.
4. **`_overlapBuffer[16]` ceiling** → kept; X3 / balance phase may bump if boss arena exceeds 16 simultaneous targets.
5. **`_targetMask = -1` default** → noted for X3 wiring setup doc: "projectile target layer must be set per prefab".
6. **`GetComponentInParent<ICombatant>()` finds nothing until X3** → confirmed; no callers exist yet (SkillComponents X2-9 first user).

---

## Type Surface Verification

### IPoolable.cs (5 LOC) ✓
- `OnSpawn()` / `OnDespawn()` — Buildup byte-identical.

### IProjectile.cs (7 LOC) ✓
- `Launch(Vector3, float, float)` / `SetHitCallback(SkillStep, SkillContext, bool)` — Buildup byte-identical.
- Inherits `IPoolable`.

### SkillProjectile.cs (133 LOC) ✓
- `[RequireComponent(Rigidbody)]` + `[RequireComponent(Collider)]`
- 3 inspector fields (`_color`, `_detectionRadius`, `_targetMask`)
- Static `_overlapBuffer[16]`
- Public: `SetPool`, `Launch`, `SetHitCallback`, `OnSpawn`, `OnDespawn`
- Private: `Awake`, `FixedUpdate`, `ShouldRunHitDetection`, `ApplyHit`, `ReturnToPool`
- All Buildup byte-identical (only comments translated to English).

### ProjectilePool.cs (~62 LOC after fix) ✓
- Static `Instance`
- `[SerializeField] _prefab` + `_initialSize = 10`
- `Queue<SkillProjectile> _available`
- Public: `Get`, `Return`, `ReturnAll`
- Private: `Awake`, `CreateInstance`
- **`Get()` empty-path patched** per Codex critical (only divergence from Buildup).

---

## ML Preservation Policy Compliance

Per SKILL_SYSTEM_DESIGN.md §10a:

| Item | Status |
|---|---|
| Public surface (Launch/SetHitCallback/Get/Return/ReturnAll/OnSpawn/OnDespawn) | ✓ byte-identical |
| Field names (`_prefab`/`_available`/`_color`/`_detectionRadius`/`_targetMask`/...) | ✓ byte-identical |
| Component composition (Rigidbody + Collider RequireComponent) | ✓ |
| GUID preservation | ✓ all 4 Buildup GUIDs |
| MonoBehaviour pattern (deferred NetworkBehaviour to X3) | ✓ |
| Forward-compat hook (`ShouldRunHitDetection`) | ✓ kept as-is |

Behavioral divergence: only `Get()` empty-path internal flow. External observers (BossObservationCollector at X4-N) cannot observe the difference (queue internals are private, no public read accessor for `_available.Count`).

---

## Behavior Contract After X2-7

- `IProjectile` / `IPoolable` interface contracts defined in `ArenaCombat.Core.Skill`.
- `SkillProjectile` Prefab-attachable component. Run hit detection client-side (X3 flips to IsServer).
- `ProjectilePool` scene singleton. `Get(pos, rot) → SkillProjectile` correctly dequeues on empty path. `Return` enqueues. `ReturnAll` iterates active children.
- **Zero call sites** (X2-9 SkillComponents `LaunchProjectile` will be the first user).

---

## Spawned Follow-ups

- **X2-8 (NEXT)**: Area subsystem (IPersistentArea + SkillArea + PersistentAreaPool + PersistentAreaManager) paired. Same circular dep pattern, ~244 LOC. Watch for analogous double-enqueue defect in PersistentAreaPool.Get() (Codex flagged via pattern hint).
- **X3 PNC3D wiring**: convert SkillProjectile MonoBehaviour → NetworkBehaviour. `ShouldRunHitDetection()` returns `NetworkManager.Singleton.IsServer`. Add NetworkObject component to projectile prefab. Wire `ProjectilePool` to call `NetworkObject.Spawn` / `Despawn` instead of `gameObject.SetActive`.
- **X3 setup doc note**: "Projectile prefab `_targetMask` MUST be set per-prefab" (Codex S-5).
- **Possible bump**: if boss arena exceeds 16 simultaneous targets in OverlapSphere, raise `OverlapBufferSize` const (Codex S-4).

---

## User-Side Verification (pending — user confirms in Unity)

1. Compile in Unity. Expect <5s recompile.
2. Console: 0 new error / 0 new warning.
3. Project window:
   - `Core/Skill/Interfaces/` new folder with `IProjectile.cs` + `IPoolable.cs`.
   - `Core/Skill/Projectile/` new folder with `SkillProjectile.cs` + `ProjectilePool.cs`.
4. Optional smoke test:
   - Scratch GameObject + `Add Component > Skill Projectile` → Rigidbody + Collider auto-required.
   - Scratch GameObject + `Add Component > Projectile Pool` → Inspector shows `_prefab` slot + `_initialSize = 10`.
5. Existing 5 yellow warnings unchanged.

---

## Lessons

- **Buildup origin can have bugs**: blindly preserving "byte-identical" doesn't equal preserving correct behavior. Pool.Get() defect would have shipped if not for Codex's eyes-on review. Lesson: ML preservation policy applies to public surface + field names, but internal correctness is still our responsibility.
- **Codex critical-found vs Buildup-verbatim trade-off**: clearly document the divergence (header comment block) so future readers see why we differ. Done in ProjectilePool.cs header.
- **Paired import pattern scales beyond X2-4**: X2-7 4-file pair worked as cleanly as X2-4 2-file pair. The circular-dep cue (`SkillProjectile holds ProjectilePool _pool` + `ProjectilePool holds Queue<SkillProjectile>`) is a clear signal for pair detection.
- **Static `_overlapBuffer[16]` shared across instances**: Unity single-threaded FixedUpdate makes this safe. Buildup pattern. Worth noting for X4-N ML observation (collisions silently truncated above 16 — affects training if boss skills hit many targets).
- **Codex caught what byte-identical preservation would have missed**: this is the value of mandatory gate. X2-6 retroactive learning paid off in X2-7 strict gate adherence.
