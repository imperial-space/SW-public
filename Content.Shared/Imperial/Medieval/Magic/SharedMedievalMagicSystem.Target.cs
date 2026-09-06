using Content.Shared.DoAfter;

namespace Content.Shared.Imperial.Medieval.Magic;


public abstract partial class SharedMedievalMagicSystem
{
    private void InitializeTargetSpells()
    {
        #region Spells Events

        SubscribeLocalEvent<MedievalSpawnSpellEvent>(OnTargetSpellCast);
        SubscribeLocalEvent<MedievalProjectileSpellEvent>(OnTargetSpellCast);
        SubscribeLocalEvent<MedievalTeleportSpellEvent>(OnTargetSpellCast);

        #endregion
    }

    private void OnTargetSpellCast(MedievalTargetSpellEvent args)
    {
        if (args.Handled) return;
        if (!PassesSpellPrerequisites(args.Action, args.Performer, args.Target)) return;

        args.Handled = true;
        AddToStack(args.Performer, args.SpeechPoints);

        if (args.SpellCastDoAfter == null)
        {
            CastSpell(args);

            return;
        }

        var casterComponent = EnsureComp<MedievalSpellCasterComponent>(args.Performer);
        var speedModifier = args.SpellCastDoAfter.SpeedModifier;

        casterComponent.SpeedModifiers.Add(speedModifier);

        Dirty(args.Performer, casterComponent);

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            args.Performer,
            args.SpellCastDoAfter.Delay,
            GetSpellDoAfterEvent(args),
            args.Performer,
            args.Performer
        );

        args.SpellCastDoAfter.CopyToDoAfter(ref doAfterArgs);
        TryStartSpellDoAfter(
            args.Performer,
            args.Action,
            casterComponent,
            speedModifier,
            doAfterArgs);
    }

    #region Helpers

    protected void CastSpell(MedievalTargetSpellEvent args)
    {
        switch (args)
        {
            case MedievalProjectileSpellEvent projectileSpell:
                CastProjectileSpell(new MedievalProjectileSpellData()
                {
                    ProjectilePrototype = projectileSpell.ProjectilePrototype,
                    Coordinates = GetNetCoordinates(projectileSpell.Target),
                    Performer = GetNetEntity(projectileSpell.Performer),
                    CastSpeedModifier = projectileSpell.SpellCastDoAfter?.SpeedModifier ?? 1.0f,
                    Action = GetNetEntity(projectileSpell.Action),
                    ProjectileSpeed = projectileSpell.ProjectileSpeed
                });
                break;
            case MedievalSpawnSpellEvent spawnSpell:
                CastSpawnSpell(new MedievalSpawnSpellData()
                {
                    SpawnedEntityPrototype = spawnSpell.SpawnedEntityPrototype,
                    Coordinates = GetNetCoordinates(spawnSpell.Target),
                    Performer = GetNetEntity(spawnSpell.Performer),
                    CastSpeedModifier = spawnSpell.SpellCastDoAfter?.SpeedModifier ?? 1.0f,
                    RelativePosition = spawnSpell.RelativePosition,
                    SpawnSpellType = spawnSpell.SpawnType,
                    Action = GetNetEntity(spawnSpell.Action)
                });
                break;
            case MedievalTeleportSpellEvent teleportSpell:
                CastTeleportSpell(new MedievalTeleportSpellData()
                {
                    Coordinates = GetNetCoordinates(teleportSpell.Target),
                    Performer = GetNetEntity(teleportSpell.Performer),
                    CastSpeedModifier = teleportSpell.SpellCastDoAfter?.SpeedModifier ?? 1.0f,
                    Action = GetNetEntity(teleportSpell.Action)
                });
                break;
        }
    }

    protected MedievalSpellDoAfterEvent GetSpellDoAfterEvent(MedievalTargetSpellEvent args)
    {
        return args switch
        {
            MedievalProjectileSpellEvent projectileSpell =>
                new MedievalCastProjectileSpellDoAfterEvent()
                {
                    SpellData = new MedievalProjectileSpellData()
                    {
                        CastSpeedModifier = projectileSpell.SpellCastDoAfter?.SpeedModifier ?? 1.0f,
                        Performer = GetNetEntity(projectileSpell.Performer),
                        Coordinates = GetNetCoordinates(projectileSpell.Target),
                        ProjectilePrototype = projectileSpell.ProjectilePrototype,
                        Action = GetNetEntity(projectileSpell.Action),
                        TargetSpellType = projectileSpell.TargetSpellType,
                        ProjectileSpeed = projectileSpell.ProjectileSpeed
                    }
                },
            MedievalSpawnSpellEvent spawnSpell =>
                new MedievalCastSpawnSpellDoAfterEvent()
                {
                    SpellData = new MedievalSpawnSpellData()
                    {
                        CastSpeedModifier = spawnSpell.SpellCastDoAfter?.SpeedModifier ?? 1.0f,
                        Performer = GetNetEntity(spawnSpell.Performer),
                        Coordinates = GetNetCoordinates(spawnSpell.Target),
                        SpawnedEntityPrototype = spawnSpell.SpawnedEntityPrototype,
                        RelativePosition = spawnSpell.RelativePosition,
                        SpawnSpellType = spawnSpell.SpawnType,
                        Action = GetNetEntity(spawnSpell.Action),
                        TargetSpellType = spawnSpell.TargetSpellType
                    }
                },
            MedievalTeleportSpellEvent teleportSpellEvent =>
                new MedievalCastTeleportSpellDoAfterEvent()
                {
                    SpellData = new MedievalTeleportSpellData()
                    {
                        CastSpeedModifier = teleportSpellEvent.SpellCastDoAfter?.SpeedModifier ?? 1.0f,
                        Performer = GetNetEntity(teleportSpellEvent.Performer),
                        Coordinates = GetNetCoordinates(teleportSpellEvent.Target),
                        Action = GetNetEntity(teleportSpellEvent.Action),
                        TargetSpellType = teleportSpellEvent.TargetSpellType
                    }
                },
            _ => throw new ArgumentOutOfRangeException("Cannot find upcast method")
        };
    }

    protected virtual bool CanInstantlyCastSpell(MedievalTargetSpellData args, TargetSpellType spellType)
    {
        return spellType != TargetSpellType.TargetOnCurrentMousePosition;
    }

    #region Server/Client implementation

    protected virtual void CastProjectileSpell(MedievalProjectileSpellData args)
    {
        RaiseLocalEvent(GetEntity(args.Action), new MedievalAfterCastSpellEvent()
        {
            Action = GetEntity(args.Action),
            Performer = GetEntity(args.Performer)
        });
    }

    protected virtual void CastSpawnSpell(MedievalSpawnSpellData args)
    {
        RaiseLocalEvent(GetEntity(args.Action), new MedievalAfterCastSpellEvent()
        {
            Action = GetEntity(args.Action),
            Performer = GetEntity(args.Performer)
        });
    }

    protected virtual void CastTeleportSpell(MedievalTeleportSpellData args)
    {
        if (!CanInstantlyCastSpell(args, args.TargetSpellType)) return;

        var performer = GetEntity(args.Performer);
        var coords = GetCoordinates(args.Coordinates);

        _transformSystem.SetCoordinates(performer, coords);

        RaiseLocalEvent(GetEntity(args.Action), new MedievalAfterCastSpellEvent()
        {
            Action = GetEntity(args.Action),
            Performer = GetEntity(args.Performer)
        });
    }

    #endregion

    #endregion
}
