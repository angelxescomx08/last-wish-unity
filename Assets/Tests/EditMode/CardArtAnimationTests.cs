using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LastWish.Domain;
using LastWish.Presentation;
using NUnit.Framework;

namespace LastWish.Tests
{
    /// <summary>Animated card illustrations (Cards/Anim, Tools/VfxLab/export_game_card_art.py): metadata and sheets, the
    /// clock, and how CardWidget draws them (animation behind a hollow face, still frame on cached faces), headless.</summary>
    public sealed class CardArtAnimationTests
    {
        private static readonly string[] Rogue = { "punalada_base", "esquiva_base", "finta_base" };
        private static readonly string[] Mage = { "proyectil_base", "egida_base", "chispa_ignea_base" };
        private static readonly string[] Warrior = { "espadazo_base", "bloqueo_base", "guardia_ferrea_base" };
        private static readonly string[] RogueLoot =
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
        private static readonly string[] Starters = Rogue.Concat(Mage).Concat(Warrior).ToArray();
        private static readonly string[] All = Starters.Concat(RogueLoot).ToArray();

        private sealed class Recorder : IRenderBackend
        {
            public readonly List<(string tex, Blend blend, string target, Rgba color, float x0, float y0, float x1, float y1)> Quads =
                new List<(string, Blend, string, Rgba, float, float, float, float)>();
            private readonly Stack<string> targets = new Stack<string>();
            private int next = 1;
            public Tex WhiteTexture { get; } = new Tex(0, "white", 1, 1);
            public Tex LoadTexture(string key) { var (w, h) = Clips.PngSize(key); return new Tex(next++, key, w, h); }
            public Tex CreateTexture(string key, int width, int height, Rgba[] pixels, bool smooth = false) => new Tex(next++, key, width, height, smooth);
            public void BeginFrame() { }
            public void Quad(Tex tex, float u0, float v0, float u1, float v1, float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3, Rgba color, Blend blend)
            {
                var xs = new[] { x0, x1, x2, x3 }; var ys = new[] { y0, y1, y2, y3 };
                Quads.Add((tex?.Key, blend, targets.Count > 0 ? targets.Peek() : null, color, xs.Min(), ys.Min(), xs.Max(), ys.Max()));
            }
            public void Triangle(float x0, float y0, float x1, float y1, float x2, float y2, Rgba color, Blend blend) { }
            public void TexTriangle(Tex tex, float x0, float y0, float u0, float v0, float x1, float y1, float u1, float v1, float x2, float y2, float u2, float v2, Rgba color, Blend blend) { }
            public Tex CreateTarget(string key, int width, int height) => new Tex(next++, key, width, height, false, true);
            public void ReleaseTarget(Tex target) { }
            public void BeginTarget(Tex target) => targets.Push(target.Key);
            public void EndTarget() => targets.Pop();
            public void SetClip(RectF? clip) { }
            public void EndFrame() { }
        }

        private static (GameContext g, Recorder render) Game()
        {
            var render = new Recorder(); var art = new Art(render, new FileAssets()); art.LoadCore();
            return (new GameContext(new Canvas(render), art, new SoundPlayer(null), new Preferences(), null) { Data = new GameData(new FileAssets()) }, render);
        }
        private static CardDefinition Starter(HeroKind hero, string id) => RunContent.StarterDeck(hero).First(c => c.Id == id);
        // a cost nobody else shows, so the static face cache (shared by every test) composes a fresh face
        private static CardShow Fresh(int cost) => new CardShow { Cost = cost };

        // ------------------------------------------------------------------ metadata and sheets

        [Test] public void EveryHeroStarterCardIsAnimated()
        {
            var (g, _) = Game();
            Assert.That(CardArtAnimations.Ids(g.Art), Is.EquivalentTo(All), "the starter cards plus every rogue loot card");
            var deck = RunContent.StarterDeck(HeroKind.Rogue).Select(c => c.Id).Distinct();
            Assert.That(deck, Is.EquivalentTo(Rogue), "every basic rogue card has its animation");
            Assert.That(RunContent.StarterDeck(HeroKind.Mage).Select(c => c.Id).Distinct(), Is.EquivalentTo(Mage),
                "every basic mage card has its animation");
            Assert.That(RunContent.StarterDeck(HeroKind.Warrior).Select(c => c.Id).Distinct(), Is.EquivalentTo(Warrior),
                "every basic warrior card has its animation");
            foreach (var id in All)
            {
                var anim = CardArtAnimations.For(g.Art, id);
                Assert.That((anim.Frames, anim.Fps, anim.Duration), Is.EqualTo((24, 12f, 2f)), id);
                Assert.That(anim.Still, Is.InRange(0, anim.Frames - 1), id);
            }
            Assert.That(CardArtAnimations.For(g.Art, "wr_tajo_brutal"), Is.Null, "loot cards keep their still art for now");
            Assert.That(CardArtAnimations.For(g.Art, null), Is.Null);
            Assert.That(CardArtAnimations.For(null, "punalada_base"), Is.Null);
        }

        [Test] public void EveryFrameAndHaloFitsItsSheet()
        {
            var (g, _) = Game();
            foreach (var id in All)
            {
                var anim = CardArtAnimations.For(g.Art, id);
                foreach (var key in new[] { id, id + "_halo" })
                    Assert.That(File.Exists(Path.Combine(Clips.Root, CardArtAnimations.Folder + key + ".png")), Is.True, key);
                for (var i = 0; i < anim.Frames; i++)
                {
                    foreach (var s in new[] { anim.Frame(i), anim.Halo(i) })
                    {
                        Assert.That(s.X >= 0 && s.Y >= 0 && s.X + s.W <= s.Texture.Width && s.Y + s.H <= s.Texture.Height, Is.True, id + " frame " + i);
                        Assert.That(s.W * 64, Is.EqualTo(s.H * 88), "the 88x64 illustration aspect");
                    }
                    Assert.That((anim.Frame(i).W, anim.Frame(i).H), Is.EqualTo((anim.Halo(i).W * 2, anim.Halo(i).H * 2)), "colour x2, halo native");
                }
                Assert.That(anim.Frame(anim.Frames).X, Is.EqualTo(anim.Frame(0).X), "frames wrap");
                Assert.That(anim.Frame(-1).X, Is.EqualTo(anim.Frame(anim.Frames - 1).X));
                Assert.That(anim.Frame(1).X - anim.Frame(0).X, Is.GreaterThan(anim.Frame(0).W), "a gutter between frames (no bilinear bleed)");
            }
        }

        [Test] public void EveryRogueCardIsAnimated()
        {
            var (g, _) = Game();
            var loot = RunContent.Cards.Where(c => c.ClassId == "rogue").Select(c => c.Id).ToArray();
            var starters = RunContent.StarterDeck(HeroKind.Rogue).Select(c => c.Id).Distinct().ToArray();
            Assert.That(loot.Length + starters.Length, Is.EqualTo(66), "the rogue's 66 cards (63 loot + 3 starters)");
            foreach (var id in loot.Concat(starters))
                Assert.That(CardArtAnimations.For(g.Art, id), Is.Not.Null, id + " has its animated illustration");
            Assert.That(RogueLoot, Is.EquivalentTo(loot), "the list here is every loot card she can find");
        }

        [Test] public void TheRogueLootCardsAreAnimated()
        {
            var (g, _) = Game();
            foreach (var id in RogueLoot)
            {
                var card = RunContent.Cards.FirstOrDefault(c => c.Id == id);
                Assert.That(card, Is.Not.Null, id + " is a real card");
                Assert.That(card.ClassId, Is.EqualTo("rogue"), id + ": a rogue card");
                Assert.That(card.Rarity, Is.InRange(1, 5), id + ": from her loot, common to legendary");
                Assert.That(RunContent.StarterDeck(HeroKind.Rogue).Any(c => c.Id == id), Is.False, id + " is not a starter card");
                var anim = CardArtAnimations.For(g.Art, id);
                Assert.That(anim, Is.Not.Null, id);
                Assert.That((anim.Frames, anim.Fps, anim.Duration), Is.EqualTo((24, 12f, 2f)), id);
                Assert.That(anim.Still, Is.InRange(0, anim.Frames - 1), id);
            }
        }

        // ------------------------------------------------------------------ clock

        [Test] public void TheClockLoopsAndSurvivesAnyTime()
        {
            var (g, _) = Game();
            var anim = CardArtAnimations.For(g.Art, "punalada_base");
            Assert.That(anim.FrameAt(0), Is.EqualTo(0));
            Assert.That(anim.FrameAt(1 / 12.0 + 1e-6), Is.EqualTo(1));
            Assert.That(anim.FrameAt(23 / 12.0 + 1e-6), Is.EqualTo(23));
            Assert.That(anim.FrameAt(2.0 + 1e-6), Is.EqualTo(0), "24 frames at 12 fps loop every 2 s");
            Assert.That(anim.FrameAt(-1 / 12.0 + 1e-6), Is.EqualTo(23), "negative time wraps backwards");
            foreach (var t in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, 1e9, -1e9, 1e18, double.MaxValue, -double.MaxValue })
                Assert.That(anim.FrameAt(t), Is.InRange(0, 23), t.ToString());
            var seen = new HashSet<int>();
            for (var i = 0; i < 1000; i++) seen.Add(anim.FrameAt(i / 120.0));
            Assert.That(seen.Count, Is.EqualTo(24), "every frame is shown");
        }

        [Test] public void PhasesComeFromTheIdAndDiffer()
        {
            var (g1, _) = Game(); var (g2, _) = Game();
            var phases = All.Select(id => CardArtAnimations.For(g1.Art, id).Phase).ToArray();
            Assert.That(All.Select(id => CardArtAnimations.For(g2.Art, id).Phase), Is.EqualTo(phases), "same id, same phase (no jump when a card is rebuilt)");
            Assert.That(phases.Distinct().Count(), Is.EqualTo(All.Length), "different cards do not animate in step");
            Assert.That(phases.All(p => p >= 0 && p < 2f), Is.True);
            Assert.That(CardArtAnimation.StableHash("punalada_base"), Is.EqualTo(CardArtAnimation.StableHash("punalada_base")));
            Assert.That(CardArtAnimation.StableHash(null), Is.EqualTo(CardArtAnimation.StableHash("")));
        }

        // ------------------------------------------------------------------ drawing

        [Test] public void TheAnimationIsDrawnBehindAHollowFace()
        {
            var (g, render) = Game();
            var card = Starter(HeroKind.Rogue, "punalada_base");
            g.Time = 0.5f;
            CardWidget.Draw(g.Canvas, g, card, 100, 300, Fresh(41));
            var screen = render.Quads.Where(q => q.target == null).ToList();
            var art = screen.FindIndex(q => q.tex == "Cards/Anim/punalada_base");
            var halo = screen.FindIndex(q => q.tex == "Cards/Anim/punalada_base_halo");
            var face = screen.FindIndex(q => q.tex != null && q.tex.StartsWith("card_face_"));
            Assert.That(art, Is.GreaterThanOrEqualTo(0), "the frame is drawn");
            Assert.That((screen[art].blend, screen[halo].blend), Is.EqualTo((Blend.Alpha, Blend.Add)));
            Assert.That(art < halo && halo < face, Is.True, "art, light, then the face over them");
            var composed = render.Quads.Where(q => q.target != null).ToList();
            Assert.That(composed, Is.Not.Empty, "a fresh face was composed");
            Assert.That(composed.Any(q => q.tex != null && (q.tex.StartsWith("Cards/Anim/") || q.tex.StartsWith("Cards/Art/"))), Is.False,
                "the hollow face leaves the art window empty");
        }

        [Test] public void TheMageAndWarriorCardsAreDrawnBehindAHollowFaceToo()
        {
            foreach (var id in Mage.Concat(Warrior))
            {
                var (g, render) = Game();
                g.Time = 0.75f;
                var hero = Mage.Contains(id) ? HeroKind.Mage : HeroKind.Warrior;
                var cost = 47 + Array.IndexOf(Mage.Concat(Warrior).ToArray(), id);   // 47..52: a fresh face per card
                CardWidget.DrawAt(g.Canvas, g, Starter(hero, id), 640, 600, Fresh(cost), 1.3f, 6f);
                var screen = render.Quads.Where(q => q.target == null).ToList();
                var art = screen.FindIndex(q => q.tex == "Cards/Anim/" + id);
                var halo = screen.FindIndex(q => q.tex == "Cards/Anim/" + id + "_halo");
                var face = screen.FindIndex(q => q.tex != null && q.tex.StartsWith("card_face_"));
                Assert.That(art >= 0 && art < halo && halo < face, Is.True, id + ": art, light, then the face over them");
                Assert.That(render.Quads.Any(q => q.tex == "Cards/Art/" + id), Is.False, id + ": the old still is not used");
            }
        }

        [Test] public void TheRogueLootCardsAreDrawnBehindAHollowFaceToo()
        {
            for (var i = 0; i < RogueLoot.Length; i++)
            {
                var id = RogueLoot[i];
                var (g, render) = Game();
                g.Time = 1.25f;
                CardWidget.DrawAt(g.Canvas, g, RunContent.Cards.First(c => c.Id == id), 640, 600, Fresh(61 + i), 1.3f, -4f);   // 61..123
                var screen = render.Quads.Where(q => q.target == null).ToList();
                var art = screen.FindIndex(q => q.tex == "Cards/Anim/" + id);
                var halo = screen.FindIndex(q => q.tex == "Cards/Anim/" + id + "_halo");
                var face = screen.FindIndex(q => q.tex != null && q.tex.StartsWith("card_face_"));
                Assert.That(art >= 0 && art < halo && halo < face, Is.True, id + ": art, light, then the face over them");
                Assert.That(render.Quads.Any(q => q.tex == "Cards/Art/" + id), Is.False, id + ": the old still is not used");
            }
        }

        [Test] public void TiltedAndScaledHandCardsAnimateToo()
        {
            var (g, render) = Game();
            var card = Starter(HeroKind.Rogue, "finta_base");
            CardWidget.DrawAt(g.Canvas, g, card, 640, 600, Fresh(42), 1.3f, -8f);
            var screen = render.Quads.Where(q => q.target == null).ToList();
            var art = screen.FindIndex(q => q.tex == "Cards/Anim/finta_base");
            var face = screen.FindIndex(q => q.tex != null && q.tex.StartsWith("card_face_"));
            Assert.That(art >= 0 && art < face, Is.True);
        }

        [Test] public void CachedFacesShowTheStillFrame()
        {
            var (g, render) = Game();
            var card = Starter(HeroKind.Rogue, "esquiva_base");
            CardWidget.Face(g, card, Fresh(43));
            var composed = render.Quads.Where(q => q.target != null).ToList();
            var anim = CardArtAnimations.For(g.Art, "esquiva_base");
            Assert.That(composed.Count(q => q.tex == "Cards/Anim/esquiva_base"), Is.EqualTo(1), "the deal flight and pack reveal show the new art");
            Assert.That(composed.Any(q => q.tex == "Cards/Art/esquiva_base"), Is.False, "the old still is not used");
            Assert.That(composed.Any(q => q.blend == Blend.Add), Is.False, "no live light inside a cached face");
            Assert.That(anim.Frame(anim.Still).Texture.Key, Is.EqualTo("Cards/Anim/esquiva_base"));
        }

        [Test] public void OtherCardsAreUnchanged()
        {
            var (g, render) = Game();
            var card = RunContent.Cards.First(c => c.Id == "wr_tajo_brutal");
            CardWidget.Draw(g.Canvas, g, card, 100, 300, Fresh(44));
            Assert.That(render.Quads.Any(q => q.tex != null && q.tex.StartsWith("Cards/Anim/")), Is.False);
            Assert.That(render.Quads.Any(q => q.target != null && q.tex == "Cards/Art/wr_tajo_brutal"), Is.EqualTo(g.Art.HasCardArt("wr_tajo_brutal")));
        }

        [Test] public void UnaffordableAndFadingCardsDimTheLight()
        {
            Rgba HaloColor(bool unaffordable, float alpha)
            {
                var (g, render) = Game();
                g.Canvas.PushAlpha(alpha);
                CardWidget.Draw(g.Canvas, g, Starter(HeroKind.Rogue, "punalada_base"), 100, 300, new CardShow { Cost = 45, Unaffordable = unaffordable });
                g.Canvas.PopAlpha();
                var halo = render.Quads.Where(q => q.tex == "Cards/Anim/punalada_base_halo").ToList();
                return halo.Count == 0 ? Rgba.Black : halo[0].color;
            }
            var normal = HaloColor(false, 1f); var dim = HaloColor(true, 1f); var faded = HaloColor(false, 0f);
            Assert.That(normal.R, Is.EqualTo(MathX.Round(255 * CardArtAnimation.HaloGain)));
            Assert.That(dim.R, Is.LessThan(normal.R / 2), "an unplayable card glows much less");
            Assert.That(faded, Is.EqualTo(Rgba.Black), "additive light follows the canvas alpha (it ignores it otherwise)");
        }

        [Test] public void ParticlesStayInsideTheArtWindowAtEveryMoment()
        {
            var (g, render) = Game();
            var card = Starter(HeroKind.Rogue, "esquiva_base");
            var z = g.Art.CardArtZone(card.Rarity);
            float x0 = MathX.Round(z.x0 * CardWidget.CardW) + 100, y0 = MathX.Round(z.y0 * CardWidget.CardH) + 300;
            float x1 = x0 + MathX.Round((z.x1 - z.x0) * CardWidget.CardW), y1 = y0 + MathX.Round((z.y1 - z.y0) * CardWidget.CardH);
            var counts = new List<int>();
            for (var i = 0; i < 2000; i++)
            {
                render.Quads.Clear();
                g.Time = i * 0.017f;
                CardWidget.Draw(g.Canvas, g, card, 100, 300, Fresh(46));
                var sparks = render.Quads.Where(q => q.target == null && q.tex == "white" && q.blend == Blend.Add).ToList();
                counts.Add(sparks.Count);
                foreach (var q in sparks)
                    Assert.That(q.x0 >= x0 && q.y0 >= y0 && q.x1 <= x1 && q.y1 <= y1, Is.True, $"t={g.Time}: ({q.x0},{q.y0})-({q.x1},{q.y1}) outside the window");
            }
            Assert.That(counts.Max(), Is.InRange(1, 13), "a few twinkles and motes, never a flood");
            Assert.That(counts.Distinct().Count(), Is.GreaterThan(3), "they come and go");
        }
    }
}
