# Pending Codex Review — BF-1 B Followups Cleanup

## Topic
Phase B Followups: 3 outstanding items from B1 closure.

## Roadmap link
- **Phase B Followups** (B2/B3 시작 전 처리 권장)

## Changes

### BF-1a: Kill-zone scored death
**File:** `Assets/ArenaCombat/Scripts/Core/Network/PlayerNetworkController3D.cs`

**Current** (line ~496-499):
```csharp
if (MapBounds3D.Instance.IsBelowKillZone(authoritativePos))
{
    Respawn(MapBounds3D.Instance.GetRespawnPointNear(lastValidatedServerPosition));
    return;
}
```

**Change to:**
```csharp
if (MapBounds3D.Instance.IsBelowKillZone(authoritativePos))
{
    Die(OwnerClientId);
    return;
}
```

**Effect:**
- Kill-zone fall now calls `Die(OwnerClientId)` (self-kill, no K/D credit to other player)
- Triggers `CombatManager3D.OnPlayerDeath3D` → deaths++ counter
- `Die()` sets `respawnTimer = respawnTime` → `FixedUpdate` countdown → `Respawn()` called automatically when timer expires
- `DeathEventRpc` fires → client-side death VFX/UI possible

**Verify:** `Die()` already handles HP=0, alive=false, queue clear, collider disable, velocity zero, respawnTimer set. The FixedUpdate respawn path must exist — check that respawnTimer countdown calls Respawn when expired.

### BF-1b: appliedStatus warning once
**File:** `Assets/ArenaCombat/Scripts/Core/Network/CombatManager3D.cs`

Find the `appliedStatus != StatusMask.None` warning in TryProcessAttack3D. Change from per-attack warning to first-time-only using a HashSet.

### BF-1c: Status duration model — DEFER
The source-tagged status duration model requires design decisions (which statuses have duration, stacking rules, cleanse mechanics). This is architectural work that belongs in B2/B3, not a simple followup. Mark as DEFERRED in roadmap.

## Verification

### BF-1a
- [ ] `Die(OwnerClientId)` called on kill-zone, not `Respawn()`
- [ ] Respawn still happens after respawnTimer expires (check FixedUpdate for respawnTimer countdown → Respawn call)
- [ ] Self-kill: `killerId == OwnerClientId` → no kills++ for anyone (verify in CombatManager3D.OnPlayerDeath3D)

### BF-1b
- [ ] Warning fires only once per AttackType with non-None appliedStatus
- [ ] No per-frame log spam
