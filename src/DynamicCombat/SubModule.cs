using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace DynamicCombat
{
    public class SubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);

            // Only add our behavior if it's a battle mission
            if (mission.HasMissionBehavior<MissionCombatantsLogic>())
            {
                mission.AddMissionBehavior(new DynamicCombatBehavior());
            }
        }
    }
}
