using Content.Shared.Examine;
using Content.Shared.Imperial.Medieval.Bee.Components;
using Content.Shared.Interaction;
using Content.Shared.Stacks;

namespace Content.Server.Imperial.Medieval.Bee.Systems;

public sealed partial class MedievalBeeSystem : EntitySystem
{
    private void InitializeSmoke()
    {
        SubscribeLocalEvent<MedievalBeeSmokeComponent, BeforeRangedInteractEvent>(SmokeInteract);
        SubscribeLocalEvent<MedievalBeeSmokeComponent, InteractUsingEvent>(SmokeInteractUsing);
        SubscribeLocalEvent<MedievalBeeSmokeComponent, ExaminedEvent>(SmokeExamined);
    }
    private void SmokeInteract(EntityUid uid, MedievalBeeSmokeComponent component, BeforeRangedInteractEvent args)
    {
        if (!args.Target.HasValue)
            return;

        if (!TryComp<MedievalBeeHiveComponent>(args.Target.Value, out var hiveComponent))
            return;

        if (component.UsesLeft <= 0)
            return;

        if (hiveComponent.Pacified)
        {
            _popup.PopupEntity(Loc.GetString("medieval-bee-pacify-already"), args.User, args.User);
            return;
        }
        if (hiveComponent.PacifyCooldown.HasValue && hiveComponent.PacifyCooldown > _timing.CurTime)
        {
            _popup.PopupEntity(Loc.GetString("medieval-bee-pacify-cooldown"), args.User, args.User);
            return;
        }
        _popup.PopupEntity(Loc.GetString("medieval-pacify-succesful"), args.User, args.User);
        Pacify((uid, hiveComponent), component.PacifyTime);
        component.UsesLeft--;
        args.Handled = true;
    }
    private void SmokeInteractUsing(EntityUid uid, MedievalBeeSmokeComponent component, InteractUsingEvent args)
    {
        if (args.Used == uid)
            return;

        if (!TryComp<StackComponent>(args.Used, out var stack) || stack.StackTypeId != component.ResourceStack)
            return;

        var add = Math.Min(component.MaxUses - component.UsesLeft, stack.Count);
        if (add <= 0)
        {
            _popup.PopupEntity(Loc.GetString("medieval-smokerefill-full"), args.User, args.User);
            return;
        }
        component.UsesLeft += add;
        _popup.PopupEntity(Loc.GetString("medieval-smokerefill-succesful", ("uses", add.ToString())), args.User, args.User);
        var newCount = stack.Count - add;
        if (newCount > 0)
            _stack.SetCount(args.Used, newCount, stack);
        else
            QueueDel(args.Used);
    }
    private void SmokeExamined(EntityUid uid, MedievalBeeSmokeComponent component, ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("medieval-bee-smoke-uses-left", ("uses", component.UsesLeft.ToString())));
    }
}
