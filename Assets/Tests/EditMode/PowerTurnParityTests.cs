using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LastWish.Domain;
using NUnit.Framework;
namespace LastWish.Tests
{
    public sealed class PowerTurnParityTests
    {
        [Test] public void PersistentPowersMatchPythonAcrossThreeFollowingTurns()
        {
            Check("mg_piel_granito", 0, new[] { 50, 0, 0, 9, 10000, 0, 0, 100, 0 });
            Check("mg_piel_granito", 1, new[] { 50, 0, 0, 10, 10000, 0, 5, 95, 0 });
            Check("mg_piel_granito", 2, new[] { 50, 0, 0, 10, 10000, 0, 5, 90, 5 });
            Check("mg_piel_granito", 3, new[] { 50, 0, 0, 10, 10000, 0, 5, 85, 10 });
            Check("wr_segunda_piel", 0, new[] { 50, 0, 0, 8, 10000, 0, 0, 100, 0 });
            Check("wr_segunda_piel", 1, new[] { 50, 0, 0, 10, 10000, 0, 5, 95, 0 });
            Check("wr_segunda_piel", 2, new[] { 50, 0, 0, 10, 10000, 0, 5, 90, 5 });
            Check("wr_segunda_piel", 3, new[] { 50, 0, 0, 10, 10000, 0, 5, 85, 10 });
            Check("m_concentracion", 0, new[] { 50, 0, 0, 9, 10000, 0, 2, 98, 0 });
            Check("m_concentracion", 1, new[] { 50, 0, 0, 10, 10000, 0, 5, 93, 2 });
            Check("m_concentracion", 2, new[] { 50, 0, 0, 10, 10000, 0, 5, 88, 7 });
            Check("m_concentracion", 3, new[] { 50, 0, 0, 10, 10000, 0, 5, 83, 12 });
            Check("r_reflejos", 0, new[] { 50, 0, 0, 9, 10000, 0, 0, 100, 0 });
            Check("r_reflejos", 1, new[] { 50, 0, 0, 10, 10000, 0, 5, 95, 0 });
            Check("r_reflejos", 2, new[] { 50, 0, 0, 10, 10000, 0, 5, 90, 5 });
            Check("r_reflejos", 3, new[] { 50, 0, 0, 10, 10000, 0, 5, 85, 10 });
            Check("r_danza_de_sombras", 0, new[] { 50, 0, 0, 8, 10000, 0, 0, 100, 0 });
            Check("r_danza_de_sombras", 1, new[] { 50, 0, 0, 10, 10000, 0, 5, 95, 0 });
            Check("r_danza_de_sombras", 2, new[] { 50, 0, 0, 10, 10000, 0, 5, 90, 5 });
            Check("r_danza_de_sombras", 3, new[] { 50, 0, 0, 10, 10000, 0, 5, 85, 10 });
            Check("mg_ojo_huracan", 0, new[] { 50, 0, 0, 8, 10000, 0, 0, 100, 0 });
            Check("mg_ojo_huracan", 1, new[] { 50, 0, 0, 10, 10000, 0, 5, 95, 0 });
            Check("mg_ojo_huracan", 2, new[] { 50, 0, 0, 10, 10000, 0, 5, 90, 5 });
            Check("mg_ojo_huracan", 3, new[] { 50, 0, 0, 10, 10000, 0, 5, 85, 10 });
            Check("wr_sangre_hirviente", 0, new[] { 50, 0, 0, 9, 10000, 0, 0, 100, 0 });
            Check("wr_sangre_hirviente", 1, new[] { 48, 0, 1, 10, 10000, 0, 5, 95, 0 });
            Check("wr_sangre_hirviente", 2, new[] { 46, 0, 2, 10, 10000, 0, 5, 90, 5 });
            Check("wr_sangre_hirviente", 3, new[] { 44, 0, 3, 10, 10000, 0, 5, 85, 10 });
            Check("wr_fortaleza_interior", 0, new[] { 50, 0, 0, 9, 10000, 0, 0, 100, 0 });
            Check("wr_fortaleza_interior", 1, new[] { 50, 0, 0, 10, 10000, 0, 5, 95, 0 });
            Check("wr_fortaleza_interior", 2, new[] { 50, 0, 0, 10, 10000, 0, 5, 90, 5 });
            Check("wr_fortaleza_interior", 3, new[] { 50, 0, 0, 10, 10000, 0, 5, 85, 10 });
            Check("wr_regeneracion", 0, new[] { 50, 0, 0, 8, 10000, 0, 0, 100, 0 });
            Check("wr_regeneracion", 1, new[] { 53, 0, 0, 10, 10000, 0, 5, 95, 0 });
            Check("wr_regeneracion", 2, new[] { 56, 0, 0, 10, 10000, 0, 5, 90, 5 });
            Check("wr_regeneracion", 3, new[] { 59, 0, 0, 10, 10000, 0, 5, 85, 10 });
            Check("ep_poder_oculto", 0, new[] { 50, 0, 0, 9, 10000, 0, 4, 96, 0 });
            Check("ep_poder_oculto", 1, new[] { 50, 0, 0, 10, 10000, 0, 5, 91, 4 });
            Check("ep_poder_oculto", 2, new[] { 50, 0, 0, 10, 10000, 0, 5, 86, 9 });
            Check("ep_poder_oculto", 3, new[] { 50, 0, 0, 10, 10000, 0, 5, 81, 14 });
            Check("r_ritmo_letal", 0, new[] { 50, 0, 0, 8, 10000, 0, 0, 100, 0 });
            Check("r_ritmo_letal", 1, new[] { 50, 0, 0, 10, 10000, 0, 5, 95, 0 });
            Check("r_ritmo_letal", 2, new[] { 50, 0, 0, 10, 10000, 0, 5, 90, 5 });
            Check("r_ritmo_letal", 3, new[] { 50, 0, 0, 10, 10000, 0, 5, 85, 10 });
            Check("r_tormenta_de_acero", 0, new[] { 50, 0, 0, 9, 10000, 0, 0, 100, 0 });
            Check("r_tormenta_de_acero", 1, new[] { 50, 0, 0, 10, 9995, 0, 5, 95, 0 });
            Check("r_tormenta_de_acero", 2, new[] { 50, 0, 0, 10, 9990, 0, 5, 90, 5 });
            Check("r_tormenta_de_acero", 3, new[] { 50, 0, 0, 10, 9985, 0, 5, 85, 10 });
            Check("r_rapina", 0, new[] { 50, 0, 0, 9, 10000, 0, 0, 100, 0 });
            Check("r_rapina", 1, new[] { 50, 0, 0, 10, 10000, 0, 5, 95, 0 });
            Check("r_rapina", 2, new[] { 50, 0, 0, 10, 10000, 0, 5, 90, 5 });
            Check("r_rapina", 3, new[] { 50, 0, 0, 10, 10000, 0, 5, 85, 10 });
            Check("r_maestra_de_dagas", 0, new[] { 50, 0, 0, 9, 10000, 0, 0, 100, 0 });
            Check("r_maestra_de_dagas", 1, new[] { 50, 0, 0, 10, 10000, 0, 5, 95, 0 });
            Check("r_maestra_de_dagas", 2, new[] { 50, 0, 0, 10, 10000, 0, 5, 90, 5 });
            Check("r_maestra_de_dagas", 3, new[] { 50, 0, 0, 10, 10000, 0, 5, 85, 10 });
            Check("r_sombra_gemela", 0, new[] { 50, 0, 0, 8, 10000, 0, 0, 100, 0 });
            Check("r_sombra_gemela", 1, new[] { 50, 0, 0, 10, 10000, 0, 5, 95, 0 });
            Check("r_sombra_gemela", 2, new[] { 50, 0, 0, 10, 10000, 0, 5, 90, 5 });
            Check("r_sombra_gemela", 3, new[] { 50, 0, 0, 10, 10000, 0, 5, 85, 10 });
            Check("r_nada_que_perder", 0, new[] { 50, 0, 0, 8, 10000, 0, 0, 100, 0 });
            Check("r_nada_que_perder", 1, new[] { 50, 0, 0, 10, 10000, 0, 6, 93, 1 });
            Check("r_nada_que_perder", 2, new[] { 50, 0, 0, 10, 10000, 0, 6, 86, 8 });
            Check("r_nada_que_perder", 3, new[] { 50, 0, 0, 10, 10000, 0, 6, 79, 15 });
            Check("r_cadena_perfecta", 0, new[] { 50, 0, 0, 8, 10000, 0, 0, 100, 0 });
            Check("r_cadena_perfecta", 1, new[] { 50, 0, 0, 10, 10000, 0, 5, 95, 0 });
            Check("r_cadena_perfecta", 2, new[] { 50, 0, 0, 10, 10000, 0, 5, 90, 5 });
            Check("r_cadena_perfecta", 3, new[] { 50, 0, 0, 10, 10000, 0, 5, 85, 10 });
            Check("r_asesina", 0, new[] { 50, 0, 0, 7, 10000, 0, 0, 100, 0 });
            Check("r_asesina", 1, new[] { 50, 0, 0, 10, 10000, 0, 5, 95, 0 });
            Check("r_asesina", 2, new[] { 50, 0, 0, 10, 10000, 0, 5, 90, 5 });
            Check("r_asesina", 3, new[] { 50, 0, 0, 10, 10000, 0, 5, 85, 10 });
            Check("mg_corazon_igneo", 0, new[] { 50, 0, 0, 8, 10000, 0, 0, 100, 0 });
            Check("mg_corazon_igneo", 1, new[] { 50, 0, 0, 10, 10000, 1, 5, 95, 0 });
            Check("mg_corazon_igneo", 2, new[] { 50, 0, 0, 10, 9999, 2, 5, 90, 5 });
            Check("mg_corazon_igneo", 3, new[] { 50, 0, 0, 10, 9997, 3, 5, 85, 10 });
            Check("mg_archimago", 0, new[] { 50, 0, 0, 7, 10000, 0, 0, 100, 0 });
            Check("mg_archimago", 1, new[] { 50, 0, 0, 10, 10000, 0, 5, 95, 0 });
            Check("mg_archimago", 2, new[] { 50, 0, 0, 10, 10000, 0, 5, 90, 5 });
            Check("mg_archimago", 3, new[] { 50, 0, 0, 10, 10000, 0, 5, 85, 10 });
            Check("wr_piel_acero", 0, new[] { 50, 0, 0, 8, 10000, 0, 0, 100, 0 });
            Check("wr_piel_acero", 1, new[] { 50, 0, 0, 10, 10000, 0, 5, 95, 0 });
            Check("wr_piel_acero", 2, new[] { 50, 0, 0, 10, 10000, 0, 5, 90, 5 });
            Check("wr_piel_acero", 3, new[] { 50, 0, 0, 10, 10000, 0, 5, 85, 10 });
            Check("wr_berserker", 0, new[] { 50, 0, 0, 8, 10000, 0, 0, 100, 0 });
            Check("wr_berserker", 1, new[] { 50, 0, 0, 10, 10000, 0, 5, 95, 0 });
            Check("wr_berserker", 2, new[] { 50, 0, 0, 10, 10000, 0, 5, 90, 5 });
            Check("wr_berserker", 3, new[] { 50, 0, 0, 10, 10000, 0, 5, 85, 10 });
            Check("wr_voluntad_hierro", 0, new[] { 50, 0, 0, 8, 10000, 0, 0, 100, 0 });
            Check("wr_voluntad_hierro", 1, new[] { 50, 0, 0, 10, 10000, 0, 5, 95, 0 });
            Check("wr_voluntad_hierro", 2, new[] { 50, 0, 0, 10, 10000, 0, 5, 90, 5 });
            Check("wr_voluntad_hierro", 3, new[] { 50, 0, 0, 10, 10000, 0, 5, 85, 10 });
        }
        private static void Check(string id, int turns, int[] expected)
        {
            var card = NumericCards.All.Single(c => c.Id == id);
            var enemy = new EnemyState("e", new EnemyDefinition("fixture", "Objetivo", "wraith", 10000), 1, 10000);
            var hero = HeroDefinition.OriginalFor(HeroKind.Warrior).WithBonuses(0, 8, 0);
            var state = new CombatState(hero, new[] { card }, 42, 50, new[] { enemy });
            var draw = (List<CardDefinition>)typeof(CombatState).GetField("draw", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(state);
            for (var i = 0; i < 100; i++) draw.Add(new CardDefinition("filler", "Relleno", 0, 0, 0));
            Assert.That(state.PlayCard(0), Is.True, id);
            for (var i = 0; i < turns; i++)
            { typeof(EnemyState).GetProperty("Intent").SetValue(enemy, new EnemyIntent(IntentKind.Attack, 0)); state.EndTurn(); }
            var actual = new[] { state.Hp, state.Block, state.Strength, state.Mana, enemy.Hp,
                enemy.Statuses[StatusKind.Fire], state.Hand.Count, state.DrawCount, state.DiscardCount };
            Assert.That(actual, Is.EqualTo(expected), id + " turn=" + turns);
        }
    }
}
