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
    /// <summary>All of the warrior's loot cards (Tools/VfxLab/warrior_pool_art.py .. _4.py) with their
    /// animated illustrations, in the real game: deck viewer (scrolled down to the loot cards) and a combat hand drawn
    /// from them (captures in TestResults/warrior-loot-*.png to inspect by eye).</summary>
    public sealed class WarriorLootCardArtTests
    {
        private static readonly string[] Loot =
        {
            "wr_corte_sangriento", "wr_embate_firme", "wr_escudo_sangre", "wr_golpe_doble", "wr_herida_abierta",
            "wr_muro_acero", "wr_postura_firme", "wr_tajo_brutal", "wr_vendaje",
            "wr_fortaleza_interior", "wr_grito_batalla", "m_grito_de_guerra", "wr_hachazo_temerario", "wr_puno_hierro",
            "wr_respiro", "wr_rugido", "wr_sangre_hirviente", "wr_sangria", "wr_sed_batalla",
            "m_conjuro", "wr_coraza_viva", "a_golpe_pesado", "a_golpe_total", "wr_golpe_escudo", "wr_martillo_sismico",
            "wr_pacto_hierro", "wr_regeneracion", "wr_represalia", "wr_sacrificio", "wr_segunda_piel",
            "wr_torbellino_acero", "e_torre", "wr_transfusion",
            "e_baluarte", "wr_furia_desatada", "a_gran_golpe", "e_gran_muralla", "wr_ira_carmesi", "wr_piel_acero",
            "ep_bastion", "wr_berserker", "wr_doblar_escudo", "ep_ejecucion", "ep_escudo_impenet", "wr_golpe_vampirico",
            "wr_muralla_infranqueable", "wr_voluntad_hierro",
        };

        [UnityTest]
        public IEnumerator TheWarriorLootCardsShowTheAnimatedArt()
        {
            yield return GameDriver.Boot();
            var manager = GameDriver.Manager;
            var art = GameDriver.Game.Context.Art;
            Assert.That(CardArtAnimations.Ids(art), Is.SupersetOf(Loot), "Cards/Anim/anim.json is in Resources");
            var run = new RunState(HeroKind.Warrior, 4243);
            foreach (var id in Loot) run.AddCard(RunContent.Cards.First(c => c.Id == id));
            manager.StartRun(run);
            yield return GameDriver.Until(() => manager.Top is MapScene, 5, "map");
            yield return GameDriver.Frames(3);

            GameDriver.Click(1159, 34);                            // "Mazo": the deck viewer draws cards with CardWidget.Draw
            yield return GameDriver.Seconds(0.6f);
            GameDriver.Capture("warrior-loot-deck");
            GameDriver.Send(new InputEvent(EventType.MouseMove, 640, 400));
            GameDriver.Send(new InputEvent(EventType.Wheel, 640, 400, wheel: -6));   // down to the loot cards
            yield return GameDriver.Seconds(0.6f);
            GameDriver.Capture("warrior-loot-deck-scrolled");
            yield return GameDriver.Seconds(0.33f);
            GameDriver.Capture("warrior-loot-deck-scrolled-later");
            GameDriver.Send(new InputEvent(EventType.Wheel, 640, 400, wheel: -6));   // the last rows
            yield return GameDriver.Seconds(0.6f);
            GameDriver.Capture("warrior-loot-deck-scrolled-more");
            GameDriver.Send(new InputEvent(EventType.Wheel, 640, 400, wheel: -6));   // the newest rows
            yield return GameDriver.Seconds(0.6f);
            GameDriver.Capture("warrior-loot-deck-scrolled-end");
            GameDriver.Send(new InputEvent(EventType.Wheel, 640, 400, wheel: -6));   // the rares
            yield return GameDriver.Seconds(0.6f);
            GameDriver.Capture("warrior-loot-deck-scrolled-last");
            for (var page = 1; page <= 3; page++)                   // the epics and legendaries (warrior_pool_art_4)
            {
                GameDriver.Send(new InputEvent(EventType.Wheel, 640, 400, wheel: -6));
                yield return GameDriver.Seconds(0.6f);
                GameDriver.Capture("warrior-loot-deck-scrolled-rares-" + page);
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
                GameDriver.Capture("warrior-loot-hand-" + i.ToString("00"));
                yield return GameDriver.Seconds(1f / 12f);
            }
            GameDriver.Key(Key.D2);                                 // pick up the second card: drawn bigger, upright
            yield return GameDriver.Seconds(0.5f);
            GameDriver.Capture("warrior-loot-hand-held");
            GameDriver.Key(Key.Escape);
            yield return GameDriver.Frames(3);
            Assert.That(state.Hand.Count, Is.EqualTo(hand), "nothing was played");
        }
    }
}
