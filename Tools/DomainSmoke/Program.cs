using System;
using System.Numerics;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using LastWish.Domain;

public static class Program
{
    public static int Main()
    {
        var suite = new LastWish.Tests.CombatTests();
        var passed = 0;
        foreach (var method in suite.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            var cases = method.GetCustomAttributes<TestCaseAttribute>().ToArray();
            if (cases.Length == 0 && method.GetCustomAttribute<TestAttribute>() == null) continue;
            var arguments = cases.Length == 0 ? new[] { Array.Empty<object>() } : cases.Select(c => c.Arguments).ToArray();
            foreach (var values in arguments)
            {
                try { method.Invoke(suite, values); passed++; }
                catch (TargetInvocationException failure) { Console.Error.WriteLine(method.Name + ": " + failure.InnerException); return 1; }
            }
        }
        Console.WriteLine(passed + " NUnit domain cases passed outside Unity");
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
        return 0;
    }
}
