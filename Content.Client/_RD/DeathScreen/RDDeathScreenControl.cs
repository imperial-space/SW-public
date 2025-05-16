/*
 * Project: raincidation
 * File: RDDeathScreenControl.cs
 * License: All rights reserved
 * Copyright: (c) 2025 TornadoTechnology
 *
 * For the full license text, see the LICENSE file in the project root.
 * Link: https://github.com/Rainlucid/raincidation
 */

using Content.Client._RD.UI;
using Content.Client.Resources;
using Content.Shared._RD.DeathScreen;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._RD.DeathScreen;

public sealed class RDDeathScreenControl : RDControl
{
    private const float FadeDuration = 4f;
    private const float DelayTime = 3f;

    [Dependency] private readonly IResourceCache _resourceCache = default!;

    public event Action? OnAnimationEnd;

    private readonly Label _label;

    private string _title = string.Empty;
    private string _reason = string.Empty;

    private float _elapsedTime;
    private float _delayElapsedTime;

    public RDDeathScreenControl()
    {
        IoCManager.InjectDependencies(this);

        SetAnchorPreset(LayoutContainer.LayoutPreset.Wide);

        _label = new Label
        {
            Text = "свинтус придет",
            FontOverride = _resourceCache.GetFont("/Fonts/_RD/KosmoletFuturism.otf", 86),
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            FontColorOverride = Color.Red,
        };

        AddChild(_label);
    }

    public void AnimationStart(RDDeathScreenShowEvent ev)
    {
        _title = ev.Title;
        _reason = ev.Reason;

        _elapsedTime = 0;
        _delayElapsedTime = 0;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_elapsedTime >= FadeDuration)
        {
            if (_delayElapsedTime < DelayTime)
            {
                _delayElapsedTime += args.DeltaSeconds;
                return;
            }

            OnAnimationEnd?.Invoke();
            return;
        }

        _elapsedTime += args.DeltaSeconds;

        _label.Modulate = Color.White.WithAlpha(MathHelper.Lerp(0f, 1f, _elapsedTime / FadeDuration));
        BackgroundColor = Color.Black.WithAlpha( MathHelper.Lerp(0f, 1f, _elapsedTime / FadeDuration));
    }
}
