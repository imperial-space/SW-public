using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Robust.Client.Input;
using Robust.Client.Player;
using Content.Shared.MeleeParry.Components;
using Content.Shared.Hands.EntitySystems;
using System.Numerics;

namespace Content.Client.Imperial.Medieval.MeleeParry;

public sealed class ParryCooldownOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    private readonly IGameTiming _timing;
    private readonly IInputManager _input;
    private readonly IEntityManager _entMan;
    private readonly IPlayerManager _player;
    private readonly SharedHandsSystem _hands;

    public ParryCooldownOverlay(IGameTiming timing, IInputManager input, IEntityManager entMan, IPlayerManager player, SharedHandsSystem hands)
    {
        _timing = timing;
        _input = input;
        _entMan = entMan;
        _player = player;
        _hands = hands;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var playerEnt = _player.LocalEntity;
        if (playerEnt == null)
            return;

        if (!_entMan.TryGetComponent<MeleeParryStorageComponent>(playerEnt, out var parryStorage))
            return;

        if (_timing.CurTime >= parryStorage.GlobalNextParryTime)
            return; // Кулдаун прошел, не рисуем

        // Вычисляем прогресс
        var cooldownDuration = TimeSpan.FromSeconds(parryStorage.GlobalCooldownParry);
        var startTime = parryStorage.GlobalNextParryTime - cooldownDuration;
        var elapsed = _timing.CurTime - startTime;
        var progress = Math.Clamp(elapsed.TotalSeconds / cooldownDuration.TotalSeconds, 0.0, 1.0);

        var screenPos = _input.MouseScreenPosition.Position;
        var handle = args.ScreenHandle;

        // Размеры и позиция (смещение на 20 пикселей вниз от курсора)
        var barWidth = 64f;
        var barHeight = 8f;
        var position = screenPos + new Vector2(-barWidth / 2f, -40f);

        // Отрисовка фона (темно-серый полупрозрачный)
        handle.DrawRect(new UIBox2(position, position + new Vector2(barWidth, barHeight)), Color.Black.WithAlpha(0.6f));

        // Отрисовка заполнения прогресса (зеленый)
        var fillWidth = barWidth * (float)progress;
        handle.DrawRect(new UIBox2(position, position + new Vector2(fillWidth, barHeight)), Color.FromHex("#ededed"));
    }
}
