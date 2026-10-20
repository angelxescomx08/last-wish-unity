using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using LastWish.Domain;

namespace LastWish.Tests
{
    /// <summary>Helpers of the floor-1 expansion tests (static: DomainSmoke does not run it as a suite).</summary>
    public static class Floor1Kit
    {
        public static HeroDefinition Warrior => HeroDefinition.OriginalFor(HeroKind.Warrior);
        public static CardDefinition Hit(BigInteger damage, string id = "hit") => new CardDefinition(id, "Golpe", 0, damage, 0);
        public static CardDefinition Wait() => new CardDefinition("wait", "Espera", 0, 0, 0);
        public static CardDefinition Huge() => Hit(BigInteger.Pow(10, 100), "huge");
        public static CardDefinition Guard(int block) => new CardDefinition("guard", "Cobertura", 0, 0, block);

        /// <summary>A fight with an invincible hero unless told otherwise.</summary>
        public static CombatState Fight(IEnumerable<EnemyState> enemies, IEnumerable<CardDefinition> deck = null, bool invincible = true, IEnumerable<OwnedRelic> relics = null, int seed = 42)
            => new CombatState(Warrior, (deck ?? new[] { Wait() }).ToList(), seed, 100, enemies, 0, relics) { Invincible = invincible };

        public static EnemyState One(string ai, string id = "e0") => EnemyCatalog.Create(ai, 1, id);

        /// <summary>The intent planned at creation and the next <paramref name="count"/> − 1 plans (ids).</summary>
        public static List<string> Trace(EnemyState enemy, IReadOnlyList<EnemyState> field, int count)
        {
            var ids = new List<string> { enemy.Intent.Id };
            for (var i = 1; i < count; i++) { enemy.Plan(new StatusSet(), field); ids.Add(enemy.Intent.Id); }
            return ids;
        }

        public static void SetInternal(object target, string name, object value)
        {
            var type = target.GetType();
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null) { field.SetValue(target, value); return; }
            var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (property == null) throw new ArgumentException("No member " + name);
            property.SetValue(target, value);
        }

        public static int IndexOf(CombatState state, string cardId)
        {
            for (var i = 0; i < state.Hand.Count; i++) if (state.Hand[i].Id == cardId) return i;
            return -1;
        }

        /// <summary>A file of the repository, found by walking up from the current directory.</summary>
        public static string RepoFile(string relative)
        {
            foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
                for (var folder = new DirectoryInfo(start); folder != null; folder = folder.Parent)
                {
                    var path = Path.Combine(folder.FullName, relative);
                    if (File.Exists(path)) return path;
                }
            throw new FileNotFoundException("Missing repository file " + relative);
        }
    }
}
