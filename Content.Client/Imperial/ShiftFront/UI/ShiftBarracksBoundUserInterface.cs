using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Communications;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;
using Content.Client.Imperial.ShiftFront.UI.Windows.Barracks;

namespace Content.Client.Imperial.ShiftFront.Ui
{
    public sealed class ShiftBarracksBoundUserInterface : BoundUserInterface
    {

        private ShiftBarracks _window;

        public ShiftBarracksBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
            _window = new ShiftBarracks();
        }

        protected override void Open()
        {
            base.Open();
            _window.Close();
            _window.OpenCentered();

        }
    }
}
