// Combat-entity behavior FSM — 7 mutually exclusive states.
//
// State resolution priority, strongest to weakest:
//   Dead > Stunned > HitStun > Parrying > Casting > Moving > Idle
//
// Numeric values are preserved from Buildup for compatibility and do not encode priority.
//
// Skill cast allowed only from { Idle, Moving } (gated additionally by StatusType.Silence
// once StatManager arrives in X2-4).

namespace ArenaCombat.Core.State
{
    public enum CombatantState
    {
        Idle     = 0,
        Moving   = 1,
        Casting  = 2,
        Parrying = 3,
        HitStun  = 4,
        Stunned  = 5,
        Dead     = 6,
    }
}
