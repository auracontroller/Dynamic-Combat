using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace DynamicCombat
{
    public class DynamicCombatSettings : AttributeGlobalSettings<DynamicCombatSettings>
    {
        public override string Id => "DynamicCombat_v1";
        public override string DisplayName => "Dynamic Combat";
        public override string FolderName => "DynamicCombat";
        public override string FormatType => "json2";

        [SettingPropertyInteger("Max Attack Slots", 1, 10, "0", Order = 1, RequireRestart = false, HintText = "Maximum number of simultaneous attackers allowed per target.")]
        [SettingPropertyGroup("Engagement Settings")]
        public int MaxAttackSlots { get; set; } = 2;

        [SettingPropertyInteger("Max Queue Slots", 1, 20, "0", Order = 2, RequireRestart = false, HintText = "Maximum number of queued attackers per target. Attackers beyond this limit will retarget.")]
        [SettingPropertyGroup("Engagement Settings")]
        public int MaxQueueSlots { get; set; } = 3;

        [SettingPropertyFloatingInteger("Min Engagement Distance", 0.5f, 5.0f, "0.00", Order = 3, RequireRestart = false, HintText = "Inner boundary threshold. Enemies will step back if the target gets closer than this.")]
        [SettingPropertyGroup("Engagement Settings")]
        public float MinDistance { get; set; } = 1.5f;

        [SettingPropertyFloatingInteger("Max Engagement Distance", 1.0f, 20.0f, "0.00", Order = 4, RequireRestart = false, HintText = "Outer boundary threshold. Enemies will advance if the target gets farther than this.")]
        [SettingPropertyGroup("Engagement Settings")]
        public float MaxDistance { get; set; } = 3.5f;

        [SettingPropertyInteger("Rear Blind-Spot Angle", 30, 180, "0", Order = 5, RequireRestart = false, HintText = "The total angle (in degrees) behind a target where enemies are forced into containment hold.")]
        [SettingPropertyGroup("Engagement Settings")]
        public int RearAngle { get; set; } = 120;
    }
}
