# Pending Codex Review — Playtest Debug Fixes (Spin Bug + isRun Spam)

## Topic
Two small fixes uncovered during C3a playtest debugging:
1. **Spin bug** — character rotates uncontrollably when colliding with boss. Caused by Rigidbody Y-axis torque not being damped between FixedUpdates. Fix: zero out `rb.angularVelocity` at end of `ProcessServerMovement` so server-authoritative Slerp rotation isn't fighting accumulated physics torque.
2. **`Parameter 'isRun' does not exist` spam** — legacy Player.cs imported from 3DSceneScript calls `animator.SetBool("isRun", ...)` 5 times per Update tick × players. Animator Controller does not have an `isRun` parameter, so each call logs a warning (~150/sec/player). This overflows Unity Console buffer and pushes out useful `[BossAI]`/`[Archetype]` logs. Fix: gate all `SetBool("isRun", ...)` calls behind a one-time `HasParameter` check cached on Awake.

## Roadmap link
Neither item is on ROADMAP. Both are bug fixes uncovered during C3a sub-phase verification — pre-requisites for completing C3a debugging.

## Goal
After this:
- Player no longer spins when bumping into boss.
- Unity Console no longer fills with `isRun` warnings, leaving room for `[BossAI]` / `[Archetype]` / `[Bias]` logs to be visible during a 30+ second test run.

## Files to touch
- **EDIT** `Assets/ArenaCombat/Scripts/Core/Network/PlayerNetworkController3D.cs` — one line in `ProcessServerMovement` (~line 892)
- **EDIT** `Assets/ArenaCombat/3DSceneScript/Scripts/Player.cs` — add `_hasIsRunParam` flag + Awake check + guard 5 SetBool call sites

## Approach

### Fix 1: Spin bug — angularVelocity zero-out

**Symptom (user report):** "보스랑 건드치면 캐릭터가 한쪽으로 쭉 돌려고 함" (character spins one direction when touching boss).

**Root cause:**
- PNC3D Rigidbody constraints at line 282: `FreezeRotationX | FreezeRotationZ` — Y is NOT frozen
- Player rotation control is `Quaternion.Slerp(transform.rotation, targetRotation, dt * rotationLerp)` at line 891
- On collision with boss, physics engine applies Y-axis torque → Rigidbody.angularVelocity accumulates
- Slerp interpolates but cannot fully overcome accumulated angular velocity if the physics tick adds more torque each step
- Result: character spins toward physics torque direction even though Slerp pulls toward serverLookYaw

**Why B5-1 introduced this:**
Before B5-1, rotation was set to movement direction (which changes per input). The frequent direction change effectively reset accumulated torque visually. Post-B5-1, rotation tracks mouse yaw which can be stationary during collision → torque accumulates unchecked.

**Why not just `FreezeRotationY`:**
Adding `FreezeRotationY` to constraints would prevent us from rotating the player at all via the physics system. Our `transform.rotation = Slerp(...)` bypasses physics rotation, but constraint additions don't hurt — they just kill physics-driven rotation. *Could* work as alternative. But the simpler and lower-risk fix is to clear angular velocity at the end of each FixedUpdate tick. That matches existing pattern (we already write `rb.linearVelocity` directly at line 878).

**Fix:**
At end of `ProcessServerMovement` (after line 891 rotation Slerp):
```csharp
rb.angularVelocity = Vector3.zero;
```

This ensures any physics torque from collisions is reset every server FixedUpdate, so the rotation Slerp is the only source of rotation change.

### Fix 2: isRun spam — Animator parameter guard

**Symptom:** Console floods with `Parameter 'isRun' does not exist.` warnings at ~150/sec.

**Root cause:**
`Assets/ArenaCombat/3DSceneScript/Scripts/Player.cs` is a legacy 2D-derived script imported during X1. It assumes the player Animator has an `isRun` boolean parameter. Animator Controllers used in 3D scene don't.

5 call sites in Player.cs:
- Line 48: when not spawned (sets false)
- Line 65: in built-in mode update
- Line 89: `UpdateAnimatorFromNetworkState`
- Line 98: when input disabled (sets false)
- Line 118, 172: similar movement-driven sets

**Fix:**
Cache parameter existence once on Awake (when the Animator is known), gate all SetBool calls behind that flag:
```csharp
private bool _hasIsRunParam;
private static readonly int IsRunHash = Animator.StringToHash("isRun");

void Awake()
{
    animator = GetComponentInChildren<Animator>();
    networkController3D = GetComponent<PlayerNetworkController3D>();
    inputHandler = GetComponent<PlayerInputHandler>();
    mainCamera = Camera.main;
    _hasIsRunParam = animator != null && HasParam(animator, IsRunHash);
}

static bool HasParam(Animator a, int hash)
{
    foreach (var p in a.parameters)
        if (p.nameHash == hash) return true;
    return false;
}
```

Then replace every `animator.SetBool("isRun", X)` with:
```csharp
if (_hasIsRunParam) animator.SetBool(IsRunHash, X);
```

Using the cached `IsRunHash` avoids string lookups too — minor perf gain.

### Why not just delete the SetBool calls
- They might still be needed if the user later adds an `isRun` param to the Animator Controller
- Guard is cheaper than deletion + regret
- Identical runtime cost when param exists (one int compare)

## Diff sketch

### `PlayerNetworkController3D.cs:892` — add one line
```csharp
// Existing:
float targetYaw = NormalizeYaw(serverLookYaw);
Quaternion targetRotation = Quaternion.Euler(0f, targetYaw, 0f);
transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * rotationLerp);

// NEW: kill accumulated collision torque so Slerp is the only rotation driver.
rb.angularVelocity = Vector3.zero;
```

### `Player.cs` — Awake + 5 sites
```csharp
// New fields:
private bool _hasIsRunParam;
private static readonly int IsRunHash = Animator.StringToHash("isRun");

// Awake addition:
_hasIsRunParam = animator != null && HasParam(animator, IsRunHash);

// New helper:
static bool HasParam(Animator a, int hash)
{
    foreach (var p in a.parameters)
        if (p.nameHash == hash) return true;
    return false;
}

// Replace each call site (5 total):
// Before:
animator.SetBool("isRun", value);
// After:
if (_hasIsRunParam) animator.SetBool(IsRunHash, value);
```

Optional: also null-guard `animator` (some sites already do this; preserve existing null checks).

## Risks / unknowns

1. **`rb.angularVelocity = Vector3.zero` server-side only?** `ProcessServerMovement` runs only on server (the FixedUpdate gate at line 483-495 already checks IsServer). Client-side player's Rigidbody is non-authoritative — server pushes networkPosition to client. So clearing angularVelocity server-side is correct and sufficient. Client's rotation is interpolated from server target, so no client-side spinning risk.

2. **Owner-side prediction rotation at line 472**: `transform.rotation = Quaternion.Slerp(...)` in owner prediction. Owner has its own Rigidbody. If owner-side prediction is enabled, owner's local Rigidbody might also accumulate torque on collision. Should we also clear angularVelocity in the owner prediction path? My pick: yes, mirror the fix in the Update() owner branch — but only if owner has authoritative Rigidbody. Need codex confirmation on the owner-side rigidbody ownership model.

3. **`_hasIsRunParam` cached at Awake**: If Animator Controller is changed at runtime (rare in this project), the cache becomes stale. Acceptable for now; document if needed.

4. **`Animator.parameters` is GC-allocating**: called once in Awake. One-time cost, no per-frame impact. Acceptable.

5. **Other `Player.cs` SetBool calls for other parameters**: only `isRun` is flagged in console. Other parameters (if any) might also fail silently. Out of scope — fix only what spams.

6. **Boss prefab `_logAutoCast` already disabled (non-.cs change)**: I directly edited `Assets/ArenaCombat/Prefabs/Boss/Boss.prefab` to set `_logAutoCast: 0`. This is a prefab YAML edit, not .cs. Per project rules: ".asset / prefab edits with no .cs implication are out-of-scope for codex-review." Codex: please confirm this side-edit is acceptable in scope or flag if not.

## Questions for Codex

1. **Owner-prediction path `rb.angularVelocity = Vector3.zero` needed?** Player has owner-side prediction via Update() at line 472 (Quaternion.Slerp using aimRot). Does the owner-side Rigidbody accumulate physics torque, or is owner using kinematic rb at that path? My read: line 281 says `rb.isKinematic = false` always, so owner's rb is non-kinematic too. So yes, owner-side also accumulates torque. Recommend adding the same fix to owner Update() rotation block. Confirmed?

2. **Alternative: `FreezeRotationY` constraint instead of angularVelocity reset?** Cleaner conceptually (physics rotation fully disabled) but might interact unexpectedly with the Slerp (Slerp writes transform.rotation directly, which is fine — constraint only blocks physics-driven rotation). Codex preference?

3. **`Player.cs` is a 3DSceneScript-imported legacy file**. Editing it might conflict with future Buildup re-imports. Should we duplicate the script to `Core/` first and edit the copy? My pick: edit in place. The 3DSceneScript folder is the active path; Buildup re-import is not currently planned.

4. **Boss prefab `_logAutoCast: 0` direct edit**: was this acceptable as a pre-codex-review change since it's a one-line prefab toggle?

5. **Need to also handle `IsTelegraphing` for owner-side?** Owner prediction may rotate while a server-side telegraph is in progress. Likely fine since rotation is independent of skill state, but worth a sanity check.

## Out of scope for this round
- Boss spawn offset bug (40m → 74m off after restart) — separate investigation, larger fix
- Console buffer size increase (Editor preference, not code)
- Removing legacy Player.cs entirely (would break the network-bridge path)
- Phase-clobber recovery for BossAI variants (already documented follow-up)
