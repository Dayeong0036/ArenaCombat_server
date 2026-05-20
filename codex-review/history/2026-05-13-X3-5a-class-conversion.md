# X3-5a: SkillProjectile / SkillArea Class Conversion — 2026-05-13

**Status**: APPLIED. Codex Round 1 APPROVED WITH CHANGES (1 critical + 5 suggestion).

Codex C-1 forced split into X3-5a (class conversion) + X3-5b (pool spawn/despawn). This round = 5a only.

## Edits

### SkillProjectile.cs
- `using Unity.Netcode;` added
- `[RequireComponent(typeof(NetworkObject))]` added
- `MonoBehaviour, IProjectile` → `NetworkBehaviour, IProjectile`
- `ShouldRunHitDetection() { return true; }` → `=> IsServer`
- Header comment updated (X3-5a state)

### SkillArea.cs
- `using Unity.Netcode;` added
- `[RequireComponent(typeof(NetworkObject))]` added
- `MonoBehaviour, IPersistentArea` → `NetworkBehaviour, IPersistentArea`
- `TickArea()`: `if (!IsServer) return;` first line
- Header comment updated (X3-5a state)

## Codex Critical Applied

- **C-1 split into 5a/5b**: pool spawn/despawn lifecycle + prefab registration deferred to X3-5b. This round just opens server-authority gate at the entity level; pool still uses Buildup Instantiate/SetActive pattern. Client visibility limited until 5b.

## Codex Suggestions Applied

- **S-1 ShouldRunHitDetection => IsServer**: applied (correct wiring point).
- **S-2 SkillArea TickArea IsServer gate**: applied.
- **S-3 Pool LogWarning + return** (X3-5b scope).
- **S-4 No NetworkTransform RequireComponent**: not added (per-prefab decision).
- **S-5 Designer setup doc**: source header + ROADMAP X3-5a entry both note NetworkObject + NetworkPrefabs registration + optional NetworkTransform.
- **S-6 PersistentAreaManager.Spawn IsServer caller contract** (X3-5b scope).

## Verification (grep)

- SkillProjectile.cs: `NetworkBehaviour, IProjectile` ✓ / `[RequireComponent(typeof(NetworkObject))]` ✓ / `ShouldRunHitDetection() => IsServer` ✓
- SkillArea.cs: `NetworkBehaviour, IPersistentArea` ✓ / `[RequireComponent(typeof(NetworkObject))]` ✓ / `TickArea()` IsServer guard ✓

## Behavior

- Host: hit detection / TickArea runs as before (IsServer = true)
- Client: hit detection / TickArea early-return (no spurious damage)
- Pool still creates local-only instances (no NetworkObject.Spawn yet) — X3-5b enables network spawn

## Spawned Follow-up

**X3-5b NEXT**: ProjectilePool / PersistentAreaPool Get → NetworkObject.Spawn, Return → NetworkObject.Despawn(false). Prefab authoring contract docs. PersistentAreaManager.Spawn IsServer caller guard. Smoke verification after prefab wiring.

## User Verification

Unity recompile. No Inspector change (RequireComponent auto-adds NetworkObject if missing — Unity will prompt for existing prefab; or designer adds manually). No MCP verification critical.
