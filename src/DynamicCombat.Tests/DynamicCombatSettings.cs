namespace DynamicCombat {
    public class DynamicCombatSettings {
        public static DynamicCombatSettings Instance { get; set; } = new DynamicCombatSettings();
        public int MaxAttackSlots { get; set; } = 2;
        public int MaxQueueSlots { get; set; } = 4;
        public float RearAngle { get; set; } = 90f;
    }
}
