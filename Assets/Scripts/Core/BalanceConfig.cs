namespace DungeonGuardians.Core
{
    public sealed class BalanceConfig
    {
        public float TickRate = 30f;
        // Movement speeds in cells per second.
        public float PlayerSpeed = 3f;
        public float GuardianSpeed = 2.3f;
        public int DigTicks = 10;
        public int HoleTicks = 180;
        public int GuardianRespawnTicks = 120;
    }
}
