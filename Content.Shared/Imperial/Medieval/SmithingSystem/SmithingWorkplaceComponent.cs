using Content.Shared.Containers.ItemSlots;
using Content.Shared.Imperial.Medieval.SmithingSystem.Bui;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.SmithingSystem;

[RegisterComponent, NetworkedComponent]
public sealed partial class SmithingWorkplaceComponent : Component
{
    [DataField]
    public SoundSpecifier HitSound = new SoundPathSpecifier("/Audio/Imperial/Medieval/Crafting/Smithing/anvil_hit.ogg");

    [DataField]
    public ItemSlot WorkpieceSlot = new();

    public Entity<SmithingWorkpieceComponent>? Workpiece;

    public SmithGameState? GameState { get; set; }
}
