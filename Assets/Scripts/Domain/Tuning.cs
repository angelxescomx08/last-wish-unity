using System;
using System.Collections.Generic;

namespace LastWish.Domain
{
    /// <summary>
    /// "Pruebas" knobs (domain/tuning.py). Defaults reproduce normal play exactly; the Pruebas
    /// screen edits <see cref="Current"/> and the runtime persists it.
    /// </summary>
    public sealed class Tuning
    {
        public const int DefaultStartingGold = 2000;
        public static readonly string[] ChromaKinds = { "card", "relic", "pack" };
        public static Tuning Current { get; private set; } = new Tuning();

        public Dictionary<string, double> ChromaChances { get; private set; } = new Dictionary<string, double>();
        public bool AllClassCards;
        public int StartingGold = DefaultStartingGold;
        public double GoldMultiplier = 1.0;
        public bool Invincible;
        public int ExtraMana, ExtraDraw, ExtraMaxHp, ExtraLuck, ExtraDamage, ExtraDexterity;
        public int ForcedBoss, ForcedEncounter, ForcedElite;
        public bool BossRooms, EliteRooms, GachaRooms;

        public static void Reset() => Current = new Tuning();
        public static void Replace(Tuning value) => Current = value ?? throw new ArgumentNullException(nameof(value));

        public static int HeroLuck(int baseLuck) => Math.Max(0, baseLuck + Current.ExtraLuck);
        public static string ChromaKey(ChromaKind chroma, string kind) => (chroma == ChromaKind.Golden ? "golden" : "diamond") + ":" + kind;

        public static double DefaultChromaChance(ChromaKind chroma, string kind)
        {
            if (chroma != ChromaKind.Golden) return 0.0;
            switch (kind) { case "card": return 0.05; case "relic": return 0.08; case "pack": return 0.06; default: return 0.0; }
        }

        public static double ChromaChance(ChromaKind chroma, string kind)
        {
            var value = Current.ChromaChances.TryGetValue(ChromaKey(chroma, kind), out var custom) ? custom : DefaultChromaChance(chroma, kind);
            return Math.Max(0.0, Math.Min(1.0, value));
        }

        public Tuning Clone()
        {
            var copy = (Tuning)MemberwiseClone();
            copy.ChromaChances = new Dictionary<string, double>(ChromaChances);
            return copy;
        }
    }
}
