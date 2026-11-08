using System.Collections.Generic;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>scenes/combat_reward_scene.py, redesigned 2026-11-08 as steps (<see cref="VictoryRewardScene"/>): the gold, then
    /// one of the offered cards (or none), then — after an elite — its relic, which you may take or leave.</summary>
    public sealed class CombatRewardScene : VictoryRewardScene
    {
        private readonly RewardStep cardStep, relicStep;
        public RelicOffer Relic { get; }
        public CardDefinition ChosenCard { get; private set; }
        /// <summary>The elite's relic was taken (it is optional since 2026-11-08).</summary>
        public bool TookRelic { get; private set; }

        /// <summary>The rarity chip under each offered card (layout tests).</summary>
        public IReadOnlyList<RectF> RarityTags => CurrentStep == cardStep ? Layout().RarityTags : (IReadOnlyList<RectF>)new List<RectF>();
        /// <summary>Where each offered card is drawn now (lifted when hovered or picked).</summary>
        public IReadOnlyList<RectF> CardRects
        {
            get
            {
                var list = new List<RectF>();
                if (CurrentStep == cardStep) for (var i = 0; i < Layout().Cards.Count; i++) list.Add(CardRect(i));
                return list;
            }
        }
        public RectF? SkipRect => Layout().HasSecondary ? Layout().Secondary : (RectF?)null;
        public RectF PrimaryRect => Layout().Primary;

        public CombatRewardScene(GameContext game, RunState run, int goldEarned, List<OfferedCard> cards, RelicOffer relic = null, bool elite = false)
            : base(game, run, elite || relic != null ? VictoryKind.Elite : VictoryKind.Combat, goldEarned, cards, relic != null ? new List<RelicOffer> { relic } : null)
        {
            Relic = relic;
            cardStep = AddStep(RewardStepKind.Card, "Carta");
            if (relic != null) relicStep = AddStep(RewardStepKind.Relic, "Reliquia");
            Start();
        }

        protected override void StepFinished(RewardStep step)
        {
            if (step == cardStep) ChosenCard = step.State == RewardStepState.Done ? step.Card : null;
            else if (step == relicStep) TookRelic = step.State == RewardStepState.Done && step.Relic != null;
        }
    }

    /// <summary>scenes/boss_reward_scene.py, redesigned 2026-11-08 as steps (<see cref="VictoryRewardScene"/>): the gold, the epic
    /// pack (opened by the scene manager, which calls <see cref="PackDone"/> when it closes) and one of the boss relics, or none.</summary>
    public sealed class BossRewardScene : VictoryRewardScene
    {
        private readonly RewardStep relicStep;
        public bool OpenPackRequested { get; set; }
        public RelicOffer ChosenRelic { get; private set; }
        public RectF PrimaryRect => Layout().Primary;
        public RectF? SkipRect => Layout().HasSecondary ? Layout().Secondary : (RectF?)null;

        public BossRewardScene(GameContext game, RunState run, int goldEarned, List<RelicOffer> relics)
            : base(game, run, VictoryKind.Boss, goldEarned, null, relics)
        {
            AddStep(RewardStepKind.Pack, "Sobre épico");
            relicStep = AddStep(RewardStepKind.Relic, "Reliquia");
            Start();
        }

        protected override void RequestPack() => OpenPackRequested = true;

        /// <summary>The epic pack closed: <paramref name="kept"/> are the cards kept from it (none = skipped).</summary>
        public void PackDone(IReadOnlyList<CardDefinition> kept = null) => ReturnFromPack(kept);

        protected override void StepFinished(RewardStep step)
        {
            if (step == relicStep) ChosenRelic = step.State == RewardStepState.Done ? step.Relic : null;
        }
    }
}
