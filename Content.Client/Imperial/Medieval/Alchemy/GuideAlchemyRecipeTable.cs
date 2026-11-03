using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Client.Guidebook.Richtext;
using Content.Client.UserInterface.Controls;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.Alchemy;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Medieval.Alchemy;

[UsedImplicitly]
public sealed class GuideAlchemyRecipeTable : TableContainer, IDocumentTag
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private AlchemyRecipeTag? _tag;

    public GuideAlchemyRecipeTable()
    {
        IoCManager.InjectDependencies(this);
        Columns = 3;
        HorizontalExpand = true;
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        if (!args.TryGetValue("Tag", out var value) ||
            !Enum.TryParse<AlchemyRecipeTag>(value, out var tag) || !Enum.IsDefined(tag))
            return false;

        _tag = tag;
        Rebuild();
        control = this;
        return true;
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _prototypes.PrototypesReloaded += OnPrototypesReloaded;
        Rebuild();
    }

    protected override void ExitedTree()
    {
        _prototypes.PrototypesReloaded -= OnPrototypesReloaded;
        base.ExitedTree();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<AlchemyRecipePrototype>() || args.WasModified<AlchemyOperationPrototype>() ||
            args.WasModified<ReagentPrototype>() || args.WasModified<EntityPrototype>())
            Rebuild();
    }

    private void Rebuild()
    {
        DisposeAllChildren();
        if (_tag == null)
            return;

        AddCell(Loc.GetString("alchemy-guide-products"), true);
        AddCell(Loc.GetString("alchemy-guide-ingredients"), true);
        AddCell(Loc.GetString("alchemy-guide-steps"), true);

        var operations = _prototypes.EnumeratePrototypes<AlchemyOperationPrototype>().ToDictionary(p => p.ID);
        var generation = new AlchemyGenerationComponent();
        var recipes = _prototypes.EnumeratePrototypes<AlchemyRecipePrototype>()
            .Where(p => !p.Abstract && !p.Randomized && p.Tag == _tag &&
                (p.Tag != AlchemyRecipeTag.Potion || p.Tier == 1))
            .Select(p => AlchemyGenerationSystem.GenerateRecipe(p, operations, new Random(0), generation))
            .Select(recipe => (Recipe: recipe, Products: DescribeContents(recipe.Products, recipe.EntityProducts)))
            .OrderBy(entry => entry.Products)
            .ThenBy(entry => entry.Recipe.Id);

        foreach (var (recipe, products) in recipes)
        {
            AddCell(products);
            AddCell(DescribeContents(recipe.Ingredients, recipe.Entities));
            AddCell(string.Join("\n", recipe.Steps.Select((id, index) =>
                Loc.GetString("alchemy-guide-step", ("number", index + 1),
                    ("operation", Loc.GetString(operations[id].Name))))));
        }
    }

    private string DescribeContents(
        IReadOnlyDictionary<string, FixedPoint2> reagents,
        IReadOnlyDictionary<string, int> entities)
    {
        var lines = reagents.Select(pair => Loc.GetString("alchemy-guide-reagent",
            ("name", _prototypes.Index<ReagentPrototype>(pair.Key).LocalizedName),
            ("amount", pair.Value.ToString())));
        return string.Join("\n", lines.Concat(entities.Select(pair => Loc.GetString("alchemy-guide-entity",
            ("name", _prototypes.Index<EntityPrototype>(pair.Key).Name), ("amount", pair.Value)))));
    }

    private void AddCell(string text, bool header = false)
    {
        var label = new RichTextLabel { Margin = new Thickness(6), HorizontalExpand = true };
        label.SetMessage(text);
        AddChild(new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = header ? Color.FromHex("#303946") : Color.FromHex("#20252D"),
                BorderColor = Color.FromHex("#555B65"),
                BorderThickness = new Thickness(1),
            },
            Children = { label },
        });
    }
}
