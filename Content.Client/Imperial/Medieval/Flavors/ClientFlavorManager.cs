using System.IO;
using System.Linq;
using Content.Shared.Imperial.Medieval.Flavors;
using Robust.Client;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Network;
using Robust.Shared.Utility;
using Robust.Shared.ContentPack;
using Content.Shared.DetailExaminable;

namespace Content.Client.Imperial.Medieval.Flavors
{
    public sealed class ClientFlavorManager : SharedFlavorManager
    {
        public const string FallbackFlavorImagePath = "/Textures/Imperial/Medieval/Flavors/flavors.rsi";
        public const string FallbackFlavorImageState = "black";
        [Dependency] private readonly IClientNetManager _netManager = default!;
        [Dependency] private readonly IBaseClient _baseClient = default!;
        [Dependency] private readonly IResourceCache _resources = default!;
        [Dependency] private readonly IDependencyCollection _collection = default!;
        private SpriteSystem _sprite = default!;
        public event Action? OnServerDataLoaded;
        public Dictionary<int, byte[]> Images { get; private set; } = new();

        public void Initialize()
        {
            _netManager.RegisterNetMessage<FlavorImagesMsg>(ImagesReceived);
            _netManager.RegisterNetMessage<MsgUpdateFlavorImage>();
            _netManager.RegisterNetMessage<UpdateFlavorCacheMsg>(CacheUpdate);
            _netManager.RegisterNetMessage<OpenFlavorWindowMsg>(OpenFlavorWindow);

            _baseClient.RunLevelChanged += BaseClientOnRunLevelChanged;
        }
        private void CacheUpdate(UpdateFlavorCacheMsg msg)
        {
            foreach (var (path, image) in msg.CacheImages)
            {
                SetImage(path, image);
            }
        }

        private void BaseClientOnRunLevelChanged(object? sender, RunLevelChangedEventArgs e)
        {
            if (e.NewLevel == ClientRunLevel.Initialize)
            {
                Images = new();
            }
        }

        public void UpdateImage(int slot, byte[]? image)
        {
            if (image == null)
                image = Array.Empty<byte>();

            Images[slot] = image;
            var msg = new MsgUpdateFlavorImage
            {
                Slot = slot,
                Image = image
            };
            _netManager.ClientSendMessage(msg);
        }

        private void ImagesReceived(FlavorImagesMsg message)
        {
            Images = message.PlayerImages;
            foreach (var (path, image) in message.CacheImages)
            {
                SetImage(path, image);
            }
            OnServerDataLoaded?.Invoke();
        }
        public (Texture texture, bool fallback) GetImageFromByteArray(byte[]? bytes)
        {
            if (_sprite == null)
                _sprite = EntityManager.System<SpriteSystem>();

            var fallback = _sprite.Frame0(new SpriteSpecifier.Rsi(new(FallbackFlavorImagePath), FallbackFlavorImageState));
            if (bytes == null || bytes.Count() == 0)
                return (fallback, true);

            Texture result;
            using (var stream = new MemoryStream(bytes))
            {
                result = Texture.LoadFromPNGStream(stream, ".webp", new() { SampleParameters = new() { Filter = true } });
            }
            if (!ImageAllowed(result.Width, result.Height))
                return (fallback, true);

            return (result, false);
        }
        public (Texture texture, bool fallback) GetImage(string path)
        {
            ResPath resPath = new($"/{path}.webp");

            byte[]? resultBytes = null;
            if (_resources.UserData.Exists(resPath))
                resultBytes = _resources.UserData.ReadAllBytes(resPath);

            return GetImageFromByteArray(resultBytes);
        }
        public void SetImage(string path, byte[] image)
        {
            _resources.UserData.WriteAllBytes(new($"/{path}.webp"), image);
        }
        public void OpenFlavorWindow(OpenFlavorWindowMsg msg)
        {
            if (msg.Path == null)
                return;

            if (!_resources.UserData.Exists(new($"/{msg.Path}.webp")))
                return;

            var window = new FlavorExamineWindow(msg.Description, msg.Path);
            window.OpenCentered();
        }
        public override bool TryExamine(EntityUid user, Entity<DetailExaminableComponent> ent)
        {
            if (EntityManager.TryGetComponent<FlavorImageComponent>(ent, out var imageComponent) && imageComponent.ImagePath != null && _resources.UserData.Exists(new($"/{imageComponent.ImagePath}.webp")))
                return true;

            return false;
        }
    }
}
