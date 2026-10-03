using System.Linq;
using Content.Server.Imperial.Medieval.SurveyMap;
using Content.Shared.Imperial.Medieval.BookAbilities;
using Content.Shared.Imperial.Medieval.Knowledge;
using Content.Shared.Imperial.Medieval.SurveyMap;
using Content.Shared.Paper;

namespace Content.Server.Imperial.Medieval.BookAbilities;

public sealed partial class MedievalBookAbilitySystem
{
    [Dependency] private readonly MedievalSurveyMapSystem _fieldMaps = default!;

    private void InitializeCartography()
    {
        SubscribeLocalEvent<LearnedKnowledgeComponent, BookSurveyActionEvent>(OnSurvey);
    }

    private void OnSurvey(EntityUid uid, LearnedKnowledgeComponent comp, BookSurveyActionEvent args)
    {
        if (args.Handled || !Knows(uid, "BookCartography"))
            return;

        var map = _hands.EnumerateHeld(uid).FirstOrDefault(item =>
            HasComp<MedievalSurveyMapComponent>(item) || HasComp<PaperComponent>(item));
        if (TryComp<MedievalSurveyMapComponent>(map, out var fieldMap) &&
            fieldMap.SurveyedMap != Transform(uid).MapUid)
        {
            _popup.PopupEntity(Loc.GetString("book-cartography-wrong-region"), uid, uid);
            return;
        }
        if (map == default || !_fieldMaps.CanSurvey(uid, map))
        {
            _popup.PopupEntity(Loc.GetString("book-cartography-requirements"), uid, uid);
            return;
        }

        var region = Transform(uid).MapUid;
        args.Handled = Start(uid, map, "BookCartography", 8,
            () => Transform(uid).MapUid == region && _fieldMaps.CanSurvey(uid, map),
            () => _fieldMaps.BeginSurvey(uid, map));
    }
}
