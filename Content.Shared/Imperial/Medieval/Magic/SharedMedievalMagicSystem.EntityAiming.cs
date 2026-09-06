using Content.Shared.DoAfter;

namespace Content.Shared.Imperial.Medieval.Magic;


public abstract partial class SharedMedievalMagicSystem
{
    private void InitializeEntityAimingSpells()
    {
        #region Spells Events

        SubscribeLocalEvent<MedievalLightningSpellEvent>(OnEntityAimingSpellCast);
        SubscribeLocalEvent<MedievalSpawnAimingEntityEvent>(OnEntityAimingSpellCast);
        SubscribeLocalEvent<MedievalHomingProjectilesSpellEvent>(OnEntityAimingSpellCast);
        SubscribeLocalEvent<MedievalEntityTargetProjectileSpellEvent>(OnEntityAimingSpellCast);

        #endregion
    }

    private void OnEntityAimingSpellCast(MedievalEntityAimingSpellEvent args)
    {
        if (args.Handled) return;
        var isContinuation = TryComp<MedievalSpellCasterComponent>(args.Performer, out var caster) &&
                             caster.TargetStack.ContainsKey(args.Action);
        if (!PassesSpellPrerequisites(args.Action, args.Performer, args.Target, isContinuation)) return;

        args.Handled = true;
        AddToStack(args.Performer, args.SpeechPoints);
        var casterComponent = EnsureComp<MedievalSpellCasterComponent>(args.Performer);

        if (args.SpellCastDoAfter == null)
        {
            CastSpell(args);

            return;
        }

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

    protected void CastSpell(MedievalEntityAimingSpellEvent args)
    {
        switch (args)
        {
            case MedievalHomingProjectilesSpellEvent projectileSpell:
                CastHomingProjectilesSpell(new MedievalHomingProjectilesSpellData()
                {
                    ProjectilePrototype = projectileSpell.ProjectilePrototype,
                    Performer = GetNetEntity(projectileSpell.Performer),
                    CastSpeedModifier = projectileSpell.SpellCastDoAfter?.SpeedModifier ?? 1.0f,
                    Action = GetNetEntity(projectileSpell.Action),
                    LinearVelocityIntensy = projectileSpell.LinearVelocityIntensy,
                    RelativeAngle = projectileSpell.RelativeAngle,
                    ProjectileSpeed = projectileSpell.ProjectileSpeed,
                    Spread = projectileSpell.Spread
                });
                break;
            case MedievalEntityTargetProjectileSpellEvent entityTargetProjectileSpell:
                CastEntityTargetProjectileSpell(new MedievalEntityTargetProjectileSpellData()
                {
                    ProjectilePrototype = entityTargetProjectileSpell.ProjectilePrototype,
                    Performer = GetNetEntity(entityTargetProjectileSpell.Performer),
                    CastSpeedModifier = entityTargetProjectileSpell.SpellCastDoAfter?.SpeedModifier ?? 1.0f,
                    Action = GetNetEntity(entityTargetProjectileSpell.Action),
                    ProjectileSpeed = entityTargetProjectileSpell.ProjectileSpeed
                });
                break;
            case MedievalLightningSpellEvent lightningSpellEvent:
                CastLightningSpell(new MedievalLightningSpellData()
                {
                    Performer = GetNetEntity(lightningSpellEvent.Performer),
                    CastSpeedModifier = lightningSpellEvent.SpellCastDoAfter?.SpeedModifier ?? 1.0f,
                    Action = GetNetEntity(lightningSpellEvent.Action),
                    Speed = lightningSpellEvent.Speed,
                    Intensity = lightningSpellEvent.Intensity,
                    Seed = lightningSpellEvent.Seed,
                    Amplitude = lightningSpellEvent.Amplitude,
                    Frequency = lightningSpellEvent.Frequency,
                    LightningColor = lightningSpellEvent.LightningColor,
                    Offset = lightningSpellEvent.Offset,
                    LifeTime = lightningSpellEvent.LifeTime,
                    LightningCollideEffects = lightningSpellEvent.LightningCollideEffects,
                    SpawnedEffectPrototype = lightningSpellEvent.SpawnedEffectPrototype
                });
                break;
            case MedievalSpawnAimingEntityEvent spawnAimingEntityEvent:
                CastSpawnAimingEntitySpell(new MedievalSpawnAimingEntityData()
                {
                    CastSpeedModifier = spawnAimingEntityEvent.SpellCastDoAfter?.SpeedModifier ?? 1.0f,
                    Performer = GetNetEntity(spawnAimingEntityEvent.Performer),
                    SpawnedEntity = spawnAimingEntityEvent.SpawnedEntity,
                    Action = GetNetEntity(spawnAimingEntityEvent.Action),
                });
                break;
        }
    }

    protected MedievalSpellDoAfterEvent GetSpellDoAfterEvent(MedievalEntityAimingSpellEvent args)
    {
        return args switch
        {
            MedievalHomingProjectilesSpellEvent projectileSpell =>
                new MedievalCastHomingProjectilesSpellDoAfterEvent()
                {
                    SpellData = new MedievalHomingProjectilesSpellData()
                    {
                        CastSpeedModifier = projectileSpell.SpellCastDoAfter?.SpeedModifier ?? 1.0f,
                        Performer = GetNetEntity(projectileSpell.Performer),
                        ProjectilePrototype = projectileSpell.ProjectilePrototype,
                        Action = GetNetEntity(projectileSpell.Action),
                        LinearVelocityIntensy = projectileSpell.LinearVelocityIntensy,
                        RelativeAngle = projectileSpell.RelativeAngle,
                        ProjectileSpeed = projectileSpell.ProjectileSpeed,
                        Spread = projectileSpell.Spread
                    }
                },
            MedievalEntityTargetProjectileSpellEvent entityTargetProjectileSpell =>
                new MedievalCastEntityTargetProjectileSpellDoAfterEvent()
                {
                    SpellData = new MedievalEntityTargetProjectileSpellData()
                    {
                        CastSpeedModifier = entityTargetProjectileSpell.SpellCastDoAfter?.SpeedModifier ?? 1.0f,
                        Performer = GetNetEntity(entityTargetProjectileSpell.Performer),
                        ProjectilePrototype = entityTargetProjectileSpell.ProjectilePrototype,
                        Action = GetNetEntity(entityTargetProjectileSpell.Action),
                        ProjectileSpeed = entityTargetProjectileSpell.ProjectileSpeed
                    }
                },
            MedievalLightningSpellEvent lightningSpellEvent =>
                new MedievalCastLightningSpellDoAfterEvent()
                {
                    SpellData = new MedievalLightningSpellData()
                    {
                        Performer = GetNetEntity(lightningSpellEvent.Performer),
                        CastSpeedModifier = lightningSpellEvent.SpellCastDoAfter?.SpeedModifier ?? 1.0f,
                        Action = GetNetEntity(lightningSpellEvent.Action),
                        Speed = lightningSpellEvent.Speed,
                        Intensity = lightningSpellEvent.Intensity,
                        Seed = lightningSpellEvent.Seed,
                        Amplitude = lightningSpellEvent.Amplitude,
                        Frequency = lightningSpellEvent.Frequency,
                        LightningColor = lightningSpellEvent.LightningColor,
                        Offset = lightningSpellEvent.Offset,
                        LifeTime = lightningSpellEvent.LifeTime,
                        LightningCollideEffects = lightningSpellEvent.LightningCollideEffects,
                        SpawnedEffectPrototype = lightningSpellEvent.SpawnedEffectPrototype
                    }
                },
            MedievalSpawnAimingEntityEvent spawnAimingEntityEvent =>
                new MedievalSpawnAimingEntityDoAfterEvent()
                {
                    SpellData = new MedievalSpawnAimingEntityData()
                    {
                        CastSpeedModifier = spawnAimingEntityEvent.SpellCastDoAfter?.SpeedModifier ?? 1.0f,
                        Performer = GetNetEntity(spawnAimingEntityEvent.Performer),
                        SpawnedEntity = spawnAimingEntityEvent.SpawnedEntity,
                        Action = GetNetEntity(spawnAimingEntityEvent.Action),
                    }
                },
            _ => throw new ArgumentOutOfRangeException("Cannot find upcast method")
        };
    }

    #endregion

    #region Server/Client implementation

    protected virtual void CastHomingProjectilesSpell(MedievalHomingProjectilesSpellData args)
    {
        RaiseLocalEvent(GetEntity(args.Action), new MedievalAfterCastSpellEvent()
        {
            Action = GetEntity(args.Action),
            Performer = GetEntity(args.Performer)
        });
    }

    protected virtual void CastEntityTargetProjectileSpell(MedievalEntityTargetProjectileSpellData args)
    {
        RaiseLocalEvent(GetEntity(args.Action), new MedievalAfterCastSpellEvent()
        {
            Action = GetEntity(args.Action),
            Performer = GetEntity(args.Performer)
        });
    }

    protected virtual void CastLightningSpell(MedievalLightningSpellData args)
    {
        RaiseLocalEvent(GetEntity(args.Action), new MedievalAfterCastSpellEvent()
        {
            Action = GetEntity(args.Action),
            Performer = GetEntity(args.Performer)
        });
    }

    protected virtual void CastSpawnAimingEntitySpell(MedievalSpawnAimingEntityData args)
    {
        RaiseLocalEvent(GetEntity(args.Action), new MedievalAfterCastSpellEvent()
        {
            Action = GetEntity(args.Action),
            Performer = GetEntity(args.Performer)
        });
    }

    #endregion
}
