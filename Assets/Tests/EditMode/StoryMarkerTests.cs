using System.Collections.Generic;
using System.Linq;
using LastWish.Domain;
using LastWish.Presentation;
using NUnit.Framework;

namespace LastWish.Tests
{
    /// <summary>Plays an event choosing the options that leave its story open (the safe ones, with gold to spare).</summary>
    internal static class StoryKit
    {
        public static void Follow(RunState run, string room, string id)
        {
            run.GainGold(999);
            var session = run.OpenEvent(room, id);
            for (var guard = 0; guard < 8 && !session.Completed; guard++)
            {
                var choices = session.Stage.Choices;
                var open = Enumerable.Range(0, choices.Count).Where(i => session.BlockReason(i).Length == 0).ToList();
                var pick = open.Where(i => choices[i].NextEventId.Length > 0 && choices[i].SuccessPercent == 100).DefaultIfEmpty(-1).First();
                if (pick < 0) pick = open.Where(i => choices[i].NextStage >= 0).DefaultIfEmpty(open.First()).First();
                session.Choose(pick);
            }
        }
    }

    /// <summary>
    /// Open stories on the map (user, 2026-11-04: "las historias que tienen continuación que tengan una forma visual de ver que
    /// la tienes pendiente"): a "Historias" panel left of the plan and a scroll over every event room where the next chapter
    /// can appear; the event result says where it continues.
    /// </summary>
    public sealed class StoryMarkerTests
    {
        [SetUp] public void ResetTuning() => Tuning.Reset();
        [TearDown] public void RestoreTuning() => Tuning.Reset();

        private static GameContext game;
        private static GameContext Game()
        {
            if (game != null) return game;
            var render = new NullRender(); var art = new Art(render, new FileAssets()); art.LoadCore();
            return game = new GameContext(new Canvas(render), art, new SoundPlayer(null), new Preferences(), null) { Data = new GameData(new FileAssets()) };
        }
        private static void Draw(Scene scene) { var g = Game(); g.Canvas.BeginFrame(); scene.Draw(g.Canvas); g.Canvas.EndFrame(); }
        private static RunState Run() => new RunState(HeroKind.Rogue, 42, EnemyRoster.Expanded);
        private static List<MapRoom> EventRooms(RunState run) => run.Map.Nodes.Where(n => n.Kind == RoomKind.Event).ToList();

        [Test] public void WithoutOpenStoriesTheMapShowsNoPanelAndNoScroll()
        {
            var run = Run(); var map = new MapScene(Game(), run); Draw(map);
            Assert.That(map.StoryRows(out _, out var hidden), Is.Empty); Assert.That(hidden, Is.Zero);
            Assert.That(EventRooms(run), Is.Not.Empty);
            Assert.That(EventRooms(run).Any(map.ShowsStory), Is.False);
        }

        [Test] public void AStoryForALaterFloorIsListedButMarksNoRoom()
        {
            var run = Run();
            run.OpenEvent("start", "lost_lantern").Choose(0);
            var map = new MapScene(Game(), run); Draw(map);
            var rows = map.StoryRows(out var panel, out _);
            Assert.That(rows.Count, Is.EqualTo(1));
            Assert.That(rows[0].Name, Is.EqualTo(new[] { "La linterna extraviada" }));
            Assert.That(rows[0].Chapter, Is.EqualTo("Capítulo 2 de 3"));
            Assert.That(rows[0].Where, Is.EqualTo(new[] { "Desde el piso 2" }));
            Assert.That(rows[0].Next, Is.False);
            Assert.That(EventRooms(run).Any(map.ShowsStory), Is.False, "nothing can continue on this floor");
        }

        [Test] public void OnItsFloorEveryUnvisitedEventRoomShowsTheScrollAndSaysWhy()
        {
            var run = Run();
            run.OpenEvent("start", "lost_lantern").Choose(0);
            run.AdvanceFloor();
            var map = new MapScene(Game(), run); Draw(map);
            var rooms = EventRooms(run);
            Assert.That(rooms.All(map.ShowsStory), Is.True);
            var row = map.StoryRows(out _, out _).Single();
            Assert.That(row.Next, Is.True); Assert.That(row.Where, Is.EqualTo(new[] { "En el próximo evento" }));
            // the scroll sits on the room's icon, inside the plan
            foreach (var r in map.Plan.Rooms.Where(r => r.Node.Kind == RoomKind.Event))
            {
                var (x, y) = map.StoryMarkerAt(r); var rect = map.RoomRect(r.Node.Id).Value;
                Assert.That(x, Is.InRange(rect.X - 8, rect.Right + 8)); Assert.That(y, Is.InRange(rect.Y - 8, rect.Bottom + 8));
            }
            // a visited event room no longer promises anything
            typeof(MapRoom).GetProperty("Visited").SetValue(rooms[0], true);
            Draw(map);
            Assert.That(map.ShowsStory(rooms[0]), Is.False);
            // hovering an event room explains the scroll
            var other = map.RoomRect(rooms[1].Id).Value;
            map.HandleEvent(new InputEvent(EventType.MouseMove, other.CenterX, other.CenterY));
            Assert.DoesNotThrow(() => Draw(map));
            // and once the chapter opens, the scroll goes away
            run.OpenEvent(rooms[1].Id);
            Draw(map);
            Assert.That(rooms.Any(map.ShowsStory), Is.False);
            Assert.That(map.StoryRows(out _, out _), Is.Empty);
        }

        [Test] public void ManyOpenStoriesStayInsideThePanelAndTheRestAreCounted()
        {
            var run = Run();
            var starts = EventCatalog.All.Where(e => e.RequiredFlag.Length == 0 && e.MinFloor <= 1 && e.Stages.SelectMany(s => s.Choices).Any(c => c.NextEventId.Length > 0)).ToList();
            Assert.That(starts.Count, Is.GreaterThan(8));
            foreach (var e in starts) StoryKit.Follow(run, "r_" + e.Id, e.Id);
            Assert.That(run.PendingStories.Count, Is.EqualTo(starts.Count));
            var map = new MapScene(Game(), run); Draw(map);
            var rows = map.StoryRows(out var panel, out var hidden);
            Assert.That(rows.Count + hidden, Is.EqualTo(starts.Count));
            Assert.That(hidden, Is.GreaterThan(0), "a long list ends with \"… y N más\"");
            Assert.That(panel.Bottom, Is.LessThanOrEqualTo(MapScene.AreaTop + MapScene.AreaH));
            Assert.That(panel.Right, Is.LessThan(map.Origin.x - MapScene.FramePad), "the panel never covers the plan");
            var big = Game().Fonts.Get(16); var small = Game().Fonts.Get(11);
            foreach (var row in rows)
            {
                Assert.That(row.Rect.Bottom, Is.LessThanOrEqualTo(panel.Bottom));
                foreach (var line in row.Name) Assert.That(big.Width(line), Is.LessThanOrEqualTo(MapScene.StoryTextW), line);
                foreach (var line in row.Where.Append(row.Chapter)) Assert.That(small.Width(line), Is.LessThanOrEqualTo(MapScene.StoryTextW), line);
                Assert.That(string.Join(" ", row.Name).TrimEnd('…'), Is.Not.Empty);
            }
            // every story name fits whole in two lines
            foreach (var e in starts)
                Assert.That(TextFit.Wrap(big, e.Name, MapScene.StoryTextW).Count, Is.LessThanOrEqualTo(2), e.Name);
            run.AdvanceFloor();
            Assert.DoesNotThrow(() => Draw(map));
        }

        [Test] public void TheEventResultSaysWhereTheStoryContinues()
        {
            var run = Run();
            run.OpenEvent("room", "lost_lantern");
            var scene = new EventScene(Game(), run, "room");
            scene.HandleEvent(new InputEvent(EventType.KeyDown, key: Key.D1));
            Assert.That(scene.Session.Completed, Is.True);
            Assert.That(scene.Layout().ResultPieces.Select(p => p.Chip.Text), Has.Some.EqualTo("La historia continuará en el piso 2"));

            var later = Run(); later.AdvanceFloor(); later.AdvanceFloor();
            later.OpenEvent("room", "smiling_lender");
            var lender = new EventScene(Game(), later, "room");
            var pick = lender.Session.Stage.Choices.ToList().FindIndex(c => c.NextEventId.Length > 0 && c.SuccessPercent == 100);
            Assert.That(pick, Is.GreaterThanOrEqualTo(0));
            later.GainGold(999);
            lender.HandleEvent(new InputEvent(EventType.KeyDown, key: Key.D1 + pick));
            Assert.That(lender.Session.Completed, Is.True);
            Assert.That(lender.Layout().ResultPieces.Select(p => p.Chip.Text), Has.Some.EqualTo("La historia continuará en otra sala de evento"));
        }
    }
}
