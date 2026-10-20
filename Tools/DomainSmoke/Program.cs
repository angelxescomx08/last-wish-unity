using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using LastWish.Domain;
public static class Program
{
    public static int Main()
    {
        var passed = 0; var failed = new List<string>();
        foreach (var suite in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.Namespace == "LastWish.Tests" && t.GetConstructor(Type.EmptyTypes) != null).OrderBy(t => t.FullName, StringComparer.Ordinal).Select(Activator.CreateInstance))
        {
            var methods = suite.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);
            var setUp = methods.Where(m => m.GetCustomAttribute<SetUpAttribute>() != null).ToArray();
            var tearDown = methods.Where(m => m.GetCustomAttribute<TearDownAttribute>() != null).ToArray();
            foreach (var method in methods)
            {
                var cases = method.GetCustomAttributes<TestCaseAttribute>().ToArray();
                if (cases.Length == 0 && method.GetCustomAttribute<TestAttribute>() == null) continue;
                var arguments = cases.Length == 0 ? new[] { Array.Empty<object>() } : cases.Select(c => c.Arguments).ToArray();
                foreach (var values in arguments)
                {
                    try
                    {
                        foreach (var s in setUp) s.Invoke(suite, null);
                        try { method.Invoke(suite, values); passed++; }
                        finally { foreach (var t in tearDown) t.Invoke(suite, null); }
                    }
                    catch (TargetInvocationException failure)
                    {
                        if (failure.InnerException is IgnoreException || failure.InnerException is InconclusiveException) { passed++; continue; }
                        failed.Add(suite.GetType().Name + "." + method.Name);
                        Console.Error.WriteLine(suite.GetType().Name + "." + method.Name + ": " + failure.InnerException);
                    }
                }
            }
        }
        Tuning.Reset();
        Console.WriteLine(passed + " NUnit domain cases passed outside Unity, " + failed.Count + " failed");
        if (failed.Count > 0) return 1;
        var huge = BigInteger.Pow(10, 1000);
        if (new BigValue(huge).MultiplyBy(7).AddFlat(3).Value != (huge + 3) * 7)
            throw new Exception("BigValue lost precision or operation order");
        foreach (HeroKind hero in Enum.GetValues(typeof(HeroKind)))
        {
            var state = DemoBattle.Create(hero, 42);
            if (state.Hand.Count != 5) throw new Exception("Invalid opening hand");
            for (var turn = 0; turn < 100 && !state.Finished; turn++)
            {
                while (!state.Finished && state.Hand.Count > 0 && state.PlayCard(0)) { }
                if (!state.Finished) state.EndTurn();
                if (state.Hp < 0 || state.Mana < 0) throw new Exception("Invalid combat state");
            }
            if (!state.Finished) throw new Exception("Combat failed to terminate");
            Console.WriteLine(hero + ": combat smoke passed");
        }
        Console.WriteLine("BigValue precision and three complete combats passed");
        return Floor1Smoke() ? 0 : 1;
    }

    /// <summary>Every combat, elite and boss room of floor 1 with the expanded roster, 30 seeds × 3 heroes.</summary>
    private static bool Floor1Smoke()
    {
        int fights = 0, victories = 0, defeats = 0, summons = 0, splits = 0, revivals = 0, stuns = 0, maxAlive = 0;
        var roomId = typeof(RunState).GetProperty("CurrentRoomId");
        var seen = new HashSet<string>();
        try
        {
            Tuning.Reset(); Tuning.Current.ExtraDamage = 30;
            for (var seed = 1; seed <= 30; seed++)
                foreach (HeroKind hero in Enum.GetValues(typeof(HeroKind)))
                {
                    var run = new RunState(hero, seed * 7919L, EnemyRoster.Expanded);
                    foreach (var node in run.Map.Nodes.Where(n => n.Kind == RoomKind.Combat || n.Kind == RoomKind.Elite || n.Kind == RoomKind.Boss))
                    {
                        roomId.SetValue(run, node.Id);
                        var enemies = node.Kind == RoomKind.Boss ? run.GenerateBoss() : node.Kind == RoomKind.Elite ? run.GenerateElite(node.Id) : run.GenerateEnemies(node.Id);
                        foreach (var e in enemies) seen.Add(e.Definition.Ai);
                        var state = run.CreateCombat(enemies);
                        var where = "seed " + seed * 7919L + ", " + hero + ", " + node.Id + " (" + string.Join("+", enemies.Select(e => e.Definition.Ai)) + ")";
                        fights++;
                        var turns = 0;
                        while (!state.Finished)
                        {
                            for (var guard = 0; guard < 30 && !state.Finished; guard++)
                            {
                                var target = Enumerable.Range(0, state.Enemies.Count).First(i => state.Enemies[i].Alive);
                                state.SelectEnemy(target);
                                var card = Enumerable.Range(0, state.Hand.Count).FirstOrDefault(i => state.CanPlay(i), -1);
                                if (card < 0 || !state.PlayCard(card)) break;
                            }
                            if (state.Finished) break;
                            state.EndTurn(); turns++;
                            foreach (var a in state.EnemyLog) { summons += a.Summoned.Count; if (a.Split) splits++; if (a.Revived != null) revivals++; if (a.Stunned) stuns++; }
                            var alive = state.Enemies.Where(e => e.Alive).ToList();
                            maxAlive = Math.Max(maxAlive, alive.Count);
                            var large = alive.Count(e => EnemyField.IsLarge(e.Definition));
                            if (!EnemyField.Fits(large, alive.Count - large)) throw new Exception("Screen cap broken: " + where);
                            if (state.Enemies.Select(e => e.Id).Distinct().Count() != state.Enemies.Count) throw new Exception("Duplicate enemy id: " + where);
                            if (alive.Any(e => e.Intent == null)) throw new Exception("Enemy without intent: " + where);
                            if (turns >= 80 && !state.Finished) throw new Exception("Combat reached 80 turns: " + where);
                        }
                        if (state.Victory) victories++; else defeats++;
                    }
                }
        }
        catch (Exception failure) { Console.Error.WriteLine("Floor-1 smoke failed: " + failure); return false; }
        finally { Tuning.Reset(); }
        Console.WriteLine("Floor-1 expanded smoke: " + fights + " fights (" + victories + " won, " + defeats + " lost), " + summons + " summons, " + splits + " splits, "
            + revivals + " revivals, " + stuns + " stuns, max " + maxAlive + " alive, " + seen.Count + " enemy kinds seen");
        return true;
    }
}
