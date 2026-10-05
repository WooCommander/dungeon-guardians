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
        // A trapped guardian climbs out this many ticks before its hole closes (0.5 s); the hole then refills empty.
        public int GuardianClimbOutTicks = 15;
        public int GuardianRespawnTicks = 120;
    }
}
