using System.Collections.Generic;
using System.Linq;
using LastWish.Domain;
using NUnit.Framework;
using static LastWish.Tests.Floor1Kit;

namespace LastWish.Tests
{
    /// <summary>Combat rules of the floor-1 expansion: summons and the on-screen cap, Pinchos, Desvío, Blindaje, Carroña,
    /// Buche, stolen Strength and block, Muda, Carga, Vigilancia, División and the paired bosses.</summary>
    public sealed class Floor1MechanicTests
    {
        private static CardDefinition[] Many(CardDefinition card, int n) => Enumerable.Repeat(card, n).ToArray();

        [Test] public void SummonsArriveAfterTheEnemyTurnWithoutActingAndNeverExceedThreeAlive()
        {
            var mother = One("madre_larva");
            var state = Fight(new[] { mother }, Many(Wait(), 10));
            state.EndTurn();
            Assert.That(state.Enemies.Count, Is.EqualTo(2));
            var larva = state.Enemies[1];
            Assert.That(larva.Id, Is.EqualTo("e0_s1"));
            Assert.That(larva.Definition.Ai, Is.EqualTo("larva"));
            Assert.That(larva.Summoned, Is.True); Assert.That(larva.PaysGold, Is.False); Assert.That(larva.SummonerId, Is.EqualTo("e0"));
            Assert.That(larva.Intent.Id, Is.EqualTo("gnaw"), "planned from step 0");
            Assert.That(state.EnemyLog.Count, Is.EqualTo(1), "the larva did not act on the turn it arrived");
            Assert.That(state.EnemyLog[0].Summoned, Is.EqualTo(new[] { "e0_s1" }));
            var maxAlive = 0;
            for (var t = 0; t < 10; t++) { state.EndTurn(); maxAlive = System.Math.Max(maxAlive, state.Enemies.Count(e => e.Alive)); }
            Assert.That(maxAlive, Is.EqualTo(3));
            Assert.That(state.Enemies.Count, Is.EqualTo(3), "full field: she feeds her brood instead of laying");
            Assert.That(new[] { "feed", "spit" }, Does.Contain(mother.Intent.Id));
        }

        [Test] public void SummonBudgetStopsTheFarm()
        {
            var mother = One("madre_larva");
            var state = Fight(new[] { mother }, Many(Wait(), 10));
            for (var t = 0; t < 20; t++)
            {
                state.EndTurn();
                foreach (var e in state.Enemies) if (e.Summoned && e.Alive) e.LoseHp(e.Hp);
            }
            Assert.That(state.Enemies.Count(e => e.Definition.Ai == "larva"), Is.EqualTo(4));
            Assert.That(mother.SummonsMade, Is.EqualTo(4));
        }

        [Test] public void MinionsPayNoVictoryGold()
        {
            var mother = One("madre_larva");
            var state = Fight(new[] { mother }, Many(Wait(), 10));
            state.EndTurn(); state.EndTurn();
            var run = new RunState(HeroKind.Warrior, 7);
            Assert.That(run.CombatGold(state.Enemies), Is.EqualTo(run.CombatGold(new[] { mother })));
        }

        [Test] public void ThornsHitTheHeroOncePerDamagingCard()
        {
            var hog = One("erizo");
            var state = Fight(new[] { hog }, Many(Hit(4), 6), invincible: false);
            var hp = state.Hp;
            state.SelectEnemy(0); Assert.That(state.PlayCard(0), Is.True);
            Assert.That(hog.Hp, Is.LessThan(hog.MaxHp));
            Assert.That(state.Hp, Is.EqualTo(hp - 2));
            Assert.That(state.LastPlay.HeroHits.Single(), Is.EqualTo(("e0", 2, "thorns")));
            var calm = Fight(new[] { One("erizo") }, Many(Wait(), 6), invincible: false);
            var before = calm.Hp; calm.PlayCard(0);
            Assert.That(calm.Hp, Is.EqualTo(before), "a card that does not damage it is free");
        }

        [Test] public void ParryCancelsTheFirstHitAndRipostes()
        {
            var duelist = One("duelista");
            var state = Fight(new[] { duelist }, Many(Hit(10), 6), invincible: false);
            var hp = state.Hp;
            state.SelectEnemy(0); state.PlayCard(0);
            Assert.That(duelist.Hp, Is.EqualTo(duelist.MaxHp));
            Assert.That(duelist.Statuses[StatusKind.Parry], Is.Zero);
            Assert.That(state.Hp, Is.EqualTo(hp - 6));
            Assert.That(state.LastPlay.Parried["e0"], Is.EqualTo(1));
            state.PlayCard(0);
            Assert.That(duelist.Hp, Is.LessThan(duelist.MaxHp));
        }

        [Test] public void ShellKeepsBlockBetweenTurns()
        {
            var beetle = One("escarabajo");
            var state = Fight(new[] { beetle }, Many(Wait(), 10));
            state.EndTurn();                       // Enroscarse: 8 block
            Assert.That(beetle.Block, Is.EqualTo(8));
            state.EndTurn();                       // keeps up to 12, then Cornada
            Assert.That(beetle.Block, Is.EqualTo(8));
            var plain = One("cuervo"); plain.GainBlock(8);
            var other = Fight(new[] { plain }, Many(Wait(), 10)); other.EndTurn();
            Assert.That(plain.Block, Is.Zero, "without Blindaje the block is lost");
        }

        [Test] public void CarrionFeedsOnRealDeathsButNotOnMinions()
        {
            var group = EnemyCatalog.CreateGroup("carrona", 1, "g");
            var mother = group[0]; var crow = group[1];
            var state = Fight(group, Many(Huge(), 10));
            state.EndTurn();                       // the mother lays a larva
            var larva = state.Enemies.Single(e => e.Definition.Ai == "larva");
            crow.LoseHp(10);
            var strength = crow.Statuses[StatusKind.Strength]; var hp = crow.Hp;
            state.SelectEnemy(state.Enemies.ToList().IndexOf(larva)); state.PlayCard(0);
            Assert.That(larva.Alive, Is.False);
            Assert.That(crow.Statuses[StatusKind.Strength], Is.EqualTo(strength), "a summoned larva does not feed Carroña");
            state.SelectEnemy(0); state.PlayCard(0);
            Assert.That(mother.Alive, Is.False);
            Assert.That(crow.Statuses[StatusKind.Strength], Is.EqualTo(strength + 2));
            Assert.That(crow.Hp, Is.EqualTo(hp + 6));
            Assert.That(state.DeathEvents.Single(d => d.EnemyId == mother.Id).Carrion.Single(), Is.EqualTo((crow.Id, 2, 6)));
        }

        [Test] public void SwallowedCardsReturnWhenTheToadDies()
        {
            var toad = One("sapo");
            var state = Fight(new[] { toad }, Many(Huge(), 12));
            state.EndTurn();                       // Lengüetazo; the hand goes to the discard pile
            var discard = state.DiscardPile.Count;
            Assert.That(toad.Intent.Id, Is.EqualTo("swallow"));
            state.EndTurn();                       // Tragar
            Assert.That(toad.SwallowedCards.Count, Is.EqualTo(1));
            Assert.That(toad.Statuses[StatusKind.Swallowed], Is.EqualTo(1));
            Assert.That(state.EnemyLog[0].Swallowed.Count, Is.EqualTo(1));
            var total = state.DiscardPile.Count + state.DrawPile.Count + state.Hand.Count;
            state.SelectEnemy(0); state.PlayCard(0);
            Assert.That(state.Victory, Is.True);
            Assert.That(state.DeathEvents.Single().ReturnedCards.Count, Is.EqualTo(1));
            Assert.That(state.DiscardPile.Count + state.DrawPile.Count + state.Hand.Count, Is.EqualTo(total + 1), "the swallowed card is back (the played one is in the discard too)");
            Assert.That(discard, Is.GreaterThan(0));
        }

        [Test] public void ConfiscationStealsStrengthOrGainsTheFallback()
        {
            var boss = One("inquisidor");
            var state = Fight(new[] { boss }, Many(Wait(), 12));
            state.EndTurn();                       // Edicto
            SetInternal(state, "Strength", 5);
            state.EndTurn();                       // Confiscación
            Assert.That(state.Strength, Is.Zero);
            Assert.That(boss.Statuses[StatusKind.Strength], Is.EqualTo(5));
            Assert.That(state.EnemyLog[0].StrengthStolen, Is.EqualTo(5));
            var poor = One("inquisidor");
            var other = Fight(new[] { poor }, Many(Wait(), 12));
            other.EndTurn(); other.EndTurn();
            Assert.That(poor.Statuses[StatusKind.Strength], Is.EqualTo(2));
            Assert.That(other.EnemyLog[0].StrengthGained, Is.EqualTo(2));
        }

        [Test] public void JudgmentTakesHalfTheHeroBlock()
        {
            var boss = One("inquisidor");
            var state = Fight(new[] { boss }, Many(Guard(20), 12));
            state.EndTurn(); state.EndTurn(); state.EndTurn();   // Edicto, Confiscación, Veredicto
            Assert.That(boss.Intent.Id, Is.EqualTo("judgment"));
            state.PlayCard(0);
            var block = state.Block;
            Assert.That(block, Is.GreaterThan(1));
            state.EndTurn();
            Assert.That(state.EnemyLog[0].BlockStolen, Is.EqualTo(block / 2));
            Assert.That(boss.Block, Is.GreaterThanOrEqualTo(block / 2));
        }

        [Test] public void MoltClearsDebuffsAndElementsIntoBlock()
        {
            var boss = One("sierpe");
            var state = Fight(new[] { boss }, Many(Wait(), 12));
            state.EndTurn(); state.EndTurn(); state.EndTurn();   // Nidada, Colmillos, Constricción
            Assert.That(boss.Intent.Id, Is.EqualTo("molt"));
            boss.Statuses.Add(StatusKind.Poison, 5); boss.Statuses.Add(StatusKind.Weak, 2); boss.Statuses.Add(StatusKind.Fire, 3);
            state.EndTurn();
            var action = state.EnemyLog.Single(a => a.MoveId == "molt");
            Assert.That(action.Cleansed, Is.EqualTo(4 + 2 + 3), "poison already ticked once");
            Assert.That(boss.Block, Is.EqualTo(18));
            Assert.That(boss.Statuses[StatusKind.Poison] + boss.Statuses[StatusKind.Weak] + boss.Statuses[StatusKind.Fire], Is.Zero);
        }

        [Test] public void BreakingTheChargeStunsTheColossus()
        {
            var colossus = One("coloso");
            var state = Fight(new[] { colossus }, Many(Hit(20), 12), invincible: false);
            state.EndTurn(); state.EndTurn();     // Pisada de Hierro, Avivar la Caldera
            Assert.That(colossus.Statuses[StatusKind.Focus], Is.EqualTo(16));
            Assert.That(colossus.Intent.Id, Is.EqualTo("pile_driver"));
            state.SelectEnemy(0); state.PlayCard(0);
            Assert.That(colossus.Intent.Kind, Is.EqualTo(IntentKind.Stun));
            Assert.That(colossus.Statuses[StatusKind.Focus], Is.Zero);
            var hp = state.Hp;
            state.EndTurn();
            Assert.That(state.EnemyLog[0].Stunned, Is.True);
            Assert.That(state.Hp, Is.EqualTo(hp), "the Martillo Pilón never lands");
        }

        [Test] public void AnUnbrokenChargeLands()
        {
            var colossus = One("coloso");
            var state = Fight(new[] { colossus }, Many(Wait(), 12), invincible: false);
            state.EndTurn(); state.EndTurn();
            var hp = state.Hp;
            state.EndTurn();
            Assert.That(state.EnemyLog[0].MoveId, Is.EqualTo("pile_driver"));
            Assert.That(hp - state.Hp, Is.EqualTo(24));
            Assert.That(colossus.Statuses[StatusKind.Focus], Is.Zero);
        }

        [Test] public void VigilPunishesLongTurns()
        {
            var boss = One("inquisidor");
            var state = Fight(new[] { boss }, Many(Wait(), 12));
            for (var i = 0; i < 3; i++) state.PlayCard(0);
            Assert.That(boss.Statuses[StatusKind.Strength], Is.Zero);
            state.PlayCard(0);
            Assert.That(boss.Statuses[StatusKind.Strength], Is.EqualTo(1));
            Assert.That(state.LastPlay.VigilGains, Is.EqualTo(new[] { "e0" }));
            state.PlayCard(0);
            Assert.That(boss.Statuses[StatusKind.Strength], Is.EqualTo(2));
        }

        [Test] public void OozeSplitsIntoTwoHalvesThatPayGold()
        {
            var ooze = One("cieno");
            var state = Fight(new[] { ooze }, Many(Wait(), 12));
            ooze.LoseHp(30);                       // 24 / 54 left
            state.EndTurn();                       // acts, then plans the split
            Assert.That(ooze.Intent.Id, Is.EqualTo("split"));
            state.EndTurn();
            Assert.That(ooze.Alive, Is.False);
            Assert.That(state.EnemyLog[0].Split, Is.True);
            var parts = state.Enemies.Where(e => e.Definition.Ai == "cieno_menor").ToList();
            Assert.That(parts.Count, Is.EqualTo(2));
            Assert.That(parts.Select(p => p.MaxHp), Is.EqualTo(new[] { 12, 12 }));
            Assert.That(parts.All(p => p.PaysGold && p.Alive), Is.True);
            Assert.That(state.Victory, Is.False);
        }

        [Test] public void TheGravediggerDigsUpTheMournerOnce()
        {
            var bosses = EnemyCatalog.CreateBoss("lapida", 1);
            var digger = bosses[0]; var mourner = bosses[1];
            Assert.That((digger.Definition.Ai, mourner.Definition.Ai), Is.EqualTo(("sepulturero", "planidera")));
            Assert.That((digger.MaxHp, mourner.MaxHp), Is.EqualTo((84, 64)));
            Assert.That(digger.PartnerId, Is.EqualTo(mourner.Id));
            var state = Fight(bosses, Many(Huge(), 12));
            state.SelectEnemy(1); state.PlayCard(0);
            Assert.That(mourner.Alive, Is.False);
            Assert.That(state.Victory, Is.False);
            Assert.That(digger.Intent.Id, Is.EqualTo("dig_grave"));
            state.EndTurn();
            Assert.That(digger.Intent.Id, Is.EqualTo("exhume"));
            Assert.That(digger.Statuses[StatusKind.Focus], Is.EqualTo(16));
            state.EndTurn();
            var revived = state.Enemies.Last();
            Assert.That(revived.Definition.Ai, Is.EqualTo("planidera"));
            Assert.That((revived.Hp, revived.MaxHp), Is.EqualTo((32, 64)));
            Assert.That(revived.PartnerId, Is.EqualTo(digger.Id)); Assert.That(digger.PartnerId, Is.EqualTo(revived.Id));
            Assert.That(revived.PaysGold, Is.True);
            Assert.That(state.EnemyLog.Single(a => a.MoveId == "exhume").Revived, Is.EqualTo(revived.Id));
            state.SelectEnemy(state.Enemies.Count - 1); state.PlayCard(0);
            Assert.That(digger.Intent.Id, Is.EqualTo("grief"), "no second revival");
            state.SelectEnemy(0); state.PlayCard(0);
            Assert.That(state.Victory, Is.True);
        }

        [Test] public void HittingTheGravediggerWhileHeDigsStopsTheRevival()
        {
            var bosses = EnemyCatalog.CreateBoss("lapida", 1);
            var state = Fight(bosses, Many(Hit(20), 12));
            bosses[1].LoseHp(bosses[1].Hp);       // she falls outside a card: he plans the dig after his turn
            state.EndTurn();
            Assert.That(bosses[0].Intent.Id, Is.EqualTo("dig_grave"));
            state.EndTurn();                       // Cavar Tumba: Carga 16
            Assert.That(bosses[0].Intent.Id, Is.EqualTo("exhume"));
            state.SelectEnemy(0); state.PlayCard(0);
            Assert.That(bosses[0].Intent.Kind, Is.EqualTo(IntentKind.Stun));
            state.EndTurn();
            Assert.That(state.Enemies.Count, Is.EqualTo(2), "nobody comes back");
            Assert.That(bosses[0].Intent.Id, Is.EqualTo("grief"));
        }

        [Test] public void TheMournerAbsorbsHerBrother()
        {
            var bosses = EnemyCatalog.CreateBoss("lapida", 1);
            var digger = bosses[0]; var mourner = bosses[1];
            digger.Statuses.Add(StatusKind.Strength, 2);
            var state = Fight(bosses, Many(Huge(), 12));
            state.SelectEnemy(0); state.PlayCard(0);
            Assert.That(mourner.Intent.Id, Is.EqualTo("soul_drain"));
            Assert.That(mourner.Intent.Buffs.Single(), Is.EqualTo(new StatusAmount(StatusKind.Strength, 5)));
        }

        [Test] public void ExpandedRunsOnlyChangeFloorOneEnemies()
        {
            var seen = new HashSet<string>();
            for (var seed = 1; seed <= 60; seed++)
            {
                var original = new RunState(HeroKind.Warrior, seed);
                var expanded = new RunState(HeroKind.Warrior, seed, EnemyRoster.Expanded);
                Assert.That(expanded.Roster, Is.EqualTo(EnemyRoster.Expanded));
                Assert.That(original.GenerateEnemies("r1").All(e => !EnemyCatalog.IsExpansion(e.Definition.Ai)), Is.True);
                foreach (var e in expanded.GenerateEnemies("r1").Concat(expanded.GenerateElite("r2")).Concat(expanded.GenerateBoss())) seen.Add(e.Definition.Ai);
                Assert.That(expanded.FloorBossKey(), Is.Not.Null);
            }
            Assert.That(seen.Count(EnemyCatalog.IsExpansion), Is.GreaterThan(5), string.Join(",", seen));
        }

        [Test] public void EveryExpansionEnemyHasASheetWithItsMoves()
        {
            foreach (var d in EnemyCatalog.ExpansionDefinitions)
            {
                var text = System.IO.File.ReadAllText(RepoFile("Assets/Resources/Game/Enemies/" + d.AssetId + "_sheet.json"));
                var profile = EnemyCatalog.Profile(d.Ai);
                foreach (var move in profile.Moves) Assert.That(text, Does.Contain("\"" + move + "\":"), d.Ai + " " + move);
                Assert.That(text.Contains("\"boss\": true"), Is.EqualTo(EnemyField.IsLarge(d)), d.Ai + " size");
                foreach (var ai in profile.Summons.Concat(profile.Spawns)) Assert.That(EnemyCatalog.Definition(ai), Is.Not.Null);
                Assert.That(System.IO.File.Exists(RepoFile("Assets/Resources/Game/Enemies/" + d.AssetId + "_sheet.png")), Is.True);
            }
        }
    }
}
