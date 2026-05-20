# X0-4: Team Assignment Wiring (2026-05-11)

ROADMAP item Phase X0-4 / Phase B Followup #1. Smallest single-cycle change in B1+ era — single file, ~12 lines added.

---

## Outcome

**Status**: APPLIED. One Codex review round, APPROVED first pass.

**Files modified**:
- `Assets/ArenaCombat/Scripts/Core/Network/PlayerSpawnManager.cs` — added `defaultPlayerTeam` SerializeField + `SetTeam` call after `SpawnAsPlayerObject` + null-warning + log enrichment.

**Doc updates**:
- `ROADMAP.md` — Phase B Followup #1 marked DONE with damage-test note, X0-4 marked DONE.

---

## Review Cycle Summary

### Round 1 — APPROVED (no Critical)

Single round. Codex suggestions (all non-blocking, all adopted in this commit):
- S-1: Warning on null controller — adopted (`Debug.LogWarning` with config troubleshooting hint).
- S-2: Default `TeamId.Team1` confirmed correct for 2P co-op.
- S-3: Header block + log format both fine.
- S-4: Damage-test procedure documented in ROADMAP entry (`defaultPlayerTeam = TeamId.None` Inspector toggle OR manual Team2 reassignment for testing damage flow).

Codex notes:
- SetTeam after Spawn is correct (NetworkVariable post-spawn write is valid in NGO 2.x).
- PNC3D.OnNetworkSpawn → CombatManager3D.Register → SetTeam ordering is fine because registry stores controller reference and reads `Team` at attack time, not at register time.
- PlayerSpawnManager-side decision keeps coupling minimal vs PNC3D-side which would require team source injection.

---

## Final Code Shape

### `PlayerSpawnManager.cs` config field

```csharp
[Header("=== Team Assignment (Phase X0-4 / Phase B Followup #1) ===")]
[Tooltip("Team assigned to all spawned players. 2P co-op uses Team1; boss spawner (Phase X4) will use Team2. Set to TeamId.None to disable team assignment (legacy behavior with friendly fire — useful for damage-flow testing).")]
[SerializeField] private TeamId defaultPlayerTeam = TeamId.Team1;
```

### `PlayerSpawnManager.SpawnPlayer` post-spawn block

```csharp
networkObject.SpawnAsPlayerObject(clientId);
spawnedPlayers[clientId] = networkObject;

// X0-4: assign team after spawn so CombatManager3D friendly-fire filter works.
// SetTeam is server-only; PlayerSpawnManager runs server-authority spawn so this is safe.
PlayerNetworkController3D controller = playerInstance.GetComponent<PlayerNetworkController3D>();
if (controller != null)
{
    controller.SetTeam(defaultPlayerTeam);
}
else
{
    Debug.LogWarning($"[PlayerSpawnManager] PlayerNetworkController3D not found on spawned player for client {clientId}; team assignment skipped. Check enforce3DController/requirePrefabHas3DController config.");
}

Debug.Log($"[PlayerSpawnManager] Player spawned for client {clientId} at {spawnPosition} team={defaultPlayerTeam}");
```

---

## Behavior Contract After X0-4

- Every player spawned by `PlayerSpawnManager.SpawnPlayer(ulong clientId)` immediately gets `TeamId.Team1` (or whatever Inspector default).
- `CombatManager3D.cs:296` friendly-fire filter `attacker.Team != TeamId.None && attacker.Team == target.Team` now evaluates true → skip → friendly fire blocked.
- Boss (when arriving in X4) will spawn from a separate spawner that assigns `TeamId.Team2`. Player vs boss continues to work (different teams).
- Damage-flow testing path: Inspector flip `defaultPlayerTeam = TeamId.None` OR manually reassign one player to Team2 via runtime call.

---

## Spawned Follow-ups

None. X0-4 closes Phase B Followup #1 cleanly. Damage testing procedure captured in ROADMAP entry.

---

## Lessons

- **Smallest cycle in months**: single file, 12 lines, one Codex round. The pattern when prep work was already done (TeamId enum + SetTeam method already existed from B1 work) — the actual wiring is small. Worth doing precursor work in earlier phases to make followup wiring lightweight.
- **`null` check + warning pattern**: when a field can be missing due to permissive enforcement config, `if (X != null) { use; } else { LogWarning("config hint"); }` beats silent skip. The warning gives the troubleshooting hint inline.
- **Codex confirmation of ordering invariants** (register-before-SetTeam ordering) saved a "does this race?" question. Codex's reasoning that "registry holds reference, reads Team at attack time" is the kind of cross-component reasoning that benefits from external review.

---

## What Comes Next

Phase X0-5 (ICombatant interface stub) — small interface definition in new namespace. Sets up for X1 prefab import where Buildup `ICombatant` fields exist.
