using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Server.Imperial.Medieval.Engineering;

[ByRefEvent]
public record struct BeforeSpawnAfterInteractEvent(EntityUid? User, bool Cancelled = false);