# Codex Review Feedback - X5-e BossManager GSM race condition fix

## Findings

- **Medium - cleanup still depends on the current singleton instead of the subscribed instance.** `BossManager.TrySubscribeGSM()` subscribes to `GameStateManager.Instance` and records only `_subscribedToGSM` (`Assets/ArenaCombat/Scripts/Core/Network/BossManager.cs:44-50`), while `OnDisable()` unsubscribes from whatever `GameStateManager.Instance` happens to be at disable time (`BossManager.cs:57-61`). If the original GSM despawns or clears/replaces `Instance` before `BossManager.OnDisable()` runs, the handler remains attached to the old GSM. This can leave a disabled/destroyed BossManager receiving future `OnMatchStateChanged` callbacks if that GSM object survives a network shutdown/restart, and it can also cause duplicate subscriptions after re-enable. This is especially plausible because `GameStateManager.OnNetworkDespawn()` clears `Instance` (`GameStateManager.cs:156-169`). Recommended fix: cache the exact subscribed object, e.g. `_subscribedGSM`, subscribe/unsubscribe against that reference, and clear it in `OnDisable()`. If GSM replacement while BossManager remains enabled is a supported flow, `Update()` should also detect `_subscribedGSM != GameStateManager.Instance` and resubscribe.

## Notes

- The retry/catch-up logic is otherwise directionally correct. Polling from `Update()` until `GameStateManager.Instance` exists fixes the scene init order race, and the catch-up path is idempotent because it checks `_spawnedBoss == null` and `TrySpawnBoss()` also enforces the server-only guard.

- I do not see a C# thread-safety problem with NGO here. `MonoBehaviour.Update`, NGO `NetworkVariable.OnValueChanged`, and the GSM event invocation run on Unity's main thread in this code path, so `_subscribedToGSM` does not need locking or `volatile`.

- Suggested smoke coverage: host match where `BossManager.OnEnable()` runs before `GameStateManager.Awake()`, plus a host shutdown/restart or scene reload path to verify the cached-subscription cleanup does not leave stale callbacks.
