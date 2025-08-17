using Content.Client.Imperial.ShiftFront.UI.Windows.Barracks;
using Robust.Client.Graphics;

namespace Content.Client.Imperial.ShiftFront.Ui
{
    public sealed class ShiftBarracksBoundUserInterface : BoundUserInterface
    {
        private ShiftBarracks? _window;

        public ShiftBarracksBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {

        }

        protected override void Open()
        {
            base.Open();

            if (_window == null || _window.Disposed)
            {
                _window = new ShiftBarracks();
                _window.OnClose += Close;
            }

            _window.OpenCentered();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                if (_window != null)
                {
                    _window.OnClose -= Close;
                    _window.Close();
                    _window = null;
                }
            }
        }
    }
}
