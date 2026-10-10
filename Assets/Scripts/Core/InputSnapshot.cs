namespace DungeonGuardians.Core
{
    public struct InputSnapshot
    {
        public bool Left;
        public bool Right;
        public bool Up;
        public bool Down;
        public bool DigLeft;
        public bool DigRight;
        public bool Restart;
        public bool Pause;
        public bool TiltVertical;

        public static InputSnapshot Empty => new InputSnapshot();
    }
}
