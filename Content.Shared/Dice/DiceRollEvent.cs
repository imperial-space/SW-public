namespace Content.Shared.Dice;

/// <summary>Raised on a die before applying and announcing its rolled side.</summary>
[ByRefEvent]
public record struct DiceRollEvent(EntityUid? User, int Sides, int Result);
