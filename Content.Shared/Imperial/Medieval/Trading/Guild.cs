using System.Linq;
using Content.Shared.Imperial.Medieval.Trading.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.Imperial.Medieval.Trading;

public sealed class Guild
{
    public Guid Id { get; }
    public string Name { get; }
    public string IconPath { get; }
    public ProtoId<GuildTypePrototype> TypePrototype { get; }
    public List<GuildTradingItem> Items { get; }

    public Guild(GuildTypePrototype prototype, IRobustRandom random, IPrototypeManager prototypeManager)
    {
        Id = Guid.NewGuid();
        TypePrototype = prototype.ID;
        Items = prototype.Items.Select(item => item with { }).ToList();

        var namePrototype = prototypeManager.Index(prototype.Name);
        Name = GenerateName(namePrototype, random);

        if (prototype.Icons.Count > 0)
        {
            var icon = random.Pick(prototype.Icons);
            IconPath = prototypeManager.Index(icon).TexturePath;
        }
        else if (prototypeManager.TryGetRandom<GuildIconPrototype>(random, out var selected) &&
                 selected is GuildIconPrototype guildIcon)
        {
            IconPath = guildIcon.TexturePath;
        }
        else
        {
            IconPath = string.Empty;
        }
    }

    private static string GenerateName(GuildNamePrototype prototype, IRobustRandom random)
    {
        var parts = new List<string>();
        for (var index = 1; index <= prototype.PartCount; index++)
        {
            var partIndex = index;
            var candidates = prototype.Parts
                .Where(part => partIndex >= part.Min && partIndex <= part.Max)
                .ToArray();
            parts.Add(random.Pick(candidates).Text);
        }

        return string.Join(prototype.Split, parts);
    }
}
