using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;
using System.Linq;

namespace LastWish.Domain
{
    public sealed class BigValue
    {
        public BigInteger BaseValue { get; }
        public BigInteger FlatBonus { get; }
        public BigInteger Multiplier { get; }
        public BigInteger Value => (BaseValue + FlatBonus) * Multiplier;
        public BigValue(BigInteger value) : this(value, BigInteger.Zero, BigInteger.One) { }
        private BigValue(BigInteger value, BigInteger flat, BigInteger multiplier)
        {
            if (value < 0 || flat < 0 || multiplier < 0) throw new ArgumentOutOfRangeException();
            BaseValue = value; FlatBonus = flat; Multiplier = multiplier;
        }
        public BigValue AddFlat(BigInteger amount) => new BigValue(BaseValue, FlatBonus + amount, Multiplier);
        public BigValue MultiplyBy(BigInteger factor) => new BigValue(BaseValue, FlatBonus, Multiplier * factor);
        public override string ToString() => Value.ToString();
    }

    public enum HeroKind { Warrior, Mage, Rogue }

    public sealed class HeroDefinition
    {
        public HeroKind Kind { get; }
        public string Name { get; }
        public int MaxHp { get; }
        public int MaxMana { get; }
        public int AttackBonus { get; }
        public int Dexterity { get; }
        public int Luck { get; }
        public string AssetId => Kind.ToString().ToLowerInvariant();
        private HeroDefinition(HeroKind kind, string name, int hp, int mana, int attack, int dexterity = 0, int luck = 0)
        { Kind = kind; Name = name; MaxHp = hp; MaxMana = mana; AttackBonus = attack; Dexterity = dexterity; Luck = luck; }
        public HeroDefinition WithBonuses(int attack, int mana, int hp, int luck = 0, int dexterity = 0) =>
            new HeroDefinition(Kind, Name, MaxHp + hp, MaxMana + mana, AttackBonus + attack, Dexterity + dexterity, Luck + luck);
        public static HeroDefinition OriginalFor(HeroKind kind)
        {
            switch (kind)
            {
                case HeroKind.Warrior: return new HeroDefinition(kind, "La Guerrera", 100, 2, 4, 4, 2);
                case HeroKind.Mage: return new HeroDefinition(kind, "El Mago", 60, 4, 8, 1, 5);
                case HeroKind.Rogue: return new HeroDefinition(kind, "La Pícara", 70, 3, 6, 2, 8);
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
        public static HeroDefinition For(HeroKind kind)
        {
            switch (kind)
            {
                case HeroKind.Warrior: return new HeroDefinition(kind, "La Guerrera", 80, 3, 3);
                case HeroKind.Mage: return new HeroDefinition(kind, "El Mago", 65, 4, 0);
                case HeroKind.Rogue: return new HeroDefinition(kind, "La Pícara", 60, 3, 1);
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
    }

    public sealed class CardDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public int Cost { get; }
        public BigValue Damage { get; }
        public int Block { get; }
        public CardKind Kind { get; }
        public bool IsAttack => Kind == CardKind.Attack;
        public ReadOnlyCollection<EffectLayer> Effects { get; }
        public int Draw { get; }
        public int ManaGain { get; }
        public bool Upgraded { get; }
        public int Rarity { get; }
        public string ClassId { get; }
        public string Theme { get; }
        public int ComboBlock { get; }
        public int EndureStrength { get; }
        public int FireStacks { get; }
        public ChromaKind Chroma { get; }
        public int CastCount => (int)Chroma + 1;
        public bool PlayOnDraw { get; }
        public bool Innate { get; }
        public CardDefinition Upgrade() => Upgraded ? this : new CardDefinition(Id, Name + "+", Damage.Value == 0 && Block == 0 && Cost > 0 ? Cost - 1 : Cost,
            Damage.Value > 0 ? Damage.Value + BigInteger.Max(3, Damage.Value / 3) : Damage.Value,
            Block > 0 ? (int)Math.Min(int.MaxValue, Block + (long)Math.Max(3, Block / 3)) : 0,
            Draw + (Damage.Value == 0 && Block == 0 && Cost == 0 ? 1 : 0), ManaGain, true, Rarity, ClassId, Theme, ComboBlock, EndureStrength, FireStacks, UpgradedEffects(), Kind, Chroma, PlayOnDraw, Innate);

        public CardDefinition WithChroma(ChromaKind chroma) => Copy(chroma, Innate);
        public CardDefinition WithInnate() => Copy(Chroma, true);
        private CardDefinition Copy(ChromaKind chroma, bool innate) => new CardDefinition(Id, Name, Cost, Damage.Value, Block, Draw, ManaGain,
            Upgraded, Rarity, ClassId, Theme, ComboBlock, EndureStrength, FireStacks, Effects?.ToArray(), Kind, chroma, PlayOnDraw, innate);

        private EffectLayer[] UpgradedEffects()
        {
            if (Effects == null) return null;
            var result = Effects.ToArray();
            if (result.Length > 0 && result[0].Condition == EffectCondition.Always)
                result[0] = result[0].UpgradeBase(Damage.Value == 0 && Block == 0 && Cost == 0 ? 1 : 0);
            return result;
        }

        /// <summary>A distinct object with the same values (each card of a fight is its own object, like the original's copy.copy).</summary>
        public CardDefinition Copy() => (CardDefinition)MemberwiseClone();
        public CardDefinition(string id, string name, int cost, BigInteger damage, int block, int draw = 0, int manaGain = 0, bool upgraded = false, int rarity = 1, string classId = "neutral", string theme = "acero", int comboBlock = 0, int endureStrength = 0, int fireStacks = 0, EffectLayer[] effects = null, CardKind kind = CardKind.Auto, ChromaKind chroma = ChromaKind.None, bool playOnDraw = false, bool innate = false)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) throw new ArgumentException();
            if (cost < 0 || block < 0 || draw < 0 || manaGain < 0 || rarity < 1 || rarity > 5 || comboBlock < 0 || endureStrength < 0 || fireStacks < 0) throw new ArgumentOutOfRangeException();
            if (!Enum.IsDefined(typeof(ChromaKind), chroma)) throw new ArgumentOutOfRangeException(nameof(chroma));
            Chroma = chroma; PlayOnDraw = playOnDraw; Innate = innate;
            Id = id; Name = name; Cost = cost; Damage = new BigValue(damage); Block = block; Draw = draw; ManaGain = manaGain; Upgraded = upgraded; Rarity = rarity; ClassId = classId; Theme = theme; ComboBlock = comboBlock; EndureStrength = endureStrength; FireStacks = fireStacks;
            if (effects != null && effects.Any(e => e == null)) throw new ArgumentException("Null effect layer");
            Effects = effects == null ? null : new List<EffectLayer>(effects).AsReadOnly();
            Kind = kind == CardKind.Auto ? (damage > 0 || effects != null && effects.Any(e => e.Damage > 0) ? CardKind.Attack : CardKind.Skill) : kind;
        }
    }

    public static class DemoBattle
    {
        public static CombatState Create(HeroKind kind, int seed)
        {
            return new CombatState(HeroDefinition.For(kind), StarterDeck(kind), seed);
        }
        public static List<CardDefinition> StarterDeck(HeroKind kind)
        {
            var deck = new List<CardDefinition>();
            var attackName = kind == HeroKind.Mage ? "Ascua" : kind == HeroKind.Rogue ? "Puñalada" : "Golpe";
            for (var i = 0; i < 5; i++) deck.Add(new CardDefinition("strike", attackName, 1, kind == HeroKind.Mage ? 8 : 6, 0));
            for (var i = 0; i < 4; i++) deck.Add(new CardDefinition("guard", "Defender", 1, 0, 5));
            deck.Add(new CardDefinition("bash", "Embate", 2, 12, 0));
            return deck;
        }
    }
}
