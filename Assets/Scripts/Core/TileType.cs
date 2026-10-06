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
        GateOpen
    }
}
