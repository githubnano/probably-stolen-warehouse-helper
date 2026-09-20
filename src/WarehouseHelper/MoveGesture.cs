namespace WarehouseHelper
{
    /// <summary>Only a fresh mouse click may finish a keyboard-started drag.</summary>
    public sealed class MoveGesture
    {
        public int Slot { get; private set; } = -1;
        public int StartFrame { get; private set; }
        private bool _ready, _pressed;
        public bool Active => Slot >= 0;

        public void Begin(int slot, int frame, bool mouseHeld)
        {
            Slot = slot;
            StartFrame = frame;
            _ready = !mouseHeld;
            _pressed = false;
        }

        public void ObserveMouse(int frame, bool held, bool down)
        {
            if (!Active || frame <= StartFrame) return;
            if (!held) _ready = true;
            if (_ready && down) _pressed = true;
        }

        public bool CanRelease(int frame) => Active && frame > StartFrame && _pressed;
        public bool MatchesSlot(int slot) => Active && Slot == slot;

        public void Reset()
        {
            Slot = -1;
            _ready = _pressed = false;
        }
    }
}
