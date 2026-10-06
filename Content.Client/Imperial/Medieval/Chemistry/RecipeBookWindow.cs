using System.Numerics;
using Content.Client.Guidebook.Controls;
using Content.Client.Message;
using Content.Client.UserInterface.Controls;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Imperial.Medieval.Chemistry;
using Content.Shared.Imperial.Medieval.ChemistryRandomization;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.Imperial.Chemistry;

public sealed class PotionBookWindow
    : FancyWindow
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    private readonly ScrollContainer _box;
    private readonly BoxContainer _container;
    private readonly LineEdit _search;

    public PotionBookWindow()
    {
        IoCManager.InjectDependencies(this);
        Title = Loc.GetString("imperial-medieval-recipebook");
        MinSize = new Vector2(100, 200);
        Resizable = true;
        SetSize = new Vector2(900, 700);
        _container = new BoxContainer()
        {
            Orientation = LayoutOrientation.Vertical
        };
        _search = new LineEdit()
        {
            PlaceHolder = Loc.GetString("guidebook-filter-placeholder-text"),
            HorizontalExpand = true
        };
        _box = new ScrollContainer()
        {
            HScrollEnabled = false,
            HorizontalExpand = true,
            VerticalExpand = true,
            Children = {
                new Control()
                {
                    Children = {
                        _container
                    }
                }
            }
        };
        AddChild(new BoxContainer()
        {
            Orientation = LayoutOrientation.Vertical,
            Children = {
                _search,
                _box
            },
            Margin = new Thickness(5, 28, 5, 5)
        });
        _search.OnTextChanged += (_) =>
        {
            foreach (var entry in _container.Children)
            {
                if (SearchForText(entry, _search.Text))
                    entry.Visible = true;
                else
                    entry.Visible = false;
            }
        };
    }
    private bool SearchForText(Control searchin, string text)
    {
        if (searchin is Label label)
        {
            if (label.Text != null)
                if (label.Text.ToLower().Contains(text.ToLower()))
                    return true;
        }
        if (searchin is RichTextLabel richlabel)
        {
            if (richlabel.Text != null)
                if (richlabel.Text.ToLower().Contains(text.ToLower()))
                    return true;
        }
        foreach (var child in searchin.Children)
        {
            if (SearchForText(child, text))
                return true;
        }
        return false;
    }

    public void UpdateState(PotionBookUserInterfaceState state)
    {
        _container.DisposeAllChildren();
        foreach (var recipe in state.Recipes)
        {
            foreach (var id in recipe.Products)
                _container.AddChild(CreatePotionCard(_proto.Index<ReagentPrototype>(id), recipe.Description));
            foreach (var id in recipe.EntityProducts)
                _container.AddChild(CreateEntityCard(_proto.Index<EntityPrototype>(id), recipe.Description));
        }
        foreach (var entry in _container.Children)
            entry.Visible = SearchForText(entry, _search.Text);
    }

    private GuideReagentEmbed CreatePotionCard(ReagentPrototype reagent, string recipe)
    {
        var card = new GuideReagentEmbed(reagent, true) { HorizontalExpand = true };
        var color = SharedChemistryRandomizationSystem.GetColor(reagent);
        card.FindControl<PanelContainer>("NameBackground").PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = color,
        };
        var textColor = 0.2126f * color.R + 0.7152f * color.G + 0.0722f * color.B > 0.5f
            ? Color.Black
            : Color.White;
        card.FindControl<RichTextLabel>("ReagentName").SetMarkup(Loc.GetString("guidebook-reagent-name",
            ("color", textColor), ("name", reagent.LocalizedName)));

        var preview = new Control
        {
            MinSize = new Vector2(64),
            VerticalAlignment = VAlignment.Top,
            Children =
            {
                new TextureRect
                {
                    TexturePath = "/Textures/Objects/Specific/Chemistry/bottle.rsi/bottle-1.png",
                    TextureScale = new Vector2(2),
                },
                new TextureRect
                {
                    TexturePath = "/Textures/Objects/Specific/Chemistry/bottle.rsi/bottle-1-6.png",
                    TextureScale = new Vector2(2),
                    ModulateSelfOverride = color,
                },
            },
        };
        var description = new RichTextLabel { HorizontalExpand = true, Margin = new Thickness(10, 0, 0, 0) };
        description.SetMessage(recipe);
        var recipes = card.FindControl<GridContainer>("RecipesDescriptionContainer");
        recipes.DisposeAllChildren();
        recipes.AddChild(new BoxContainer { HorizontalExpand = true, Children = { preview, description } });
        card.FindControl<BoxContainer>("RecipesContainer").Visible = true;
        return card;
    }

    private Control CreateEntityCard(EntityPrototype prototype, string recipe)
    {
        var name = new RichTextLabel { HorizontalAlignment = HAlignment.Center };
        name.SetMarkup(Loc.GetString("guidebook-reagent-name", ("color", Color.White), ("name", prototype.Name)));
        var description = new RichTextLabel { HorizontalExpand = true, Margin = new Thickness(10, 0, 0, 0) };
        description.SetMessage(recipe);
        var body = new CollapsibleBody
        {
            Children =
            {
                new BoxContainer
                {
                    HorizontalExpand = true,
                    Margin = new Thickness(10, 0, 10, 0),
                    Children =
                    {
                        new GuideEntityEmbed(prototype.ID, false, false)
                        {
                            MinSize = new Vector2(64),
                            Scale = new Vector2(2),
                            VerticalAlignment = VAlignment.Top,
                        },
                        description,
                    },
                },
            },
        };
        var details = new RichTextLabel { HorizontalExpand = true, Margin = new Thickness(10, 5, 10, 10) };
        details.SetMessage(prototype.Description);
        return new PanelContainer
        {
            HorizontalExpand = true,
            Margin = new Thickness(5),
            PanelOverride = new StyleBoxFlat { BorderThickness = new Thickness(1), BorderColor = Color.FromHex("#777777") },
            Children =
            {
                new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    Children =
                    {
                        new PanelContainer
                        {
                            HorizontalExpand = true,
                            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#303946") },
                            Children = { name },
                        },
                        new Collapsible(new CollapsibleHeading(Loc.GetString("guidebook-reagent-recipes-header")), body)
                        {
                            HorizontalExpand = true,
                        },
                        details,
                    },
                },
            },
        };
    }

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
    {
        return DragMode.Move;
    }
}
