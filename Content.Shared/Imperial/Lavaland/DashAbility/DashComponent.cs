using Content.Shared.Imperial.Abilities.Urs.Systems;
using System.Numerics;
using Content.Shared.Damage;





namespace Content.Shared.Imperial.Abilities.Urs.Components
{
    [RegisterComponent, AutoGenerateComponentState(true)]
    [Access(typeof(UrsDashSystem))]
    public sealed partial class UrsDashComponent : Component

    {
        [ViewVariables(VVAccess.ReadWrite)]
        public HashSet<EntityUid> CollidingEntities = new();
        [ViewVariables(VVAccess.ReadWrite)]
        public bool IsDashing = false;

        [DataField] public float Accumulator = 0f;
        [DataField] public float UpdateInterval = 5.3f;
        [DataField] public float UpdateIntervalToDash = 5.0f;
        [DataField] public float PushStrength { get; set; } = 10f;
        [DataField] public Vector2 Direction { get; set; }
        [DataField(required: true), AutoNetworkedField]
        public DamageSpecifier Damage = default!;


    }
}

