using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Content.Shared.Speech;
using Robust.Shared.Random;

namespace Content.Server.Speech.EntitySystems;

public sealed class ZveresAccentSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    private static readonly Regex RegexUpperR = new(@"Р+", RegexOptions.Compiled);
    private static readonly Regex RegexLowerR = new(@"р+", RegexOptions.Compiled);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ZveresAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, ZveresAccentComponent component, AccentGetEvent args)
    {
        var message = args.Message;
        message = RegexUpperR.Replace(
            message,
            _ => _random.Pick(new List<string>() { "Рр", "Ррр" })
        );
        message = RegexLowerR.Replace(
            message,
            _ => _random.Pick(new List<string>() { "рр", "ррр" })
        );
        args.Message = message;
    }
}
