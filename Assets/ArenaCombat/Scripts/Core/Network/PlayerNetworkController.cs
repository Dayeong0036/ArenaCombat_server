// ARCH TAG: LEGACY_2D
// ARCH SCOPE: Current server-authoritative 2D gameplay controller. 3D replacement planned.
// ARCH STATUS: TARGET_3D_PENDING

using Unity.Netcode;
using UnityEngine;
using System;
using ArenaCombat.Core;

namespace ArenaCombat.Core.Network
{
    /// <summary>
    /// Main player network controller with HP, combat, and state management
    /// Server authoritative - all state changes happen on server
    /// 2D Side-scrolling (horizontal movement + jump)
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class PlayerNetworkController : NetworkBehaviour
    {
        [Header("=== Player Settings ===")]
        [SerializeField] private float maxHP = 100f;
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float jumpForce = 10f;

        [Header("=== Network Settings ===")]
        [SerializeField] private float inputSendRate = 0.016f;
        [SerializeField] private float positionSyncThreshold = 0.1f;

        [Header("=== Combat Settings ===")]
        [SerializeField] private float respawnTime = 3f;
        [SerializeField] private float invulnerabilityDuration = 2f;

        [Header("=== Attack Settings ===")]
        [SerializeField] private float attackRange = 2f;
        [SerializeField] private float attackHeight = 1.5f;

        [Header("=== Parry Settings ===")]
        [SerializeField] private float parryWindowDuration = 0.3f;
        [SerializeField] private float parryCooldown = 1f;

        [Header("=== Rope Settings ===")]
        [SerializeField] private float ropeMaxDistance = 10f;
        [SerializeField] private float ropeSpeed = 15f;
        [SerializeField] private float ropeCooldown = 2f;
        [SerializeField] private LayerMask ropeAnchorLayer;

        [Header("=== Inspector Runtime Debug (Read-Only) ===")]
        [SerializeField] private bool inspectorRuntimeDebug = true;
        [SerializeField] private ulong debugOwnerClientId;
        [SerializeField] private bool debugIsSpawned;
        [SerializeField] private bool debugIsServer;
        [SerializeField] private bool debugIsOwner;
        [SerializeField] private bool debugIsHost;
        [SerializeField] private bool debugIsGrounded;
        [SerializeField] private float debugServerMoveInput;
        [SerializeField] private Vector3 debugNetworkPosition;
        [SerializeField] private bool debugNetworkFacingRight;
        [SerializeField] private float debugNetworkHP;
        [SerializeField] private bool debugNetworkIsAlive;
        [SerializeField] private CharacterStateId debugNetworkStateId;
        [SerializeField] private StatusMask debugNetworkStatusMask;
        [SerializeField] private TeamId debugNetworkTeamId;
        [SerializeField] private bool debugNetworkIsParrying;
        [SerializeField] private bool debugNetworkIsRoping;
        [SerializeField] private Vector2 debugNetworkRopeTarget;

        [Header("=== Local Player Only ===")]
        private PlayerInputHandler inputHandler;

        // Components (2D)
        private Rigidbody2D rb;
        private Collider2D playerCollider;
        private Animator animator;

        // Grounded state (updated by server collision callbacks)
        private bool isGrounded = false;

        #region Network Variables (Server -> All Clients)

        // Position and Movement
        private NetworkVariable<Vector3> networkPosition = new NetworkVariable<Vector3>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private NetworkVariable<bool> networkFacingRight = new NetworkVariable<bool>(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // HP and Combat
        private NetworkVariable<float> networkHP = new NetworkVariable<float>(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private NetworkVariable<bool> networkIsAlive = new NetworkVariable<bool>(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // State
        private NetworkVariable<CharacterStateId> networkStateId = new NetworkVariable<CharacterStateId>(
            CharacterStateId.Idle,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private NetworkVariable<StatusMask> networkStatusMask = new NetworkVariable<StatusMask>(
            StatusMask.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // Team
        private NetworkVariable<TeamId> networkTeamId = new NetworkVariable<TeamId>(
            TeamId.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // Parry State
        private NetworkVariable<bool> networkIsParrying = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // Rope State
        private NetworkVariable<bool> networkIsRoping = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private NetworkVariable<Vector2> networkRopeTarget = new NetworkVariable<Vector2>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        #endregion

        #region Public Properties

        public float CurrentHP => networkHP.Value;
        public float MaxHP => maxHP;
        public float HPPercent => networkHP.Value / maxHP;
        public bool IsAlive => networkIsAlive.Value;
        public CharacterStateId CurrentStateId => networkStateId.Value;
        public StatusMask CurrentStatus => networkStatusMask.Value;
        public TeamId Team => networkTeamId.Value;
        public bool IsFacingRight => networkFacingRight.Value;
        public bool IsParrying => networkIsParrying.Value;
        public bool IsRoping => networkIsRoping.Value;
        public Vector2 RopeTarget => networkRopeTarget.Value;

        #endregion

        #region Events

        public event Action<float, float> OnHPChanged;           // (oldHP, newHP)
        public event Action<float, ulong> OnDamageTaken;         // (damage, attackerId)
        public event Action<ulong> OnDeath;                       // (killerId)
        public event Action OnRespawn;
        public event Action<CharacterStateId, CharacterStateId> OnStateChanged;
        public event Action<StatusMask> OnStatusChanged;
        public event Action<bool> OnParryResult;                  // (success)
        public event Action<Vector2> OnRopeStart;                 // (targetPosition)
        public event Action OnRopeEnd;
        public event Action<byte> OnAttackPerformed;              // (attackType)

        #endregion

        // Server-side data
        private float serverMoveInput; // horizontal only (side-scrolling)
        private float lastInputTime;
        private float respawnTimer;
        private float invulnerabilityTimer;

        // Server-side cooldowns
        private float parryCooldownTimer;
        private float parryWindowTimer;
        private float ropeCooldownTimer;
        private Vector2 ropeTargetPosition;
        private bool isRopeMoving;

        // Client-side
        private float lastInputSendTime;
        private float lastSentInput;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            playerCollider = GetComponent<Collider2D>();
            animator = GetComponent<Animator>();
            ResolvePlayerReferences();
        }

        private void ResolvePlayerReferences()
        {
            if (inputHandler == null)
            {
                inputHandler = GetComponent<PlayerInputHandler>();
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            ResolvePlayerReferences();

            // Subscribe to network variable changes
            networkHP.OnValueChanged += HandleHPChanged;
            networkIsAlive.OnValueChanged += HandleAliveChanged;
            networkStateId.OnValueChanged += HandleStateIdChanged;
            networkStatusMask.OnValueChanged += HandleStatusChanged;

            // All non-server clients receive position sync from server
            if (!IsServer)
            {
                networkPosition.OnValueChanged += HandlePositionChanged;
                networkFacingRight.OnValueChanged += HandleFacingChanged;
            }

            // Server initialization
            if (IsServer)
            {
                networkHP.Value = maxHP;
                networkIsAlive.Value = true;
                networkPosition.Value = transform.position;

                // Register with InputValidator
                if (InputValidator.Instance != null)
                {
                    InputValidator.Instance.RegisterClient(OwnerClientId);
                }

                if (CombatManager.Instance != null)
                {
                    CombatManager.Instance.RegisterPlayer(OwnerClientId, this);
                }
            }

            // Local player initialization (Owner only)
            if (IsOwner)
            {
                // Enable owner-only input handling
                if (inputHandler != null)
                {
                    inputHandler.enabled = true;
                    SubscribeToInputEvents();
                }

                // Set up camera to follow this player
                SetupCameraFollow();

                // Mark as local player for easy identification
                gameObject.name = $"Player_LOCAL_{OwnerClientId}";

                Debug.Log($"[PlayerNetworkController] LOCAL player spawned! ClientId: {OwnerClientId}");
            }
            else
            {
                // Keep remote input disabled
                if (inputHandler != null)
                {
                    inputHandler.enabled = false;
                }

                // Remote player
                gameObject.name = $"Player_REMOTE_{OwnerClientId}";
            }

            // Log for debugging
            Debug.Log($"[PlayerNetworkController] Player {OwnerClientId} spawned. IsOwner: {IsOwner}, IsServer: {IsServer}, IsHost: {IsHost}");
            UpdateInspectorRuntimeDebug();
        }

        /// <summary>
        /// Set up main camera to follow this player (Owner only)
        /// </summary>
        private void SetupCameraFollow()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                CameraFollow cameraFollow = mainCamera.GetComponent<CameraFollow>();
                if (cameraFollow == null)
                {
                    cameraFollow = mainCamera.gameObject.AddComponent<CameraFollow>();
                }

                cameraFollow.SetTarget(transform);
                Debug.Log($"[PlayerNetworkController] Camera set to follow player {OwnerClientId}");
            }
            else
            {
                Debug.LogWarning("[PlayerNetworkController] Main camera not found!");
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            networkHP.OnValueChanged -= HandleHPChanged;
            networkIsAlive.OnValueChanged -= HandleAliveChanged;
            networkStateId.OnValueChanged -= HandleStateIdChanged;
            networkStatusMask.OnValueChanged -= HandleStatusChanged;

            if (!IsServer)
            {
                networkPosition.OnValueChanged -= HandlePositionChanged;
                networkFacingRight.OnValueChanged -= HandleFacingChanged;
            }

            // Owner-only cleanup
            if (IsOwner)
            {
                UnsubscribeFromInputEvents();
            }

            // Unregister from InputValidator
            if (IsServer && InputValidator.Instance != null)
            {
                InputValidator.Instance.UnregisterClient(OwnerClientId);
            }

            if (IsServer && CombatManager.Instance != null)
            {
                CombatManager.Instance.UnregisterPlayer(OwnerClientId);
            }

            ClearInspectorRuntimeDebug();
        }

        private void Update()
        {
            if (!IsSpawned) return;

            // Owner sends cached input at a fixed rate
            if (IsOwner)
            {
                SendCachedInputToServer();
            }

            // All non-server clients interpolate position from server
            // (Host doesn't need this - server physics moves directly)
            if (!IsServer)
            {
                InterpolatePosition();
            }

            UpdateInspectorRuntimeDebug();
        }

        #region Input Event Subscription (Owner Only)

        private void SubscribeToInputEvents()
        {
            if (inputHandler == null) return;

            inputHandler.OnMoveInput += HandleMoveInput;
            inputHandler.OnJumpInput += HandleJumpInput;
            inputHandler.OnLightAttack += HandleLightAttack;
            inputHandler.OnHeavyAttack += HandleHeavyAttack;
            inputHandler.OnParry += HandleParryInput;
            inputHandler.OnRopeAction += HandleRopeInput;
        }

        private void UnsubscribeFromInputEvents()
        {
            if (inputHandler == null) return;

            inputHandler.OnMoveInput -= HandleMoveInput;
            inputHandler.OnJumpInput -= HandleJumpInput;
            inputHandler.OnLightAttack -= HandleLightAttack;
            inputHandler.OnHeavyAttack -= HandleHeavyAttack;
            inputHandler.OnParry -= HandleParryInput;
            inputHandler.OnRopeAction -= HandleRopeInput;
        }

        // Cached owner input (consumed in FixedUpdate)
        private float cachedMoveInput;
        private bool cachedJump;

        private void HandleMoveInput(float horizontal)
        {
            cachedMoveInput = horizontal;
        }

        private void HandleJumpInput()
        {
            cachedJump = true;
        }

        private void HandleLightAttack()
        {
            if (!networkIsAlive.Value) return;

            float dir = networkFacingRight.Value ? 1f : -1f;
            Vector2 attackPos = (Vector2)transform.position + Vector2.right * dir * attackRange;
            RequestAttackRpc((byte)AttackType.Light, attackPos);
            LogDebug("Light Attack");
        }

        private void HandleHeavyAttack()
        {
            if (!networkIsAlive.Value) return;

            float dir = networkFacingRight.Value ? 1f : -1f;
            Vector2 attackPos = (Vector2)transform.position + Vector2.right * dir * attackRange;
            RequestAttackRpc((byte)AttackType.Heavy, attackPos);
            LogDebug("Heavy Attack");
        }

        private void HandleParryInput()
        {
            if (!networkIsAlive.Value) return;

            RequestParryRpc();
            LogDebug("Parry");
        }

        private void HandleRopeInput(Vector2 ropeDir)
        {
            if (!networkIsAlive.Value) return;

            RequestRopeRpc(ropeDir);
            LogDebug($"Rope Action dir:{ropeDir}");
        }

        #endregion

        private void LogDebug(string action)
        {
            Debug.Log($"[PlayerNetworkController] [{OwnerClientId}] {action}");
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer) return;

            // Server processes movement
            if (networkIsAlive.Value)
            {
                ProcessMovement();

                // Kill zone check and auto-respawn
                if (MapBounds.Instance != null && MapBounds.Instance.IsBelowKillZone(transform.position))
                {
                    Vector2 respawnPos = MapBounds.Instance.GetRespawnPoint();
                    Respawn(new Vector3(respawnPos.x, respawnPos.y, 0f));
                    return;
                }
            }

            // Update timers
            UpdateServerTimers();

            // Sync position
            networkPosition.Value = transform.position;
            networkFacingRight.Value = transform.localScale.x > 0;
            UpdateInspectorRuntimeDebug();
        }

        #region Input Handling

        /// <summary>
        /// Sends cached owner input to server with rate limiting.
        /// Update caches intent; FixedUpdate applies server-side simulation.
        /// </summary>
        private void SendCachedInputToServer()
        {
            if (Time.time - lastInputSendTime < inputSendRate) return;
            lastInputSendTime = Time.time;

            float horizontal = cachedMoveInput;

            // Client-side filtering based on status
            if (!StatusHelper.CanMove(networkStatusMask.Value))
            {
                horizontal = 0f;
            }

            // Consume one-shot jump input
            bool jump = cachedJump;
            cachedJump = false;

            // Send to server
            SendMoveInputRpc(horizontal, jump);
            lastSentInput = horizontal;
        }

        [Rpc(SendTo.Server)]
        private void SendMoveInputRpc(float moveInput, bool jump)
        {
            // Validate with InputValidator
            if (InputValidator.Instance != null)
            {
                if (!InputValidator.Instance.CheckRateLimit(OwnerClientId, "move"))
                {
                    return;
                }

                // Clamp horizontal input
                moveInput = Mathf.Clamp(moveInput, -1f, 1f);
            }

            // Server-side filtering
            if (!StatusHelper.CanMove(networkStatusMask.Value) || !networkIsAlive.Value)
            {
                moveInput = 0f;
            }

            serverMoveInput = moveInput;
            lastInputTime = Time.time;

            // Handle jump
            if (jump && StatusHelper.CanAct(networkStatusMask.Value) && networkIsAlive.Value)
            {
                TryJump();
            }
        }

        private void ProcessMovement()
        {
            if (rb == null) return;

            // Apply horizontal velocity from validated server input
            rb.linearVelocity = new Vector2(serverMoveInput * moveSpeed, rb.linearVelocity.y);

            // Flip facing by movement direction
            if (serverMoveInput > 0.1f)
                transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
            else if (serverMoveInput < -0.1f)
                transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);

            // Update movement animation
            if (animator != null)
                animator.SetBool("IsWalk", Mathf.Abs(serverMoveInput) > 0.01f);

            // State transitions (except during jump state)
            if (networkStateId.Value != CharacterStateId.Jumping)
            {
                if (Mathf.Abs(serverMoveInput) > 0.01f)
                    SetStateId(CharacterStateId.Moving);
                else
                    SetStateId(CharacterStateId.Idle);
            }
        }

        private void TryJump()
        {
            // Ground check is driven by server-side collision callbacks.
            if (!isGrounded || rb == null) return;

            // Apply jump impulse
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            isGrounded = false;

            SetStateId(CharacterStateId.Jumping);

            if (animator != null)
                animator.SetBool("IsJump", true);
        }

        // Landing check uses Ground tag on the collided object.
        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!IsServer) return;
            if (!collision.gameObject.CompareTag("Ground")) return;

            isGrounded = true;

            if (animator != null)
                animator.SetBool("IsJump", false);

            if (networkStateId.Value == CharacterStateId.Jumping)
                SetStateId(CharacterStateId.Idle);
        }

        private void OnCollisionExit2D(Collision2D collision)
        {
            if (!IsServer) return;
            if (collision.gameObject.CompareTag("Ground"))
                isGrounded = false;
        }

        #endregion

        #region Position Sync

        private void HandlePositionChanged(Vector3 oldPos, Vector3 newPos)
        {
            // Position is interpolated in Update
        }

        private void HandleFacingChanged(bool oldFacing, bool newFacing)
        {
            float scaleX = newFacing ? Mathf.Abs(transform.localScale.x) : -Mathf.Abs(transform.localScale.x);
            transform.localScale = new Vector3(scaleX, transform.localScale.y, transform.localScale.z);
        }

        private void InterpolatePosition()
        {
            float distance = Vector3.Distance(transform.position, networkPosition.Value);

            if (distance > positionSyncThreshold)
            {
                float t = distance > 1f ? 0.5f : 0.2f;
                transform.position = Vector3.Lerp(transform.position, networkPosition.Value, t);
            }
        }

        #endregion

        #region HP and Combat (Server Authority)

        /// <summary>
        /// Apply damage to this player (Server only)
        /// </summary>
        public void TakeDamage(float damage, ulong attackerId, DamageType damageType = DamageType.Physical)
        {
            if (!IsServer)
            {
                Debug.LogWarning("[PlayerNetworkController] Only server can apply damage");
                return;
            }

            if (!networkIsAlive.Value) return;

            // Check invulnerability
            if (invulnerabilityTimer > 0f) return;

            // Check status
            if (!StatusHelper.CanTakeDamage(networkStatusMask.Value)) return;

            // Apply damage
            float oldHP = networkHP.Value;
            networkHP.Value = Mathf.Max(0f, networkHP.Value - damage);

            Debug.Log($"[PlayerNetworkController] Player {OwnerClientId} took {damage} damage from {attackerId}. HP: {oldHP} -> {networkHP.Value}");

            // Notify clients
            DamageEventRpc(damage, attackerId, (byte)damageType);

            // Check death
            if (networkHP.Value <= 0f)
            {
                Die(attackerId);
            }
            else
            {
                // Apply hit stun if can be interrupted
                if (StatusHelper.CanBeInterrupted(networkStatusMask.Value))
                {
                    SetStateId(CharacterStateId.Hit);
                }
            }
        }

        /// <summary>
        /// Heal this player (Server only)
        /// </summary>
        public void Heal(float amount)
        {
            if (!IsServer) return;
            if (!networkIsAlive.Value) return;

            float oldHP = networkHP.Value;
            networkHP.Value = Mathf.Min(maxHP, networkHP.Value + amount);

            Debug.Log($"[PlayerNetworkController] Player {OwnerClientId} healed {amount}. HP: {oldHP} -> {networkHP.Value}");

            // Notify clients
            HealEventRpc(amount);
        }

        /// <summary>
        /// Kill this player (Server only)
        /// </summary>
        private void Die(ulong killerId)
        {
            if (!IsServer) return;

            networkHP.Value = 0f;
            networkIsAlive.Value = false;
            SetStateId(CharacterStateId.Dead);

            // Clear status effects
            networkStatusMask.Value = StatusMask.None;

            // Disable collision
            if (playerCollider != null)
            {
                playerCollider.enabled = false;
            }

            // Start respawn timer
            respawnTimer = respawnTime;

            Debug.Log($"[PlayerNetworkController] Player {OwnerClientId} died. Killer: {killerId}");

            // Notify clients
            DeathEventRpc(killerId);

            // Notify CombatManager
            if (CombatManager.Instance != null)
            {
                CombatManager.Instance.OnPlayerDeath(OwnerClientId, killerId);
            }
        }

        /// <summary>
        /// Respawn this player (Server only)
        /// </summary>
        public void Respawn(Vector3 position)
        {
            if (!IsServer) return;

            // Reset HP
            networkHP.Value = maxHP;
            networkIsAlive.Value = true;
            SetStateId(CharacterStateId.Idle);

            // Reset position
            transform.position = position;
            networkPosition.Value = position;

            // Enable collision
            if (playerCollider != null)
            {
                playerCollider.enabled = true;
            }

            // Grant invulnerability
            invulnerabilityTimer = invulnerabilityDuration;
            AddStatus(StatusMask.Invulnerable);

            Debug.Log($"[PlayerNetworkController] Player {OwnerClientId} respawned at {position}");

            // Notify clients
            RespawnEventRpc(position);
        }

        private void UpdateServerTimers()
        {
            float dt = Time.fixedDeltaTime;

            // Respawn timer
            if (!networkIsAlive.Value && respawnTimer > 0f)
            {
                respawnTimer -= dt;

                if (respawnTimer <= 0f)
                {
                    // Auto respawn at spawn point
                    Vector3 spawnPos = GetSpawnPosition();
                    Respawn(spawnPos);
                }
            }

            // Invulnerability timer
            if (invulnerabilityTimer > 0f)
            {
                invulnerabilityTimer -= dt;

                if (invulnerabilityTimer <= 0f)
                {
                    RemoveStatus(StatusMask.Invulnerable);
                }
            }

            // Parry cooldown
            if (parryCooldownTimer > 0f)
                parryCooldownTimer -= dt;

            // Parry window
            if (parryWindowTimer > 0f)
            {
                parryWindowTimer -= dt;

                if (parryWindowTimer <= 0f)
                {
                    // Parry window ended
                    networkIsParrying.Value = false;
                    ParryResultRpc(false, "Window closed");
                }
            }

            // Rope cooldown
            if (ropeCooldownTimer > 0f)
                ropeCooldownTimer -= dt;

            // Rope movement (2D)
            if (isRopeMoving && networkIsRoping.Value)
            {
                Vector2 currentPos = (Vector2)transform.position;
                Vector2 direction = (ropeTargetPosition - currentPos).normalized;
                float distance = Vector2.Distance(currentPos, ropeTargetPosition);

                if (distance < 0.5f)
                {
                    // Reached target
                    transform.position = new Vector3(ropeTargetPosition.x, ropeTargetPosition.y, 0f);
                    EndRopeAction();
                }
                else
                {
                    // Move towards target
                    Vector2 newPos = currentPos + direction * ropeSpeed * dt;
                    transform.position = new Vector3(newPos.x, newPos.y, 0f);
                    networkPosition.Value = transform.position;
                }
            }
        }

        /// <summary>
        /// End rope action (Server only)
        /// </summary>
        private void EndRopeAction()
        {
            if (!IsServer) return;

            networkIsRoping.Value = false;
            isRopeMoving = false;
            RemoveStatus(StatusMask.Rooted);
            RopeEndRpc();
            LogServerDebug("Rope action ended");
        }

        private Vector3 GetSpawnPosition()
        {
            // Prefer map-defined respawn point when available
            if (MapBounds.Instance != null)
            {
                Vector2 respawnPos = MapBounds.Instance.GetRespawnPoint();
                return new Vector3(respawnPos.x, respawnPos.y, 0f);
            }

            // Fallback while waiting for dedicated spawn integration
            if (PlayerSpawnManager.Instance != null)
            {
                return transform.position + Vector3.up * 5f;
            }

            return new Vector3(0f, 1f, 0f);
        }

        #endregion

        #region Status Effects (Server Authority)

        /// <summary>
        /// Add status effect (Server only)
        /// </summary>
        public void AddStatus(StatusMask status)
        {
            if (!IsServer) return;

            networkStatusMask.Value = StatusHelper.AddStatus(networkStatusMask.Value, status);
            Debug.Log($"[PlayerNetworkController] Player {OwnerClientId} status added: {status}. Current: {networkStatusMask.Value}");
        }

        /// <summary>
        /// Remove status effect (Server only)
        /// </summary>
        public void RemoveStatus(StatusMask status)
        {
            if (!IsServer) return;

            networkStatusMask.Value = StatusHelper.RemoveStatus(networkStatusMask.Value, status);
            Debug.Log($"[PlayerNetworkController] Player {OwnerClientId} status removed: {status}. Current: {networkStatusMask.Value}");
        }

        /// <summary>
        /// Clear all status effects (Server only)
        /// </summary>
        public void ClearAllStatus()
        {
            if (!IsServer) return;

            networkStatusMask.Value = StatusMask.None;
        }

        /// <summary>
        /// Set state ID (Server only)
        /// </summary>
        private void SetStateId(CharacterStateId stateId)
        {
            if (!IsServer) return;

            if (networkStateId.Value != stateId)
            {
                networkStateId.Value = stateId;
            }
        }

        /// <summary>
        /// Set team (Server only)
        /// </summary>
        public void SetTeam(TeamId team)
        {
            if (!IsServer) return;

            networkTeamId.Value = team;
        }

        #endregion

        #region Network Variable Callbacks

        private void HandleHPChanged(float oldHP, float newHP)
        {
            OnHPChanged?.Invoke(oldHP, newHP);
        }

        private void HandleAliveChanged(bool oldAlive, bool newAlive)
        {
            if (!newAlive)
            {
                // Local death handling (animations, effects)
            }
            else
            {
                OnRespawn?.Invoke();
            }
        }

        private void HandleStateIdChanged(CharacterStateId oldState, CharacterStateId newState)
        {
            OnStateChanged?.Invoke(oldState, newState);
        }

        private void HandleStatusChanged(StatusMask oldStatus, StatusMask newStatus)
        {
            OnStatusChanged?.Invoke(newStatus);
        }

        private void UpdateInspectorRuntimeDebug()
        {
            if (!inspectorRuntimeDebug)
            {
                return;
            }

            debugOwnerClientId = OwnerClientId;
            debugIsSpawned = IsSpawned;
            debugIsServer = IsServer;
            debugIsOwner = IsOwner;
            debugIsHost = IsHost;
            debugIsGrounded = isGrounded;
            debugServerMoveInput = serverMoveInput;
            debugNetworkPosition = networkPosition.Value;
            debugNetworkFacingRight = networkFacingRight.Value;
            debugNetworkHP = networkHP.Value;
            debugNetworkIsAlive = networkIsAlive.Value;
            debugNetworkStateId = networkStateId.Value;
            debugNetworkStatusMask = networkStatusMask.Value;
            debugNetworkTeamId = networkTeamId.Value;
            debugNetworkIsParrying = networkIsParrying.Value;
            debugNetworkIsRoping = networkIsRoping.Value;
            debugNetworkRopeTarget = networkRopeTarget.Value;
        }

        private void ClearInspectorRuntimeDebug()
        {
            debugOwnerClientId = 0;
            debugIsSpawned = false;
            debugIsServer = false;
            debugIsOwner = false;
            debugIsHost = false;
            debugIsGrounded = false;
            debugServerMoveInput = 0f;
            debugNetworkPosition = Vector3.zero;
            debugNetworkFacingRight = false;
            debugNetworkHP = 0f;
            debugNetworkIsAlive = false;
            debugNetworkStateId = CharacterStateId.Idle;
            debugNetworkStatusMask = StatusMask.None;
            debugNetworkTeamId = TeamId.None;
            debugNetworkIsParrying = false;
            debugNetworkIsRoping = false;
            debugNetworkRopeTarget = Vector2.zero;
        }

        #endregion

        #region Client RPCs (Server -> Clients)

        [Rpc(SendTo.ClientsAndHost)]
        private void DamageEventRpc(float damage, ulong attackerId, byte damageType)
        {
            OnDamageTaken?.Invoke(damage, attackerId);
            Debug.Log($"[PlayerNetworkController] Received damage event: {damage} from {attackerId}");
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void HealEventRpc(float amount)
        {
            Debug.Log($"[PlayerNetworkController] Received heal event: {amount}");
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void DeathEventRpc(ulong killerId)
        {
            OnDeath?.Invoke(killerId);
            Debug.Log($"[PlayerNetworkController] Received death event. Killer: {killerId}");
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void RespawnEventRpc(Vector3 position)
        {
            transform.position = position;
            Debug.Log($"[PlayerNetworkController] Received respawn event at {position}");
        }

        /// <summary>
        /// Attack result notification
        /// </summary>
        [Rpc(SendTo.ClientsAndHost)]
        private void AttackResultRpc(byte attackType, bool success, string detail)
        {
            OnAttackPerformed?.Invoke(attackType);
            Debug.Log($"[PlayerNetworkController] Attack result: {(AttackType)attackType} {(success ? "HIT" : "MISS")} - {detail}");
        }

        /// <summary>
        /// Parry result notification
        /// </summary>
        [Rpc(SendTo.ClientsAndHost)]
        private void ParryResultRpc(bool success, string detail)
        {
            OnParryResult?.Invoke(success);
            Debug.Log($"[PlayerNetworkController] Parry result: {(success ? "SUCCESS" : "FAILED")} - {detail}");
        }

        /// <summary>
        /// Parry window started
        /// </summary>
        [Rpc(SendTo.ClientsAndHost)]
        private void ParryStartRpc()
        {
            Debug.Log($"[PlayerNetworkController] Parry window started");
        }

        /// <summary>
        /// Parry success - attacker was parried
        /// </summary>
        [Rpc(SendTo.ClientsAndHost)]
        public void ParrySuccessRpc(ulong attackerId)
        {
            OnParryResult?.Invoke(true);
            Debug.Log($"[PlayerNetworkController] Successfully parried attack from {attackerId}");
        }

        /// <summary>
        /// Rope action result notification
        /// </summary>
        [Rpc(SendTo.ClientsAndHost)]
        private void RopeResultRpc(bool success, Vector2 targetPos, string detail)
        {
            if (success)
            {
                OnRopeStart?.Invoke(targetPos);
            }
            Debug.Log($"[PlayerNetworkController] Rope result: {(success ? "SUCCESS" : "FAILED")} - {detail}");
        }

        /// <summary>
        /// Rope action ended
        /// </summary>
        [Rpc(SendTo.ClientsAndHost)]
        private void RopeEndRpc()
        {
            OnRopeEnd?.Invoke();
            Debug.Log($"[PlayerNetworkController] Rope action completed");
        }

        #endregion

        #region Action Requests (Client -> Server)

        /// <summary>
        /// Request attack (Client sends to Server)
        /// </summary>
        [Rpc(SendTo.Server)]
        public void RequestAttackRpc(byte attackType, Vector2 targetPosition)
        {
            if (!Enum.IsDefined(typeof(AttackType), (AttackType)attackType))
            {
                AttackResultRpc(attackType, false, "Invalid attack type");
                return;
            }

            AttackType type = (AttackType)attackType;

            // Check if can act
            if (!StatusHelper.CanAct(networkStatusMask.Value) || !networkIsAlive.Value)
            {
                AttackResultRpc(attackType, false, "Cannot act");
                return;
            }

            // Check if parrying or roping
            if (networkIsParrying.Value || networkIsRoping.Value)
            {
                AttackResultRpc(attackType, false, "Busy");
                return;
            }

            if (CombatManager.Instance == null)
            {
                AttackResultRpc(attackType, false, "Combat manager missing");
                return;
            }

            bool processed = CombatManager.Instance.TryProcessAttack(
                OwnerClientId,
                type,
                transform.position,
                targetPosition,
                out int hitCount,
                out string detail
            );

            if (processed)
            {
                SetStateId(CharacterStateId.Attacking);
            }

            AttackResultRpc(attackType, hitCount > 0, detail);
            LogServerDebug($"Attack {type}: {detail}");
        }

        /// <summary>
        /// Request parry (Client sends to Server)
        /// </summary>
        [Rpc(SendTo.Server)]
        public void RequestParryRpc()
        {
            // Check cooldown
            if (parryCooldownTimer > 0f)
            {
                ParryResultRpc(false, "Cooldown");
                return;
            }

            // Check if can act
            if (!StatusHelper.CanAct(networkStatusMask.Value) || !networkIsAlive.Value)
            {
                ParryResultRpc(false, "Cannot act");
                return;
            }

            // Check if already doing something
            if (networkIsParrying.Value || networkIsRoping.Value)
            {
                ParryResultRpc(false, "Busy");
                return;
            }

            // Start parry
            networkIsParrying.Value = true;
            parryWindowTimer = parryWindowDuration;
            parryCooldownTimer = parryCooldown;
            SetStateId(CharacterStateId.Idle);  // Could add Parry state

            ParryStartRpc();
            LogServerDebug("Parry started");
        }

        /// <summary>
        /// Request rope action (Client sends to Server) - 2D Physics
        /// </summary>
        [Rpc(SendTo.Server)]
        public void RequestRopeRpc(Vector2 direction)
        {
            // Check cooldown
            if (ropeCooldownTimer > 0f)
            {
                RopeResultRpc(false, Vector2.zero, "Cooldown");
                return;
            }

            // Check if can act
            if (!StatusHelper.CanAct(networkStatusMask.Value) || !networkIsAlive.Value)
            {
                RopeResultRpc(false, Vector2.zero, "Cannot act");
                return;
            }

            // Check if already doing something
            if (networkIsParrying.Value || networkIsRoping.Value)
            {
                RopeResultRpc(false, Vector2.zero, "Busy");
                return;
            }

            // 2D Raycast to find anchor point
            Vector2 origin = (Vector2)transform.position + Vector2.up * 0.5f;
            RaycastHit2D hit = Physics2D.Raycast(origin, direction, ropeMaxDistance, ropeAnchorLayer);

            if (hit.collider == null)
            {
                // No anchor with layer - try general raycast
                hit = Physics2D.Raycast(origin, direction, ropeMaxDistance);
            }

            if (hit.collider == null)
            {
                RopeResultRpc(false, Vector2.zero, "No anchor");
                return;
            }

            // Validate anchor target is inside map bounds
            if (MapBounds.Instance != null && !MapBounds.Instance.IsInsideBounds(hit.point))
            {
                RopeResultRpc(false, Vector2.zero, "Out of bounds");
                return;
            }

            // Start rope movement
            networkIsRoping.Value = true;
            networkRopeTarget.Value = hit.point;
            ropeTargetPosition = hit.point;
            isRopeMoving = true;
            ropeCooldownTimer = ropeCooldown;

            // Lock movement during rope
            AddStatus(StatusMask.Rooted);

            RopeResultRpc(true, hit.point, "Started");
            LogServerDebug($"Rope started to {hit.point}");
        }

        private void LogServerDebug(string message)
        {
            Debug.Log($"[PlayerNetworkController] [{OwnerClientId}] Server: {message}");
        }

        #endregion

        #region Debug

        private void OnDrawGizmos()
        {
            if (!IsSpawned) return;

            // Owner indicator
            if (IsOwner)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.5f, 0.2f);
            }

            // Dead indicator
            if (!networkIsAlive.Value)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(transform.position + Vector3.up, Vector3.one * 0.3f);
            }

            // Invulnerable indicator
            if (StatusHelper.HasStatus(networkStatusMask.Value, StatusMask.Invulnerable))
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position, 0.8f);
            }

            // Attack range visualization (2D)
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            float dir = (transform.localScale.x > 0) ? 1f : -1f;
            Vector3 attackCenter = transform.position + Vector3.right * dir * (attackRange / 2f) + Vector3.up * 0.5f;
            Gizmos.DrawWireCube(attackCenter, new Vector3(attackRange, attackHeight, 0.1f));
        }

        #endregion
    }
}


