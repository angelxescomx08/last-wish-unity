using System.Collections.Generic;
using System.Linq;
using LastWish.Domain;
using NUnit.Framework;
using static LastWish.Tests.Floor1Kit;

namespace LastWish.Tests
{
    /// <summary>Planned intent sequences of the 15 floor-2 enemies (8+ plans each, phases, stages and alternatives) and the catalog.</summary>
    public sealed class Floor2PatternTests
    {
        private static EnemyState Two(string ai, string id = "e0") => EnemyCatalog.Create(ai, 2, id);

        private static void Cycle(string ai, params string[] expected)
        {
            var enemy = Two(ai);
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

        [Test] public void TheCatalogHasFifteenFloorTwoEnemies()
        {
            var defs = EnemyCatalog.Floor2Definitions;
            Assert.That(defs.Count, Is.EqualTo(15));
            Assert.That(defs.Count(d => EnemyCatalog.Profile(d.Ai).Tier == EnemyTier.Normal), Is.EqualTo(6));
            Assert.That(defs.Count(d => EnemyCatalog.Profile(d.Ai).Tier == EnemyTier.Minion), Is.EqualTo(3));
            Assert.That(defs.Count(d => d.Elite), Is.EqualTo(3));
            Assert.That(defs.Count(d => d.Boss), Is.EqualTo(3));
            var names = EnemyCatalog.AllDefinitions.Select(d => d.Name).ToList();
            Assert.That(names.Distinct().Count(), Is.EqualTo(names.Count), "every enemy name is unique");
            var assets = EnemyCatalog.AllDefinitions.Select(d => d.AssetId).ToList();
            foreach (var d in defs) Assert.That(assets.Count(a => a == d.AssetId), Is.EqualTo(1), d.AssetId + " is its own sheet");
            foreach (var d in defs)
            {
                var p = EnemyCatalog.Profile(d.Ai);
                Assert.That((p.MinFloor, p.MaxFloor), Is.EqualTo((2, 2)), d.Ai);
                Assert.That(EnemyCatalog.IsFloor2(d.Ai), Is.True);
                Assert.That(p.Title, Is.Not.Empty); Assert.That(p.Mechanics.Count, Is.GreaterThan(0));
                foreach (var ai in p.Summons) Assert.That(EnemyCatalog.Definition(ai), Is.Not.Null);
            }
            Assert.That(EnemyCatalog.IsFloor2("madre_larva"), Is.False);
            Assert.That(EnemyCatalog.IsFloor2("espectro"), Is.False);
        }

        [Test] public void HpScalesFifteenPercentOnFloorTwo()
        {
            Assert.That(Two("centinela").MaxHp, Is.EqualTo(64));
            Assert.That(Two("trol").MaxHp, Is.EqualTo(64));
            Assert.That(Two("paladin").MaxHp, Is.EqualTo(101));
            Assert.That(Two("hidra").MaxHp, Is.EqualTo(172));
            Assert.That(Two("hidra").Intent.Value, Is.EqualTo(EnemyCatalog.Scale(4, 2)));
            Assert.That(EnemyCatalog.Create("hidra", 1, "x").MaxHp, Is.EqualTo(150), "base HP on floor 1 (Pruebas)");
        }

        [Test] public void NormalsAndMinionsRepeatTheirCycles()
        {
            Cycle("centinela", "rune_maul", "ward_rune", "rune_burst", "rune_maul", "ward_rune", "rune_burst", "rune_maul", "ward_rune");
            Cycle("fanatico", "dark_rite", "ritual_dagger", "blood_slash", "ritual_dagger", "blood_slash", "ritual_dagger", "blood_slash", "ritual_dagger");
            Cycle("sombra", "shadow_claw", "grudge_grow", "rend", "shadow_claw", "grudge_grow", "rend", "shadow_claw", "grudge_grow");
            Cycle("sanguijuela", "suction_cup", "drain", "clot", "suction_cup", "drain", "clot", "suction_cup", "drain");
            Cycle("trol", "club", "lick_wounds", "double_claw", "club", "lick_wounds", "double_claw", "club", "lick_wounds");
            Cycle("ilusionista", "mirror_split", "prism_ray", "blinding_flash", "mirror_split", "prism_ray", "blinding_flash", "mirror_split", "prism_ray");
            Cycle("ilusion", "phantom_strike", "blink", "phantom_strike", "blink", "phantom_strike", "blink", "phantom_strike", "blink");
            Cycle("esqueleto", "rusty_slash", "shield_bash", "rusty_slash", "shield_bash", "rusty_slash", "shield_bash", "rusty_slash", "shield_bash");
            Cycle("esquirla", "cutting_glint", "refract", "cutting_glint", "refract", "cutting_glint", "refract", "cutting_glint", "refract");
        }

        [Test] public void ElitesAndBossesRepeatTheirFirstPhase()
        {
            Cycle("paladin", "shield_of_faith", "judgment_hammer", "condemn", "shield_of_faith", "judgment_hammer", "condemn", "shield_of_faith", "judgment_hammer");
            Cycle("condesa", "crimson_kiss", "bleeding_cut", "mist_veil", "crimson_bite", "crimson_kiss", "bleeding_cut", "mist_veil", "crimson_bite");
            Cycle("sacerdote", "abyss_call", "ceremonial_dagger", "abyss_punish", "abyss_flames", "abyss_call", "ceremonial_dagger", "abyss_punish", "abyss_flames");
            Cycle("nigromante", "raise_bones", "necrotic_touch", "soul_harvest", "bone_wall", "raise_bones", "necrotic_touch", "soul_harvest", "bone_wall");
            Cycle("hidra", "many_bites", "acid_breath", "submerge", "many_bites", "acid_breath", "submerge", "many_bites", "acid_breath");
            Cycle("reina_espejos", "shatter", "prism_cut", "cursed_mirror", "mirror_ray", "shatter", "prism_cut", "cursed_mirror", "mirror_ray");
        }

        [Test] public void IntentNumbersAndKindsFollowTheTables()
        {
            var sentinel = Two("centinela"); var field = new[] { sentinel };
            Assert.That(sentinel.Intent.Kind, Is.EqualTo(IntentKind.Attack)); Assert.That(sentinel.Intent.Value, Is.EqualTo(EnemyCatalog.Scale(10, 2)));
            sentinel.Plan(new StatusSet(), field);
            Assert.That(sentinel.Intent.Kind, Is.EqualTo(IntentKind.Buff));
            Assert.That(sentinel.Intent.Buffs.Single(), Is.EqualTo(new StatusAmount(StatusKind.Ward, 4)));
            Assert.That(sentinel.Intent.AllyBuffs, Is.Empty, "alone it has nobody to protect");
            var group = EnemyCatalog.CreateGroup("conclave", 2, "g");
            group[1].Plan(new StatusSet(), group);
            Assert.That(group[1].Intent.AllyBuffs.Single(), Is.EqualTo(new StatusAmount(StatusKind.Ward, 4)));

            Assert.That(Two("fanatico").Intent.Buffs.Single(), Is.EqualTo(new StatusAmount(StatusKind.Ritual, 2)));
            Assert.That(Two("sombra").Statuses[StatusKind.Grudge], Is.EqualTo(6));
            var leech = Two("sanguijuela");
            Assert.That(leech.Intent.Debuffs.Single(), Is.EqualTo(new StatusAmount(StatusKind.Bleed, 2)));
            leech.Plan(new StatusSet(), new[] { leech });
            Assert.That(leech.Intent.Lifesteal, Is.True);
            Assert.That(Two("trol").Statuses[StatusKind.Regen], Is.EqualTo(3));
            var mage = Two("ilusionista");
            Assert.That(mage.Intent.Kind, Is.EqualTo(IntentKind.Summon)); Assert.That(mage.Intent.Extras.Summons.Single().Ai, Is.EqualTo("ilusion"));
            Assert.That(Two("esquirla").Statuses[StatusKind.Grudge], Is.EqualTo(4));
            Assert.That(Two("condesa").Statuses[StatusKind.Thirst], Is.EqualTo(1));
            var hydra = Two("hidra");
            Assert.That((hydra.Statuses[StatusKind.Heads], hydra.Statuses[StatusKind.Regen]), Is.EqualTo((3, 5)));
            Assert.That(hydra.Intent.Hits, Is.EqualTo(3), "one bite per head");
            var queen = Two("reina_espejos");
            Assert.That(queen.Statuses[StatusKind.Reflect], Is.EqualTo(2));
            Assert.That(queen.Intent.Extras.Summons.Single().Count, Is.EqualTo(2));
            var necro = Two("nigromante");
            Assert.That(necro.Intent.Extras.Summons.Single(), Is.EqualTo(new SummonSpec("esqueleto", 2, 1.0, false)));
            necro.Plan(new StatusSet(), new[] { necro });
            Assert.That(necro.Intent.Debuffs.Single(), Is.EqualTo(new StatusAmount(StatusKind.Necrosis, 2)));
            Assert.That(EnemyCatalog.Create("ilusion", 2, "i").Bound, Is.False, "only a summoned Ilusión is bound");
        }

        [Test] public void PhasesTriggerOnceAtHalfHpAndRestartTheCycle()
        {
            foreach (var (ai, phase, next) in new[] { ("paladin", "oath_broken", "unholy_fury"), ("condesa", "eternal_hunger", "crimson_kiss"),
                ("sacerdote", "ascension", "abyss_call"), ("nigromante", "danse_macabre", "soul_harvest"), ("reina_espejos", "thousand_reflections", "prism_cut") })
            {
                var e = Two(ai); var field = new List<EnemyState> { e };
                e.Plan(new StatusSet(), field);
                e.LoseHp(e.Hp - e.MaxHp / 2);
                e.Plan(new StatusSet(), field);
                Assert.That(e.Intent.Id, Is.EqualTo(phase), ai);
                e.Plan(new StatusSet(), field);
                Assert.That(e.Intent.Id, Is.EqualTo(next), ai + " restarts its second cycle");
                for (var i = 0; i < 8; i++) { e.Plan(new StatusSet(), field); Assert.That(e.Intent.Id, Is.Not.EqualTo(phase), ai + " only once"); }
            }
        }

        [Test] public void TheOathBreakerGrowsWithRitual()
        {
            var paladin = Two("paladin"); paladin.LoseHp(60);
            paladin.Plan(new StatusSet(), new[] { paladin });
            Assert.That(paladin.Intent.Buffs, Is.EquivalentTo(new[] { new StatusAmount(StatusKind.Ritual, 2), new StatusAmount(StatusKind.Strength, 2) }));
        }

        [Test] public void TheHydraGrowsHeadsAtTwoThirdsAndOneThird()
        {
            var hydra = Two("hidra"); var field = new[] { hydra };
            hydra.LoseHp(hydra.MaxHp - hydra.MaxHp * 2 / 3);
            hydra.Plan(new StatusSet(), field);
            Assert.That(hydra.Intent.Id, Is.EqualTo("new_heads"));
            Assert.That(hydra.Intent.Buffs, Does.Contain(new StatusAmount(StatusKind.Heads, 2)));
            hydra.Plan(new StatusSet(), field);
            Assert.That(hydra.Intent.Id, Is.EqualTo("acid_breath"), "the stage did not advance its cycle");
            hydra.LoseHp(hydra.Hp - hydra.MaxHp / 3);
            hydra.Plan(new StatusSet(), field);
            Assert.That(hydra.Intent.Id, Is.EqualTo("hydra_fury"));
            for (var i = 0; i < 9; i++) { hydra.Plan(new StatusSet(), field); Assert.That(new[] { "new_heads", "hydra_fury" }, Does.Not.Contain(hydra.Intent.Id)); }
            var full = Two("hidra", "h2"); full.Statuses.Add(StatusKind.Heads, 5);   // 8 heads: nothing left to grow
            full.LoseHp(full.MaxHp / 2);
            full.Plan(new StatusSet(), new[] { full });
            Assert.That(full.Intent.Buffs.All(b => b.Kind != StatusKind.Heads), Is.True, "capped at " + Floor2Rules.MaxHeads);
        }

        [Test] public void GroupsAndBossFightsOfFloorTwo()
        {
            Assert.That(EnemyCatalog.Floor2Groups.Count, Is.EqualTo(6));
            foreach (var g in EnemyCatalog.Floor2Groups)
            {
                var enemies = EnemyCatalog.CreateGroup(g.Key, 2, "g");
                Assert.That(enemies.Count, Is.EqualTo(g.Members.Count));
                Assert.That(enemies.Any(e => EnemyCatalog.IsFloor2(e.Definition.Ai)), Is.True, g.Key + " has a floor-2 enemy");
                Assert.That(enemies.All(e => e.PairName == g.Name), Is.True);
                var large = enemies.Count(e => EnemyField.IsLarge(e.Definition));
                Assert.That(EnemyField.Fits(large, enemies.Count - large), Is.True, g.Key);
                Assert.That(g.Synergy, Is.Not.Empty);
            }
            Assert.That(EnemyCatalog.Floor2Groups.Count(g => g.Members.Any(m => !EnemyCatalog.IsExpansion(m))), Is.GreaterThanOrEqualTo(3), "mixed with originals");
            Assert.That(EnemyCatalog.FloorBossKeys(2), Is.EqualTo(new[] { "nigromante", "hidra", "reina_espejos" }));
            var boss = EnemyCatalog.CreateBoss("hidra", 2);
            Assert.That(boss.Single().Id, Is.EqualTo("boss_f2"));
            Assert.That(EnemyCatalog.FloorElites(2), Is.EqualTo(new[] { "verdugo", "bruja", "gargola", "minotauro", "escorpion", "paladin", "condesa", "sacerdote" }));
            Assert.That(EnemyCatalog.FloorElites(1).Count, Is.EqualTo(8));
            Assert.That(EnemyCatalog.FloorElites(4), Is.EqualTo(EnemyCatalog.EliteOrder));
        }

        [Test] public void PruebasListsKeepTheirIndicesAndAddFloorTwoAtTheEnd()
        {
            const EnemyRoster x = EnemyRoster.Expanded;
            Assert.That(EnemyCatalog.EncounterTotal(x), Is.EqualTo(54), "floor 3 comes after floor 2");
            Assert.That(EnemyCatalog.EncounterLabel(18, x), Is.EqualTo("Madre Larva"));
            Assert.That(EnemyCatalog.EncounterLabel(30, x), Is.EqualTo("Grupo: Cripta Infestada"));
            Assert.That(EnemyCatalog.EncounterLabel(31, x), Is.EqualTo("Centinela Rúnico (piso 2)"));
            Assert.That(EnemyCatalog.EncounterLabel(37, x), Is.EqualTo("Grupo: Cónclave Abisal (piso 2)"));
            Assert.That(EnemyCatalog.EncounterLabel(42, x), Is.EqualTo("Grupo: Forja Rúnica (piso 2)"));
            Assert.That(EnemyCatalog.EncounterLabel(55, x), Is.EqualTo("Al azar"));
            Assert.That(EnemyCatalog.CreateEncounter(36, 2, "r", x).Single().Definition.Ai, Is.EqualTo("ilusionista"));
            Assert.That(EnemyCatalog.CreateEncounter(41, 2, "r", x).Select(e => e.Definition.Ai), Is.EqualTo(new[] { "sombra", "sombra" }));
            Assert.That(EnemyCatalog.EliteList(x).Count, Is.EqualTo(14));
            Assert.That(EnemyCatalog.EliteLabel(6, x), Is.EqualTo("Rey Rata"));
            Assert.That(EnemyCatalog.EliteLabel(9, x), Is.EqualTo("Paladín Caído (piso 2)"));
            Assert.That(EnemyCatalog.BossKeys(x).Count, Is.EqualTo(12));
            Assert.That(EnemyCatalog.BossLabel(6, x), Is.EqualTo("Los Hermanos Lápida"));
            Assert.That(EnemyCatalog.BossLabel(7, x), Is.EqualTo("El Nigromante (piso 2)"));
            Assert.That(EnemyCatalog.BossLabel(9, x), Is.EqualTo("La Reina de los Espejos (piso 2)"));
            Assert.That(EnemyCatalog.BossLabel(13, x), Is.EqualTo("Por semilla"));
            Assert.That(EnemyCatalog.EncounterTotal(EnemyRoster.Original), Is.EqualTo(17), "the original list never changes");
            Assert.That(EnemyCatalog.EliteList(EnemyRoster.Original).Count, Is.EqualTo(5));
        }

        [Test] public void EveryFloorTwoEnemyHasASheetWithItsMoves()
        {
            foreach (var d in EnemyCatalog.Floor2Definitions)
            {
                var text = System.IO.File.ReadAllText(RepoFile("Assets/Resources/Game/Enemies/" + d.AssetId + "_sheet.json"));
                var profile = EnemyCatalog.Profile(d.Ai);
                foreach (var move in profile.Moves) Assert.That(text, Does.Contain("\"" + move + "\":"), d.Ai + " " + move);
                Assert.That(text.Contains("\"boss\": true"), Is.EqualTo(EnemyField.IsLarge(d)), d.Ai + " size");
                Assert.That(System.IO.File.Exists(RepoFile("Assets/Resources/Game/Enemies/" + d.AssetId + "_sheet.png")), Is.True);
                Assert.That(System.IO.File.Exists(RepoFile("Assets/Resources/Game/Enemies/" + d.AssetId + "_sheet.png.meta")), Is.True);
            }
        }

        [Test] public void FloorTwoStatusNamesCollideWithNothing()
        {
            var cards = System.IO.File.ReadAllText(RepoFile("Assets/Resources/Game/Data/cards.json"));
            var relics = System.IO.File.ReadAllText(RepoFile("Assets/Resources/Game/Data/relics.json"));
            var enemies = System.IO.File.ReadAllText(RepoFile("Assets/Resources/Game/Data/enemies.json"));
            var patterns = System.IO.File.ReadAllText(RepoFile("Assets/Scripts/Domain/EnemyPatterns.Generated.cs"));
            System.Text.RegularExpressions.Regex Word(string name) => new System.Text.RegularExpressions.Regex("(?<![\\wÁÉÍÓÚáéíóúñÑ])" + System.Text.RegularExpressions.Regex.Escape(name) + "(?![\\wáéíóúñ])");
            Assert.That(Word("Veneno").IsMatch(cards), Is.True, "control: the right file was read");
            foreach (var name in EnemyCatalog.Floor2StatusNames.Values)
                foreach (var (file, text) in new[] { ("cards", cards), ("relics", relics), ("enemies", enemies), ("patterns", patterns) })
                    Assert.That(Word(name).IsMatch(text), Is.False, name + " already appears in " + file);
            var floor1 = EnemyCatalog.ExpansionStatusNames.Values.ToList();
            foreach (var name in EnemyCatalog.Floor2StatusNames.Values) Assert.That(floor1, Does.Not.Contain(name));
        }
    }
}
