// ARCH TAG: LEGACY_2D
// ARCH SCOPE: Legacy 2D combat plus 3D perk-trigger prework path before final 3D hit judgment migration.
// ARCH STATUS: TARGET_3D_PENDING

using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;
using System;

namespace ArenaCombat.Core.Network
{
    /// <summary>
    /// Manages combat logic: hit detection, damage calculation, kill tracking
    /// Server authoritative - all combat decisions made on server
    /// </summary>
    public class CombatManager : NetworkBehaviour
    {
        public static CombatManager Instance { get; private set; }

        [Header("=== Attack Settings ===")]
        [SerializeField] private float lightAttackDamage = 10f;
        [SerializeField] private float heavyAttackDamage = 25f;
        [SerializeField] private float skillAttackDamage = 35f;
        [SerializeField] private float ultimateAttackDamage = 50f;

        [Header("=== Range Settings ===")]
        [SerializeField] private float meleeRange = 2f;
        [SerializeField] private float skillRange = 5f;
        [SerializeField] private float attackHeight = 1.5f;

        [Header("=== Cooldowns ===")]
        [SerializeField] private float lightAttackCooldown = 0.3f;
        [SerializeField] private float heavyAttackCooldown = 0.8f;
        [SerializeField] private float skillCooldown = 3f;
        [SerializeField] private float ultimateCooldown = 10f;

        [Header("=== 3D Perk Trigger Prework ===")]
        [SerializeField] private float perkTriggerCooldown3D = 0.2f;

        [Header("=== Legacy RPC Compatibility ===")]
        [SerializeField] private bool enableLegacyDirectAttackRpc = false;

        // Player tracking
        private Dictionary<ulong, PlayerNetworkController> players = new Dictionary<ulong, PlayerNetworkController>();
        private Dictionary<ulong, PlayerNetworkController3D> players3D = new Dictionary<ulong, PlayerNetworkController3D>();

        // Kill/Death tracking (Server only)
        private Dictionary<ulong, int> kills = new Dictionary<ulong, int>();
        private Dictionary<ulong, int> deaths = new Dictionary<ulong, int>();
        private Dictionary<ulong, int> assists = new Dictionary<ulong, int>();

        // Damage tracking for assists
        private Dictionary<ulong, Dictionary<ulong, float>> recentDamage = new Dictionary<ulong, Dictionary<ulong, float>>();
        private const float ASSIST_WINDOW = 10f;

        // Attack cooldown tracking
        private Dictionary<ulong, Dictionary<AttackType, float>> attackCooldowns = new Dictionary<ulong, Dictionary<AttackType, float>>();
        private Dictionary<ulong, Dictionary<int, float>> perkTriggerCooldowns3D = new Dictionary<ulong, Dictionary<int, float>>();

        // Events
        public event Action<ulong, ulong> OnPlayerKill;          // (killerId, victimId)
        public event Action<ulong, ulong, float> OnDamageDealt;  // (attackerId, victimId, damage)
        public event Action<int, int> OnTeamScoreChanged;        // (team1Score, team2Score)

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                Debug.Log("[CombatManager] Server initialized");
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        #region Player Registration

        /// <summary>
        /// Register a player for combat tracking
        /// </summary>
        public void RegisterPlayer(ulong clientId, PlayerNetworkController player)
        {
            if (players.ContainsKey(clientId))
            {
                players[clientId] = player;
            }
            else
            {
                players.Add(clientId, player);
                EnsurePlayerSessionBuckets(clientId);
            }

            Debug.Log($"[CombatManager] Player {clientId} registered");
        }

        /// <summary>
        /// Registers a 3D migration controller for perk-trigger prework flow.
        /// </summary>
        public void RegisterPlayer3D(ulong clientId, PlayerNetworkController3D player)
        {
            if (players3D.ContainsKey(clientId))
            {
                players3D[clientId] = player;
            }
            else
            {
                players3D.Add(clientId, player);
                EnsurePlayerSessionBuckets(clientId);
            }

            Debug.Log($"[CombatManager] 3D player {clientId} registered");
        }

        /// <summary>
        /// Unregister a player
        /// </summary>
        public void UnregisterPlayer(ulong clientId)
        {
            players.Remove(clientId);
            players3D.Remove(clientId);
            // Keep stats for session
            Debug.Log($"[CombatManager] Player {clientId} unregistered");
        }

        public void UnregisterPlayer3D(ulong clientId)
        {
            players3D.Remove(clientId);
            Debug.Log($"[CombatManager] 3D player {clientId} unregistered");
        }

        /// <summary>
        /// Get player by client ID
        /// </summary>
        public PlayerNetworkController GetPlayer(ulong clientId)
        {
            players.TryGetValue(clientId, out var player);
            return player;
        }

        public PlayerNetworkController3D GetPlayer3D(ulong clientId)
        {
            players3D.TryGetValue(clientId, out var player);
            return player;
        }

        private void EnsurePlayerSessionBuckets(ulong clientId)
        {
            if (!kills.ContainsKey(clientId)) kills[clientId] = 0;
            if (!deaths.ContainsKey(clientId)) deaths[clientId] = 0;
            if (!assists.ContainsKey(clientId)) assists[clientId] = 0;
            if (!recentDamage.ContainsKey(clientId)) recentDamage[clientId] = new Dictionary<ulong, float>();
            if (!attackCooldowns.ContainsKey(clientId)) attackCooldowns[clientId] = new Dictionary<AttackType, float>();
            if (!perkTriggerCooldowns3D.ContainsKey(clientId)) perkTriggerCooldowns3D[clientId] = new Dictionary<int, float>();
        }

        #endregion

        #region Attack Processing (Server Only)

        /// <summary>
        /// Backward-compatible attack entry point.
        /// </summary>
        public void ProcessAttack(ulong attackerId, AttackType attackType, Vector3 attackerPosition, Vector3 targetPosition)
        {
            TryProcessAttack(attackerId, attackType, attackerPosition, targetPosition, out _, out _);
        }

        /// <summary>
        /// Server-side authoritative attack processing with result details.
        /// </summary>
        public bool TryProcessAttack(
            ulong attackerId,
            AttackType attackType,
            Vector3 attackerPosition,
            Vector3 targetPosition,
            out int hitCount,
            out string detail)
        {
            hitCount = 0;
            detail = "Unknown";

            if (!IsServer)
            {
                detail = "Server only";
                Debug.LogWarning("[CombatManager] Only server can process attacks");
                return false;
            }

            if (!players.TryGetValue(attackerId, out var attacker))
            {
                detail = "Invalid attacker";
                Debug.LogWarning($"[CombatManager] Unknown attacker: {attackerId}");
                return false;
            }

            if (attacker == null || !attacker.IsAlive)
            {
                detail = "Cannot act";
                return false;
            }

            if (!StatusHelper.CanAct(attacker.CurrentStatus))
            {
                detail = "Cannot act";
                return false;
            }

            if (attacker.IsParrying || attacker.IsRoping)
            {
                detail = "Busy";
                return false;
            }

            if (!CheckAttackCooldown(attackerId, attackType))
            {
                detail = "Cooldown";
                return false;
            }

            float damage = GetAttackDamage(attackType);
            float range = GetAttackRange(attackType);

            // Keep existing behavior: valid attack attempts consume cooldown (hit/miss/parried).
            SetAttackCooldown(attackerId, attackType);

            List<ulong> hitTargets = FindTargetsInRange(attackerId, attackerPosition, range, out var parryTarget);

            if (parryTarget != null)
            {
                attacker.AddStatus(StatusMask.Stunned);
                parryTarget.ParrySuccessRpc(attackerId);
                AttackMissRpc(attackerId, (byte)attackType, attackerPosition, targetPosition);
                detail = "Parried";
                return false;
            }

            foreach (ulong targetId in hitTargets)
            {
                ApplyDamage(attackerId, targetId, damage, DamageType.Physical);
            }

            hitCount = hitTargets.Count;

            if (hitTargets.Count > 0)
            {
                AttackHitRpc(attackerId, (byte)attackType, attackerPosition, targetPosition, hitTargets.ToArray());
                detail = $"Hit {hitTargets.Count}";
            }
            else
            {
                AttackMissRpc(attackerId, (byte)attackType, attackerPosition, targetPosition);
                detail = "Miss";
            }

            Debug.Log($"[CombatManager] Player {attackerId} used {attackType}, result: {detail}");
            return true;
        }

        /// <summary>
        /// Find targets in attack hitbox.
        /// </summary>
        private List<ulong> FindTargetsInRange(
            ulong attackerId,
            Vector3 attackerPos,
            float range,
            out PlayerNetworkController parryTarget)
        {
            List<ulong> targets = new List<ulong>();
            parryTarget = null;

            if (!players.TryGetValue(attackerId, out var attacker) || attacker == null)
            {
                return targets;
            }

            float facing = attacker.IsFacingRight ? 1f : -1f;
            Vector2 hitboxCenter = (Vector2)attackerPos + Vector2.right * facing * (range / 2f) + Vector2.up * 0.5f;
            Vector2 hitboxSize = new Vector2(range, attackHeight);

            Collider2D[] hits = Physics2D.OverlapBoxAll(hitboxCenter, hitboxSize, 0f);
            HashSet<ulong> uniqueTargets = new HashSet<ulong>();

            foreach (Collider2D hit in hits)
            {
                PlayerNetworkController target = hit.GetComponent<PlayerNetworkController>();
                if (target == null) continue;
                if (target.OwnerClientId == attackerId) continue;
                if (!target.IsAlive) continue;
                if (!uniqueTargets.Add(target.OwnerClientId)) continue;

                // Team filtering
                if (attacker.Team != TeamId.None && attacker.Team == target.Team)
                {
                    continue;
                }

                if (target.IsParrying)
                {
                    parryTarget = target;
                    targets.Clear();
                    return targets;
                }

                targets.Add(target.OwnerClientId);
            }

            return targets;
        }

        /// <summary>
        /// Apply damage from attacker to target
        /// </summary>
        private void ApplyDamage(ulong attackerId, ulong targetId, float damage, DamageType damageType)
        {
            if (!players.TryGetValue(targetId, out var target))
            {
                return;
            }

            // Track damage for assists
            TrackDamageForAssist(attackerId, targetId, damage);

            // Apply damage
            target.TakeDamage(damage, attackerId, damageType);

            // Fire event
            OnDamageDealt?.Invoke(attackerId, targetId, damage);

            Debug.Log($"[CombatManager] Player {attackerId} dealt {damage} damage to {targetId}");
        }

        #endregion

        #region 3D Perk Trigger Prework (No Final Hit Judgment)

        /// <summary>
        /// Server-side gate for 3D perk trigger flow.
        /// This prework validates ownership/state/cooldown and emits accepted trigger events.
        /// Final hit judgment is intentionally deferred.
        /// </summary>
        public bool TryProcessPerkTrigger3D(
            ulong casterId,
            int triggerId,
            Vector3 origin,
            Vector3 targetHint,
            out string detail)
        {
            detail = "Unknown";

            if (!IsServer)
            {
                detail = "Server only";
                return false;
            }

            if (!players3D.TryGetValue(casterId, out var caster) || caster == null)
            {
                detail = "Invalid caster";
                return false;
            }

            if (!caster.IsAlive || !StatusHelper.CanAct(caster.CurrentStatus))
            {
                detail = "Cannot act";
                return false;
            }

            EnsurePlayerSessionBuckets(casterId);

            if (!perkTriggerCooldowns3D.TryGetValue(casterId, out var cooldownTable))
            {
                cooldownTable = new Dictionary<int, float>();
                perkTriggerCooldowns3D[casterId] = cooldownTable;
            }

            if (cooldownTable.TryGetValue(triggerId, out float lastTime))
            {
                if (Time.time - lastTime < perkTriggerCooldown3D)
                {
                    detail = "Cooldown";
                    return false;
                }
            }

            cooldownTable[triggerId] = Time.time;
            PerkTriggerAccepted3DRpc(casterId, triggerId, origin, targetHint);
            detail = "Accepted";
            return true;
        }

        #endregion

        #region Cooldown Management

        private bool CheckAttackCooldown(ulong playerId, AttackType attackType)
        {
            if (!attackCooldowns.TryGetValue(playerId, out var cooldowns))
            {
                return true;
            }

            if (cooldowns.TryGetValue(attackType, out float lastTime))
            {
                float cooldown = GetAttackCooldown(attackType);
                return Time.time - lastTime >= cooldown;
            }

            return true;
        }

        private void SetAttackCooldown(ulong playerId, AttackType attackType)
        {
            if (!attackCooldowns.ContainsKey(playerId))
            {
                attackCooldowns[playerId] = new Dictionary<AttackType, float>();
            }

            attackCooldowns[playerId][attackType] = Time.time;
        }

        private float GetAttackCooldown(AttackType attackType)
        {
            return attackType switch
            {
                AttackType.Light => lightAttackCooldown,
                AttackType.Heavy => heavyAttackCooldown,
                AttackType.Skill => skillCooldown,
                AttackType.Ultimate => ultimateCooldown,
                _ => lightAttackCooldown
            };
        }

        private float GetAttackDamage(AttackType attackType)
        {
            return attackType switch
            {
                AttackType.Light => lightAttackDamage,
                AttackType.Heavy => heavyAttackDamage,
                AttackType.Skill => skillAttackDamage,
                AttackType.Ultimate => ultimateAttackDamage,
                _ => lightAttackDamage
            };
        }

        private float GetAttackRange(AttackType attackType)
        {
            return attackType switch
            {
                AttackType.Light => meleeRange,
                AttackType.Heavy => meleeRange * 1.2f,
                AttackType.Skill => skillRange,
                AttackType.Ultimate => skillRange * 1.5f,
                _ => meleeRange
            };
        }

        #endregion

        #region Kill/Death/Assist Tracking

        /// <summary>
        /// Called when a player dies (from PlayerNetworkController)
        /// </summary>
        public void OnPlayerDeath(ulong victimId, ulong killerId)
        {
            if (!IsServer) return;

            EnsurePlayerSessionBuckets(victimId);
            EnsurePlayerSessionBuckets(killerId);

            // Update kill count
            if (kills.ContainsKey(killerId))
            {
                kills[killerId]++;
            }

            // Update death count
            if (deaths.ContainsKey(victimId))
            {
                deaths[victimId]++;
            }

            // Calculate assists
            List<ulong> assisters = GetAssisters(victimId, killerId);
            foreach (ulong assisterId in assisters)
            {
                if (assists.ContainsKey(assisterId))
                {
                    assists[assisterId]++;
                }
            }

            // Clear damage tracking for victim
            if (recentDamage.TryGetValue(victimId, out var victimDamageLog))
            {
                victimDamageLog.Clear();
            }

            // Fire event
            OnPlayerKill?.Invoke(killerId, victimId);

            // Notify clients
            PlayerKilledRpc(killerId, victimId, assisters.ToArray());

            Debug.Log($"[CombatManager] Kill: {killerId} killed {victimId}. Assists: {string.Join(", ", assisters)}");
        }

        /// <summary>
        /// Track damage for assist calculation
        /// </summary>
        private void TrackDamageForAssist(ulong attackerId, ulong victimId, float damage)
        {
            if (!recentDamage.ContainsKey(victimId))
            {
                recentDamage[victimId] = new Dictionary<ulong, float>();
            }

            if (recentDamage[victimId].ContainsKey(attackerId))
            {
                recentDamage[victimId][attackerId] += damage;
            }
            else
            {
                recentDamage[victimId][attackerId] = damage;
            }
        }

        /// <summary>
        /// Get players who assisted in a kill
        /// </summary>
        private List<ulong> GetAssisters(ulong victimId, ulong killerId)
        {
            List<ulong> assisters = new List<ulong>();

            if (!recentDamage.TryGetValue(victimId, out var damagers))
            {
                return assisters;
            }

            foreach (var kvp in damagers)
            {
                ulong attackerId = kvp.Key;
                float damage = kvp.Value;

                // Skip the killer
                if (attackerId == killerId) continue;

                // Minimum damage threshold for assist
                if (damage >= 10f)
                {
                    assisters.Add(attackerId);
                }
            }

            return assisters;
        }

        #endregion

        #region Stats API

        /// <summary>
        /// Get kills for a player
        /// </summary>
        public int GetKills(ulong clientId)
        {
            return kills.TryGetValue(clientId, out int k) ? k : 0;
        }

        /// <summary>
        /// Get deaths for a player
        /// </summary>
        public int GetDeaths(ulong clientId)
        {
            return deaths.TryGetValue(clientId, out int d) ? d : 0;
        }

        /// <summary>
        /// Get assists for a player
        /// </summary>
        public int GetAssists(ulong clientId)
        {
            return assists.TryGetValue(clientId, out int a) ? a : 0;
        }

        /// <summary>
        /// Get K/D/A string
        /// </summary>
        public string GetKDAString(ulong clientId)
        {
            return $"{GetKills(clientId)}/{GetDeaths(clientId)}/{GetAssists(clientId)}";
        }

        /// <summary>
        /// Reset all stats
        /// </summary>
        public void ResetAllStats()
        {
            if (!IsServer) return;

            foreach (var key in kills.Keys)
            {
                kills[key] = 0;
                deaths[key] = 0;
                assists[key] = 0;
            }

            foreach (var kvp in recentDamage)
            {
                kvp.Value.Clear();
            }

            foreach (var kvp in attackCooldowns)
            {
                kvp.Value.Clear();
            }

            foreach (var kvp in perkTriggerCooldowns3D)
            {
                kvp.Value.Clear();
            }

            Debug.Log("[CombatManager] All stats reset");
        }

        #endregion

        #region Client RPCs

        [Rpc(SendTo.ClientsAndHost)]
        private void AttackHitRpc(ulong attackerId, byte attackType, Vector3 attackerPos, Vector3 targetPos, ulong[] hitTargets)
        {
            // Play attack hit effects, sounds, etc.
            Debug.Log($"[CombatManager] Attack hit: {attackerId} used {(AttackType)attackType}, hit {hitTargets.Length} targets");
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void AttackMissRpc(ulong attackerId, byte attackType, Vector3 attackerPos, Vector3 targetPos)
        {
            // Play attack miss effects
            Debug.Log($"[CombatManager] Attack miss: {attackerId} used {(AttackType)attackType}");
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PlayerKilledRpc(ulong killerId, ulong victimId, ulong[] assisters)
        {
            // Show kill feed, play sounds, etc.
            string assistStr = assisters.Length > 0 ? $" (Assists: {string.Join(", ", assisters)})" : "";
            Debug.Log($"[CombatManager] Kill notification: {killerId} killed {victimId}{assistStr}");
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PerkTriggerAccepted3DRpc(ulong casterId, int triggerId, Vector3 origin, Vector3 targetHint)
        {
            Debug.Log($"[CombatManager] 3D perk trigger accepted: caster={casterId}, trigger={triggerId}");
        }

        #endregion

        #region Server RPCs (Client -> Server)

        /// <summary>
        /// Client requests attack validation
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestAttackRpc(
            byte attackType,
            Vector3 targetPosition,
            int clientTick = 0,
            RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;

            if (!enableLegacyDirectAttackRpc)
            {
                Debug.LogWarning($"[CombatManager] Legacy direct attack RPC is disabled. client={clientId}");
                return;
            }

            if (InputValidator.Instance != null)
            {
                if (!InputValidator.Instance.CheckRateLimit(clientId, "attack2d_direct"))
                {
                    return;
                }

                if (!InputValidator.Instance.ValidateTickOrder(clientId, clientTick, "attack2d_direct"))
                {
                    return;
                }

                targetPosition = InputValidator.Instance.SanitizeVector3(targetPosition);
            }

            if (!players.TryGetValue(clientId, out var player))
            {
                Debug.LogWarning($"[CombatManager] Unknown player {clientId} requested attack");
                return;
            }

            bool processed = TryProcessAttack(
                clientId,
                (AttackType)attackType,
                player.transform.position,
                targetPosition,
                out int hitCount,
                out string detail
            );

            Debug.Log($"[CombatManager] RPC attack from {clientId}: processed={processed}, hits={hitCount}, detail={detail}");
        }

        #endregion

        #region Utility

        /// <summary>
        /// Check if two players are on the same team
        /// </summary>
        public bool AreTeammates(ulong player1, ulong player2)
        {
            if (!players.TryGetValue(player1, out var p1) || !players.TryGetValue(player2, out var p2))
            {
                return false;
            }

            if (p1.Team == TeamId.None || p2.Team == TeamId.None)
            {
                return false;
            }

            return p1.Team == p2.Team;
        }

        /// <summary>
        /// Check if player can attack target
        /// </summary>
        public bool CanAttack(ulong attackerId, ulong targetId)
        {
            // Can't attack self
            if (attackerId == targetId) return false;

            // Can't attack teammates
            if (AreTeammates(attackerId, targetId)) return false;

            // Check if target is alive
            if (players.TryGetValue(targetId, out var target))
            {
                return target.IsAlive;
            }

            return false;
        }

        #endregion
    }
}


