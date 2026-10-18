using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;

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
        public string AssetId => Kind.ToString().ToLowerInvariant();
        private HeroDefinition(HeroKind kind, string name, int hp, int mana, int attack)
        { Kind = kind; Name = name; MaxHp = hp; MaxMana = mana; AttackBonus = attack; }
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
        public bool IsAttack => Damage.Value > 0;
        public CardDefinition(string id, string name, int cost, BigInteger damage, int block)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) throw new ArgumentException();
            if (cost < 0 || block < 0) throw new ArgumentOutOfRangeException();
            Id = id; Name = name; Cost = cost; Damage = new BigValue(damage); Block = block;
        }
    }

    public sealed class CombatState
    {
        private readonly List<CardDefinition> draw = new List<CardDefinition>();
        private readonly List<CardDefinition> discard = new List<CardDefinition>();
        private readonly List<CardDefinition> hand = new List<CardDefinition>();
        private readonly Random random;
        public ReadOnlyCollection<CardDefinition> Hand { get; }
        public HeroDefinition Hero { get; }
        public int Hp { get; private set; }
        public int Block { get; private set; }
        public int Mana { get; private set; }
        public int EnemyHp { get; private set; } = 45;
        public int EnemyBlock { get; private set; }
        public int Turn { get; private set; } = 1;
        public int DrawCount => draw.Count;
        public int DiscardCount => discard.Count;
        public bool Victory => EnemyHp == 0;
        public bool Defeat => Hp == 0;
        public bool Finished => Victory || Defeat;
        public bool EnemyIntendsBlock => Turn % 3 == 0;
        public int EnemyDamage => 8 + (Turn - 1) / 3;

        public CombatState(HeroDefinition hero, IEnumerable<CardDefinition> deck, int seed)
        {
            Hero = hero ?? throw new ArgumentNullException(nameof(hero));
            if (deck == null) throw new ArgumentNullException(nameof(deck));
            foreach (var card in deck) draw.Add(card ?? throw new ArgumentException("Null card"));
            if (draw.Count == 0) throw new ArgumentException("Deck must not be empty", nameof(deck));
            Hand = hand.AsReadOnly(); random = new Random(seed);
            Hp = hero.MaxHp; Mana = hero.MaxMana;
            Shuffle(); DrawHand();
        }

        public bool CanPlay(int index) => !Finished && index >= 0 && index < hand.Count && hand[index].Cost <= Mana;

        public bool PlayCard(int index)
        {
            if (!CanPlay(index)) return false;
            var card = hand[index];
            Mana -= card.Cost;
            if (card.IsAttack)
            {
                var damage = card.Damage.AddFlat(Hero.AttackBonus).Value;
                var absorbed = BigInteger.Min(EnemyBlock, damage);
                EnemyBlock -= (int)absorbed;
                damage -= absorbed;
                EnemyHp = damage >= EnemyHp ? 0 : EnemyHp - (int)damage;
            }
            Block = (int)Math.Min(int.MaxValue, (long)Block + card.Block);
            hand.RemoveAt(index); discard.Add(card);
            return true;
        }

        public bool EndTurn()
        {
            if (Finished) return false;
            discard.AddRange(hand); hand.Clear();
            EnemyBlock = 0;
            if (EnemyIntendsBlock) EnemyBlock = 8;
            else Hp = Math.Max(0, Hp - Math.Max(0, EnemyDamage - Block));
            Block = 0;
            if (!Defeat) { Turn++; Mana = Hero.MaxMana; DrawHand(); }
            return true;
        }

        private void DrawHand()
        {
            while (hand.Count < 5)
            {
                if (draw.Count == 0)
                {
                    if (discard.Count == 0) break;
                    draw.AddRange(discard); discard.Clear(); Shuffle();
                }
                var index = draw.Count - 1;
                hand.Add(draw[index]); draw.RemoveAt(index);
            }
        }

        private void Shuffle()
        {
            for (var i = draw.Count - 1; i > 0; i--)
            {
                var other = random.Next(i + 1);
                var card = draw[i]; draw[i] = draw[other]; draw[other] = card;
            }
        }
    }

    public static class DemoBattle
    {
        public static CombatState Create(HeroKind kind, int seed)
        {
            var hero = HeroDefinition.For(kind);
            var deck = new List<CardDefinition>();
            var attackName = kind == HeroKind.Mage ? "Ascua" : kind == HeroKind.Rogue ? "Puñalada" : "Golpe";
            for (var i = 0; i < 5; i++) deck.Add(new CardDefinition("strike", attackName, 1, kind == HeroKind.Mage ? 8 : 6, 0));
            for (var i = 0; i < 4; i++) deck.Add(new CardDefinition("guard", "Defender", 1, 0, 5));
            deck.Add(new CardDefinition("bash", "Embate", 2, 12, 0));
            return new CombatState(hero, deck, seed);
        }
    }
}
