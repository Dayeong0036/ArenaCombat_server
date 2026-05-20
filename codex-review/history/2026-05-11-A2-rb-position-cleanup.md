# A2: rb.position Cleanup (2026-05-11)

ROADMAP item A2 — `PlayerNetworkController3D` server-path direct position writes replaced with collision-respecting `MovePosition` via local `authoritativePos`.

---

## Outcome

**Status**: APPLIED. Three Codex review rounds, final verdict APPROVED.

**Files changed**:
- `Assets/ArenaCombat/Scripts/Core/Network/PlayerNetworkController3D.cs` — `FixedUpdate` (lines 360–402) + `UpdateServerTimers` signature/body (lines 1111–1175)

**Doc updates after apply**:
- `ROADMAP.md` — A2 marked DONE, A2-followup registered as NEXT, A3 (legacy GetSpawnPosition) re-classified as DEFERRED to D1, old A3/A4 renumbered to A4/A5
- `PROJECT_STRUCTURE.md` §5.1 known-bug section updated
- Memory `project_known_bugs.md` updated to reflect resolution

**Verification grep** after apply (`rg "rb\.position|transform\.position\s*="`):
- `rb.position` references in `PlayerNetworkController3D.cs`: **0**
- `transform.position = X` remaining hits: 4, all out of A2 scope (Respawn intentional teleport, two client-smoothing paths, RespawnEventRpc client teleport)

---

## Review Cycle Summary

### Round 1 — APPROVED WITH CHANGES (3 Critical)

Round 1 proposed `rb.MovePosition` + retain `transform.position = X` writes for in-frame downstream reads, with publish from `rb.position` for the new authoritative position.

Codex critical issues:
1. CI-1: keeping `transform.position = X` weakens the MovePosition intent; use a local final-position variable instead
2. CI-2: `rb.position` synchronous read after `MovePosition` is not guaranteed by Unity 6.3 docs; publish from a local stored target instead
3. CI-3: rope arrival (line 1150) needs `ResolveServerPosition` wrap before `MovePosition` because `TryResolveRopeTarget` only does bounds clamp, not walkable validation

Codex questions:
- Q-1: refactor to local var possible?
- Q-2: A2 scope — include arrival resolve?

### Round 2 — APPROVED WITH CHANGES (1 Critical)

Round 2 adopted all three CIs: introduced local `authoritativePos`, dropped `transform.position = X`, switched publish source, expanded scope to include arrival. Used `ref Vector3 authoritativePos` parameter on `UpdateServerTimers`.

Codex critical issue:
- CI-R2-1: respawn timer path inside `UpdateServerTimers` calls `Respawn(spawnPos)` which writes `transform.position`, but the `ref` `authoritativePos` is not updated → publish would emit OLD position (regression)

Codex preference:
- S-R2-3: prefer return value over `ref Vector3` for explicit "position gets updated" semantics at the call site

### Round 3 — APPROVED

Round 3 adopted both: switched `UpdateServerTimers` to return-style (`Vector3 UpdateServerTimers(Vector3 authoritativePos)`), and added `authoritativePos = transform.position;` after the `Respawn(spawnPos)` call to capture the post-spawn position.

Codex final notes:
- Direction is correct
- Trade-off: publishing intent before physics simulation accepted as A2 minimum scope
- Suggested grep verification after apply (executed, see above)

---

## Spawned Follow-ups

1. **ROADMAP A2-followup** (NEXT): `lastValidatedServerPosition` is updated at line 381 BEFORE `UpdateServerTimers`, so rope step's second arg to `ResolveServerPosition` is one-fixed-step stale. Whether this manifests as the original "rope re-pushes outside bounds" issue requires runtime verification.

2. **Phase D1**: legacy `PlayerNetworkController.GetSpawnPosition()` Vector3.up*5 cleanup as part of legacy 2D removal.

3. **Possible later**: `Respawn()` at line 1055 still writes `transform.position = position` directly. Intentional teleport via `GetSafeSpawnPoint`, low collision risk. Codex confirmed deferral acceptable.

---

## Final Code State (key region)

`FixedUpdate`:
```csharp
Vector3 authoritativePos = transform.position;
// ... bounds clamp uses MovePosition + updates authoritativePos ...
authoritativePos = UpdateServerTimers(authoritativePos);
networkPosition.Value = authoritativePos;
```

`UpdateServerTimers(Vector3 authoritativePos) → Vector3`:
- Respawn timer: after `Respawn(spawnPos)` → `authoritativePos = transform.position;`
- Rope arrival: `ResolveServerPosition` → `MovePosition(arrivalPos)` → `authoritativePos = arrivalPos;`
- Rope step: `MovePosition(next)` → `authoritativePos = next;`
- Returns final `authoritativePos`

---

## Lessons

- Codex caught two distinct refactor-introduced bugs (CI-2 publish source, CI-R2-1 respawn omission) that pure code inspection might have missed without the round-trip protocol.
- Return-style is meaningfully safer than `ref` for "this function may update X" patterns — call site forces author to think about the return value.
- "Memory says X" claims need verification against current code: original memory listed bug as in `PlayerNetworkController.GetSpawnPosition`, but actual fix scope was in `PlayerNetworkController3D.FixedUpdate` + rope movement (different file/method entirely).
