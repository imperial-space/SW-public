using System;
using System.Collections.Generic;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking.Events;
using Content.Server.MagicBarrier.Components;
using Content.Server.RoundEnd;
using Content.Server.Voting;
using Content.Server.Voting.Managers;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Shared.Configuration;
using Robust.Shared.Random;

namespace Content.Server.GameTicking.Systems;

public sealed partial class AutoRoundExtendSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly RoundEndSystem _roundEndSystem = default!;
    [Dependency] private readonly IVoteManager _voteManager = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private bool _isEnded;
    private bool _leadEventTriggered;
    private TimeSpan _targetDuration;

    private TimeSpan _initialDuration;
    private TimeSpan _maxDuration;
    private TimeSpan _voteLeadTime;
    private TimeSpan _extensionTime;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStart);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundCleanup);

        _cfg.OnValueChanged(CCVars.AutoRoundInitialDuration, v => _initialDuration = TimeSpan.FromMinutes(v), true);
        _cfg.OnValueChanged(CCVars.AutoRoundMaxDuration, v => _maxDuration = TimeSpan.FromMinutes(v), true);
        _cfg.OnValueChanged(CCVars.AutoRoundExtensionTime, v => _extensionTime = TimeSpan.FromMinutes(v), true);
        _cfg.OnValueChanged(CCVars.AutoRoundVoteLeadTime, v => _voteLeadTime = TimeSpan.FromMinutes(v), true);
    }

    private void OnRoundStart(RoundStartingEvent ev)
    {
        ResetState();
    }

    private void OnRoundCleanup(RoundRestartCleanupEvent ev)
    {
        ResetState();
    }

    private void ResetState()
    {
        _isEnded = false;
        _leadEventTriggered = false;
        _targetDuration = _initialDuration;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_isEnded || _ticker.RunLevel != GameRunLevel.InRound)
            return;

        var currentDuration = _ticker.RoundDuration();

        if (currentDuration >= _targetDuration)
        {
            _isEnded = true;
            _roundEndSystem.EndRound();
            return;
        }

        if (!_leadEventTriggered && currentDuration >= _targetDuration - _voteLeadTime)
        {
            _leadEventTriggered = true;

            if (_targetDuration < _maxDuration)
            {
                StartExtensionVote();
            }
            else
            {
                ArmyAttack();
            }
        }
    }

    private void StartExtensionVote()
    {
        var options = new VoteOptions
        {
            InitiatorText = Loc.GetString("ui-vote-extend-round-initiator"),
            Title = Loc.GetString("ui-vote-extend-round-title"),
            Options =
            {
                (Loc.GetString("ui-vote-extend-yes"), "yes"),
                (Loc.GetString("ui-vote-extend-no"), "no")
            },
            Duration = TimeSpan.FromMinutes(3),
            DisplayVotes = true
        };

        var vote = _voteManager.CreateVote(options);

        vote.OnFinished += (_, _) =>
        {
            var yes = vote.VotesPerOption["yes"];
            var no = vote.VotesPerOption["no"];

            if (yes > no)
            {
                _targetDuration += _extensionTime;
                _leadEventTriggered = false;
                _chatManager.DispatchServerAnnouncement(Loc.GetString("ui-vote-extend-success"));
            }
            else
            {
                _chatManager.DispatchServerAnnouncement(Loc.GetString("ui-vote-extend-fail"));
            }
        };

        _chatManager.DispatchServerAnnouncement(Loc.GetString("ui-vote-extend-announcement"));
    }

    private void ArmyAttack()
    {
        _chat.DispatchGlobalAnnouncement(
            Loc.GetString("auto-round-end-army-attack"),
            playSound: true,
            colorOverride: Color.FromHex("#9403fc"),
            sender: Loc.GetString("auto-round-end-army-sender"));

        var cursespawners = new List<EntityUid>();
        var query = EntityQueryEnumerator<MagicBarrierNecroSpawnComponent>();

        while (query.MoveNext(out var uid, out var _))
            cursespawners.Add(uid);

        if (cursespawners.Count == 0)
            return;

        for (int i = 0; i < 100; i++)
        {
            var chosenSpawner = _random.Pick(cursespawners);
            var cursexform = Transform(chosenSpawner);
            Spawn("MedievalSpawnNecroSenderPreset", cursexform.Coordinates);
        }
    }
}
