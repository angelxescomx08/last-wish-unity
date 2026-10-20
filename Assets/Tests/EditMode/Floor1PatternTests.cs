using System.Collections.Generic;
using System.Linq;
using LastWish.Domain;
using NUnit.Framework;
using static LastWish.Tests.Floor1Kit;

namespace LastWish.Tests
{
    /// <summary>Planned intent sequences of the 17 floor-1 enemies (8+ plans each, phases and alternatives).</summary>
    public sealed class Floor1PatternTests
    {
        private static void Cycle(string ai, params string[] expected)
        {
            var enemy = One(ai);
            var field = new List<EnemyState> { enemy };
            var seen = new List<EnemyIntent> { enemy.Intent };
            var ids = new List<string> { enemy.Intent.Id };
            for (var i = 1; i < expected.Length; i++) { enemy.Plan(new StatusSet(), field); ids.Add(enemy.Intent.Id); seen.Add(enemy.Intent); }
            Assert.That(ids, Is.EqualTo(expected), ai);
            foreach (var intent in seen)
            {
                Assert.That(intent.Name, Is.Not.Empty, ai + " " + intent.Id);
                Assert.That(intent.Description, Is.Not.Empty, ai + " " + intent.Id);
                Assert.That(EnemyCatalog.Profile(ai).Moves, Does.Contain(intent.Id), ai + " lists " + intent.Id);
            }
        }

        [Test] public void NormalsRepeatTheirCycles()
        {
            Cycle("madre_larva", "lay", "spit", "lay", "lay", "spit", "lay", "lay", "spit");
            Cycle("erizo", "roll", "bristle", "quills", "roll", "bristle", "quills", "roll", "bristle");
            Cycle("sapo", "tongue", "swallow", "belly", "tongue", "swallow", "belly", "tongue", "swallow");
            Cycle("cuervo", "peck", "caw", "gouge", "peck", "caw", "gouge", "peck", "caw");
            Cycle("rata", "pack_bite", "pack_bite", "squeak", "pack_bite", "pack_bite", "squeak", "pack_bite", "pack_bite");
            Cycle("escarabajo", "curl", "horn", "ram", "curl", "horn", "ram", "curl", "horn");
            Cycle("cieno", "engulf", "sticky", "gel", "engulf", "sticky", "gel", "engulf", "sticky");
            Cycle("larva", "gnaw", "gnaw", "harden", "gnaw", "gnaw", "harden", "gnaw", "gnaw");
            Cycle("cieno_menor", "nibble", "glob", "nibble", "glob", "nibble", "glob", "nibble", "glob");
            Cycle("vibora", "venom_fang", "lurk", "venom_fang", "lurk", "venom_fang", "lurk", "venom_fang", "lurk");
        }

        [Test] public void IntentNumbersAndKindsFollowTheTables()
        {
            var larva = One("madre_larva");
            Assert.That(larva.Intent.Kind, Is.EqualTo(IntentKind.Summon)); Assert.That(larva.Intent.Block, Is.EqualTo(5));
            Assert.That(larva.Intent.Extras.Summons.Single().Ai, Is.EqualTo("larva"));
            var hog = One("erizo"); Assert.That(hog.Statuses[StatusKind.Thorns], Is.EqualTo(2)); Assert.That(hog.Intent.Value, Is.EqualTo(7));
            hog.Plan(new StatusSet(), new[] { hog }); Assert.That(hog.Intent.Kind, Is.EqualTo(IntentKind.Buff)); Assert.That(hog.Intent.Buffs.Single().Kind, Is.EqualTo(StatusKind.Thorns));
            hog.Plan(new StatusSet(), new[] { hog }); Assert.That((hog.Intent.Value, hog.Intent.Hits), Is.EqualTo((3, 3)));
            var toad = One("sapo"); toad.Plan(new StatusSet(), new[] { toad });
            Assert.That(toad.Intent.Kind, Is.EqualTo(IntentKind.Debuff)); Assert.That(toad.Intent.Extras.Swallow, Is.EqualTo(1));
            var crow = One("cuervo"); Assert.That(crow.Statuses[StatusKind.Carrion], Is.EqualTo(2)); Assert.That((crow.Intent.Value, crow.Intent.Hits), Is.EqualTo((3, 2)));
            var beetle = One("escarabajo"); Assert.That(beetle.Statuses[StatusKind.Shell], Is.EqualTo(12));
            Assert.That(beetle.Intent.Kind, Is.EqualTo(IntentKind.Block)); Assert.That(beetle.Intent.Value, Is.EqualTo(8));
            var king = One("rey_rata"); Assert.That(king.Intent.Extras.Summons.Single().Count, Is.EqualTo(2));
            Assert.That(king.MaxHp, Is.EqualTo(84));
            Assert.That(One("duelista").Statuses[StatusKind.Parry], Is.EqualTo(1)); Assert.That(One("duelista").Riposte, Is.EqualTo(6));
            Assert.That(One("inquisidor").Statuses[StatusKind.Vigil], Is.EqualTo(3));
            var stunned = EnemyPatterns.Stunned(1); Assert.That(stunned.Kind, Is.EqualTo(IntentKind.Stun)); Assert.That(stunned.Description, Is.Not.Empty);
        }

        [Test] public void PackBiteCountsTheOtherLivingRats()
        {
            var rats = EnemyCatalog.CreateGroup("plaga", 1, "g");
            Assert.That(rats.Select(r => r.Intent.Value), Is.EqualTo(new[] { 4, 4, 4 }));
            Assert.That(rats[0].MaxHp, Is.EqualTo(16));
            var state = Fight(rats, new[] { Huge() });
            state.SelectEnemy(0); state.PlayCard(0);
            Assert.That(rats[1].Intent.Value, Is.EqualTo(3)); Assert.That(rats[2].Intent.Value, Is.EqualTo(3));
        }

        [Test] public void ArmoredRamAddsHalfTheBlockItKeeps()
        {
            var beetle = One("escarabajo"); var field = new[] { beetle };
            beetle.GainBlock(20);
            beetle.Plan(new StatusSet(), field); beetle.Plan(new StatusSet(), field);
            Assert.That(beetle.Intent.Id, Is.EqualTo("ram")); Assert.That(beetle.Intent.Value, Is.EqualTo(4 + 12 / 2));
        }

        [Test] public void OozeSplitsWhenPlanningAtHalfHpWithoutAdvancingItsCycle()
        {
            var ooze = One("cieno"); var field = new[] { ooze };
            ooze.LoseHp(27);
            ooze.Plan(new StatusSet(), field); Assert.That(ooze.Intent.Id, Is.EqualTo("split")); Assert.That(ooze.Intent.Kind, Is.EqualTo(IntentKind.Summon));
            ooze.Plan(new StatusSet(), field); Assert.That(ooze.Intent.Id, Is.EqualTo("split"), "split is planned again until it is executed");
            Assert.That(ooze.Step, Is.EqualTo(1));
        }

        [Test] public void SummonersPlanTheirAlternativeWhenTheFieldIsFullOrTheBudgetIsSpent()
        {
            var mother = One("madre_larva");
            var full = new List<EnemyState> { mother, One("larva", "a"), One("larva", "b") };
            SetInternal(mother, "Step", 0); mother.Plan(new StatusSet(), full);
            Assert.That(mother.Intent.Id, Is.EqualTo("feed")); Assert.That(mother.Intent.AllyBuffs.Single().Amount, Is.EqualTo(2));
            SetInternal(mother, "Step", 0); mother.Plan(new StatusSet(), new[] { mother });
            Assert.That(mother.Intent.Id, Is.EqualTo("lay"));
            SetInternal(mother, "SummonsMade", 4);
            SetInternal(mother, "Step", 0); mother.Plan(new StatusSet(), new[] { mother });
            Assert.That(mother.Intent.Id, Is.EqualTo("feed"), "4 larvae per fight");

            var king = One("rey_rata");
            SetInternal(king, "Step", 0); king.Plan(new StatusSet(), new[] { king, One("rata", "r") });
            Assert.That(king.Intent.Id, Is.EqualTo("call_swarm")); Assert.That(king.Intent.Extras.Summons.Single().Count, Is.EqualTo(1), "one free slot");
            SetInternal(king, "Step", 0); king.Plan(new StatusSet(), new[] { king, One("rata", "r"), One("rata", "s") });
            Assert.That(king.Intent.Id, Is.EqualTo("rally"));
        }

        [Test] public void RatKingFuryOnceAtHalfHpAndKeepsItsCycle()
        {
            var king = One("rey_rata"); var field = new List<EnemyState> { king };
            var ids = Trace(king, field, 2);
            king.LoseHp(42);
            ids.AddRange(Enumerable.Range(0, 6).Select(_ => { king.Plan(new StatusSet(), field); return king.Intent.Id; }));
            Assert.That(ids, Is.EqualTo(new[] { "call_swarm", "royal_bite", "plague_fury", "court_feast", "tail_whip", "call_swarm", "royal_bite", "court_feast" }));
        }

        [Test] public void ColossusChargesAndOverheatsAfterItsHammer()
        {
            var colossus = One("coloso"); var field = new[] { colossus };
            Assert.That(Trace(colossus, field, 3), Is.EqualTo(new[] { "iron_step", "stoke", "pile_driver" }));
            Assert.That(colossus.Intent.Value, Is.EqualTo(24)); Assert.That(colossus.Intent.Extras.ConsumesFocus, Is.True);
            colossus.Statuses.Add(StatusKind.Focus, 16); colossus.LoseHp(10); // still charging: the transition waits
            colossus.LoseHp(40);
            Assert.That(colossus.Statuses[StatusKind.Focus], Is.Zero);
            colossus.Statuses.Add(StatusKind.Focus, 5);
            colossus.Plan(new StatusSet(), field); Assert.That(colossus.Intent.Id, Is.EqualTo("iron_step"), "deferred while it has Carga");
            colossus.Statuses.Remove(StatusKind.Focus);
            var ids = Trace(colossus, field, 1);
            colossus.Plan(new StatusSet(), field); ids.Add(colossus.Intent.Id);
            colossus.Plan(new StatusSet(), field); ids.Add(colossus.Intent.Id); var stoke = colossus.Intent;
            colossus.Plan(new StatusSet(), field); ids.Add(colossus.Intent.Id);
            colossus.Plan(new StatusSet(), field); ids.Add(colossus.Intent.Id);
            Assert.That(ids, Is.EqualTo(new[] { "iron_step", "overheat", "stoke", "pile_driver", "iron_step" }));
            Assert.That(stoke.Extras.SetSelf.Single().Amount, Is.EqualTo(20));
        }

        [Test] public void DuelistAndItsSecondWind()
        {
            Cycle("duelista", "en_garde", "lunge", "feint", "fleche", "en_garde", "lunge", "feint", "fleche");
            var duelist = One("duelista"); var field = new[] { duelist };
            duelist.LoseHp(40);
            Assert.That(Trace(duelist, field, 4), Is.EqualTo(new[] { "en_garde", "second_wind", "lunge", "feint" }));
        }

        [Test] public void InquisitorPhasesRestartTheCycle()
        {
            Cycle("inquisidor", "edict", "confiscate", "verdict", "judgment", "expiation", "edict", "confiscate", "verdict");
            var boss = One("inquisidor"); var field = new[] { boss };
            boss.Plan(new StatusSet(), field);
            boss.LoseHp(72);
            var ids = Trace(boss, field, 1);
            for (var i = 0; i < 6; i++) { boss.Plan(new StatusSet(), field); ids.Add(boss.Intent.Id); }
            Assert.That(ids, Is.EqualTo(new[] { "confiscate", "auto_de_fe", "verdict", "judgment", "confiscate", "pyre", "verdict" }));
            Assert.That(boss.Intent.Hits, Is.EqualTo(3));
        }

        [Test] public void SerpentBroodMoltAndNewSkin()
        {
            Cycle("sierpe", "brood", "fangs", "constrict", "molt", "brood", "fangs", "constrict", "molt");
            var boss = One("sierpe"); var field = new List<EnemyState> { boss };
            boss.LoseHp(66);
            Assert.That(Trace(boss, field, 1).Concat(Enumerable.Range(0, 6).Select(_ => { boss.Plan(new StatusSet(), field); return boss.Intent.Id; })),
                Is.EqualTo(new[] { "brood", "new_skin", "brood_frenzy", "death_bite", "molt", "tail", "brood_frenzy" }));
            Assert.That(boss.Intent.Extras.Summons.Single().Count, Is.EqualTo(2));
            field.Add(One("vibora", "v")); field.Add(One("vibora", "w"));
            SetInternal(boss, "Step", 0); boss.Plan(new StatusSet(), field); Assert.That(boss.Intent.Id, Is.EqualTo("hiss"), "two large-boss slots are taken");
        }

        [Test] public void UnlinkedGravediggerAndMournerRepeatTheirLinkedCycle()
        {
            Cycle("sepulturero", "dig_in", "shovel", "requiem", "dig_in", "shovel", "requiem", "dig_in", "shovel");
            Cycle("planidera", "lament", "wail", "requiem", "lament", "wail", "requiem", "lament", "wail");
            foreach (var ai in new[] { "sepulturero", "planidera" })
            {
                var e = One(ai); var ids = Trace(e, new[] { e }, 12);
                Assert.That(ids.Intersect(new[] { "dig_grave", "exhume", "grief", "soul_drain" }), Is.Empty, ai);
                Assert.That(e.Intent.AllyBuffs, Is.Empty); Assert.That(e.Intent.AllyBlock, Is.Zero);
            }
        }
    }
}
