namespace Content.Shared.Imperial.Medieval.Plague;

public abstract partial class SharedMedievalPlagueSystem : EntitySystem
{
    public virtual void TryProgressInfection(EntityUid uid, float amount, string? reagent, int? curePower)
    { }
    public virtual void GrantPlagueImmunity(EntityUid uid, string? cure)
    { }
}
