
using Content.Server.Popups;
using Content.Shared.Imperial.Medieval.Chemistry;
using Content.Shared.Interaction;
using Robust.Server.GameObjects;
using Content.Server.Imperial.Medieval.Alchemy;

namespace Content.Server.Imperial.Medieval.Chemistry;

public sealed class RecipeBookSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly AlchemySystem _alchemy = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<MedievalRecipeBookComponent, InteractUsingEvent>(PutRecipe);
        SubscribeLocalEvent<MedievalRecipeBookComponent, BoundUIOpenedEvent>(OnOpened);
    }
    private void OnOpened(EntityUid uid, MedievalRecipeBookComponent component, BoundUIOpenedEvent args)
    {
        _ui.SetUiState(uid, RecipeBookUi.Key, _alchemy.BookState(component.Recipes));
    }
    public void PutRecipe(EntityUid uid, MedievalRecipeBookComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;
        if (!TryComp<MedievalRandomChemistryRecipeComponent>(args.Used, out var recipe))
            return;
        var resolved = _alchemy.ResolveScroll(recipe);
        if (resolved == null)
            return;
        args.Handled = true;
        if (component.Recipes.Contains(resolved.Id))
        {
            _popup.PopupEntity(Loc.GetString("imperial-medieval-recipe-already"), uid, args.User);
            return;
        }
        component.Recipes.Add(resolved.Id);
        QueueDel(args.Used);
        _popup.PopupEntity(Loc.GetString("imperial-medieval-recipe-inserted"), uid, args.User);
        if (_ui.HasUi(uid, RecipeBookUi.Key))
        {
            _ui.SetUiState(uid, RecipeBookUi.Key,
                _alchemy.BookState(component.Recipes));
        }
    }
}
