using Content.Shared.Imperial.ColorHelper;
using Content.Shared.Imperial.Medieval.MedievalItemRustComponent;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Client.Imperial.Medieval;


/// <summary>
/// Covers items with <see cref="MedievalMeleeResourceComponent" /> rust depending on their condition
/// </summary>
public sealed partial class MedievalItemRustSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;


    private Dictionary<EntityUid, ShaderInstance> _shaders = new();


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalItemRustComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<MedievalItemRustComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<MedievalItemRustComponent, BeforePostShaderRenderEvent>(OnShaderRender);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _shaders.Clear();
    }

    private void OnStartup(EntityUid uid, MedievalItemRustComponent component, ComponentStartup args)
    {
        _shaders.Add(
            uid,
            _prototypeManager.Index<ShaderPrototype>("MedievalRust").InstanceUnique()
        );

        component.Seed = MathF.Floor(_random.NextFloat() * 100);

        SetShader(uid, true);
    }

    private void OnShutdown(EntityUid uid, MedievalItemRustComponent component, ComponentShutdown args)
    {
        _shaders.Remove(uid);

        SetShader(uid, false);
    }


    private void OnShaderRender(EntityUid uid, MedievalItemRustComponent component, BeforePostShaderRenderEvent args)
    {
        _shaders[uid].SetParameter("rustColor", ColorHelper.ToVector3(component.RustColor));
        _shaders[uid].SetParameter("rustPrecent", component.RustPercentage);
        _shaders[uid].SetParameter("seed", component.Seed);
    }

    #region Helpers

    private void SetShader(EntityUid uid, bool enabled, SpriteComponent? sprite = null)
    {
        if (!Resolve(uid, ref sprite, false))
            return;

        sprite.Color = Color.White;
        sprite.PostShader = enabled ? _shaders[uid] : null;
        sprite.RaiseShaderEvent = enabled;
    }

    #endregion
}
