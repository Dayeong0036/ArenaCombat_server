// ARCH TAG: TARGET_3D
// ARCH SCOPE: Server-authoritative top-down 3D player controller pre-migration path.
// ARCH STATUS: ACTIVE_PREP

using System;
using System.Collections.Generic;
using ArenaCombat.Core;
using Unity.Netcode;
using UnityEngine;

namespace ArenaCombat.Core.Network
{
    /// <summary>
    /// Top-down 3D migration controller.
    /// This class implements server-authoritative movement/rope/perk request gates.
    /// Final combat hit judgment integration is intentionally deferred.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class PlayerNetworkController3D : NetworkBehaviour
    {
        [Header("=== 3D Movement Settings ===")]
        [SerializeField] private float maxHP = 100f;
        [SerializeField] private float moveSpeed = TopDown3DConstants.DEFAULT_MOVE_SPEED;
        [SerializeField] private float rotationLerp = TopDown3DConstants.DEFAULT_ROTATION_LERP;
        [SerializeField] private float inputSendRate = NetworkTickRate.INPUT_SEND_RATE;
        [SerializeField] private float positionSyncThreshold = 0.15f;

        [Header("=== Survival Settings ===")]
        [SerializeField] private float respawnTime = CombatConstants.RESPAWN_TIME;
        [SerializeField] private float invulnerabilityDuration = CombatConstants.INVULNERABILITY_AFTER_SPAWN;

        [Header("=== Rope Settings ===")]
        [SerializeField] private float ropeMaxDistance = TopDown3DConstants.DEFAULT_ROPE_MAX_DISTANCE;
        [SerializeField] private float ropeSpeed = TopDown3DConstants.DEFAULT_ROPE_SPEED;
        [SerializeField] private float ropeCooldown = TopDown3DConstants.DEFAULT_ROPE_COOLDOWN;
        [SerializeField] private LayerMask ropeAnchorLayer;

        [Header("=== Owner Input Integration ===")]
        [SerializeField] private bool useBuiltInInputHandler = true;
        [SerializeField] private bool autoSendMoveRequests = true;
        [SerializeField] private bool force3DAimProjectionForBuiltInInput = true;
        [SerializeField] private float builtInAimGroundY = 0f;

        [Header("=== Owner Camera Binding ===")]
        [SerializeField] private Camera ownerCameraOverride;
        [SerializeField] private bool rebindOwnerCameraWhenMissing = true;

        [Header("=== Server Queue Settings ===")]
        [SerializeField] private int maxQueuedActions = 32;

        [Header("=== Client Sync Smoothing ===")]
        [SerializeField] private float respawnInterpolationSuppressDuration = 0.15f;

        [Header("=== Inspector Runtime Debug (Read-Only) ===")]
        [SerializeField] private bool inspectorRuntimeDebug = true;
        [SerializeField] private ulong debugOwnerClientId;
        [SerializeField] private bool debugIsSpawned;
        [SerializeField] private bool debugIsServer;
        [SerializeField] private bool debugIsOwner;
        [SerializeField] private bool debugIsHost;
        [SerializeField] private int debugLocalTick;
        [SerializeField] private int debugQueuedActionCount;
        [SerializeField] private Vector2 debugServerMoveInput;
        [SerializeField] private float debugServerLookYaw;
        [SerializeField] private Vector3 debugNetworkPosition;
        [SerializeField] private float debugNetworkYaw;
        [SerializeField] private float debugNetworkHP;
        [SerializeField] private bool debugNetworkIsAlive;
        [SerializeField] private CharacterStateId debugNetworkStateId;
        [SerializeField] private StatusMask debugNetworkStatusMask;
        [SerializeField] private TeamId debugNetworkTeamId;
        [SerializeField] private bool debugNetworkIsRoping;
        [SerializeField] private Vector3 debugNetworkRopeTarget;

        // Components
        private Rigidbody rb;
        private Collider playerCollider;
        private PlayerInputHandler inputHandler;
        private Camera ownerCamera;

        #region NetworkVariables (Server Write, Everyone Read)

        private readonly NetworkVariable<Vector3> networkPosition = new NetworkVariable<Vector3>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private readonly NetworkVariable<float> networkYaw = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private readonly NetworkVariable<float> networkHP = new NetworkVariable<float>(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private readonly NetworkVariable<bool> networkIsAlive = new NetworkVariable<bool>(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private readonly NetworkVariable<CharacterStateId> networkStateId = new NetworkVariable<CharacterStateId>(
            CharacterStateId.Idle,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private readonly NetworkVariable<StatusMask> networkStatusMask = new NetworkVariable<StatusMask>(
            StatusMask.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private readonly NetworkVariable<TeamId> networkTeamId = new NetworkVariable<TeamId>(
            TeamId.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private readonly NetworkVariable<bool> networkIsRoping = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private readonly NetworkVariable<Vector3> networkRopeTarget = new NetworkVariable<Vector3>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        #endregion

        #region Public Properties

        public float CurrentHP => networkHP.Value;
        public float MaxHP => maxHP;
        public bool IsAlive => networkIsAlive.Value;
        public CharacterStateId CurrentStateId => networkStateId.Value;
        public StatusMask CurrentStatus => networkStatusMask.Value;
        public TeamId Team => networkTeamId.Value;
        public bool IsRoping => networkIsRoping.Value;
        public Vector3 RopeTarget => networkRopeTarget.Value;
        public float CurrentYaw => networkYaw.Value;
        public bool UseBuiltInInputHandler => useBuiltInInputHandler;
        public bool AutoSendMoveRequests => autoSendMoveRequests;
        public int LocalTick => localTick;
        public Camera OwnerCamera => ownerCamera;

        #endregion

        #region Events

        public event Action<float, float> OnHPChanged;
        public event Action<CharacterStateId, CharacterStateId> OnStateChanged;
        public event Action<StatusMask> OnStatusChanged;
        public event Action<bool, string> OnRopeResult;
        public event Action<int, bool, string> OnPerkTriggerResult;

        #endregion

        // Server side movement state
        private Vector2 serverMoveInput;
        private float serverLookYaw;

        // Server side timers and action tracking
        private float respawnTimer;
        private float invulnerabilityTimer;
        private float ropeCooldownTimer;
        private bool isRopeMoving;
        private Vector3 ropeTargetPosition;
        private Vector3 lastValidatedServerPosition;

        private enum QueuedActionType
        {
            Rope = 0,
            PerkTrigger = 1
        }

        private struct QueuedServerAction
        {
            public QueuedActionType ActionType;
            public int ClientTick;
            public float ReceivedAt;
            public Vector3 AnchorHint;
            public Vector3 Direction;
            public int TriggerId;
            public Vector3 TargetHint;
        }

        private readonly List<QueuedServerAction> queuedServerActions = new List<QueuedServerAction>();

        // Client side interpolation state
        private float lastInputSendTime;
        private Vector2 cachedMoveInput;
        private float cachedLookYaw;
        private int localTick;
        private float suppressInterpolationUntil;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            playerCollider = GetComponent<Collider>();
            ResolvePlayerReferences();

            // Top-down movement assumes no vertical physics-driven gameplay by default.
            rb.useGravity = false;
            rb.isKinematic = false;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
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

            networkHP.OnValueChanged += HandleHPChanged;
            networkStateId.OnValueChanged += HandleStateChanged;
            networkStatusMask.OnValueChanged += HandleStatusChanged;

            if (!IsServer)
            {
                networkPosition.OnValueChanged += HandlePositionChanged;
                networkYaw.OnValueChanged += HandleYawChanged;
            }

            if (IsServer)
            {
                networkHP.Value = maxHP;
                networkIsAlive.Value = true;
                networkPosition.Value = transform.position;
                networkYaw.Value = transform.eulerAngles.y;
                lastValidatedServerPosition = transform.position;

                if (InputValidator.Instance != null)
                {
                    InputValidator.Instance.RegisterClient(OwnerClientId);
                }

                if (CombatManager.Instance != null)
                {
                    CombatManager.Instance.RegisterPlayer3D(OwnerClientId, this);
                }
            }

            if (IsOwner)
            {
                if (inputHandler != null)
                {
                    inputHandler.enabled = useBuiltInInputHandler;
                    if (useBuiltInInputHandler)
                    {
                        if (force3DAimProjectionForBuiltInInput)
                        {
                            inputHandler.ConfigureAimProjection(true, builtInAimGroundY);
                        }

                        SubscribeInputEvents();
                    }
                }

                SetupTopDownCamera();
                gameObject.name = $"Player3D_LOCAL_{OwnerClientId}";
            }
            else
            {
                if (inputHandler != null)
                {
                    inputHandler.enabled = false;
                }

                gameObject.name = $"Player3D_REMOTE_{OwnerClientId}";
            }

            UpdateInspectorRuntimeDebug();
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            networkHP.OnValueChanged -= HandleHPChanged;
            networkStateId.OnValueChanged -= HandleStateChanged;
            networkStatusMask.OnValueChanged -= HandleStatusChanged;

            if (!IsServer)
            {
                networkPosition.OnValueChanged -= HandlePositionChanged;
                networkYaw.OnValueChanged -= HandleYawChanged;
            }

            if (IsOwner && useBuiltInInputHandler)
            {
                UnsubscribeInputEvents();
            }

            if (IsServer && InputValidator.Instance != null)
            {
                InputValidator.Instance.UnregisterClient(OwnerClientId);
            }

            if (IsServer && CombatManager.Instance != null)
            {
                CombatManager.Instance.UnregisterPlayer3D(OwnerClientId);
            }

            if (IsServer)
            {
                queuedServerActions.Clear();
            }

            suppressInterpolationUntil = 0f;
            ownerCamera = null;
            ClearInspectorRuntimeDebug();
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsOwner && autoSendMoveRequests)
            {
                SendCachedInputToServer();
            }

            if (IsOwner && rebindOwnerCameraWhenMissing &&
                (ownerCamera == null || !ownerCamera.isActiveAndEnabled))
            {
                SetupTopDownCamera();
            }

            if (!IsServer)
            {
                InterpolatePosition();
                InterpolateRotation();
            }

            UpdateInspectorRuntimeDebug();
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer)
            {
                return;
            }

            if (networkIsAlive.Value)
            {
                ProcessQueuedServerActions();
                ProcessServerMovement();

                if (MapBounds3D.Instance != null)
                {
                    Vector3 resolvedPos = MapBounds3D.Instance.ResolveServerPosition(transform.position, lastValidatedServerPosition);
                    if ((resolvedPos - transform.position).sqrMagnitude > 0.0001f)
                    {
                        rb.position = resolvedPos;
                        transform.position = resolvedPos;
                    }

                    lastValidatedServerPosition = transform.position;

                    if (MapBounds3D.Instance.IsBelowKillZone(transform.position))
                    {
                        Respawn(MapBounds3D.Instance.GetRespawnPointNear(lastValidatedServerPosition));
                        return;
                    }
                }
                else
                {
                    lastValidatedServerPosition = transform.position;
                }
            }

            UpdateServerTimers();

            networkPosition.Value = transform.position;
            networkYaw.Value = transform.eulerAngles.y;
            UpdateInspectorRuntimeDebug();
        }

        #region Owner Input Collection

        private void SubscribeInputEvents()
        {
            if (inputHandler == null)
            {
                return;
            }

            inputHandler.OnMoveInput += HandleMoveInput;
            inputHandler.OnMoveInput2D += HandleMoveInput2D;
            inputHandler.OnAimPosition += HandleAimPosition;
        }

        private void UnsubscribeInputEvents()
        {
            if (inputHandler == null)
            {
                return;
            }

            inputHandler.OnMoveInput -= HandleMoveInput;
            inputHandler.OnMoveInput2D -= HandleMoveInput2D;
            inputHandler.OnAimPosition -= HandleAimPosition;
        }

        private void HandleMoveInput(float horizontal)
        {
            // Backward-compatible fallback when only horizontal event is in use.
            cachedMoveInput = new Vector2(horizontal, cachedMoveInput.y);
        }

        private void HandleMoveInput2D(Vector2 input)
        {
            cachedMoveInput = input;
        }

        private void HandleAimPosition(Vector2 aimWorldPos)
        {
            // 3D controller expects XZ-projected aim coordinates.
            // Ignore incompatible XY payloads from 2D projection mode.
            if (inputHandler != null && !inputHandler.Use3DGroundAimProjection)
            {
                return;
            }

            Vector3 toAim = new Vector3(aimWorldPos.x, 0f, aimWorldPos.y) - new Vector3(transform.position.x, 0f, transform.position.z);
            if (toAim.sqrMagnitude > 0.0001f)
            {
                cachedLookYaw = Mathf.Atan2(toAim.x, toAim.z) * Mathf.Rad2Deg;
            }
        }

        /// <summary>
        /// External-client entry point.
        /// Updates local cached move/aim intent that can be auto-sent or manually sent.
        /// </summary>
        public void SetLocalMoveIntent(Vector2 moveInput, float lookYaw)
        {
            if (!IsOwner || !IsSpawned)
            {
                return;
            }

            cachedMoveInput = moveInput;
            cachedLookYaw = lookYaw;
        }

        /// <summary>
        /// External-client entry point.
        /// Sends a move request immediately using current cache or provided values.
        /// Recommended when autoSendMoveRequests is disabled.
        /// </summary>
        public void SubmitMoveIntent(Vector2 moveInput, float lookYaw)
        {
            if (!IsOwner || !IsSpawned)
            {
                return;
            }

            cachedMoveInput = moveInput;
            cachedLookYaw = lookYaw;
            SendMoveRequestNow();
        }

        /// <summary>
        /// External-client entry point.
        /// Sends one move request immediately and advances local tick.
        /// </summary>
        public void SendMoveRequestNow()
        {
            if (!IsOwner || !IsSpawned)
            {
                return;
            }

            if (IsLocalGameplayBlockedByCardDraft())
            {
                return;
            }

            lastInputSendTime = Time.time;
            localTick++;
            RequestMoveRpc(cachedMoveInput, cachedLookYaw, localTick);
        }

        /// <summary>
        /// External-client entry point for rope action request.
        /// </summary>
        public void SubmitRopeIntent(Vector3 anchorHint, Vector3 direction)
        {
            if (!IsOwner || !IsSpawned)
            {
                return;
            }

            if (IsLocalGameplayBlockedByCardDraft())
            {
                return;
            }

            localTick++;
            RequestRopeRpc(anchorHint, direction, localTick);
        }

        /// <summary>
        /// External-client entry point for perk trigger request.
        /// </summary>
        public void SubmitPerkTriggerIntent(int triggerId, Vector3 targetHint)
        {
            if (!IsOwner || !IsSpawned)
            {
                return;
            }

            if (IsLocalGameplayBlockedByCardDraft())
            {
                return;
            }

            localTick++;
            RequestPerkTriggerRpc(triggerId, targetHint, localTick);
        }

        /// <summary>
        /// Allows external controller to reset or seed the local tick counter.
        /// </summary>
        public void ResetLocalTick(int newTick = 0)
        {
            localTick = Mathf.Max(0, newTick);
        }

        private void SendCachedInputToServer()
        {
            if (IsLocalGameplayBlockedByCardDraft())
            {
                return;
            }

            if (Time.time - lastInputSendTime < inputSendRate)
            {
                return;
            }

            lastInputSendTime = Time.time;
            localTick++;

            RequestMoveRpc(cachedMoveInput, cachedLookYaw, localTick);
        }

        #endregion

        #region Server Movement

        [Rpc(SendTo.Server)]
        private void RequestMoveRpc(Vector2 moveInput, float lookYaw, int clientTick)
        {
            if (IsServerGameplayBlockedByCardDraft())
            {
                serverMoveInput = Vector2.zero;
                return;
            }

            if (InputValidator.Instance != null)
            {
                if (!InputValidator.Instance.CheckRateLimit(OwnerClientId, "move3d"))
                {
                    return;
                }

                if (!InputValidator.Instance.ValidateTickOrder(OwnerClientId, clientTick, "move3d"))
                {
                    return;
                }

                InputValidator.Instance.ValidateMoveInput2D(moveInput, out moveInput);
                lookYaw = InputValidator.Instance.SanitizeFloat(lookYaw);
            }

            if (!networkIsAlive.Value || !StatusHelper.CanMove(networkStatusMask.Value) || networkIsRoping.Value)
            {
                serverMoveInput = Vector2.zero;
                return;
            }

            serverMoveInput = moveInput;
            serverLookYaw = lookYaw;
        }

        private void ProcessServerMovement()
        {
            if (IsServerGameplayBlockedByCardDraft())
            {
                rb.linearVelocity = Vector3.zero;
                SetStateId(CharacterStateId.Idle);
                return;
            }

            if (networkIsRoping.Value)
            {
                rb.linearVelocity = Vector3.zero;
                return;
            }

            Vector3 desiredVelocity = new Vector3(serverMoveInput.x, 0f, serverMoveInput.y) * moveSpeed;

            if (StatusHelper.HasStatus(networkStatusMask.Value, StatusMask.Slowed))
            {
                desiredVelocity *= 0.6f;
            }

            rb.linearVelocity = new Vector3(desiredVelocity.x, 0f, desiredVelocity.z);

            float targetYaw = transform.eulerAngles.y;

            if (serverMoveInput.sqrMagnitude > 0.001f)
            {
                targetYaw = Mathf.Atan2(serverMoveInput.x, serverMoveInput.y) * Mathf.Rad2Deg;
                SetStateId(CharacterStateId.Moving);
            }
            else
            {
                targetYaw = serverLookYaw;
                if (!networkIsRoping.Value)
                {
                    SetStateId(CharacterStateId.Idle);
                }
            }

            Quaternion targetRotation = Quaternion.Euler(0f, targetYaw, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * rotationLerp);
        }

        #endregion

        #region Server Action Queue

        private bool TryEnqueueServerAction(QueuedServerAction action, out string rejectReason)
        {
            rejectReason = string.Empty;

            if (queuedServerActions.Count >= Mathf.Max(1, maxQueuedActions))
            {
                rejectReason = "QueueFull";
                return false;
            }

            queuedServerActions.Add(action);
            return true;
        }

        private void ProcessQueuedServerActions()
        {
            if (!IsServer || queuedServerActions.Count == 0)
            {
                return;
            }

            if (IsServerGameplayBlockedByCardDraft())
            {
                queuedServerActions.Clear();
                return;
            }

            queuedServerActions.Sort(CompareQueuedActions);

            foreach (QueuedServerAction action in queuedServerActions)
            {
                switch (action.ActionType)
                {
                    case QueuedActionType.Rope:
                        ExecuteQueuedRopeAction(action);
                        break;

                    case QueuedActionType.PerkTrigger:
                        ExecuteQueuedPerkTriggerAction(action);
                        break;
                }
            }

            queuedServerActions.Clear();
        }

        private int CompareQueuedActions(QueuedServerAction a, QueuedServerAction b)
        {
            int tickCompare = a.ClientTick.CompareTo(b.ClientTick);
            if (tickCompare != 0)
            {
                return tickCompare;
            }

            int priorityCompare = GetQueuedActionPriority(a.ActionType).CompareTo(GetQueuedActionPriority(b.ActionType));
            if (priorityCompare != 0)
            {
                return priorityCompare;
            }

            return a.ReceivedAt.CompareTo(b.ReceivedAt);
        }

        private int GetQueuedActionPriority(QueuedActionType actionType)
        {
            // Rope first to lock movement deterministically before other action checks in same tick.
            return actionType switch
            {
                QueuedActionType.Rope => 0,
                QueuedActionType.PerkTrigger => 1,
                _ => 10
            };
        }

        private void ExecuteQueuedRopeAction(QueuedServerAction action)
        {
            if (IsServerGameplayBlockedByCardDraft())
            {
                RopeResultRpc(false, Vector3.zero, "CardDraftActive");
                return;
            }

            if (!networkIsAlive.Value || !StatusHelper.CanAct(networkStatusMask.Value))
            {
                RopeResultRpc(false, Vector3.zero, "CannotAct");
                return;
            }

            if (networkIsRoping.Value || ropeCooldownTimer > 0f)
            {
                RopeResultRpc(false, Vector3.zero, "CooldownOrBusy");
                return;
            }

            Vector3 origin = transform.position + Vector3.up * 0.5f;
            if (!TryResolveRopeCandidateTarget(origin, action.AnchorHint, action.Direction, out Vector3 candidateTarget, out string detail))
            {
                RopeResultRpc(false, Vector3.zero, detail);
                return;
            }

            networkIsRoping.Value = true;
            networkRopeTarget.Value = candidateTarget;
            ropeTargetPosition = candidateTarget;
            isRopeMoving = true;
            ropeCooldownTimer = ropeCooldown;

            AddStatus(StatusMask.Rooted);
            SetStateId(CharacterStateId.Roping);
            RopeResultRpc(true, candidateTarget, "Started");
        }

        private bool TryResolveRopeCandidateTarget(
            Vector3 origin,
            Vector3 anchorHint,
            Vector3 direction,
            out Vector3 candidateTarget,
            out string detail)
        {
            if (MapBounds3D.Instance != null)
            {
                return MapBounds3D.Instance.TryResolveRopeTarget(
                    origin,
                    anchorHint,
                    direction,
                    ropeMaxDistance,
                    ropeAnchorLayer,
                    out candidateTarget,
                    out detail
                );
            }

            Vector3 moveDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
            if (moveDirection != Vector3.zero)
            {
                if (!Physics.Raycast(origin, moveDirection, out RaycastHit hit, ropeMaxDistance, ropeAnchorLayer))
                {
                    candidateTarget = Vector3.zero;
                    detail = "NoAnchor";
                    return false;
                }

                candidateTarget = hit.point;
                detail = "Resolved";
                return true;
            }

            candidateTarget = anchorHint;
            detail = "Resolved";
            return anchorHint != Vector3.zero;
        }

        private void ExecuteQueuedPerkTriggerAction(QueuedServerAction action)
        {
            if (IsServerGameplayBlockedByCardDraft())
            {
                PerkTriggerResultRpc(action.TriggerId, false, "CardDraftActive");
                return;
            }

            if (!networkIsAlive.Value || !StatusHelper.CanAct(networkStatusMask.Value) || !StatusHelper.CanUseSkill(networkStatusMask.Value))
            {
                PerkTriggerResultRpc(action.TriggerId, false, "CannotAct");
                return;
            }

            if (networkIsRoping.Value)
            {
                PerkTriggerResultRpc(action.TriggerId, false, "Busy");
                return;
            }

            if (CombatManager.Instance == null)
            {
                PerkTriggerResultRpc(action.TriggerId, false, "CombatManagerMissing");
                return;
            }

            bool accepted = CombatManager.Instance.TryProcessPerkTrigger3D(
                OwnerClientId,
                action.TriggerId,
                transform.position,
                action.TargetHint,
                out string detail
            );

            if (accepted)
            {
                SetStateId(CharacterStateId.PerkCasting);
            }

            PerkTriggerResultRpc(action.TriggerId, accepted, detail);
        }

        #endregion

        #region Rope Prework (No Final Anchor Judgment)

        [Rpc(SendTo.Server)]
        private void RequestRopeRpc(Vector3 anchorHint, Vector3 direction, int clientTick)
        {
            if (IsServerGameplayBlockedByCardDraft())
            {
                RopeResultRpc(false, Vector3.zero, "CardDraftActive");
                return;
            }

            if (InputValidator.Instance != null)
            {
                if (!InputValidator.Instance.CheckRateLimit(OwnerClientId, "rope3d"))
                {
                    RopeResultRpc(false, Vector3.zero, "RateLimited");
                    return;
                }

                if (!InputValidator.Instance.ValidateTickOrder(OwnerClientId, clientTick, "rope3d"))
                {
                    RopeResultRpc(false, Vector3.zero, "TickRejected");
                    return;
                }

                anchorHint = InputValidator.Instance.SanitizeVector3(anchorHint);
                direction = InputValidator.Instance.SanitizeVector3(direction);
            }

            QueuedServerAction queuedAction = new QueuedServerAction
            {
                ActionType = QueuedActionType.Rope,
                ClientTick = clientTick,
                ReceivedAt = Time.time,
                AnchorHint = anchorHint,
                Direction = direction
            };

            if (!TryEnqueueServerAction(queuedAction, out string rejectReason))
            {
                RopeResultRpc(false, Vector3.zero, rejectReason);
            }
        }

        private void EndRopeAction()
        {
            if (!IsServer)
            {
                return;
            }

            networkIsRoping.Value = false;
            isRopeMoving = false;
            RemoveStatus(StatusMask.Rooted);
            SetStateId(CharacterStateId.Idle);
            RopeEndRpc();
        }

        #endregion

        #region Perk Trigger Prework (No Damage Judgment Yet)

        [Rpc(SendTo.Server)]
        private void RequestPerkTriggerRpc(int triggerId, Vector3 targetHint, int clientTick)
        {
            if (IsServerGameplayBlockedByCardDraft())
            {
                PerkTriggerResultRpc(triggerId, false, "CardDraftActive");
                return;
            }

            if (triggerId < 0)
            {
                PerkTriggerResultRpc(triggerId, false, "InvalidTrigger");
                return;
            }

            if (InputValidator.Instance != null)
            {
                if (!InputValidator.Instance.CheckRateLimit(OwnerClientId, "perk3d"))
                {
                    PerkTriggerResultRpc(triggerId, false, "RateLimited");
                    return;
                }

                if (!InputValidator.Instance.ValidateTickOrder(OwnerClientId, clientTick, "perk3d"))
                {
                    PerkTriggerResultRpc(triggerId, false, "TickRejected");
                    return;
                }

                targetHint = InputValidator.Instance.SanitizeVector3(targetHint);
            }

            QueuedServerAction queuedAction = new QueuedServerAction
            {
                ActionType = QueuedActionType.PerkTrigger,
                ClientTick = clientTick,
                ReceivedAt = Time.time,
                TriggerId = triggerId,
                TargetHint = targetHint
            };

            if (!TryEnqueueServerAction(queuedAction, out string rejectReason))
            {
                PerkTriggerResultRpc(triggerId, false, rejectReason);
            }
        }

        #endregion

        #region Survival and Status

        public void TakeDamage(float damage, ulong attackerId, DamageType damageType = DamageType.Physical)
        {
            if (!IsServer)
            {
                return;
            }

            if (!networkIsAlive.Value)
            {
                return;
            }

            if (invulnerabilityTimer > 0f || !StatusHelper.CanTakeDamage(networkStatusMask.Value))
            {
                return;
            }

            float oldHP = networkHP.Value;
            networkHP.Value = Mathf.Max(0f, networkHP.Value - damage);
            DamageEventRpc(damage, attackerId, (byte)damageType);

            if (networkHP.Value <= 0f)
            {
                Die(attackerId);
            }
            else if (StatusHelper.CanBeInterrupted(networkStatusMask.Value))
            {
                SetStateId(CharacterStateId.Hit);
            }
        }

        public void Heal(float amount)
        {
            if (!IsServer || !networkIsAlive.Value)
            {
                return;
            }

            networkHP.Value = Mathf.Min(maxHP, networkHP.Value + amount);
            HealEventRpc(amount);
        }

        private void Die(ulong killerId)
        {
            networkHP.Value = 0f;
            networkIsAlive.Value = false;
            networkStatusMask.Value = StatusMask.None;
            SetStateId(CharacterStateId.Dead);
            queuedServerActions.Clear();
            serverMoveInput = Vector2.zero;
            networkIsRoping.Value = false;
            isRopeMoving = false;

            if (playerCollider != null)
            {
                playerCollider.enabled = false;
            }

            rb.linearVelocity = Vector3.zero;
            respawnTimer = respawnTime;
            DeathEventRpc(killerId);

            if (CombatManager.Instance != null)
            {
                CombatManager.Instance.OnPlayerDeath(OwnerClientId, killerId);
            }
        }

        public void Respawn(Vector3 position)
        {
            if (!IsServer)
            {
                return;
            }

            if (MapBounds3D.Instance != null)
            {
                position = MapBounds3D.Instance.GetSafeSpawnPoint(position);
            }

            networkHP.Value = maxHP;
            networkIsAlive.Value = true;
            networkStatusMask.Value = StatusMask.None;
            SetStateId(CharacterStateId.Idle);

            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, serverLookYaw, 0f);
            rb.linearVelocity = Vector3.zero;
            lastValidatedServerPosition = position;

            if (playerCollider != null)
            {
                playerCollider.enabled = true;
            }

            invulnerabilityTimer = invulnerabilityDuration;
            AddStatus(StatusMask.Invulnerable);
            RespawnEventRpc(position);
        }

        public void AddStatus(StatusMask status)
        {
            if (!IsServer)
            {
                return;
            }

            networkStatusMask.Value = StatusHelper.AddStatus(networkStatusMask.Value, status);
        }

        public void RemoveStatus(StatusMask status)
        {
            if (!IsServer)
            {
                return;
            }

            networkStatusMask.Value = StatusHelper.RemoveStatus(networkStatusMask.Value, status);
        }

        private void SetStateId(CharacterStateId stateId)
        {
            if (!IsServer)
            {
                return;
            }

            if (networkStateId.Value != stateId)
            {
                networkStateId.Value = stateId;
            }
        }

        public void SetTeam(TeamId team)
        {
            if (!IsServer)
            {
                return;
            }

            networkTeamId.Value = team;
        }

        private void UpdateServerTimers()
        {
            float dt = Time.fixedDeltaTime;

            if (!networkIsAlive.Value && respawnTimer > 0f)
            {
                respawnTimer -= dt;
                if (respawnTimer <= 0f)
                {
                    Vector3 spawnPos = MapBounds3D.Instance != null
                        ? MapBounds3D.Instance.GetRespawnPointNear(lastValidatedServerPosition)
                        : transform.position;
                    Respawn(spawnPos);
                }
            }

            if (invulnerabilityTimer > 0f)
            {
                invulnerabilityTimer -= dt;
                if (invulnerabilityTimer <= 0f)
                {
                    RemoveStatus(StatusMask.Invulnerable);
                }
            }

            if (ropeCooldownTimer > 0f)
            {
                ropeCooldownTimer -= dt;
            }

            if (isRopeMoving && networkIsRoping.Value)
            {
                Vector3 current = transform.position;
                Vector3 toTarget = ropeTargetPosition - current;
                float distance = toTarget.magnitude;
                float step = ropeSpeed * dt;

                if (distance <= 0.25f || step >= distance)
                {
                    transform.position = ropeTargetPosition;
                    rb.linearVelocity = Vector3.zero;
                    EndRopeAction();
                }
                else
                {
                    Vector3 direction = toTarget / distance;
                    Vector3 next = current + direction * step;
                    if (MapBounds3D.Instance != null)
                    {
                        next = MapBounds3D.Instance.ResolveServerPosition(next, current);
                    }

                    rb.linearVelocity = Vector3.zero;
                    rb.position = next;
                    transform.position = next;
                }
            }
        }

        #endregion

        #region Client Smoothing

        private void HandlePositionChanged(Vector3 oldPos, Vector3 newPos)
        {
            if (Time.time < suppressInterpolationUntil)
            {
                transform.position = newPos;
            }
        }

        private void HandleYawChanged(float oldYaw, float newYaw)
        {
            // Applied through interpolation in Update.
        }

        private void InterpolatePosition()
        {
            if (Time.time < suppressInterpolationUntil)
            {
                return;
            }

            float distance = Vector3.Distance(transform.position, networkPosition.Value);
            if (distance > positionSyncThreshold)
            {
                float t = distance > 2f ? 0.5f : 0.2f;
                transform.position = Vector3.Lerp(transform.position, networkPosition.Value, t);
            }
        }

        private void InterpolateRotation()
        {
            Quaternion targetRot = Quaternion.Euler(0f, networkYaw.Value, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 0.2f);
        }

        #endregion

        #region Camera and Callbacks

        private void SetupTopDownCamera()
        {
            if (!TryResolveOwnerCamera(out Camera cam))
            {
                return;
            }

            ownerCamera = cam;

            TopDownCameraFollow3D follow = cam.GetComponent<TopDownCameraFollow3D>();
            if (follow == null)
            {
                follow = cam.gameObject.AddComponent<TopDownCameraFollow3D>();
            }

            follow.SetTarget(transform);

            // Keep imported camera script aligned when present.
            PlayerCamera playerCamera = cam.GetComponent<PlayerCamera>();
            if (playerCamera != null)
            {
                playerCamera.SetTarget(transform);
            }
        }

        private bool TryResolveOwnerCamera(out Camera camera)
        {
            camera = null;

            if (ownerCameraOverride != null && ownerCameraOverride.isActiveAndEnabled)
            {
                camera = ownerCameraOverride;
                return true;
            }

            if (ownerCamera != null && ownerCamera.isActiveAndEnabled)
            {
                camera = ownerCamera;
                return true;
            }

            if (Camera.main != null && Camera.main.isActiveAndEnabled)
            {
                camera = Camera.main;
                return true;
            }

            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (Camera candidate in cameras)
            {
                if (candidate != null && candidate.isActiveAndEnabled)
                {
                    camera = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Allows external local camera manager to bind the owner camera explicitly.
        /// </summary>
        public void SetOwnerCamera(Camera camera)
        {
            if (!IsOwner)
            {
                return;
            }

            ownerCameraOverride = camera;
            ownerCamera = camera;
            SetupTopDownCamera();
        }

        /// <summary>
        /// Returns owner camera for owner-side raycasts (rope, aim, etc.).
        /// </summary>
        public bool TryGetOwnerCamera(out Camera camera)
        {
            if (ownerCamera != null && ownerCamera.isActiveAndEnabled)
            {
                camera = ownerCamera;
                return true;
            }

            if (IsOwner)
            {
                SetupTopDownCamera();
            }

            camera = ownerCamera;
            return camera != null && camera.isActiveAndEnabled;
        }

        private void HandleHPChanged(float oldHP, float newHP)
        {
            OnHPChanged?.Invoke(oldHP, newHP);
        }

        private void HandleStateChanged(CharacterStateId oldState, CharacterStateId newState)
        {
            OnStateChanged?.Invoke(oldState, newState);
        }

        private void HandleStatusChanged(StatusMask oldStatus, StatusMask newStatus)
        {
            OnStatusChanged?.Invoke(newStatus);
        }

        #endregion

        #region Client RPCs

        [Rpc(SendTo.ClientsAndHost)]
        private void DamageEventRpc(float damage, ulong attackerId, byte damageType)
        {
            // Client-side effects/UI hook point.
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void HealEventRpc(float amount)
        {
            // Client-side effects/UI hook point.
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void DeathEventRpc(ulong killerId)
        {
            // Client-side effects/UI hook point.
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void RespawnEventRpc(Vector3 position)
        {
            transform.position = position;
            if (!IsServer)
            {
                suppressInterpolationUntil = Time.time + Mathf.Max(0f, respawnInterpolationSuppressDuration);
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void RopeResultRpc(bool success, Vector3 target, string reason)
        {
            OnRopeResult?.Invoke(success, reason);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void RopeEndRpc()
        {
            OnRopeResult?.Invoke(true, "Ended");
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PerkTriggerResultRpc(int triggerId, bool accepted, string detail)
        {
            OnPerkTriggerResult?.Invoke(triggerId, accepted, detail);
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
            debugLocalTick = localTick;
            debugQueuedActionCount = queuedServerActions.Count;
            debugServerMoveInput = serverMoveInput;
            debugServerLookYaw = serverLookYaw;
            debugNetworkPosition = networkPosition.Value;
            debugNetworkYaw = networkYaw.Value;
            debugNetworkHP = networkHP.Value;
            debugNetworkIsAlive = networkIsAlive.Value;
            debugNetworkStateId = networkStateId.Value;
            debugNetworkStatusMask = networkStatusMask.Value;
            debugNetworkTeamId = networkTeamId.Value;
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
            debugLocalTick = 0;
            debugQueuedActionCount = 0;
            debugServerMoveInput = Vector2.zero;
            debugServerLookYaw = 0f;
            debugNetworkPosition = Vector3.zero;
            debugNetworkYaw = 0f;
            debugNetworkHP = 0f;
            debugNetworkIsAlive = false;
            debugNetworkStateId = CharacterStateId.Idle;
            debugNetworkStatusMask = StatusMask.None;
            debugNetworkTeamId = TeamId.None;
            debugNetworkIsRoping = false;
            debugNetworkRopeTarget = Vector3.zero;
        }

        private bool IsLocalGameplayBlockedByCardDraft()
        {
            if (!IsOwner)
            {
                return false;
            }

            return IsGlobalCardDraftActive();
        }

        private bool IsServerGameplayBlockedByCardDraft()
        {
            if (!IsServer)
            {
                return false;
            }

            return IsGlobalCardDraftActive();
        }

        private bool IsGlobalCardDraftActive()
        {
            GameStateManager gameStateManager = GameStateManager.Instance;
            return gameStateManager != null &&
                   gameStateManager.IsSpawned &&
                   gameStateManager.IsGlobalCardDraftActive;
        }

        #endregion
    }
}
