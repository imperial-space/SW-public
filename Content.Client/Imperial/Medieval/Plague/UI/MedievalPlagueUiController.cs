using System.Linq;
using Content.Shared.Imperial.Medieval.Plague;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Medieval.Plague.UI;

public sealed class MedievalPlagueUiController : UIController
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;

    private MedievalPlagueMenu? _menu;
    private MedievalPlagueSymptomMiniMenu? _miniMenu;

    public void ToggleMenu(Dictionary<ProtoId<MedievalPlagueSymptomPrototype>, MedievalPlagueSymptomData> data, int allowedPoints)
    {
        if (_menu != null)
        {
            _menu.Close();
            return;
        }

        _menu = new();
        _menu.OnSymptomSelect = args => SelectSymptom(args, data, allowedPoints);
        _menu.OnClose += () =>
        {
            _miniMenu?.Close();
            _miniMenu = null;
            _menu = null;
        };

        _menu.Populate(_proto, data);
        _menu.OpenCentered();
    }

    public void Populate(Dictionary<ProtoId<MedievalPlagueSymptomPrototype>, MedievalPlagueSymptomData> data, int allowedPoints)
    {
        if (_menu != null)
        {
            _menu.Populate(_proto, data);
            _menu.OnSymptomSelect = args => SelectSymptom(args, data, allowedPoints);
        }

        if (_miniMenu != null)
        {
            var proto = _proto.Index(_miniMenu.Proto);
            var points = proto.Cost;

            if (data.TryGetValue(proto.ID, out var dat))
                points -= dat.Points;

            if (!ReqsMet(proto, data))
                points = -1;

            points = Math.Clamp(points, -1, allowedPoints);

            _miniMenu.Populate(proto, points);
        }
    }

    private void SelectSymptom(ProtoId<MedievalPlagueSymptomPrototype> protoId, Dictionary<ProtoId<MedievalPlagueSymptomPrototype>, MedievalPlagueSymptomData> data, int allowedPoints)
    {
        if (_miniMenu == null)
        {
            _miniMenu = new();
            _miniMenu.OnClose += () => _miniMenu = null;
            _miniMenu.OpenCenteredRight();
        }

        var proto = _proto.Index(protoId);
        var points = proto.Cost;
        if (data.TryGetValue(proto.ID, out var dat))
            points -= dat.Points;

        if (!ReqsMet(proto, data))
            points = -1;

        points = Math.Clamp(points, -1, allowedPoints);

        _miniMenu.Populate(proto, points);
        var ent = EntityManager.GetNetEntity(_player.LocalEntity);
        if (!ent.HasValue)
            return;

        _miniMenu.OnAddPoints = (_, args) => EntityManager.RaisePredictiveEvent(new AddPlaguePointsMessage(proto, args, ent.Value));
    }

    private bool ReqsMet(MedievalPlagueSymptomPrototype proto, Dictionary<ProtoId<MedievalPlagueSymptomPrototype>, MedievalPlagueSymptomData> dict)
    {
        var unlocked = dict.Where(x => x.Value.Unlocked && proto.Required.ToList().Contains(x.Key));
        return unlocked.Count() == proto.Required.Count();
    }
}
