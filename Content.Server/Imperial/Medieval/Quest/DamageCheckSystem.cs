using Content.Shared.Examine;
using Content.Shared.Damage;

namespace Content.Server.DamageCheck;
public partial class DamageCheckSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DamageCheckableComponent, ExaminedEvent>(OnExamine);
    }
    private void OnExamine(EntityUid uid, DamageCheckableComponent comp, ExaminedEvent args)
    {
        // Bad shitcode for gates. Fix later
        if (!TryComp<DamageableComponent>(uid, out var damageable)) return;
        if (damageable.TotalDamage > 3600)
            args.PushMarkup("[color=red]Объект весь покрыт крупными трещинами и вот-вот развалится[/color]");
        else if (damageable.TotalDamage > 2700)
            args.PushMarkup("[color=orange]Объект весь покрыт крупными трещинами[/color]");
        else if (damageable.TotalDamage > 1800)
            args.PushMarkup("[color=orange]По объекту расходятся трещины[/color]");
        else if (damageable.TotalDamage > 900)
            args.PushMarkup("[color=yellow]Заметны серьезные царапины[/color]");
        else if (damageable.TotalDamage > 220)
            args.PushMarkup("[color=green]Заметны легкие царапины[/color]");

    }

}
