using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Enchantments;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Achievements;
using MegaCrit.Sts2.Core.Models.Afflictions;
using MegaCrit.Sts2.Core.Models.Badges;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;
using RandomForeseer.RandomForeseerCode.InCombat.Extensions;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Card;

using Registry = MethodMirrorRegistry<AbstractModel, AfterCardPlayedMirrorContext>;

// Mirrors the prediction-relevant parts of Hook.AfterCardPlayed and its late phase.
internal static class AfterCardPlayedMirrors
{
    private static readonly MirrorMethodSpec AfterCardPlayed = MirrorMethodSpec.Hook(
        nameof(AbstractModel.AfterCardPlayed),
        [typeof(PlayerChoiceContext), typeof(CardPlay)]);

    private static readonly MirrorMethodSpec AfterCardPlayedLate = MirrorMethodSpec.Hook(
        nameof(AbstractModel.AfterCardPlayedLate),
        [typeof(PlayerChoiceContext), typeof(CardPlay)]);

    private static readonly Registry Registry = CreateRegistry();
    private static readonly Registry LateRegistry = CreateLateRegistry();

    public static void Invoke(AbstractModel listener, AfterCardPlayedMirrorContext context)
    {
        Registry.Invoke(listener, context);
    }

    public static void InvokeLate(AbstractModel listener, AfterCardPlayedMirrorContext context)
    {
        LateRegistry.Invoke(listener, context);
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(AfterCardPlayed);

        registry.RegisterIgnored<Play20CardsSingleTurnAchievement>();
        registry.RegisterIgnored<SkillSilent1Achievement>();

        registry.RegisterIgnored<CccComboModel>();

        registry.Register<BansheesCry>(HandleBansheesCry);
        registry.Register<Pinpoint>(HandlePinpoint);

        registry.Register<Glam>(HandleGlam);
        registry.Register<Goopy>(HandleGoopy);
        registry.Register<Vigorous>(HandleVigorous);

        registry.Register<AfterimagePower>(HandleAfterimagePower);
        registry.Register<BlackHolePower>(HandleBlackHolePower);
        registry.Register<CalamityPower>(HandleCalamityPower);
        registry.Register<CurlUpPower>(HandleCurlUpPower);
        registry.Register<DevourLifePower>(HandleDevourLifePower);
        registry.RegisterIgnored<EchoFormPower>();
        registry.RegisterIgnored<EnragePower>();
        registry.Register<GalvanicPower>(HandleGalvanicPower);
        registry.Register<GravityPower>(HandleGravityPower);
        registry.Register<HauntPower>(HandleHauntPower);
        registry.Register<ImitationLearningPower>(HandleImitationLearningPower);
        registry.Register<MasterPlannerPower>(HandleMasterPlannerPower);
        registry.Register<MonologuePower>(HandleMonologuePower);
        registry.Register<OblivionPower>(HandleOblivionPower);
        registry.RegisterIgnored<PaleBlueDotPower>();
        registry.Register<PanachePower>(HandlePanachePower);
        registry.Register<RagePower>(HandleRagePower);
        registry.Register<RupturePower>(HandleRupturePower);
        registry.Register<SerpentFormPower>(HandleSerpentFormPower);
        registry.Register<SlowPower>(HandleSlowPower);
        registry.Register<SmoggyPower>(HandleSmoggyPower);
        registry.Register<SneakyPower>(HandleSneakyPower);
        registry.Register<StormPower>(HandleStormPower);
        registry.Register<StranglePower>(HandleStranglePower);
        registry.Register<SubroutinePower>(HandleSubroutinePower);
        registry.Register<TenderPower>(HandleTenderPower);
        registry.Register<VitalSparkPower>(HandleVitalSparkPower);
        registry.Register<VoidFormPower>(HandleVoidFormPower);
        registry.Register<WitheringPresencePower>(HandleWitheringPresencePower);

        registry.RegisterIgnored<ArtOfWar>();
        registry.Register<BrilliantScarf>(HandleBrilliantScarf);
        registry.Register<DaughterOfTheWind>(HandleDaughterOfTheWind);
        registry.Register<GamePiece>(HandleGamePiece);
        registry.Register<HelicalDart>(HandleHelicalDart);
        registry.Register<IronClub>(HandleIronClub);
        registry.Register<IvoryTile>(HandleIvoryTile);
        registry.Register<Kunai>(HandleKunai);
        registry.Register<Kusarigama>(HandleKusarigama);
        registry.Register<LetterOpener>(HandleLetterOpener);
        registry.Register<LostWisp>(HandleLostWisp);
        registry.Register<MummifiedHand>(HandleMummifiedHand);
        registry.Register<MusicBox>(HandleMusicBox);
        registry.Register<Nunchaku>(HandleNunchaku);
        registry.Register<OrnamentalFan>(HandleOrnamentalFan);
        registry.Register<PaelsLegion>(HandlePaelsLegion);
        registry.Register<PenNib>(HandlePenNib);
        registry.Register<Permafrost>(HandlePermafrost);
        registry.RegisterIgnored<Pocketwatch>();
        registry.Register<RainbowRing>(HandleRainbowRing);
        registry.Register<RazorTooth>(HandleRazorTooth);
        registry.RegisterIgnored<RippleBasin>();
        registry.Register<Shuriken>(HandleShuriken);
        registry.Register<TuningFork>(HandleTuningFork);
        registry.Register<UnsettlingLamp>(HandleUnsettlingLamp);
        registry.Register<Vambrace>(HandleVambrace);
        registry.Register<VelvetChoker>(HandleVelvetChoker);

        return registry;
    }

    private static Registry CreateLateRegistry()
    {
        var registry = new Registry(AfterCardPlayedLate);

        registry.Register<MakeItSo>(HandleMakeItSo);
        registry.Register<RightHandHand>(HandleRightHandHand);

        return registry;
    }

    private static void HandleDaughterOfTheWind(DaughterOfTheWind relic, AfterCardPlayedMirrorContext context)
    {
        if (context.PreviewCard.Owner == relic.Owner && context.PreviewCard.Type == CardType.Attack)
        {
            context.Simulator.GainBlock(relic.Owner.Creature, relic.DynamicVars.Block);
        }
    }

    private static void HandleBrilliantScarf(BrilliantScarf relic, AfterCardPlayedMirrorContext context)
    {
        if (!context.CardPlay.IsAutoPlay && context.PreviewCard.Owner == relic.Owner)
        {
            var state = context.StateStore.Get(relic, () => new CounterPredictionState(relic._cardsPlayedThisTurn));
            state.Value++;
        }
    }

    private static void HandleGamePiece(GamePiece relic, AfterCardPlayedMirrorContext context)
    {
        if (context.PreviewCard.Owner == relic.Owner && context.PreviewCard.Type == CardType.Power)
        {
            context.Simulator.Draw(relic.Owner, relic.DynamicVars.Cards.BaseValue);
        }
    }

    private static void HandleHelicalDart(HelicalDart relic, AfterCardPlayedMirrorContext context)
    {
        if (context.PreviewCard.Owner == relic.Owner && context.PreviewCard.Tags.Contains(CardTag.Shiv))
        {
            context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
        }
    }

    private static void HandleIronClub(IronClub relic, AfterCardPlayedMirrorContext context)
    {
        if (context.PreviewCard.Owner != relic.Owner)
        {
            return;
        }

        var state = context.StateStore.Get(relic, () => new CounterPredictionState(relic.CardsPlayed));
        if (++state.Value % relic.DynamicVars.Cards.IntValue == 0)
        {
            context.Simulator.Draw(relic.Owner, 1);
        }
    }

    private static void HandleIvoryTile(IvoryTile relic, AfterCardPlayedMirrorContext context)
    {
        if (context.PreviewCard.Owner == relic.Owner &&
            context.CardPlay.Resources.EnergyValue >= relic.DynamicVars[IvoryTile._energyThresholdKey].IntValue)
        {
            context.Simulator.GainEnergy(relic.Owner, relic.DynamicVars.Energy.BaseValue);
        }
    }

    private static void HandleKusarigama(Kusarigama relic, AfterCardPlayedMirrorContext context)
    {
        if (!IncrementCounter(relic, relic._attacksPlayedThisTurn, CardType.Attack, context))
        {
            return;
        }

        var target = context.Rng.CombatTargets.NextItem(context.State.HittableEnemies);
        if (target is not null)
        {
            context.Simulator.Damage(target, relic.DynamicVars.Damage, relic.Owner.Creature);
        }
    }

    private static void HandleLetterOpener(LetterOpener relic, AfterCardPlayedMirrorContext context)
    {
        if (IncrementCounter(relic, relic._skillsPlayedThisTurn, CardType.Skill, context))
        {
            context.Simulator.Damage(context.State.HittableEnemies, relic.DynamicVars.Damage, relic.Owner.Creature);
        }
    }

    private static void HandleKunai(Kunai relic, AfterCardPlayedMirrorContext context)
    {
        if (IncrementCounter(relic, relic._attacksPlayedThisTurn, CardType.Attack, context))
        {
            context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
        }
    }

    private static void HandleLostWisp(LostWisp relic, AfterCardPlayedMirrorContext context)
    {
        if (context.PreviewCard.Owner == relic.Owner && context.PreviewCard.Type == CardType.Power)
        {
            context.Simulator.Damage(context.State.HittableEnemies, relic.DynamicVars.Damage, relic.Owner.Creature);
        }
    }

    private static void HandleMummifiedHand(MummifiedHand relic, AfterCardPlayedMirrorContext context)
    {
        if (context.PreviewCard.Owner != relic.Owner || context.PreviewCard.Type != CardType.Power)
        {
            return;
        }

        var playerState = context.State.GetPlayerCombatState(relic.Owner);
        var handCards = playerState.Hand.Cards;
        var naturallyCostly = handCards
            .Where(card => card.Preview.EnergyCost._base > 0 || card.Preview.BaseStarCost > 0)
            .ToList();
        bool CostsResources(PredictedCard card) =>
            card.GetEnergyCostWithModifiers(context.Simulator, playerState) > 0 ||
            card.GetStarCostWithModifiers(context.Simulator, playerState) > 0;

        var rng = context.Rng.CombatCardSelection;
        var selectedCard = rng.NextItem(naturallyCostly.Where(CostsResources))
            ?? rng.NextItem(handCards.Where(CostsResources))
            ?? rng.NextItem(naturallyCostly)
            ?? rng.NextItem(handCards);
        if (selectedCard is not null)
        {
            selectedCard.SetToFreeThisTurn();
            context.Simulator.History.CardsSelected([selectedCard]);
        }
    }

    private static void HandleMusicBox(MusicBox relic, AfterCardPlayedMirrorContext context)
    {
        var state = context.StateStore.Get(relic, () => new MusicBoxPredictionState(relic));
        if (state.CardBeingPlayed != context.Card.Original)
        {
            return;
        }

        var clone = context.Card.CreateClone();
        clone.MutablePreview.LocalKeywords.Add(CardKeyword.Ethereal);
        context.Simulator.AddGeneratedCardToCombat(
            clone,
            PileType.Hand,
            relic.Owner,
            resultKind: CardGenerationResultKind.Contextual);
        state.WasUsedThisTurn = true;
        state.CardBeingPlayed = null;
    }

    private static void HandleNunchaku(Nunchaku relic, AfterCardPlayedMirrorContext context)
    {
        if (IncrementCounter(relic, relic.AttacksPlayed, CardType.Attack, context))
        {
            context.Simulator.GainEnergy(relic.Owner, relic.DynamicVars.Energy.BaseValue);
        }
    }

    private static void HandleOrnamentalFan(OrnamentalFan relic, AfterCardPlayedMirrorContext context)
    {
        if (IncrementCounter(relic, relic._attacksPlayedThisTurn, CardType.Attack, context))
        {
            context.Simulator.GainBlock(relic.Owner.Creature, relic.DynamicVars.Block);
        }
    }

    private static void HandlePermafrost(Permafrost relic, AfterCardPlayedMirrorContext context)
    {
        if (context.PreviewCard.Owner != relic.Owner || context.PreviewCard.Type != CardType.Power)
        {
            return;
        }

        var state = context.StateStore.Get(relic, () => new FlagPredictionState(relic._activatedThisCombat));
        if (!state.Value)
        {
            context.Simulator.GainBlock(relic.Owner.Creature, relic.DynamicVars.Block);
            state.Value = true;
        }
    }

    private static void HandlePaelsLegion(PaelsLegion relic, AfterCardPlayedMirrorContext context)
    {
        var state = context.StateStore.Get(relic, () => new PaelsLegionPredictionState(relic));
        if (state.AffectedCardPlay == context.CardPlay)
        {
            state.AffectedCardPlay = null;
            state.Cooldown = relic.DynamicVars["Turns"].IntValue;
            state.TriggeredBlockLastTurn = true;
        }
    }

    private static void HandlePenNib(PenNib relic, AfterCardPlayedMirrorContext context)
    {
        var state = context.StateStore.Get(relic, () => new PenNibPredictionState(relic));
        if (state.AttackToDouble == context.Card.Original)
        {
            state.AttackToDouble = null;
        }
    }

    private static void HandleRazorTooth(RazorTooth relic, AfterCardPlayedMirrorContext context)
    {
        if (context.PreviewCard.Owner == relic.Owner &&
            context.PreviewCard.Type is CardType.Attack or CardType.Skill &&
            context.PreviewCard.IsUpgradable)
        {
            context.Simulator.Upgrade(context.Card);
        }
    }

    private static void HandleRainbowRing(RainbowRing relic, AfterCardPlayedMirrorContext context)
    {
        if (context.PreviewCard.Owner != relic.Owner)
        {
            return;
        }

        var state = context.StateStore.Get(relic, () => new RainbowRingPredictionState(relic));
        if (state.ActivationCountThisTurn >= 1)
        {
            return;
        }

        state.AttacksPlayedThisTurn += context.PreviewCard.Type == CardType.Attack ? 1 : 0;
        state.SkillsPlayedThisTurn += context.PreviewCard.Type == CardType.Skill ? 1 : 0;
        state.PowersPlayedThisTurn += context.PreviewCard.Type == CardType.Power ? 1 : 0;
        if (state.AttacksPlayedThisTurn > 0 && state.SkillsPlayedThisTurn > 0 && state.PowersPlayedThisTurn > 0)
        {
            state.ActivationCountThisTurn++;
            context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
        }
    }

    private static void HandleTuningFork(TuningFork relic, AfterCardPlayedMirrorContext context)
    {
        if (context.PreviewCard.Owner != relic.Owner || context.PreviewCard.Type != CardType.Skill)
        {
            return;
        }

        var state = context.StateStore.Get(relic, () => new CounterPredictionState(relic.SkillsPlayed));
        if (++state.Value >= relic.DynamicVars.Cards.IntValue)
        {
            context.Simulator.GainBlock(relic.Owner.Creature, relic.DynamicVars.Block);
            state.Value -= relic.DynamicVars.Cards.IntValue;
        }
    }

    private static void HandleVambrace(Vambrace relic, AfterCardPlayedMirrorContext context)
    {
        var state = context.StateStore.Get(relic, () => new VambracePredictionState(relic));
        if (context.PreviewCard.Owner == relic.Owner &&
            context.Card.Original == state.TriggeringCard &&
            !state.BlockGainedThisCombat)
        {
            state.BlockGainedThisCombat = true;
        }
    }

    private static void HandleUnsettlingLamp(UnsettlingLamp relic, AfterCardPlayedMirrorContext context)
    {
        // Depends on Power hooks; mirror not available for now.
    }

    private static void HandleVelvetChoker(VelvetChoker relic, AfterCardPlayedMirrorContext context)
    {
        if (context.PreviewCard.Owner == relic.Owner)
        {
            var state = context.StateStore.Get(relic, () => new CounterPredictionState(relic._cardsPlayedThisTurn));
            state.Value++;
        }
    }

    private static void HandleShuriken(Shuriken relic, AfterCardPlayedMirrorContext context)
    {
        if (IncrementCounter(relic, relic._attacksPlayedThisTurn, CardType.Attack, context))
        {
            context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
        }
    }

    private static void HandleAfterimagePower(AfterimagePower power, AfterCardPlayedMirrorContext context)
    {
        if (TakePairAmount(power, context) is > 0 and var amount)
        {
            context.Simulator.GainBlock(power.Owner, amount, ValueProp.Unpowered);
        }
    }

    private static void HandleBlackHolePower(BlackHolePower power, AfterCardPlayedMirrorContext context)
    {
        if (context.CardPlay.Resources.StarsSpent > 0 &&
            context.PreviewCard.Owner == power.Owner.Player &&
            context.CardPlay.IsLastInSeries)
        {
            context.Simulator.Damage(context.State.HittableEnemies, power.Amount, ValueProp.Unpowered, power.Owner);
        }
    }

    private static void HandleCalamityPower(CalamityPower power, AfterCardPlayedMirrorContext context)
    {
        if (TakePairAmount(power, context) is null || power.Owner.Player is not { } player)
        {
            return;
        }

        var cards = player.GetUnlockedCharacterCards()
            .Where(card => card.Type == CardType.Attack)
            .GetForCombat(player, power.Amount, context.Rng.CombatCardGeneration)
            .ToList();
        context.Simulator.AddGeneratedCardsToCombat(cards, PileType.Hand, player);
    }

    private static void HandleCurlUpPower(CurlUpPower power, AfterCardPlayedMirrorContext context)
    {
        var state = context.StateStore.Get<CurlUpPredictionState>(power);
        if (state.Consumed || state.PlayedCard != context.Card.Original)
        {
            return;
        }

        state.PlayedCard = null;
        context.Simulator.GainBlock(power.Owner, power.Amount, ValueProp.Unpowered);
        state.Consumed = true;
        // Shadow consumption replaces prediction-relevant power removal. LouseProgenitor.Curled
        // is read only by later monster moves, outside the current-player-turn prediction scope.
    }

    private static void HandleDevourLifePower(DevourLifePower power, AfterCardPlayedMirrorContext context)
    {
        if (context.PreviewCard is Soul && context.PreviewCard.Owner.Creature == power.Owner)
        {
            context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
        }
    }

    private static void HandleGalvanicPower(GalvanicPower power, AfterCardPlayedMirrorContext context)
    {
        if (context.PreviewCard.Affliction is Galvanized)
        {
            context.Simulator.Damage(
                context.PreviewCard.Owner.Creature,
                power.Amount,
                DamageProps.cardUnpowered,
                dealer: null);
        }
    }

    private static void HandleGravityPower(GravityPower power, AfterCardPlayedMirrorContext context)
    {
        if (TakePairAmount(power, context) is > 0 and var amount)
        {
            context.Simulator.Damage(context.State.HittableEnemies, amount, ValueProp.Unpowered, power.Owner);
        }
    }

    private static void HandleHauntPower(HauntPower power, AfterCardPlayedMirrorContext context)
    {
        if (context.PreviewCard is Soul && context.PreviewCard.Owner.Creature == power.Owner)
        {
            var target = context.Rng.CombatTargets.NextItem(context.State.HittableEnemies);
            if (target is not null)
            {
                context.Simulator.Damage(target, power.Amount, DamageProps.nonCardHpLoss, dealer: null);
            }
        }
    }

    private static void HandleMasterPlannerPower(MasterPlannerPower power, AfterCardPlayedMirrorContext context)
    {
        if (context.PreviewCard.Owner == power.Owner.Player && context.PreviewCard.Type == CardType.Skill)
        {
            context.MutablePreviewCard.LocalKeywords.Add(CardKeyword.Sly);
        }
    }

    private static void HandleImitationLearningPower(
        ImitationLearningPower power,
        AfterCardPlayedMirrorContext context)
    {
        var state = context.StateStore.Get(power, () => new ImitationLearningPredictionState(power));
        if (state.Amount <= 0)
        {
            return;
        }

        var index = state.CardAndClones.FindIndex(pair => pair.Card == context.Card);
        if (index < 0)
        {
            return;
        }

        var clone = state.CardAndClones[index].Clone;
        state.CardAndClones.RemoveAt(index);

        state.Amount--;
        context.Simulator.AutoPlay(clone);
    }

    private static void HandleMonologuePower(MonologuePower power, AfterCardPlayedMirrorContext context)
    {
        RecordRiskIfPaired(power, context);
    }

    private static void HandleOblivionPower(OblivionPower power, AfterCardPlayedMirrorContext context)
    {
        RecordRiskIfPaired(power, context);
    }

    private static void HandlePanachePower(PanachePower power, AfterCardPlayedMirrorContext context)
    {
        if (context.PreviewCard.Owner != power.Owner.Player)
        {
            return;
        }

        var state = context.StateStore.Get(power, () => new PanachePredictionState(power));
        if (state.AlreadyApplied && --state.CardsLeft <= 0)
        {
            context.Simulator.Damage(context.State.HittableEnemies, power.Amount, ValueProp.Unpowered, power.Owner);
            state.CardsLeft = PanachePower._baseCardsLeft;
        }
        state.AlreadyApplied = true;
    }

    private static void HandleRagePower(RagePower power, AfterCardPlayedMirrorContext context)
    {
        if (context.PreviewCard.Owner == power.Owner.Player && context.PreviewCard.Type == CardType.Attack)
        {
            context.Simulator.GainBlock(power.Owner, power.Amount, ValueProp.Unpowered);
        }
    }

    private static void HandleSerpentFormPower(SerpentFormPower power, AfterCardPlayedMirrorContext context)
    {
        if (TakePairAmount(power, context) is > 0 and var amount)
        {
            var target = context.Rng.CombatTargets.NextItem(context.State.HittableEnemies);
            if (target is not null)
            {
                context.Simulator.Damage(target, amount, ValueProp.Unpowered, power.Owner);
            }
        }
    }

    private static void HandleRupturePower(RupturePower power, AfterCardPlayedMirrorContext context)
    {
        // The damage hook records incomplete risk only when owner HP loss actually occurs.
    }

    private static void HandleSmoggyPower(SmoggyPower power, AfterCardPlayedMirrorContext context)
    {
        if (context.PreviewCard.Owner.Creature != power.Owner || context.PreviewCard.Type != CardType.Skill)
        {
            return;
        }

        foreach (var card in context.State.GetPlayerCombatState(context.PreviewCard.Owner).AllCards.ToList())
        {
            if (card.Preview.Type == CardType.Skill && card.Preview.Affliction is null)
            {
                context.Simulator.Afflict<Smog>(card, 1);
            }
        }
    }

    private static void HandleSlowPower(SlowPower power, AfterCardPlayedMirrorContext context)
    {
        var state = context.StateStore.Get(
            power,
            () => new CounterPredictionState(power.DynamicVars[SlowPower._slowAmountKey].IntValue));
        state.Value++;
    }

    private static void HandleSneakyPower(SneakyPower power, AfterCardPlayedMirrorContext context)
    {
        if (context.PreviewCard.Owner.Creature != power.Owner && context.PreviewCard.Type == CardType.Attack)
        {
            context.Simulator.GainBlock(power.Owner, power.Amount, ValueProp.Unpowered);
        }
    }

    private static void HandleStormPower(StormPower power, AfterCardPlayedMirrorContext context)
    {
        if (TakePairAmount(power, context) is > 0 and var amount && power.Owner.Player is { } player)
        {
            context.Simulator.OrbChannel<LightningOrb>(player, amount);
        }
    }

    private static void HandleStranglePower(StranglePower power, AfterCardPlayedMirrorContext context)
    {
        if (TakePairAmount(power, context) is { } amount)
        {
            context.Simulator.Damage(power.Owner, amount, DamageProps.nonCardHpLoss, dealer: null);
        }
    }

    private static void HandleSubroutinePower(SubroutinePower power, AfterCardPlayedMirrorContext context)
    {
        if (TakePairAmount(power, context) is > 0 and var amount && power.Owner.Player is { } player)
        {
            context.Simulator.GainEnergy(player, amount);
        }
    }

    private static void HandleVoidFormPower(VoidFormPower power, AfterCardPlayedMirrorContext context)
    {
        if (context.PreviewCard.Owner.Creature == power.Owner &&
            context.CardPlay is { IsAutoPlay: false, IsLastInSeries: true })
        {
            context.StateStore.Get(power, () => new VoidFormPredictionState(power)).CardsPlayedThisTurn++;
        }
    }

    private static void HandleTenderPower(TenderPower power, AfterCardPlayedMirrorContext context)
    {
        if (context.PreviewCard.Owner == power.Owner.Player)
        {
            context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
        }
    }

    private static void HandleVitalSparkPower(VitalSparkPower power, AfterCardPlayedMirrorContext context)
    {
        // Ignored for now; does not affect prediction-relevant state.
    }

    private static void HandleWitheringPresencePower(WitheringPresencePower power, AfterCardPlayedMirrorContext context)
    {
        if (context.PreviewCard.Owner != power.Target?.Player)
        {
            return;
        }

        var state = context.StateStore.Get(power,
            () => new CounterPredictionState(power.DynamicVars[WitheringPresencePower._cardsLeftKey].IntValue));
        if (--state.Value <= 0)
        {
            context.Simulator.CreateAndAddGeneratedCardsToCombat<Wither>(
                context.PreviewCard.Owner,
                PileType.Hand,
                1,
                creator: null);
            state.Value = WitheringPresencePower._baseCardsLeft;
        }
    }

    private static void HandleBansheesCry(BansheesCry card, AfterCardPlayedMirrorContext context)
    {
        if (context.PreviewCard.Owner == card.Owner &&
            context.Card.GetKeywords(context.Simulator).Contains(CardKeyword.Ethereal) &&
            context.State.FindCard(card) is { } predictedCard)
        {
            predictedCard.MutablePreview.EnergyCost.AddThisCombat(-card.DynamicVars.Energy.IntValue);
        }
    }

    private static void HandlePinpoint(Pinpoint card, AfterCardPlayedMirrorContext context)
    {
        if (context.PreviewCard.Owner == card.Owner &&
            context.PreviewCard.Type == CardType.Skill &&
            context.State.FindCard(card) is { } predictedCard)
        {
            predictedCard.MutablePreview.EnergyCost.AddThisTurn(-1);
        }
    }

    private static void HandleGoopy(Goopy enchantment, AfterCardPlayedMirrorContext context)
    {
        if (context.Card.References(enchantment.Card) && context.MutablePreviewCard.Enchantment is Goopy preview)
        {
            preview._amount++;
        }
    }

    private static void HandleGlam(Glam enchantment, AfterCardPlayedMirrorContext context)
    {
        if (context.Card.References(enchantment.Card) && context.MutablePreviewCard.Enchantment is Glam preview)
        {
            preview._usedThisCombat = true;
            preview._status = EnchantmentStatus.Disabled;
        }
    }

    private static void HandleVigorous(Vigorous enchantment, AfterCardPlayedMirrorContext context)
    {
        if (context.Card.References(enchantment.Card) && context.MutablePreviewCard.Enchantment is Vigorous preview)
        {
            preview._status = EnchantmentStatus.Disabled;
        }
    }

    private static void HandleMakeItSo(MakeItSo card, AfterCardPlayedMirrorContext context)
    {
        if (context.PreviewCard.Owner != card.Owner ||
            context.PreviewCard.Type != CardType.Skill ||
            context.State.FindCard(card) is not { } predictedCard ||
            predictedCard.GetPile(context.State)?.Type == PileType.Hand)
        {
            return;
        }

        var count = CombatManager.Instance.History.CardPlaysFinished.Count(entry =>
            entry.HappenedThisTurn(context.CombatState) &&
            entry.CardPlay.Card.Type == CardType.Skill &&
            entry.CardPlay.Player == card.Owner);
        count += context.History.OfType<CombatPredictionCardPlayFinishedEntry>().Count(entry =>
            entry.CardPlay.Card.Type == CardType.Skill && entry.CardPlay.Player == card.Owner);
        if (count % card.DynamicVars.Cards.IntValue == 0)
        {
            context.Simulator.AddToPile(predictedCard, PileType.Hand);
        }
    }

    private static void HandleRightHandHand(RightHandHand card, AfterCardPlayedMirrorContext context)
    {
        if (context.PreviewCard.Owner == card.Owner &&
            context.CardPlay.Resources.EnergyValue >= card.DynamicVars.Energy.IntValue &&
            context.State.FindCard(card) is { } predictedCard &&
            predictedCard.GetPile(context.State)?.Type == PileType.Discard)
        {
            context.Simulator.AddToPile(predictedCard, PileType.Hand);
        }
    }

    private static bool IncrementCounter(
        RelicModel relic,
        int initialValue,
        CardType cardType,
        AfterCardPlayedMirrorContext context)
    {
        if (context.PreviewCard.Owner != relic.Owner || context.PreviewCard.Type != cardType)
        {
            return false;
        }

        var state = context.StateStore.Get(relic, () => new CounterPredictionState(initialValue));
        return ++state.Value % relic.DynamicVars.Cards.IntValue == 0;
    }

    private static int? TakePairAmount(AbstractModel model, AfterCardPlayedMirrorContext context)
    {
        var state = context.StateStore.Get<CardPlayPairPredictionState>(model);
        return state.Amounts.Remove(context.CardPlay, out var amount) ? amount : null;
    }

    private static void RecordRiskIfPaired(AbstractModel model, AfterCardPlayedMirrorContext context)
    {
        if (TakePairAmount(model, context) is not null)
        {
            context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
        }
    }
}

internal sealed class AfterCardPlayedMirrorContext : CombatCardMirrorContext
{
    public required CardPlay CardPlay { get; init; }
}

internal sealed class FlagPredictionState(bool value)
{
    public bool Value { get; set; } = value;
}

internal sealed class PanachePredictionState(PanachePower power)
{
    public bool AlreadyApplied { get; set; } = power.GetInternalData<PanachePower.Data>().alreadyApplied;

    public int CardsLeft { get; set; } = power.DynamicVars["CardsLeft"].IntValue;
}

internal sealed class RainbowRingPredictionState(RainbowRing relic)
{
    public int AttacksPlayedThisTurn { get; set; } = relic._attacksPlayedThisTurn;

    public int SkillsPlayedThisTurn { get; set; } = relic._skillsPlayedThisTurn;

    public int PowersPlayedThisTurn { get; set; } = relic._powersPlayedThisTurn;

    public int ActivationCountThisTurn { get; set; } = relic._activationCountThisTurn;
}

internal sealed class CurlUpPredictionState
{
    public CardModel? PlayedCard { get; set; }

    public bool Consumed { get; set; }
}
