# SYNC-1/2 — Projectile & Area Client Visual Sync

**Date**: 2026-05-20
**Scope**: SkillProjectile.cs, SkillArea.cs
**Risk**: Medium (adds network components + RPC to pooled NetworkObjects)

## Problem

On the joining client (non-host), skill visual effects don't replicate:
1. **Projectile**: Server sets `rb.linearVelocity` in `Launch()` but no position sync to client — client sees nothing moving
2. **SkillArea**: Server calls `Initialize()` → `ApplyVisual()` but visual params (scale, rotation) are server-local — client sees default-scale object

## Solution

### SYNC-1: SkillProjectile (Position sync via NetworkTransform)

**SkillProjectile.cs:**
1. Added `using Unity.Netcode.Components;`
2. Added `[RequireComponent(typeof(NetworkTransform))]` — Unity auto-adds NetworkTransform to prefab
3. Added `OnNetworkSpawn()` override — sets client Rigidbody to kinematic (prevents client physics from fighting NetworkTransform position updates)

**Prefab requirement:** User must verify NetworkTransform component is on the prefab with:
- Position sync: ON
- Rotation sync: OFF (projectile rotation not gameplay-relevant)
- Scale sync: OFF
- Interpolation: ON

Flow: Server `Launch()` → `rb.linearVelocity` moves projectile → NetworkTransform replicates position → client sees movement

### SYNC-2: SkillArea (Visual params via RPC)

**SkillArea.cs:**
1. Added `[Rpc(SendTo.NotServer)] ApplyVisualRpc(Vector3 forward, float radius, int shape, float angleDeg)`
2. In `Initialize()`, after `ApplyVisual()`, server calls `ApplyVisualRpc()` to broadcast visual params
3. Client receives RPC → stores params → calls `ApplyVisual()` → correct scale/rotation/color

Using `SendTo.NotServer` (not ClientsAndHost) because host already ran `ApplyVisual()` in `Initialize()`.

## Review Checklist

- [ ] NetworkTransform RequireComponent: Unity 6.3 `Unity.Netcode.Components.NetworkTransform` namespace accessible
- [ ] Client kinematic: `_rb.isKinematic = true` on client prevents physics drift, doesn't affect server hit detection
- [ ] RPC signature: `int shape` instead of `AreaShape` enum — NGO 2.x RPC requires serializable types (int is safe)
- [ ] SendTo.NotServer: avoids double ApplyVisual on host (already called in Initialize)
- [ ] Pooled object lifecycle: NetworkTransform state resets on Despawn/re-Spawn correctly in NGO 2.x
- [ ] No interference with server-only hit detection: `ShouldRunHitDetection()` and `TickArea()` still gated by `IsServer`
