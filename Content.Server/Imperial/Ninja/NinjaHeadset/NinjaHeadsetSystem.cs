using Content.Shared.Imperial.NinjaHeadset.Components;
using Content.Shared.Imperial.NinjaHeadset.Events;
using Content.Shared.Interaction;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Content.Shared.Inventory;
using Content.Shared.Radio.Components;
using Content.Shared.Ninja.Components;
using Content.Shared.Examine;
using System.Text;

namespace Content.Server.Imperial.NinjaHeadset.Systems;

public sealed class NinjaHeadsetSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaHeadsetComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<NinjaHeadsetComponent, NinjaHackDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<NinjaHeadsetComponent, ExaminedEvent>(OnExamine);
    }

    private readonly Dictionary<string, string> _frequencyTranslations = new()
    {
        {"Common", "Общий"},
        {"Security", "Служба Безопасности"},
        {"Command", "Командный"},
        {"Medical", "Медицинский"},
        {"Engineering", "Инженерный"},
        {"Science", "Научный"},
        {"Supply", "Снабжение"},
        {"Service", "Сервис"},
        {"CentCom", "ЦентКом"},
        {"Syndicate", "Синдикат"},
        {"Binary", "Двоичный"},
    };
    private string TranslateFrequency(string frequencyId)
    {
        return _frequencyTranslations.TryGetValue(frequencyId, out var translation)
            ? translation
            : frequencyId;
    }

    private void OnExamine(EntityUid uid, NinjaHeadsetComponent component, ExaminedEvent args)
    {
        if (component.CopiedFrequencies.Count == 0)
        {
            args.PushMarkup("[color=yellow]Скопированных частот нет.[/color]");
            return;
        }

        var message = new StringBuilder();
        message.AppendLine("[color=green]Скопированные частоты:[/color]");

        foreach (var frequency in component.CopiedFrequencies)
        {
            var translatedFrequency = TranslateFrequency(frequency);
            message.AppendLine($"[color=cyan]- {translatedFrequency}[/color]");
        }

        args.PushMarkup(message.ToString());
    }

    private void OnAfterInteract(EntityUid uid, NinjaHeadsetComponent component, AfterInteractEvent args)
    {
        if (!HasComp<SpaceNinjaComponent>(args.User))
            return;

        if (!args.CanReach || args.Target == null)
            return;

        if (!TryGetTargetHeadset(args.Target.Value, out var headsetUid) || !headsetUid.HasValue)
            return;

        if (args.Target == args.User)
            return;

        if (component.HackingTarget != null)
            return;

        component.HackingTarget = args.Target.Value;
        component.TargetHeadset = headsetUid.Value;

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, component.CopyFrequenciesTime, new NinjaHackDoAfterEvent(), uid, target: args.Target)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
            Broadcast = true
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
        {
            component.HackingTarget = null;
            component.TargetHeadset = null;
        }
    }

    private void OnDoAfter(EntityUid uid, NinjaHeadsetComponent component, NinjaHackDoAfterEvent args)
    {
        if (args.Cancelled)
        {
            component.HackingTarget = null;
            component.TargetHeadset = null;
            return;
        }

        if (component.TargetHeadset == null || !Exists(component.TargetHeadset.Value))
        {
            component.HackingTarget = null;
            component.TargetHeadset = null;
            return;
        }

        var newFrequencies = new HashSet<string>();
        var translatedNewFrequencies = new List<string>();

        if (TryComp<EncryptionKeyHolderComponent>(component.TargetHeadset.Value, out var targetEncryption) &&
            TryComp<EncryptionKeyHolderComponent>(uid, out var ninjaEncryption))
        {
            foreach (var channel in targetEncryption.Channels)
            {
                if (!component.CopiedFrequencies.Contains(channel))
                {
                    component.CopiedFrequencies.Add(channel);
                    newFrequencies.Add(channel);
                    translatedNewFrequencies.Add(TranslateFrequency(channel));

                    if (!ninjaEncryption.Channels.Contains(channel))
                    {
                        ninjaEncryption.Channels.Add(channel);
                    }
                }
            }
        }

        Dirty(uid, component);

        if (newFrequencies.Count > 0)
        {
            var freqList = string.Join(", ", translatedNewFrequencies);
            _popup.PopupEntity("Частоты успешно скопированы.", uid, args.User);
        }
        else
        {
            _popup.PopupEntity("Новых частот не обнаружено.", uid, args.User);
        }

        component.HackingTarget = null;
        component.TargetHeadset = null;
    }

    private bool TryGetTargetHeadset(EntityUid targetUid, out EntityUid? headsetUid)
    {
        headsetUid = null;

        if (_inventorySystem.TryGetSlotEntity(targetUid, "ears", out var earsItem) &&
            HasComp<EncryptionKeyHolderComponent>(earsItem))
        {
            headsetUid = earsItem;
            return true;
        }

        return false;
    }

    private new bool Exists(EntityUid uid)
    {
        return !Deleted(uid) && !Terminating(uid);
    }
}
