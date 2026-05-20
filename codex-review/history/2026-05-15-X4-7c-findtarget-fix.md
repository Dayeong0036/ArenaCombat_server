---
round: X4-7c
title: "SkillManager.FindNearestTarget opposing team fix + GameManager player registration"
date: 2026-05-15
---

# X4-7c: Boss targeting fix — FindNearestTarget searches opposing team

## Problem

`SkillManager.FindNearestTarget()` always searched `_gameManager.Bosses`. When the boss entity's SkillManager calls this, it finds itself as a target and attacks itself. Additionally, `GameManager.RegisterPlayer()` was never called, so the Players list was always empty.

## Changes

### 1. SkillManager.cs (`Core/Skill/Core/`)
- `FindNearestTarget()`: Now checks `_statManager.Kind == CombatantKind.Boss` to decide search list. Boss → searches `_gameManager.Players`. Player → searches `_gameManager.Bosses`.
- `Awake()`: Added `if (_gameManager == null) _gameManager = GameManager.Instance;` fallback (boss prefab has no Inspector wiring for GameManager).

### 2. PlayerNetworkController3D.cs (`Core/Network/`)
- `OnNetworkSpawn` server block: Added `GameManager.Instance.RegisterPlayer(gameObject)` after existing CombatManager registrations.
- `OnNetworkDespawn` server block: Added `GameManager.Instance.UnregisterPlayer(gameObject)` cleanup.

## Verification Checklist

1. SkillManager.cs uses `CombatantKind.Boss` from `ArenaCombat.Core.Stats` (already imported at line 7)
2. SkillManager.cs uses `_gameManager.Players` (IReadOnlyList<GameObject>) — same pattern as `_gameManager.Bosses`
3. PNC3D already has `using ArenaCombat.Core;` (line 7) for GameManager access
4. Register is in IsServer block (server authority — matches existing CombatManager pattern)
5. Unregister is also in IsServer block with null check
6. No changes to GameManager.cs itself
