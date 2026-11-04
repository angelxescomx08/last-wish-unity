using System.Collections;
using System.Linq;
using LastWish.Domain;
using LastWish.Presentation;
using NUnit.Framework;
using UnityEngine.TestTools;

using EventType = LastWish.Presentation.EventType;
using Key = LastWish.Presentation.Key;

namespace LastWish.Tests
{
    /// <summary>All of the rogue's loot cards (Tools/VfxLab/rogue_pool_art.py .. _6.py) with their
    /// animated illustrations, in the real game: deck viewer (scrolled down to the loot cards) and a combat hand drawn
    /// from them (captures in TestResults/rogue-loot-*.png to inspect by eye).</summary>
    public sealed class RogueLootCardArtTests
    {
        private static readonly string[] Loot =
        {
            "r_pinchazo", "r_tajo_veloz", "r_lanzar_la_daga", "r_lluvia_de_dagas", "r_cuchillada_errante",
            "r_marcar_objetivo", "r_abrir_la_guardia", "r_corte_y_guardia", "r_afilar", "r_carterista",
            "r_chatarra", "r_deshacerse", "r_finta_doble", "r_quiebro", "r_tirar_y_cortar",
            "r_hoja_envenenada", "m_veneno", "r_muro_de_humo", "r_contraataque", "r_golpe_de_gracia",
            "a_corte_rapido", "a_tajo", "r_punalada_trapera", "r_golpe_desesperado", "a_instinto",
            "r_juego_de_manos", "r_manto_raido", "r_reflejos", "e_escudo_reactivo", "r_guardia_evasiva",
            "e_agilidad", "r_senuelo", "r_sombra_esquiva", "r_capa_de_sombras", "r_corte_afortunado",
            "r_hoja_unica", "r_rafaga_de_cortes", "r_tirar_los_dados", "m_torbellino", "r_dagas_ocultas",
            "r_paso_atras", "r_rodar", "r_rebuscar", "r_vaciar_bolsillos", "r_abanico_de_cuchillas", "r_astucia",
            "r_danza_de_sombras", "r_estilo_propio", "r_estocada_oportuna", "r_fortuna_audaz", "r_maestra_de_dagas",
            "r_preparacion", "r_rapina", "r_sombra_gemela", "r_asesina", "r_cadena_perfecta", "ep_lluvia_de_golpes",
            "ep_mazo_impecable", "r_mil_cortes", "r_nada_que_perder", "r_ritmo_letal", "r_tormenta_de_acero",
            "ep_tormenta_veneno",
        };

        [UnityTest]
        public IEnumerator TheRogueLootCardsShowTheAnimatedArt()
        {
            yield return GameDriver.Boot();
            var manager = GameDriver.Manager;
            var art = GameDriver.Game.Context.Art;
            Assert.That(CardArtAnimations.Ids(art), Is.SupersetOf(Loot), "Cards/Anim/anim.json is in Resources");
            var run = new RunState(HeroKind.Rogue, 4242);
            foreach (var id in Loot) run.AddCard(RunContent.Cards.First(c => c.Id == id));
            manager.StartRun(run);
            yield return GameDriver.Until(() => manager.Top is MapScene, 5, "map");
            yield return GameDriver.Frames(3);

            GameDriver.Click(1159, 34);                            // "Mazo": the deck viewer draws cards with CardWidget.Draw
            yield return GameDriver.Seconds(0.6f);
            GameDriver.Capture("rogue-loot-deck");
            GameDriver.Send(new InputEvent(EventType.MouseMove, 640, 400));
            GameDriver.Send(new InputEvent(EventType.Wheel, 640, 400, wheel: -6));   // down to the loot cards
            yield return GameDriver.Seconds(0.6f);
            GameDriver.Capture("rogue-loot-deck-scrolled");
            yield return GameDriver.Seconds(0.33f);
            GameDriver.Capture("rogue-loot-deck-scrolled-later");
            GameDriver.Send(new InputEvent(EventType.Wheel, 640, 400, wheel: -6));   // the last rows
            yield return GameDriver.Seconds(0.6f);
            GameDriver.Capture("rogue-loot-deck-scrolled-more");
            GameDriver.Send(new InputEvent(EventType.Wheel, 640, 400, wheel: -6));   // the newest rows
            yield return GameDriver.Seconds(0.6f);
            GameDriver.Capture("rogue-loot-deck-scrolled-end");
            GameDriver.Send(new InputEvent(EventType.Wheel, 640, 400, wheel: -6));   // the fourth batch (rares)
            yield return GameDriver.Seconds(0.6f);
            GameDriver.Capture("rogue-loot-deck-scrolled-last");
            for (var page = 1; page <= 3; page++)                   // the epics and legendaries (rogue_pool_art_5/_6)
            {
                GameDriver.Send(new InputEvent(EventType.Wheel, 640, 400, wheel: -6));
                yield return GameDriver.Seconds(0.6f);
                GameDriver.Capture("rogue-loot-deck-scrolled-rares-" + page);
            }
            GameDriver.Key(Key.Escape);
            yield return GameDriver.Frames(3);

            ((MapScene)manager.Top).SelectedNode = run.Map.AvailableNodes().First(n => n.Kind == RoomKind.Combat);
            yield return GameDriver.Until(() => manager.Top is CombatScene, 5, "combat");
            var state = ((CombatScene)manager.Top).State;
            yield return GameDriver.Seconds(1.6f);                  // the opening hand is dealt
            Assert.That(state.Hand.Count(c => CardArtAnimations.For(art, c.Id) != null), Is.EqualTo(state.Hand.Count),
                "basic and loot cards alike: every card in this hand is animated");
            var hand = state.Hand.Count;
            for (var i = 0; i < 24; i++)                            // one loop of the 12 fps illustrations
            {
                GameDriver.Capture("rogue-loot-hand-" + i.ToString("00"));
                yield return GameDriver.Seconds(1f / 12f);
            }
            GameDriver.Key(Key.D2);                                 // pick up the second card: drawn bigger, upright
            yield return GameDriver.Seconds(0.5f);
            GameDriver.Capture("rogue-loot-hand-held");
            GameDriver.Key(Key.Escape);
            yield return GameDriver.Frames(3);
            Assert.That(state.Hand.Count, Is.EqualTo(hand), "nothing was played");
        }
    }
}
