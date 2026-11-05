using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LastWish.Domain;
using LastWish.Presentation;
using NUnit.Framework;

namespace LastWish.Tests
{
    /// <summary>
    /// User, 2026-11-04: "cuando se hace hover en una carta como que el texto cambia […] coste, texto, daño" and "que se vea
    /// mejor el texto en la explicación y agregar la calidad de la carta, legendaria o así". The hand used to lay a hovered
    /// card out again at 1.3× (other pixel-font sizes, other line breaks, a tiny name); now every card of the hand area is
    /// laid out at 1.0 and only resized (rendered at ×2 when shown bigger). The card tooltip is opaque, its title and frame
    /// take the rarity colour and chips say the rarity, type and cost; rewards and packs show the rarity under each card.
    /// </summary>
    public sealed class CardHoverAndTooltipTests
    {
        private const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        [SetUp] public void ResetTuning() => Tuning.Reset();
        [TearDown] public void RestoreTuning() => Tuning.Reset();

        private sealed class Recorder : IRenderBackend
        {
            public struct Q { public string Tex, Target; public int TexW, TexH; public float U0, V0, U1, V1, X0, Y0, X1, Y1, Edge; public Rgba Color; public Blend Blend; }
            public readonly List<Q> Quads = new List<Q>();
            public readonly List<(string key, int w, int h)> Targets = new List<(string, int, int)>();
            private readonly Stack<string> targets = new Stack<string>();
            private int next = 1;
            public Tex WhiteTexture { get; } = new Tex(0, "white", 1, 1);
            public Tex LoadTexture(string key) { var (w, h) = Clips.PngSize(key); return new Tex(next++, key, w, h); }
            public Tex CreateTexture(string key, int width, int height, Rgba[] pixels, bool smooth = false) => new Tex(next++, key, width, height, smooth);
            public void BeginFrame() { }
            public void Quad(Tex tex, float u0, float v0, float u1, float v1, float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3, Rgba color, Blend blend)
            {
                var xs = new[] { x0, x1, x2, x3 }; var ys = new[] { y0, y1, y2, y3 };
                Quads.Add(new Q { Tex = tex?.Key, TexW = tex?.Width ?? 0, TexH = tex?.Height ?? 0, Target = targets.Count > 0 ? targets.Peek() : null, U0 = u0, V0 = v0, U1 = u1, V1 = v1,
                    X0 = xs.Min(), Y0 = ys.Min(), X1 = xs.Max(), Y1 = ys.Max(), Edge = (float)Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0)), Color = color, Blend = blend });
            }
            public void Triangle(float x0, float y0, float x1, float y1, float x2, float y2, Rgba color, Blend blend) { }
            public void TexTriangle(Tex tex, float x0, float y0, float u0, float v0, float x1, float y1, float u1, float v1, float x2, float y2, float u2, float v2, Rgba color, Blend blend) { }
            public Tex CreateTarget(string key, int width, int height) { Targets.Add((key, width, height)); return new Tex(next++, key, width, height, false, true); }
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

        private static IEnumerable<CardDefinition> EveryCard()
        {
            foreach (var c in RunContent.Cards) yield return c;
            foreach (var hero in new[] { HeroKind.Warrior, HeroKind.Mage, HeroKind.Rogue })
                foreach (var c in RunContent.StarterDeck(hero).GroupBy(c => c.Id).Select(g => g.First())) yield return c;
        }

        // ------------------------------------------------------------------ the face never changes when a card grows

        [Test] public void AFaceRenderedTwiceAsLargeHasExactlyTheSameLayout()
        {
            var (g, render) = Game();
            var cost = 3000;     // a cost nobody else shows: the shared face cache composes fresh faces
            foreach (var card in EveryCard())
            {
                cost++;
                var show = new CardShow { Cost = cost, BonusDamage = 2, BonusBlock = 1 };
                render.Quads.Clear();
                g.Canvas.BeginFrame();
                var one = CardWidget.Face(g, card, show, CardWidget.CardW, CardWidget.CardH, 1);
                var two = CardWidget.Face(g, card, show, CardWidget.CardW, CardWidget.CardH, 2);
                g.Canvas.EndFrame();
                Assert.That((one.W, one.H), Is.EqualTo((CardWidget.CardW, CardWidget.CardH)), card.Id);
                Assert.That((two.W, two.H), Is.EqualTo((CardWidget.CardW * 2, CardWidget.CardH * 2)), card.Id + ": ×2 texels");
                var a = render.Quads.Where(q => q.Target == one.Texture.Key).ToList();
                var b = render.Quads.Where(q => q.Target == two.Texture.Key).ToList();
                Assert.That(a.Count, Is.GreaterThan(5), card.Id);
                Assert.That(b.Count, Is.EqualTo(a.Count), card.Id + ": same pieces (no other fonts or line breaks)");
                for (var i = 0; i < a.Count; i++)
                {
                    var (p, q) = (a[i], b[i]);
                    Assert.That((q.Tex, q.U0, q.V0, q.U1, q.V1, q.Color, q.Blend), Is.EqualTo((p.Tex, p.U0, p.V0, p.U1, p.V1, p.Color, p.Blend)), card.Id + " piece " + i);
                    Assert.That(new[] { q.X0, q.Y0, q.X1, q.Y1 }, Is.EqualTo(new[] { p.X0 * 2, p.Y0 * 2, p.X1 * 2, p.Y1 * 2 }).Within(0.001f), card.Id + " piece " + i + " at twice the place");
                }
            }
        }

        [TestCase(140, 140, 1)] [TestCase(140, 98, 1)] [TestCase(140, 141, 2)] [TestCase(140, 154, 2)] [TestCase(140, 182, 2)] [TestCase(140, 280, 2)] [TestCase(140, 100000, 2)]
        public void BiggerThanItsLayoutIsRenderedAtTwice(int layoutW, int shownW, int res) => Assert.That(CardWidget.ResFor(layoutW, shownW), Is.EqualTo(res));

        [Test] public void DrawAtResizesTheFaceItWasToldToLayOut()
        {
            var (g, render) = Game();
            var card = RunContent.Cards.First(c => c.Id == "wr_tajo_brutal");
            foreach (var (scale, w, tw) in new[] { (1f, 140, 140), (1.1f, 154, 280), (1.3f, 182, 280), (0.6f, 84, 140), (2f, 280, 280) })
            {
                render.Quads.Clear();
                g.Canvas.BeginFrame();
                CardWidget.DrawAt(g.Canvas, g, card, 640, 360, new CardShow { Cost = 4100 }, scale, 0f, null, 1f);
                g.Canvas.EndFrame();
                var face = render.Quads.Last(q => q.Target == null && q.Tex != null && q.Tex.StartsWith("card_face_", StringComparison.Ordinal));
                Assert.That(face.X1 - face.X0, Is.EqualTo(w).Within(0.01f), "shown at " + scale);
                Assert.That((face.TexW, face.TexH), Is.EqualTo((tw, tw * CardWidget.CardH / CardWidget.CardW)), "laid out at 1.0, ×2 when bigger: " + scale);
            }
        }

        private static CombatScene Fight(GameContext g, HeroKind hero = HeroKind.Warrior)
        {
            var run = new RunState(hero, 7, EnemyRoster.Expanded);
            var node = run.Map.Nodes.First(n => n.Kind == RoomKind.Combat);
            var state = run.CreateCombat(run.GenerateEnemies(node.Id)); state.Invincible = true;
            var scene = new CombatScene(g, state, run.OwnedRelics);
            for (var i = 0; i < 240; i++) Frame(g, scene, i % 20 == 0);
            return scene;
        }

        private static void Frame(GameContext g, Scene scene, bool draw = true, float dt = 1f / 60f)
        {
            g.Time += dt; scene.Update(dt);
            if (draw) { g.Canvas.BeginFrame(); scene.Draw(g.Canvas); g.Canvas.EndFrame(); }
        }

        /// <summary>Card faces drawn on the screen this frame: (shown width, texture width, texture height).</summary>
        private static List<(float shown, int texW, int texH)> FacesOnScreen(Recorder render) =>
            render.Quads.Where(q => q.Target == null && q.Tex != null && q.Tex.StartsWith("card_face_", StringComparison.Ordinal) && q.Blend == Blend.Alpha)
                .Select(q => (q.Edge, q.TexW, q.TexH)).ToList();     // top edge: the width even when tilted

        [Test] public void HoveringAimingAndPlayingACardNeverLaysItsFaceOutAgain()
        {
            var (g, render) = Game();
            var scene = Fight(g);
            var rest = new HashSet<(int, int)> { (CardWidget.CardW, CardWidget.CardH), (CardWidget.CardW * 2, CardWidget.CardH * 2) };
            void Check(string when)
            {
                render.Quads.Clear(); Frame(g, scene);
                var faces = FacesOnScreen(render);
                Assert.That(faces, Is.Not.Empty, when);
                foreach (var (shown, w, h) in faces)
                {
                    Assert.That(rest.Contains((w, h)), Is.True, when + ": a face laid out at " + w + "×" + h + " (only 1.0, rendered ×1 or ×2)");
                    if (shown > CardWidget.CardW + 1) Assert.That(w, Is.EqualTo(CardWidget.CardW * 2), when + ": a bigger card uses the sharp ×2 render");
                }
            }
            Check("at rest");
            var hand = scene.State.Hand.Count;
            for (var k = 0; k < hand; k++)
            {
                var rect = ((List<IRect>)typeof(CombatScene).GetField("cardRects", F).GetValue(scene))[k];
                scene.HandleEvent(new InputEvent(EventType.MouseMove, rect.CenterX, rect.Y + 40));
                for (var i = 0; i < 30; i++) Frame(g, scene, false);
                Check("hovering card " + k);
                var hovered = FacesOnScreen(render).Max(f => f.shown);
                Assert.That(hovered, Is.EqualTo(MathX.Round(CardWidget.CardW * 1.3f)).Within(1f), "the hovered card still grows to 1.3×");
            }
            // pick a card up and aim it at an enemy (1.1×), then cancel
            scene.HandleEvent(new InputEvent(EventType.MouseMove, 640, 650));
            for (var i = 0; i < 20; i++) Frame(g, scene, false);
            scene.HandleEvent(new InputEvent(EventType.MouseDown, 640, 650, 1));
            for (var k = 0; k < 12; k++) { scene.HandleEvent(new InputEvent(EventType.MouseMove, 640 + k * 25, 650 - k * 40)); Frame(g, scene, false); }
            for (var i = 0; i < 20; i++) Frame(g, scene, false);
            Check("aiming");
            scene.HandleEvent(new InputEvent(EventType.MouseDown, 900, 200, 3));
            for (var i = 0; i < 40; i++) Frame(g, scene, i % 4 == 0);
            Check("after cancelling");
        }

        [Test] public void AHoveredCardShowsTheSameCostTextAndDamageAsAtRest()
        {
            // what the user saw: the hovered copy used other fonts. Glyph pieces of the hovered face = the resting face's.
            var (g, render) = Game();
            var card = RunContent.Cards.First(c => c.Id == "wr_torbellino_acero");
            var show = new CardShow { Cost = 4200, BonusDamage = 3 };
            g.Canvas.BeginFrame();
            CardWidget.DrawAt(g.Canvas, g, card, 400, 400, show, 1f, 0f, null, 1f);
            CardWidget.DrawAt(g.Canvas, g, card, 800, 400, show, 1.3f, 0f, null, 1f);
            g.Canvas.EndFrame();
            var faces = render.Quads.Where(q => q.Target == null && q.Tex != null && q.Tex.StartsWith("card_face_", StringComparison.Ordinal)).Select(q => (q.Tex, q.TexW)).Distinct().ToList();
            Assert.That(faces.Count, Is.EqualTo(2));
            var glyphs = faces.Select(f => render.Quads.Where(q => q.Target == f.Tex && q.Tex != null && q.Tex.StartsWith("Fonts/", StringComparison.Ordinal))
                .Select(q => (q.Tex, q.U0, q.V0, q.Color)).ToList()).ToList();
            Assert.That(glyphs[0], Is.Not.Empty);
            Assert.That(glyphs[1], Is.EqualTo(glyphs[0]), "same glyphs, same fonts, same colours");
        }

        // ------------------------------------------------------------------ the card tooltip

        [Test] public void EveryCardTooltipSaysItsRarityTypeAndCostOnOneRow()
        {
            var (g, _) = Game();
            foreach (var card in EveryCard().Concat(new[] { "status_espora", "status_moho", "status_herida" }.Select(CombatState.StatusCard)))
            {
                var tip = Tooltip.Card(g, card, 0);
                var rarity = Math.Max(1, Math.Min(5, card.Rarity));
                Assert.That(tip.Chips.Count, Is.EqualTo(3), card.Id);
                Assert.That((tip.Chips[0].Text, tip.Chips[0].Gem, tip.Chips[0].Color), Is.EqualTo((RunContent.RarityLabel(rarity), true, Tooltip.RarityInk(rarity))), card.Id + ": rarity first");
                Assert.That(tip.Chips[2].Text, Does.StartWith("Coste ").Or.EqualTo("Injugable"), card.Id);
                Assert.That((tip.Accent, tip.Bold), Is.EqualTo(((Rgba?)Tooltip.RarityInk(rarity), true)), card.Id + ": title and frame in the rarity colour");
                Assert.That(tip.Tag, Is.EqualTo(RunContent.ClassLabel(card.ClassId ?? "neutral")), card.Id + ": the class on the title row");
                Assert.That(tip.AllText(), Does.Contain(RunContent.RarityLabel(rarity)), card.Id);
                var chips = Tooltip.ChipRects(g, tip);
                Assert.That(chips.Select(r => r.Y).Distinct().Count(), Is.EqualTo(1), card.Id + ": one row");
                Assert.That(chips.Last().Right, Is.LessThanOrEqualTo(Tooltip.MainW - Tooltip.Pad), card.Id + ": inside the box");
                for (var i = 0; i < chips.Count; i++)
                    Assert.That(chips[i].W, Is.EqualTo(Tooltip.ChipWidth(g.Art, tip.Chips[i])), card.Id + ": never squeezed");
            }
        }

        [Test] public void TheFiveRaritiesHaveFiveColours()
        {
            var colours = Enumerable.Range(1, 5).Select(Tooltip.RarityInk).ToList();
            Assert.That(colours.Distinct().Count(), Is.EqualTo(5));
            Assert.That(Tooltip.RarityInk(0), Is.EqualTo(Tooltip.RarityInk(1)));
            Assert.That(Tooltip.RarityInk(99), Is.EqualTo(Tooltip.RarityInk(5)));
            Assert.That(Enumerable.Range(1, 5).Select(r => Tooltip.RarityChip(r).Text), Is.EqualTo(new[] { "Común", "Poco común", "Rara", "Épica", "Legendaria" }));
        }

        [Test] public void TooltipsAreOpaqueSoNothingShowsThroughTheirText()
        {
            var (g, render) = Game();
            foreach (var id in new[] { "wr_tajo_brutal", "wr_muralla_infranqueable", "r_danza_de_sombras", "mg_bola_de_fuego" })
            {
                var card = EveryCard().FirstOrDefault(c => c.Id == id) ?? RunContent.Cards.First();
                render.Quads.Clear();
                g.Canvas.BeginFrame();
                Tooltip.DrawAt(g.Canvas, g, Tooltip.Card(g, card.WithChroma(ChromaKind.Golden), 4, 2, uint.MaxValue), 20, 20);
                g.Canvas.EndFrame();
                Assert.That(render.Quads, Is.Not.Empty);
                Assert.That(render.Quads.Where(q => q.Blend == Blend.Alpha).All(q => q.Color.A == 255), Is.True, id + ": every box, chip and letter opaque");
            }
        }

        [Test] public void TheWarlockAndThePurgeKeepTheRarityChips()
        {
            var (g, _) = Game();
            var tip = Tooltip.Card(g, RunContent.Cards.First(c => c.Rarity == 5), 0);
            var copy = tip.WithLines(new List<string> { "Precio: 75 de oro", "" });
            Assert.That((copy.Title, copy.Icon, copy.Tag, copy.Accent, copy.Bold, copy.Subtitle), Is.EqualTo((tip.Title, tip.Icon, tip.Tag, tip.Accent, tip.Bold, tip.Subtitle)));
            Assert.That(copy.Chips, Is.EqualTo(tip.Chips));
            Assert.That(copy.Panels, Is.SameAs(tip.Panels));
            Assert.That(copy.Lines[0], Is.EqualTo("Precio: 75 de oro"));
        }

        // ------------------------------------------------------------------ the rarity under the cards that are offered

        private static List<OfferedCard> OneOfEach() =>
            Enumerable.Range(1, 5).Select(r => new OfferedCard(RunContent.Cards.First(c => c.Rarity == r).Copy())).ToList();

        [Test] public void TheCombatRewardShowsTheRarityUnderEveryCard()
        {
            var (g, _) = Game();
            var run = new RunState(HeroKind.Warrior, 5, EnemyRoster.Expanded);
            foreach (var withRelic in new[] { false, true })
            {
                var offers = OneOfEach().Skip(2).ToList();
                var relic = withRelic ? new RelicOffer(RunContent.Relics.First(), ChromaKind.None) : null;
                var scene = new CombatRewardScene(g, run, 30, offers, relic);
                Frame(g, scene);
                Assert.That(scene.RarityTags.Count, Is.EqualTo(offers.Count));
                for (var i = 0; i < offers.Count; i++)
                {
                    var (tag, card) = (scene.RarityTags[i], scene.CardRects[i]);
                    Assert.That(tag.CenterX, Is.EqualTo(card.CenterX).Within(1f), "under its card");
                    Assert.That(tag.Y, Is.GreaterThan(card.Bottom), "below the card");
                    Assert.That(tag.W, Is.LessThanOrEqualTo(card.W));
                    Assert.That(Overlaps(tag, scene.SkipRect.Value), Is.False, "clear of Omitir");
                }
                scene.HandleEvent(new InputEvent(EventType.MouseMove, scene.CardRects[1].CenterX, scene.CardRects[1].CenterY));
                Frame(g, scene);
                Assert.That(scene.RarityTags[1].Y, Is.GreaterThan(scene.CardRects[1].Bottom + 20), "the tag stays put while its card lifts");
            }
        }

        [Test] public void APackShowsTheRarityUnderEveryCard()
        {
            var (g, _) = Game();
            var scene = new PackOpeningScene(g, OneOfEach(), "Sobre Épico", PackTheme.Epico, 3, reroll: OneOfEach);
            scene.SkipAnimation();
            for (var i = 0; i < 3; i++) Frame(g, scene);
            Assert.That(scene.Phase, Is.EqualTo("pick"));
            Assert.That(scene.RarityTags.Count, Is.EqualTo(5));
            for (var i = 0; i < 5; i++)
            {
                var (tag, card) = (scene.RarityTags[i], scene.CardRects[i]);
                Assert.That(tag.CenterX, Is.EqualTo(card.CenterX).Within(1f));
                Assert.That(tag.Y, Is.GreaterThan(card.Bottom));
                Assert.That(tag.W, Is.LessThanOrEqualTo(card.W));
                Assert.That(Overlaps(tag, scene.SkipRect.Value) || Overlaps(tag, scene.RerollRect.Value), Is.False, "clear of the buttons");
            }
            Assert.That(Overlaps(scene.SkipRect.Value, scene.RerollRect.Value), Is.False);
        }

        private static bool Overlaps(RectF a, RectF b) => a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom;
    }
}
