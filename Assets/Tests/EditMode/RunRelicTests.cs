using System.Linq;
using LastWish.Domain;
using NUnit.Framework;
namespace LastWish.Tests
{
    public sealed class RunRelicTests
    {
        [SetUp] public void ResetTuning() => Tuning.Reset();
        [Test] public void HpRelicsIncreaseMaximumWithoutHealingAndLuckIsPersistent()
        {
            var run=new RunState(HeroKind.Warrior,42);run.AcquireRelic("iron_heart");
            Assert.That(run.MaxHp,Is.EqualTo(115));Assert.That(run.Hp,Is.EqualTo(100));
            run.AcquireRelic("seven_leaf_clover",ChromaKind.Golden);Assert.That(run.Luck,Is.EqualTo(202));
        }
        [Test] public void CombatUsesOwnedRelicRulesWithoutDoubleApplyingManaDrawOrDamage()
        {
            var run=new RunState(HeroKind.Warrior,42);run.AcquireRelic("combat_amulet");run.AcquireRelic("broken_totem");run.AcquireRelic("fire_orb");
            var room=run.EnterRoom(run.Map.Nodes.Single(n=>n.Available).Id);var battle=run.CreateCombat(run.GenerateEnemies(room.Id));
            Assert.That(battle.ManaMaximum,Is.EqualTo(3));Assert.That(battle.Hand.Count,Is.EqualTo(6));Assert.That(battle.Hero.AttackBonus,Is.EqualTo(4));
        }
        [Test] public void MirrorKeepsGoldenCopyAndDiamondConversionAffectsFutureCards()
        {
            var run=new RunState(HeroKind.Warrior,42);run.AddCard(run.Deck[0].WithChroma(ChromaKind.Golden));run.AcquireRelic("singular_mirror");
            Assert.That(run.Deck.Select(c=>c.Id).Distinct().Count(),Is.EqualTo(run.Deck.Count));Assert.That(run.Deck[0].Chroma,Is.EqualTo(ChromaKind.Golden));
            run.AcquireRelic("diamond_cards");Assert.That(run.Deck[0].Chroma,Is.EqualTo(ChromaKind.Diamond));
            run.AddCard(NumericCards.All.First().WithChroma(ChromaKind.Golden));Assert.That(run.Deck.Last().Chroma,Is.EqualTo(ChromaKind.Diamond));
        }
        [Test] public void PendingDeckChoicesFollowTheLimitsAndCannotRemoveTheLastCard()
        {
            var run=new RunState(HeroKind.Warrior,42);run.AcquireRelic("tailor_scissors");
            Assert.That(run.PendingChoices[0].Count,Is.EqualTo(3));
            var choice=run.NextChoice();Assert.That(run.ChoiceLimit(choice.Kind,choice.Count),Is.EqualTo(3));
            Assert.That(run.ApplyChoice(choice.Kind,new[]{0,0,1,2,-1}),Is.EqualTo(3));
            Assert.That(run.Deck.Count,Is.EqualTo(7));Assert.That(run.PendingChoices.Count,Is.Zero);
            run.AcquireRelic("marked_cards");choice=run.NextChoice();run.ApplyChoice(choice.Kind,new[]{0});Assert.That(run.Deck[0].Innate,Is.True);
            while(run.Deck.Count>1)run.ApplyChoice("remove",new[]{0});Assert.That(run.ApplyChoice("remove",new[]{0}),Is.Zero);
        }
        [Test] public void InterestIsPaidOnceOnPositiveGainsOnly()
        {
            var run=new RunState(HeroKind.Warrior,42);run.AcquireRelic("compound_interest");run.GainGold(100);
            Assert.That(run.Gold,Is.EqualTo(2310));run.GainGold(0);Assert.That(run.Gold,Is.EqualTo(2310));Assert.That(run.LastInterest,Is.Zero);
        }
        [Test] public void VoucherAndMemberPricesAndFreePullsAreStateful()
        {
            var run=new RunState(HeroKind.Warrior,42);run.AcquireRelic("member_card");run.AcquireRelic("voucher");
            Assert.That(run.ShopOfferPrice(150,true,0),Is.Zero);Assert.That(run.ShopOfferPrice(150,false,0),Is.EqualTo(75));Assert.That(run.ShopOfferPrice(150,false,1),Is.EqualTo(150));
            Assert.That(run.UseVoucher(),Is.True);Assert.That(run.ShopOfferPrice(150,true,1),Is.EqualTo(150));
            run.AcquireRelic("two_faced_coin");Assert.That(run.GachaPrice(PullKind.Normal),Is.Zero);Assert.That(run.Pull(PullKind.Normal).Success,Is.True);Assert.That(run.GachaPrice(PullKind.Normal),Is.EqualTo(120));
        }
        [Test] public void CheckpointDoesNotReplayPickupEffectsAndPreservesRelicChargesAndChromas()
        {
            var run=new RunState(HeroKind.Warrior,42);run.AcquireRelic("portable_anvil");run.AcquireRelic("spectral_shield",ChromaKind.Golden);
            run.OwnedRelics.Last().Restore(true,1);run.AddCard(NumericCards.All.First().WithChroma(ChromaKind.Diamond).WithInnate());
            var restored=RunState.FromSave(run.CreateSave());
            Assert.That(restored.Deck.Count(c=>c.Upgraded),Is.EqualTo(1));Assert.That(restored.OwnedRelics.Last().TimesTriggered,Is.EqualTo(1));
            Assert.That(restored.OwnedRelics.Last().Chroma,Is.EqualTo(ChromaKind.Golden));Assert.That(restored.Deck.Last().Chroma,Is.EqualTo(ChromaKind.Diamond));Assert.That(restored.Deck.Last().Innate,Is.True);
        }
        [Test] public void BottomlessBagKeepsThreeFromGoldenPacks()
        {
            var run=new RunState(HeroKind.Mage,1);Assert.That(run.PackPicks(ChromaKind.Golden),Is.EqualTo(2));
            run.AcquireRelic("bottomless_bag");Assert.That(run.PackPicks(ChromaKind.Golden),Is.EqualTo(3));Assert.That(run.PackPicks(ChromaKind.None),Is.EqualTo(1));
        }
        [Test] public void MasterKeyOffersTwoTreasureRelics()
        {
            var run=new RunState(HeroKind.Rogue,5);Assert.That(run.PickTreasureRelics("x").Count,Is.EqualTo(1));
            run.AcquireRelic("master_key");Assert.That(run.PickTreasureRelics("x").Count,Is.EqualTo(2));
        }
    }
}
