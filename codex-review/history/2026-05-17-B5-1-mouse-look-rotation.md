# B5-1: Mouse Look Rotation Fix

## Verdict: APPROVED WITH CHANGES (all 4 critical issues resolved)

## Changes Applied

**File:** `Assets/ArenaCombat/Scripts/Core/Network/PlayerNetworkController3D.cs`

### 1. ProcessServerMovement() — Always use serverLookYaw
- Removed: `targetYaw = Atan2(moveInput.x, moveInput.y)` when moving
- State (Moving/Idle) set independently of rotation
- Rotation always follows `NormalizeYaw(serverLookYaw)`

### 2. Update() — Owner-side immediate rotation
- Owner applies `cachedLookYaw` directly (no NV round trip)
- Guarded by same conditions server uses to reject input:
  `networkIsAlive && CanMove(statusMask) && !isRoping && !cardDraft`
- Non-owner and BT agents fall back to `InterpolateRotation()`

### 3. RequestMoveRpc — RpcParams sender validation + yaw normalize
- Added `RpcParams rpcParams = default` parameter
- `rpcParams.Receive.SenderClientId != OwnerClientId` → reject
- `lookYaw = NormalizeYaw(lookYaw)` after sanitize

### 4. NormalizeYaw() helper
- `static float NormalizeYaw(float yaw)` → [-180, 180] range
- Used in ProcessServerMovement, RequestMoveRpc, and owner Update prediction

## Codex Review Critical Issues (all addressed)
1. ~~Extra closing brace in Update~~ → fixed (correct nesting)
2. ~~Owner prediction ignores server-reject states~~ → added full guard
3. ~~No yaw normalization~~ → NormalizeYaw helper applied everywhere
4. ~~No RpcParams sender check~~ → added to RequestMoveRpc
