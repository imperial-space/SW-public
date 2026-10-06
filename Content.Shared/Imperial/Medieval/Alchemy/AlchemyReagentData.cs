using System.Linq;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Alchemy;

[Serializable, NetSerializable]
public sealed partial class AlchemyReagentData : ReagentData
{
    [DataField]
    public List<string> Operations = new();

    public override ReagentData Clone() => new AlchemyReagentData { Operations = new(Operations) };

    public override bool Equals(ReagentData? other) =>
        other is AlchemyReagentData data && Operations.SequenceEqual(data.Operations);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var operation in Operations)
            hash.Add(operation);
        return hash.ToHashCode();
    }
}
