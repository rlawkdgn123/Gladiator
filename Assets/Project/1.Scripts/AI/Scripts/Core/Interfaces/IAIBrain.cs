using Game.Core.Types;
using Game.Core.Enums;

namespace Game.Core.Interfaces
{
    public interface IAIBrain
    {
        CombatAction Decide(in CombatObservation observation);
    }
}