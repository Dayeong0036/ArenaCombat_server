# X3-5b: Pool NGO Spawn/Despawn Lifecycle — 2026-05-13

**Status**: APPLIED. Codex Round 1 APPROVED WITH CHANGES (3 critical + 4 suggestion, all applied).

## Edits (4 files, ~50 LOC net)

### ProjectilePool.cs
- `using Unity.Netcode;` added
- `IsServerContext` private static helper (NetworkManager.Singleton + IsServer check)
- Get: server-only guard (warn + return null), `NetworkObject.Spawn()` with null + IsSpawned checks
- Return: server-only guard, `NetworkObject.Despawn(false)` with null + IsSpawned checks
- ReturnAll: server-only guard at top
- Header comment updated (X3-5b state, NetworkPrefabs registration requirement)

### PersistentAreaPool.cs
- Same pattern as ProjectilePool (IsServerContext + Spawn/Despawn guards + ReturnAll guard)

### PersistentAreaManager.cs
- `using Unity.Netcode;` added
- Spawn: server-only caller contract guard (warn + return on client) — Codex S-6
- Spawn: null check on `_pool.Get(position)` result

### SkillComponents.cs
- LaunchProjectile (#32): null check on `ProjectilePool.Instance.Get()` result — Codex critical 2 (NRE prevention)

## Codex Critical Applied

- **C-1 IsServerContext via NetworkManager.Singleton**: pools are MonoBehaviour, not NetworkBehaviour. Cannot use `IsServer` directly. Helper pattern adopted.
- **C-2 Get null handling**: PersistentAreaManager.Spawn + SkillComponents.LaunchProjectile both added null checks. Pool returns null on client; callers must handle.
- **C-3 NetworkObject lifecycle guards**: `no == null` check before Spawn, `no != null && no.IsSpawned` check before Despawn. Prevents NRE / state drift exceptions.

## Codex Suggestions Applied

- **S-1 Client pre-fill waste accepted**: not gated. Acceptable per Codex.
- **S-2 Manager warn vs throw**: warn + return chosen.
- **S-3 Despawn(false) confirmed correct**: keeps server's GameObject for reuse.
- **S-4 Re-Spawn smoke test deferred**: noted as user verification (Play mode host match). Not automated in this round per token economy.

## Surface Verification

- ProjectilePool: `IsServerContext` 1 + `NetworkObject` 4 + `Despawn(false)` 1 = 11 hits ✓
- PersistentAreaPool: 9 hits ✓
- SkillComponents.LaunchProjectile: `if (proj == null) return;` added ✓
- PersistentAreaManager.Spawn: 2 guards (IsServer + null pool result) ✓

## Designer Setup (required for runtime)

- SkillProjectile prefab: NetworkObject component + registered in NetworkManager.NetworkPrefabs
- SkillArea prefab: NetworkObject component + registered in NetworkManager.NetworkPrefabs
- ProjectilePool / PersistentAreaPool / PersistentAreaManager GameObjects placed in match scene with prefab refs wired
- (Optional) NetworkTransform on prefabs for client-side interpolation

## NGO Pool Contract

- **Server**: Awake pre-fills local un-spawned instances. Get → Spawn → networks to clients. Return → Despawn(false) → un-networks but keeps server instance for reuse. Re-Spawn supported in NGO 2.x.
- **Client**: Awake pre-fill creates local idle instances (waste, no networking impact). Real projectile/area spawned via NGO replication from server's Spawn. Pool.Get/Return guarded — early return.

## User Verification (smoke test)

Required to validate re-spawn pattern:
1. Play mode → Host start
2. Trigger LaunchProjectile (or SpawnPersistentArea) via skill cast or test script
3. Wait for projectile range expiry → Return → Despawn
4. Trigger same projectile again → Re-Spawn from pool entry
5. Verify both clients see both spawns + no NRE

Not automated this round (Play mode + skill cast trigger required). X3-7 end-to-end smoke test will exercise this naturally.

## Spawned Follow-up

- **X3-6 NEXT**: CardManager 4 LEGACY patterns → GSM RPC routing.
- **X3-7**: SkillManager auto-cast end-to-end smoke test (will validate X3-5b pool re-spawn).
- **Future**: ProjectilePool / PersistentAreaPool client pre-fill skip (perf optimization, low priority).
