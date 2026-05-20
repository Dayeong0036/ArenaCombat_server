# B6-1: Match End UI + In-Place Restart

## Verdict: R3 APPROVED WITH CHANGES (all issues addressed in implementation)

## 3 review rounds
- R1 REJECTED: 5 critical (local victory check, duplicate event, minPlayers bypass, sender auth, 1-player edge)
- R2 REJECTED: 3 critical (NV race, reason-before-validation, unconsolidated paths)
- R3 direction approved, 3 remaining (MatchEnd gameplay blocking, reason=None fallback, using missing)

All 11 total critical issues resolved in final implementation.

## Changes Applied

### NetworkConstants.cs
- `MatchEndReason` enum: None/BossDefeated/AllPlayersDead

### GameStateManager.cs
- `networkMatchEndReason` NV (server-write, everyone-read)
- `CurrentMatchEndReason` + `NetworkMatchEndReason` properties
- `EndMatch(MatchEndReason reason = None)` — replaces old `EndMatch()`, adds IsValidTransition guard before reason write
- `RequestRestartRpc()` — SendTo.Server, RequireOwnership=false, MatchEnd state guard
- `RestartMatch()` — DespawnBoss → player snapshot (ClearAll+SetAutoCast+Respawn) → reason reset → ResetCardDraft → WaitingForPlayers → StartMatchCountdown
- `ResetToWaiting()` — added reason NV reset
- `using ArenaCombat.Core.Skill;` added

### BossManager.cs
- `HandleBossDefeated`: `TransitionToState(MatchEnd)` → `EndMatch(BossDefeated)`

### CombatManager3D.cs
- `AreAllPlayersDead()` — `players3D.Count < 2` guard + foreach IsAlive check
- `GetAllPlayersSnapshot()` — safe List copy
- `OnPlayerDeath3D` — calls `GSM.EndMatch(AllPlayersDead)` when all dead

### PlayerNetworkController3D.cs
- `IsServerGameplayBlockedByCardDraft()` — extended to also block in MatchEnd state
- `UpdateServerTimers` respawn timer — paused during MatchEnd

### NEW: MatchEndUI.cs
- Dual NV subscription (OnMatchStateChanged + networkMatchEndReason.OnValueChanged)
- `RefreshPanel()`: only shows when BOTH state==MatchEnd AND reason!=None (eliminates race)
- Cached GSM reference for safe unsubscribe
- `OnRestartClicked()` → `GSM.RequestRestartRpc()`

## Scene work remaining
- Chapter1.unity: Add MatchEndPanel GO under Main Canvas (panel + TMP_Text + Button + MatchEndUI component)
