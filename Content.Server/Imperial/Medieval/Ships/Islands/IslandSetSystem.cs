using System.Text.RegularExpressions;
using Content.Server.Shuttles.Systems;
using Content.Shared.Imperial.Medieval.Ships.Islands;
using Content.Shared.Paper;
using NetCord;

namespace Content.Server.Imperial.Medieval.Ships.Islands;

/// <summary>
/// This handles...
/// </summary>
public sealed class IslandSetSystem : EntitySystem
{
    [Dependency] private readonly ShuttleSystem _shuttleSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    private static readonly Regex IslandNameRegex = new Regex(
        @"Я нарекаю сей остров ""([^""]+)""",
        RegexOptions.Compiled // Оптимизация для часто используемых шаблонов
    );


    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<IslandSetterComponent, ComponentInit>(OnInit);
    }

    private void OnInit(EntityUid uid, IslandSetterComponent component, ComponentInit args)
    {
        var land = _transform.GetParentUid(uid);

        EnsureComp<IslandComponent>(land);

        _shuttleSystem.Disable(land);

        if (!TryComp<PaperComponent>(uid, out var paperComp))
        {
            Del(uid);
            return;
        }

        var text = paperComp.Content;

        var match = IslandNameRegex.Match(text);

        if (!match.Success)
            return;

        var extractedWord = match.Groups[1].Value;

        _metaData.SetEntityName(land, extractedWord);
        Del(uid);
    }
}
