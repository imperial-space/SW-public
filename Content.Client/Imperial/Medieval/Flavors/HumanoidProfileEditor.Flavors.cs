
using Content.Client.Imperial.Medieval.Flavors;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    public byte[]? FlavorImage;
    private FlavorHelpWindow? _helpWindow;
    private FlavorExamineWindow? _testWindow;
    public void UpdateFlavorHandle()
    {
        if (_flavorText == null)
            return;
        _flavorText.OnFlavorImageChanged += OnImageSelected;
        _flavorText.OnHelpPressed += OnHelpPressed;
        _flavorText.OnTestPressed += OnTestPressed;
    }
    public void OnImageSelected(byte[] image)
    {
        if (_flavorText == null)
            return;

        FlavorImage = image;
        _flavorText.FlavorImage.TextureNormal = _flavors.GetImageFromByteArray(image).texture;
        IsDirty = true;
        UpdateSaveButton();
    }
    public void SetFlavorImage(byte[]? image)
    {
        if (_flavorText == null)
            return;

        FlavorImage = image;
        _flavorText.FlavorImage.TextureNormal = _flavors.GetImageFromByteArray(image).texture;
    }
    public void OnHelpPressed()
    {
        if (_helpWindow != null)
            _helpWindow.Close();

        _helpWindow = new();
        _helpWindow.OpenCentered();
    }
    public void OnTestPressed()
    {
        if (_testWindow != null)
            _testWindow.Close();

        if (Profile == null)
            return;

        _testWindow = new(Profile.FlavorText, FlavorImage);
        _testWindow.OpenCentered();
    }
}
