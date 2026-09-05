using System.Globalization;
using System.Linq;
using Content.Client.UserInterface.Controls;
using Content.Shared.Imperial.Medieval.SkillCheck;
using Content.Shared.Imperial.Medieval.Skills;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Medieval.SkillCheck;

public sealed class SkillCheckSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SpriteSystem _sprites = default!;
    [Dependency] private readonly SharedPointLightSystem _lights = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;

    public IEnumerable<RadialMenuOptionBase> AddSkillCheckMenu(
        IEnumerable<RadialMenuOptionBase> emotes,
        EntityUid? player)
    {
        foreach (var emote in emotes)
            yield return emote;

        if (!TryComp<SkillsComponent>(player, out var skills))
            yield break;

        var buttons = new List<RadialMenuOptionBase>();
        foreach (var skill in _prototypes.EnumeratePrototypes<SkillPrototype>()
                     .OrderBy(skill => Loc.GetString(skill.Name)))
        {
            var level = skills.Levels.GetValueOrDefault(skill.ID, 10);
            var icon = skill.Icons.OrderBy(pair => pair.Key)
                .LastOrDefault(pair => pair.Key <= level, skill.Icons.First());
            buttons.Add(new RadialMenuActionOption<ProtoId<SkillPrototype>?>(RequestSkillCheck, skill.ID)
            {
                ToolTip = Loc.GetString(skill.Name),
                IconSpecifier = RadialMenuIconSpecifier.With(
                    new SpriteSpecifier.Rsi(new ResPath(skill.RsiPath), icon.Value)),
            });
        }

        var dieIcon = new SpriteSpecifier.Rsi(
            new ResPath("/Textures/Imperial/Medieval/Effects/SkillCheck/d20.rsi"), "20");
        buttons.Add(new RadialMenuActionOption<ProtoId<SkillPrototype>?>(RequestSkillCheck, null)
        {
            ToolTip = Loc.GetString("medieval-skill-check-roll-die"),
            IconSpecifier = RadialMenuIconSpecifier.With(dieIcon),
        });

        yield return new RadialMenuNestedLayerOption(buttons)
        {
            ToolTip = Loc.GetString("medieval-skill-check-category"),
            IconSpecifier = RadialMenuIconSpecifier.With(dieIcon),
        };
    }

    private void RequestSkillCheck(ProtoId<SkillPrototype>? skill)
    {
        RaiseNetworkEvent(new SkillCheckRequestEvent(skill));
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<SkillCheckDieComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var die, out var sprite))
        {
            if (!_sprites.TryGetLayer((uid, sprite), 0, out var layer, false))
                continue;

            var stateId = die.Result.ToString(CultureInfo.InvariantCulture);
            if (layer.State != stateId)
                _sprites.LayerSetRsiState(layer, stateId);

            if (layer.ActualRsi is not { } rsi || !rsi.TryGetState(layer.State, out var state))
                continue;

            _sprites.LayerSetAutoAnimated(layer, false);
            var lastFrameDelay = state.GetDelay(state.DelayCount - 1);
            var lastFrameStart = state.TotalDelay - lastFrameDelay;
            var elapsed = (_timing.CurTime - die.RollStartedAt).TotalSeconds;
            var progress = Math.Clamp(elapsed / die.AnimationDuration.TotalSeconds, 0, 1);
            var animationTime = progress >= 1
                ? lastFrameStart + lastFrameDelay / 2
                : lastFrameStart * (float) progress;
            _sprites.LayerSetAnimationTime(layer, animationTime);

            if (_lights.TryGetLight(uid, out var light) && light.Enabled)
            {
                var rotation = _transform.GetWorldRotation(uid) + _eyeManager.CurrentEye.Rotation;
                light.Offset = (-rotation).RotateVec(sprite.Offset);
            }
        }
    }
}
