namespace Content.Shared.Imperial.Medieval.Clothing;

[ByRefEvent]
public record struct ModifyClothingMovespeedModifierEvent(float WalkMod, float SprintMod, float Walk = 0, float Sprint = 0);
