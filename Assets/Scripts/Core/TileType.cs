namespace DungeonGuardians.Core
{
    public enum TileType
    {
        Air,
        Solid,
        Brick,
        Ladder,
        Bar,
        ExitClosed,
        ExitOpen,
        Altar,
        // Seal trial: a pressure plate in the floor, and the gates it opens.
        PressurePlate,
        GateClosed,
        GateOpen,
        // Wooden planks: they hold the explorer and ordinary guardians, but give way under a heavy one.
        FragileFloor
    }
}
