using System.Collections.Generic;
using System.Linq;
using LastWish.Domain;
using NUnit.Framework;
using static LastWish.Tests.Floor1Kit;

namespace LastWish.Tests
{
    /// <summary>Planned intent sequences of the 15 floor-3 enemies (8+ plans each, phases and alternatives) and the catalog.</summary>
    public sealed class Floor3PatternTests
    {
        private static EnemyState Three(string ai, string id = "e0") => EnemyCatalog.Create(ai, 3, id);

        private static void Cycle(string ai, params string[] expected)
        {
            var enemy = Three(ai);
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

        [Test] public void TheCatalogHasFifteenFloorThreeEnemies()
        {
            var defs = EnemyCatalog.Floor3Definitions;
            Assert.That(defs.Count, Is.EqualTo(15));
            Assert.That(defs.Count(d => EnemyCatalog.Profile(d.Ai).Tier == EnemyTier.Normal), Is.EqualTo(6));
            Assert.That(defs.Count(d => EnemyCatalog.Profile(d.Ai).Tier == EnemyTier.Minion), Is.EqualTo(3));
            Assert.That(defs.Count(d => d.Elite), Is.EqualTo(3));
            Assert.That(defs.Count(d => d.Boss), Is.EqualTo(3));
            var names = EnemyCatalog.AllDefinitions.Select(d => d.Name).ToList();
            Assert.That(names.Distinct().Count(), Is.EqualTo(names.Count), "every enemy name is unique");
            var ais = EnemyCatalog.AllDefinitions.Select(d => d.Ai).ToList();
            Assert.That(ais.Distinct().Count(), Is.EqualTo(ais.Count), "every ai is unique");
            var assets = EnemyCatalog.AllDefinitions.Select(d => d.AssetId).ToList();
            foreach (var d in defs) Assert.That(assets.Count(a => a == d.AssetId), Is.EqualTo(1), d.AssetId + " is its own sheet");
            foreach (var d in defs)
            {
                var p = EnemyCatalog.Profile(d.Ai);
                Assert.That((p.MinFloor, p.MaxFloor), Is.EqualTo((3, 3)), d.Ai);
                Assert.That(EnemyCatalog.IsFloor3(d.Ai), Is.True);
                Assert.That(EnemyCatalog.IsFloor2(d.Ai), Is.False);
                Assert.That(p.Title, Is.Not.Empty); Assert.That(p.Mechanics.Count, Is.GreaterThan(0));
                foreach (var ai in p.Summons) Assert.That(EnemyCatalog.Definition(ai), Is.Not.Null);
            }
            Assert.That(EnemyCatalog.IsFloor3("centinela"), Is.False);
            Assert.That(EnemyCatalog.IsFloor3("espectro"), Is.False);
            Assert.That(EnemyCatalog.Profile("abanderado").SoloPool, Is.False, "the Abanderado only comes with company");
        }

        [Test] public void HpScalesThirtyPercentOnFloorThree()
        {
            Assert.That(Three("abanderado").MaxHp, Is.EqualTo(59));
            Assert.That(Three("barbaro").MaxHp, Is.EqualTo(67));
            Assert.That(Three("comandante").MaxHp, Is.EqualTo(111));
            Assert.That(Three("dragona").MaxHp, Is.EqualTo(192));
            Assert.That(Three("abanderado").Intent.Value, Is.EqualTo(EnemyCatalog.Scale(8, 3)));
            Assert.That(EnemyCatalog.Create("dragona", 1, "x").MaxHp, Is.EqualTo(148), "base HP on floor 1 (Pruebas)");
        }

        [Test] public void NormalsAndMinionsRepeatTheirCycles()
        {
            Cycle("abanderado", "pole_strike", "raise_banner", "banner_charge", "pole_strike", "raise_banner", "banner_charge", "pole_strike", "raise_banner");
            Cycle("barbaro", "axe_chop", "war_cry", "whirlwind", "axe_chop", "war_cry", "whirlwind", "axe_chop", "war_cry");
            Cycle("arcanista", "mana_drain", "void_orb", "arcane_barrier", "mana_drain", "void_orb", "arcane_barrier", "mana_drain", "void_orb");
            Cycle("cazador", "ice_javelin", "snow_stalk", "bolt_volley", "ice_javelin", "snow_stalk", "bolt_volley", "ice_javelin", "snow_stalk");
            Cycle("anima", "chill_touch", "frost_veil", "icicles", "chill_touch", "frost_veil", "icicles", "chill_touch", "frost_veil");
            Cycle("ensamblador", "assemble", "wrench", "spark", "assemble", "wrench", "spark", "assemble", "wrench");
            Cycle("automata", "bolt_shot", "wind_up", "bolt_shot", "wind_up", "bolt_shot", "wind_up", "bolt_shot", "wind_up");
            Cycle("escudero", "shield_up", "spear_thrust", "shield_up", "spear_thrust", "shield_up", "spear_thrust", "shield_up", "spear_thrust");
            Cycle("filacteria", "cold_pulse", "life_beat", "cold_pulse", "life_beat", "cold_pulse", "life_beat", "cold_pulse", "life_beat");
        }

        [Test] public void ElitesAndBossesRepeatTheirFirstPhase()
        {
            Cycle("comandante", "call_guard", "command_thrust", "bugle_call", "piercing_lance", "call_guard", "command_thrust", "bugle_call", "piercing_lance");
            Cycle("wendigo", "twin_claws", "frozen_howl", "devour_flesh", "twin_claws", "frozen_howl", "devour_flesh", "twin_claws", "frozen_howl");
            Cycle("cronomante", "slow_time", "paradox", "rewind", "future_strike", "slow_time", "paradox", "rewind", "future_strike");
            Cycle("liche", "blizzard", "frost_lance", "ice_shroud", "void_touch", "blizzard", "frost_lance", "ice_shroud", "void_touch");
            Cycle("relojero", "build_automata", "clock_hands", "stop_time", "chime", "build_automata", "clock_hands", "stop_time", "chime");
            Cycle("dragona", "frost_breath", "piercing_claw", "fold_wings", "glacier_tail", "frost_breath", "piercing_claw", "fold_wings", "glacier_tail");
        }

        [Test] public void IntentNumbersAndKindsFollowTheTables()
        {
            Assert.That(Three("abanderado").Statuses[StatusKind.Banner], Is.EqualTo(2));
            Assert.That(Three("barbaro").Statuses[StatusKind.Rage], Is.EqualTo(1));
            var drain = Three("arcanista").Intent;
            Assert.That(drain.Kind, Is.EqualTo(IntentKind.Attack));
            Assert.That(drain.Debuffs.Single(), Is.EqualTo(new StatusAmount(StatusKind.Exhaustion, 1)));
            var javelin = Three("cazador").Intent;
            Assert.That((javelin.Kind, javelin.Value, javelin.Extras.Pierce), Is.EqualTo((IntentKind.Attack, EnemyCatalog.Scale(8, 3), true)));
            Assert.That(Three("anima").Intent.Debuffs.Single(), Is.EqualTo(new StatusAmount(StatusKind.Frostbite, 2)));
            var tinker = Three("ensamblador");
            Assert.That(tinker.Intent.Kind, Is.EqualTo(IntentKind.Summon));
            Assert.That(tinker.Intent.Extras.Summons.Single(), Is.EqualTo(new SummonSpec("automata", 1, 1.0, false)));
            Assert.That(Three("automata").Intent.Extras.Pierce, Is.True);
            Assert.That(Three("escudero").Intent.Kind, Is.EqualTo(IntentKind.Block));
            Assert.That(Three("comandante").Intent.Extras.Summons.Single().Ai, Is.EqualTo("escudero"));
            var crono = Three("cronomante"); var field = new[] { crono };
            crono.Plan(new StatusSet(), field); crono.Plan(new StatusSet(), field);
            Assert.That((crono.Intent.Id, crono.Intent.Kind, crono.Intent.Extras.Rewind), Is.EqualTo(("rewind", IntentKind.Buff, true)));
            var clock = Three("relojero");
            Assert.That(clock.Statuses[StatusKind.Spring], Is.EqualTo(2));
            Assert.That(clock.Intent.Extras.Summons.Single().Count, Is.EqualTo(2));
            var clockField = new[] { clock };
            clock.Plan(new StatusSet(), clockField); clock.Plan(new StatusSet(), clockField); clock.Plan(new StatusSet(), clockField);
            Assert.That((clock.Intent.Id, clock.Intent.Hits), Is.EqualTo(("chime", 2)), "one hit per Cuerda");
            Assert.That(clock.Intent.Extras.SetSelf.Single(), Is.EqualTo(new StatusAmount(StatusKind.Spring, 1)));
            Assert.That(Three("dragona").Statuses[StatusKind.Shell], Is.EqualTo(12));
            Assert.That(EnemyCatalog.Profile("liche").UndyingWhile, Is.EqualTo("filacteria"));
        }

        [Test] public void PhasesTriggerOnceAtHalfHpAndRestartTheCycle()
        {
            foreach (var (ai, phase, next) in new[] { ("comandante", "last_charge", "call_guard"), ("wendigo", "endless_hunger", "twin_claws"),
                ("cronomante", "time_loop", "slow_time"), ("liche", "phylactery_rite", "frost_lance"), ("relojero", "zero_hour", "build_automata"),
                ("dragona", "glacial_fury", "frost_breath") })
            {
                var e = Three(ai); var field = new List<EnemyState> { e };
                e.Plan(new StatusSet(), field);
                e.LoseHp(e.Hp - e.MaxHp / 2);
                e.Plan(new StatusSet(), field);
                Assert.That(e.Intent.Id, Is.EqualTo(phase), ai);
                e.Plan(new StatusSet(), field);
                Assert.That(e.Intent.Id, Is.EqualTo(next), ai + " restarts its cycle");
                for (var i = 0; i < 8; i++) { e.Plan(new StatusSet(), field); Assert.That(e.Intent.Id, Is.Not.EqualTo(phase), ai + " only once"); }
            }
        }

        [Test] public void TheLichOnlyForgesANewPhylacteryIfTheOldOneFell()
        {
            var party = EnemyCatalog.CreateBoss("liche", 3);
            var lich = party[0];
            lich.LoseHp(lich.Hp - lich.MaxHp / 2);
            lich.Plan(new StatusSet(), party);
            Assert.That(lich.Intent.Id, Is.EqualTo("lich_fury"), "its Filacteria still stands");
            Assert.That(lich.Intent.Buffs.Single(), Is.EqualTo(new StatusAmount(StatusKind.Strength, 3)));
            var alone = Three("liche", "l2");
            alone.LoseHp(alone.Hp - alone.MaxHp / 2);
            alone.Plan(new StatusSet(), new[] { alone });
            Assert.That(alone.Intent.Id, Is.EqualTo("phylactery_rite"));
            Assert.That(alone.Intent.Extras.Summons.Single().Ai, Is.EqualTo("filacteria"));
        }

        [Test] public void TheDragonShedsItsArmourAndRages()
        {
            var dragon = Three("dragona");
            dragon.LoseHp(dragon.Hp - dragon.MaxHp / 2);
            dragon.Plan(new StatusSet(), new[] { dragon });
            Assert.That(dragon.Intent.Id, Is.EqualTo("glacial_fury"));
            Assert.That(dragon.Intent.Buffs, Is.EquivalentTo(new[] { new StatusAmount(StatusKind.Rage, 2), new StatusAmount(StatusKind.Strength, 2) }));
            Assert.That(dragon.Intent.Extras.SetSelf.Single(), Is.EqualTo(new StatusAmount(StatusKind.Shell, 0)));
        }

        [Test] public void GroupsAndBossFightsOfFloorThree()
        {
            Assert.That(EnemyCatalog.Floor3Groups.Count, Is.EqualTo(6));
            foreach (var g in EnemyCatalog.Floor3Groups)
            {
                var enemies = EnemyCatalog.CreateGroup(g.Key, 3, "g");
                Assert.That(enemies.Count, Is.EqualTo(2), g.Key + ": a pair, so the trio roll has room for a third");
                Assert.That(enemies.Any(e => EnemyCatalog.IsFloor3(e.Definition.Ai)), Is.True, g.Key + " has a floor-3 enemy");
                Assert.That(enemies.All(e => e.PairName == g.Name), Is.True);
                Assert.That(enemies.Any(e => EnemyField.IsLarge(e.Definition)), Is.False, g.Key);
                Assert.That(g.Synergy, Is.Not.Empty);
                Assert.That((g.MinFloor, g.MaxFloor), Is.EqualTo((3, 3)));
            }
            Assert.That(EnemyCatalog.Floor3Groups.Count(g => g.Members.Any(m => !EnemyCatalog.IsExpansion(m))), Is.GreaterThanOrEqualTo(3), "mixed with originals");
            Assert.That(EnemyCatalog.Floor3Groups.Count(g => g.Members.Contains("abanderado")), Is.GreaterThanOrEqualTo(3), "the Abanderado comes with company");
            Assert.That(EnemyCatalog.FloorBossKeys(3), Is.EqualTo(new[] { "liche", "relojero", "dragona" }));
            Assert.That(EnemyCatalog.CreateBoss("dragona", 3).Single().Id, Is.EqualTo("boss_f3"));
            var lich = EnemyCatalog.CreateBoss("liche", 3);
            Assert.That(lich.Select(e => (e.Id, e.Definition.Ai)), Is.EqualTo(new[] { ("boss_f3", "liche"), ("boss_f3_e1", "filacteria") }));
            Assert.That(lich[1].PaysGold, Is.True); Assert.That(lich[1].Summoned, Is.False);
            Assert.That(EnemyField.Fits(1, 1), Is.True);
            Assert.That(EnemyCatalog.FloorElites(3), Is.EqualTo(new[] { "verdugo", "bruja", "gargola", "minotauro", "escorpion", "comandante", "wendigo", "cronomante" }));
            Assert.That(EnemyCatalog.FloorElites(4), Is.EqualTo(EnemyCatalog.EliteOrder));
            Assert.That(EnemyCatalog.FloorBossKeys(4), Is.Empty);
        }

        [Test] public void PruebasListsAddFloorThreeAtTheEnd()
        {
            const EnemyRoster x = EnemyRoster.Expanded;
            Assert.That(EnemyCatalog.EncounterTotal(x), Is.EqualTo(54));
            Assert.That(EnemyCatalog.EncounterLabel(42, x), Is.EqualTo("Grupo: Forja Rúnica (piso 2)"));
            Assert.That(EnemyCatalog.EncounterLabel(43, x), Is.EqualTo("Abanderado Helado (piso 3)"));
            Assert.That(EnemyCatalog.EncounterLabel(48, x), Is.EqualTo("Ensamblador (piso 3)"));
            Assert.That(EnemyCatalog.EncounterLabel(49, x), Is.EqualTo("Grupo: Guardia de la Ciudadela (piso 3)"));
            Assert.That(EnemyCatalog.EncounterLabel(54, x), Is.EqualTo("Grupo: Vanguardia Infernal (piso 3)"));
            Assert.That(EnemyCatalog.EncounterLabel(55, x), Is.EqualTo("Al azar"));
            Assert.That(EnemyCatalog.CreateEncounter(43, 3, "r", x).Single().Definition.Ai, Is.EqualTo("abanderado"), "Pruebas can test it alone");
            Assert.That(EnemyCatalog.CreateEncounter(52, 3, "r", x).Select(e => e.Definition.Ai), Is.EqualTo(new[] { "anima", "arcanista" }));
            Assert.That(EnemyCatalog.EliteList(x).Count, Is.EqualTo(14));
            Assert.That(EnemyCatalog.EliteLabel(11, x), Is.EqualTo("Sumo Sacerdote del Abismo (piso 2)"));
            Assert.That(EnemyCatalog.EliteLabel(12, x), Is.EqualTo("Comandante de la Ciudadela (piso 3)"));
            Assert.That(EnemyCatalog.EliteLabel(15, x), Is.EqualTo("Al azar"));
            Assert.That(EnemyCatalog.BossKeys(x).Count, Is.EqualTo(12));
            Assert.That(EnemyCatalog.BossLabel(10, x), Is.EqualTo("El Rey Liche (piso 3)"));
            Assert.That(EnemyCatalog.BossLabel(12, x), Is.EqualTo("La Dragona de Escarcha (piso 3)"));
            Assert.That(EnemyCatalog.BossLabel(13, x), Is.EqualTo("Por semilla"));
            Assert.That(EnemyCatalog.BossFightFloor("lapida"), Is.EqualTo(1));
            Assert.That(EnemyCatalog.BossFightFloor("hidra"), Is.EqualTo(2));
            Assert.That(EnemyCatalog.BossFightFloor("relojero"), Is.EqualTo(3));
            Assert.That(EnemyCatalog.EncounterTotal(EnemyRoster.Original), Is.EqualTo(17), "the original list never changes");
        }

        [Test] public void EveryFloorThreeEnemyHasASheetWithItsMoves()
        {
            foreach (var d in EnemyCatalog.Floor3Definitions)
            {
                var text = System.IO.File.ReadAllText(RepoFile("Assets/Resources/Game/Enemies/" + d.AssetId + "_sheet.json"));
                var profile = EnemyCatalog.Profile(d.Ai);
                foreach (var move in profile.Moves) Assert.That(text, Does.Contain("\"" + move + "\":"), d.Ai + " " + move);
                Assert.That(text.Contains("\"boss\": true"), Is.EqualTo(EnemyField.IsLarge(d)), d.Ai + " size");
                foreach (var ext in new[] { "_sheet.png", "_sheet.png.meta", "_sheet.json.meta" })
                    Assert.That(System.IO.File.Exists(RepoFile("Assets/Resources/Game/Enemies/" + d.AssetId + ext)), Is.True, d.AssetId + ext);
                if (profile.Tier == EnemyTier.Minion) Assert.That(text, Does.Contain("\"spawn\":"), d.Ai + " arrives with a spawn animation");
            }
        }

        [Test] public void FloorThreeStatusNamesCollideWithNothing()
        {
            var cards = System.IO.File.ReadAllText(RepoFile("Assets/Resources/Game/Data/cards.json"));
            var relics = System.IO.File.ReadAllText(RepoFile("Assets/Resources/Game/Data/relics.json"));
            var enemies = System.IO.File.ReadAllText(RepoFile("Assets/Resources/Game/Data/enemies.json"));
            var patterns = System.IO.File.ReadAllText(RepoFile("Assets/Scripts/Domain/EnemyPatterns.Generated.cs"));
            System.Text.RegularExpressions.Regex Word(string name) => new System.Text.RegularExpressions.Regex("(?<![\\wÁÉÍÓÚáéíóúñÑ])" + System.Text.RegularExpressions.Regex.Escape(name) + "(?![\\wáéíóúñ])");
            Assert.That(Word("Veneno").IsMatch(cards), Is.True, "control: the right file was read");
            foreach (var name in EnemyCatalog.Floor3StatusNames.Values)
                foreach (var (file, text) in new[] { ("cards", cards), ("relics", relics), ("enemies", enemies), ("patterns", patterns) })
                    Assert.That(Word(name).IsMatch(text), Is.False, name + " already appears in " + file);
            var earlier = EnemyCatalog.ExpansionStatusNames.Values.Concat(EnemyCatalog.Floor2StatusNames.Values).ToList();
            foreach (var name in EnemyCatalog.Floor3StatusNames.Values) Assert.That(earlier, Does.Not.Contain(name));
        }
    }
}
