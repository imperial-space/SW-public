using Content.Shared.Examine;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;

namespace Content.Server.DamageCheck;
public partial class DamageCheckSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!; 

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DamageCheckableComponent, ExaminedEvent>(OnExamine);
    }
    private void OnExamine(EntityUid uid, DamageCheckableComponent comp, ExaminedEvent args)
    {
        // Bad shitcode for gates. Fix later
        if (!TryComp<DamageableComponent>(uid, out var damageable)) return;
        var total = _damageable.GetTotalDamage((uid, damageable)); 
        if (total > 3600)
            args.PushMarkup("[color=red]Объект весь покрыт крупными трещинами и вот-вот развалится[/color]");
        else if (total > 2700)
            args.PushMarkup("[color=orange]Объект весь покрыт крупными трещинами[/color]");
        else if (total > 1800)
            args.PushMarkup("[color=orange]По объекту расходятся трещины[/color]");
        else if (total > 900)
            args.PushMarkup("[color=yellow]Заметны серьезные царапины[/color]");
        else if (total > 220)
            args.PushMarkup("[color=green]Заметны легкие царапины[/color]");

    }

}
