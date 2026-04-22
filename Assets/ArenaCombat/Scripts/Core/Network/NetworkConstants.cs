// ARCH TAG: SHARED
// ARCH SCOPE: Core enums/constants used across legacy 2D and target 3D.
// ARCH STATUS: TARGET_3D_PENDING

using System;

namespace ArenaCombat.Core.Network
{
    /// <summary>
    /// Match state for game flow management
    /// Server authoritative - only server can change
    /// </summary>
    public enum MatchState : byte
    {
        None = 0,           // Initial state
        WaitingForPlayers,  // Lobby, waiting for players
        Countdown,          // Match starting countdown
        InProgress,         // Match in progress
        Paused,             // Match paused
        RoundEnd,           // Round ended, showing results
        MatchEnd,           // Match ended, final results
        Disconnected        // Connection lost
    }

    /// <summary>
    /// Game mode types
    /// </summary>
    public enum GameMode : byte
    {
        None = 0,
        FreeForAll,         // Everyone vs Everyone
        TeamDeathmatch,     // Team vs Team
        BossRaid,           // Players vs Boss
        Practice            // Training mode
    }

    /// <summary>
    /// Character state IDs for state machine
    /// Used for animation and behavior sync
    /// </summary>
    public enum CharacterStateId : byte
    {
        Idle = 0,
        Moving,
        Jumping,
        Falling,
        Landing,
        Attacking,
        Hit,                // Taking damage
        Stunned,
        Knockback,
        Dead,
        Reviving,
        Roping,             // Rope movement state (top-down 3D target)
        PerkCasting         // Perk trigger wind-up/cast state (target)
    }

    /// <summary>
    /// Status effect mask (bitflags)
    /// Can have multiple effects at once
    /// </summary>
    [Flags]
    public enum StatusMask : ushort
    {
        None = 0,
        Stunned = 1 << 0,       // Cannot act
        Rooted = 1 << 1,        // Cannot move
        Silenced = 1 << 2,      // Cannot use skills
        Slowed = 1 << 3,        // Movement speed reduced
        Invulnerable = 1 << 4,  // Cannot take damage
        SuperArmor = 1 << 5,    // Cannot be interrupted
        Burning = 1 << 6,       // Damage over time
        Poisoned = 1 << 7,      // Damage over time
        Frozen = 1 << 8,        // Cannot act + visual effect
        Invisible = 1 << 9     // Hidden from enemies
    }

    /// <summary>
    /// Boss phase for boss raid mode
    /// </summary>
    public enum BossPhase : byte
    {
        None = 0,
        Phase1,             // 100% - 70% HP
        Phase2,             // 70% - 40% HP
        Phase3,             // 40% - 10% HP
        Enrage,             // Below 10% HP
        Defeated
    }

    /// <summary>
    /// Damage types for combat calculation
    /// </summary>
    public enum DamageType : byte
    {
        Physical = 0,
        Magical,
        True,               // Ignores defense
        Environmental       // Stage hazards
    }

    /// <summary>
    /// Attack types for hit detection
    /// </summary>
    public enum AttackType : byte
    {
        Light = 0,          // Fast, low damage
        Heavy,              // Slow, high damage
        Skill,              // Skill attack
        Ultimate,           // Ultimate attack
        Counter             // Counter attack
    }

    /// <summary>
    /// Team IDs for team-based modes
    /// </summary>
    public enum TeamId : byte
    {
        None = 0,
        Team1,
        Team2,
        Spectator
    }

    /// <summary>
    /// Network tick rate constants
    /// </summary>
    public static class NetworkTickRate
    {
        public const float INPUT_SEND_RATE = 0.016f;        // 60Hz input send
        public const float POSITION_SYNC_RATE = 0.033f;     // 30Hz position sync
        public const float STATE_SYNC_RATE = 0.05f;         // 20Hz state sync
        public const int TICKS_PER_SECOND = 60;
        public const float TICK_DURATION = 1f / TICKS_PER_SECOND;
    }

    /// <summary>
    /// Network validation constants
    /// </summary>
    public static class NetworkValidation
    {
        public const int MAX_INPUT_QUEUE_SIZE = 32;
        public const float MAX_POSITION_DEVIATION = 5f;     // Max allowed position difference
        public const float INPUT_TIMEOUT = 0.5f;            // Input considered stale after this
        public const int MAX_MISSED_INPUTS = 10;            // Disconnect after this many missed inputs
        public const float RATE_LIMIT_WINDOW = 1f;          // Rate limit check window
        public const int MAX_REQUESTS_PER_SECOND = 60;      // Max requests per second
    }

    /// <summary>
    /// Target top-down 3D movement limits used by migration controllers.
    /// </summary>
    public static class TopDown3DConstants
    {
        public const float MAX_MOVE_INPUT_MAGNITUDE = 1f;
        public const float DEFAULT_MOVE_SPEED = 7f;
        public const float DEFAULT_ROTATION_LERP = 16f;
        public const float DEFAULT_ROPE_SPEED = 18f;
        public const float DEFAULT_ROPE_MAX_DISTANCE = 12f;
        public const float DEFAULT_ROPE_COOLDOWN = 1.5f;
    }

    /// <summary>
    /// Combat constants
    /// </summary>
    public static class CombatConstants
    {
        public const float DEFAULT_MAX_HP = 100f;
        public const float RESPAWN_TIME = 3f;
        public const float INVULNERABILITY_AFTER_SPAWN = 2f;
        public const float HIT_STUN_DURATION = 0.2f;
        public const float KNOCKBACK_DURATION = 0.3f;
        public const float COMBO_WINDOW = 0.5f;             // Time window for combo input
    }

    /// <summary>
    /// Helper methods for status effects
    /// </summary>
    public static class StatusHelper
    {
        public static bool HasStatus(StatusMask current, StatusMask check)
        {
            return (current & check) != StatusMask.None;
        }

        public static StatusMask AddStatus(StatusMask current, StatusMask add)
        {
            return current | add;
        }

        public static StatusMask RemoveStatus(StatusMask current, StatusMask remove)
        {
            return current & ~remove;
        }

        public static bool CanMove(StatusMask status)
        {
            return !HasStatus(status, StatusMask.Stunned | StatusMask.Rooted | StatusMask.Frozen);
        }

        public static bool CanAct(StatusMask status)
        {
            return !HasStatus(status, StatusMask.Stunned | StatusMask.Frozen);
        }

        public static bool CanUseSkill(StatusMask status)
        {
            return !HasStatus(status, StatusMask.Stunned | StatusMask.Silenced | StatusMask.Frozen);
        }

        public static bool CanTakeDamage(StatusMask status)
        {
            return !HasStatus(status, StatusMask.Invulnerable);
        }

        public static bool CanBeInterrupted(StatusMask status)
        {
            return !HasStatus(status, StatusMask.SuperArmor | StatusMask.Invulnerable);
        }
    }
}
