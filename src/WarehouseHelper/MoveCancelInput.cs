namespace WarehouseHelper
{
    /// <summary>Consume the entire cancelling click, including its later release.</summary>
    public sealed class MoveCancelInput
    {
        private int _blockedFrame = -1;
        private bool _rightReleasePending;

        public bool Blocks(int frame) => _rightReleasePending || frame <= _blockedFrame;

        public bool Observe(int frame, bool moving, bool rightHeld, bool rightDown, bool escapeDown)
        {
            bool cancel = moving && (rightDown || escapeDown);
            if (cancel)
            {
                _blockedFrame = frame;
                _rightReleasePending |= rightDown;
            }
            if (_rightReleasePending)
            {
                _blockedFrame = frame;
                if (!rightHeld) _rightReleasePending = false;
            }
            return cancel;
        }

        public void Reset()
        {
            _blockedFrame = -1;
            _rightReleasePending = false;
        }
    }
}
