using System.Collections.Generic;
using System.Linq;
using LastWish.Domain;
using NUnit.Framework;
using static LastWish.Tests.FixtureJson;

namespace LastWish.Tests
{
    /// <summary>Compares run generation with what the original Python game produced (Tools/ExportRunOracle.py).</summary>
    public sealed class RunOracleTests
    {
        [SetUp] public void ResetTuning() => Tuning.Reset();
        [TearDown] public void RestoreTuning() => Tuning.Reset();

        private static string ChromaId(ChromaKind chroma) => chroma == ChromaKind.Golden ? "golden" : chroma == ChromaKind.Diamond ? "diamond" : "";
        private static List<string> Cards(IEnumerable<OfferedCard> cards) => cards.Select(c => c.Card.Id + "|" + ChromaId(c.Card.Chroma) + "|" + c.Lucky).ToList();
        private static List<string> ExpectedCards(object raw) => Arr(raw).Select(o => { var a = Arr(o); return Str(a[0]) + "|" + Str(a[1]) + "|" + ((bool)a[2]); }).ToList();
        private static List<string> Relics(IEnumerable<RelicOffer> relics) => relics.Select(r => r.Definition.Tag + "|" + ChromaId(r.Chroma)).ToList();
        private static List<string> ExpectedRelics(object raw) => Arr(raw).Select(o => { var a = Arr(o); return Str(a[0]) + "|" + Str(a[1]); }).ToList();
        private static List<string> Enemies(IEnumerable<EnemyState> enemies) => enemies.Select(e => e.Id + "|" + (e.Definition.Ai == "crypt" ? "" : e.Definition.Ai) + "|" + e.Definition.Name + "|" + e.MaxHp + "|" + (e.PartnerId ?? "") + "|" + (string.IsNullOrEmpty(e.Intent.Id) ? e.Intent.Kind.ToString().ToUpperInvariant() : e.Intent.Id)).ToList();
        private static List<string> ExpectedEnemies(object raw) => Arr(raw).Select(o => string.Join("|", Arr(o).Select(v => v is long l ? l.ToString() : Str(v)))).ToList();

        [Test] public void SeedsReproduceTheOriginalRuns()
        {
            var cases = Arr(Obj(Load("run-oracle.json"))["cases"]);
            Assert.That(cases.Count, Is.GreaterThan(20));
            foreach (var raw in cases)
            {
                Tuning.Reset();
                var c = Obj(raw);
                var hero = Str(c["hero"]) == "warrior" ? HeroKind.Warrior : Str(c["hero"]) == "mage" ? HeroKind.Mage : HeroKind.Rogue;
                var label = c["seed"] + "/" + hero + "/" + string.Join(",", Strs(c["relics"]));
                var run = new RunState(hero, Long(c["seed"]));
                foreach (var tag in Strs(c["relics"])) run.AcquireRelic(tag);
                foreach (var fraw in Arr(c["floors"]))
                {
                    var f = Obj(fraw); var map = run.Map;
                    var where = label + " floor " + run.Floor;
                    Assert.That(map.Rows, Is.EqualTo(Int(f["rows"])), where);
                    Assert.That(map.Columns, Is.EqualTo(Int(f["cols"])), where);
                    Assert.That(map.BossId, Is.EqualTo(Str(f["boss"])), where);
                    var nodes = Arr(f["nodes"]);
                    Assert.That(map.Nodes.Select(n => n.Id), Is.EqualTo(nodes.Select(n => Str(Arr(n)[0]))), where + " order");
                    for (var i = 0; i < nodes.Count; i++)
                    {
                        var n = Arr(nodes[i]); var node = map.Nodes[i];
                        Assert.That(node.Kind.ToString().ToUpperInvariant(), Is.EqualTo(Str(n[1])), where + " " + node.Id);
                        Assert.That(node.Connections.OrderBy(x => x, System.StringComparer.Ordinal), Is.EqualTo(Strs(n[2])), where + " " + node.Id);
                        Assert.That(node.Available, Is.EqualTo((bool)n[3]), where + " " + node.Id);
                    }
                    var rooms = Obj(f["rooms"]);
                    foreach (var pair in rooms)
                    {
                        var room = Obj(pair.Value); var node = map.Find(pair.Key); var at = where + " " + pair.Key;
                        typeof(RunState).GetProperty("CurrentRoomId").SetValue(run, pair.Key);
                        switch (node.Kind)
                        {
                            case RoomKind.Combat:
                                Assert.That(Enemies(run.GenerateEnemies(pair.Key)), Is.EqualTo(ExpectedEnemies(room["enemies"])), at);
                                Assert.That(Cards(run.PickRewardCards(pair.Key)), Is.EqualTo(ExpectedCards(room["reward"])), at + " reward");
                                break;
                            case RoomKind.Elite:
                                Assert.That(Enemies(run.GenerateElite(pair.Key)), Is.EqualTo(ExpectedEnemies(room["enemies"])), at);
                                Assert.That(Relics(new[] { run.PickEliteRelic(pair.Key) }), Is.EqualTo(ExpectedRelics(room["relic"])), at + " relic");
                                Assert.That(Cards(run.PickRewardCards(pair.Key, luckBonus: RunState.EliteCardLuck)), Is.EqualTo(ExpectedCards(room["reward"])), at + " reward");
                                break;
                            case RoomKind.Boss:
                                Assert.That(Enemies(run.GenerateBoss()), Is.EqualTo(ExpectedEnemies(room["enemies"])), at);
                                Assert.That(Relics(run.PickBossRelics()), Is.EqualTo(ExpectedRelics(room["relics"])), at + " relics");
                                break;
                            case RoomKind.Treasure:
                                Assert.That(Relics(run.PickTreasureRelics(pair.Key)), Is.EqualTo(ExpectedRelics(room["relics"])), at);
                                break;
                            case RoomKind.Event:
                                Assert.That(run.EventGold(pair.Key), Is.EqualTo(Int(room["gold"])), at);
                                break;
                            case RoomKind.Shop:
                                var (packs, relics) = run.PickShopStock();
                                Assert.That(packs.Select(p => p.Definition.Id + "|" + ChromaId(p.Chroma)), Is.EqualTo(Arr(room["packs"]).Select(o => Str(Arr(o)[0]) + "|" + Str(Arr(o)[1]))), at + " packs");
                                Assert.That(Relics(relics), Is.EqualTo(ExpectedRelics(room["relics"])), at + " relics");
                                break;
                        }
                    }
                    foreach (var pack in Obj(f["packs"]))
                    {
                        var theme = RunContent.Packs.First(p => p.Id == pack.Key).Theme;
                        Assert.That(Cards(run.PickPackCards(theme)), Is.EqualTo(ExpectedCards(pack.Value)), where + " pack " + pack.Key);
                    }
                    Assert.That(Cards(run.PickPackCards(PackTheme.Acero, reroll: 1)), Is.EqualTo(ExpectedCards(f["reroll"])), where + " reroll");
                    typeof(RunState).GetProperty("CurrentRoomId").SetValue(run, map.BossId);
                    run.AdvanceFloor();
                }
                typeof(RunState).GetProperty("Gold").SetValue(run, 1000000000);
                var i2 = 0;
                foreach (var praw in Arr(c["gacha"]))
                {
                    var p = Arr(praw);
                    var kind = Str(p[0]) == "stellar" ? PullKind.Stellar : PullKind.Normal;
                    Assert.That(run.GachaPrice(kind), Is.EqualTo(Int(p[1])), label + " pull price " + i2);
                    var result = run.Pull(kind);
                    Assert.That(result.Relic.Definition.Tag + "|" + ChromaId(result.Relic.Chroma) + "|" + result.Duplicate, Is.EqualTo(Str(p[2]) + "|" + Str(p[3]) + "|" + (bool)p[4]), label + " pull " + i2);
                    if (i2 % 2 == 0) run.AcceptPull(result);
                    i2++;
                }
                Assert.That(run.Luck, Is.EqualTo(Int(c["luck"])), label + " luck");
            }
        }
    }
}
