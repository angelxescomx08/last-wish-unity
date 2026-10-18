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
        public int Dexterity { get; }
        public int Luck { get; }
        public string AssetId => Kind.ToString().ToLowerInvariant();
        private HeroDefinition(HeroKind kind, string name, int hp, int mana, int attack, int dexterity = 0, int luck = 0)
        { Kind = kind; Name = name; MaxHp = hp; MaxMana = mana; AttackBonus = attack; Dexterity = dexterity; Luck = luck; }
        public HeroDefinition WithBonuses(int attack, int mana, int hp) =>
            new HeroDefinition(Kind, Name, MaxHp + hp, MaxMana + mana, AttackBonus + attack, Dexterity, Luck);
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
        public bool IsAttack => Damage.Value > 0;
        public int Draw { get; }
        public int ManaGain { get; }
        public bool Upgraded { get; }
        public int Rarity { get; }
        public string ClassId { get; }
        public string Theme { get; }
        public int ComboBlock { get; }
        public int EndureStrength { get; }
        public int FireStacks { get; }
        public CardDefinition Upgrade() => Upgraded ? this : new CardDefinition(Id, Name + "+", Damage.Value == 0 && Block == 0 && Cost > 0 ? Cost - 1 : Cost,
            Damage.Value > 0 ? Damage.Value + BigInteger.Max(3, Damage.Value / 3) : Damage.Value,
            Block > 0 ? Block + Math.Max(3, Block / 3) : 0,
            Draw + (Damage.Value == 0 && Block == 0 && Cost == 0 ? 1 : 0), ManaGain, true, Rarity, ClassId, Theme, ComboBlock, EndureStrength, FireStacks);

        public CardDefinition(string id, string name, int cost, BigInteger damage, int block, int draw = 0, int manaGain = 0, bool upgraded = false, int rarity = 1, string classId = "neutral", string theme = "acero", int comboBlock = 0, int endureStrength = 0, int fireStacks = 0)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) throw new ArgumentException();
            if (cost < 0 || block < 0 || draw < 0 || manaGain < 0 || rarity < 1 || rarity > 5 || comboBlock < 0 || endureStrength < 0 || fireStacks < 0) throw new ArgumentOutOfRangeException();
            Id = id; Name = name; Cost = cost; Damage = new BigValue(damage); Block = block; Draw = draw; ManaGain = manaGain; Upgraded = upgraded; Rarity = rarity; ClassId = classId; Theme = theme; ComboBlock = comboBlock; EndureStrength = endureStrength; FireStacks = fireStacks;
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
        public int EnemyHp { get; private set; }
        public int EnemyMaxHp { get; }
        public string EnemyName { get; }
        private readonly int enemyBaseDamage;
        private readonly int handSize;
        private int cardsPlayed;
        public int Strength { get; private set; }
        public int Fire { get; private set; }
        public int EnemyBlock { get; private set; }
        public int Turn { get; private set; } = 1;
        public int DrawCount => draw.Count;
        public int DiscardCount => discard.Count;
        public bool Victory => EnemyHp == 0;
        public bool Defeat => Hp == 0;
        public bool Finished => Victory || Defeat;
        public bool EnemyIntendsBlock => Turn % 3 == 0;
        public int EnemyDamage => enemyBaseDamage + (Turn - 1) / 3;

        public CombatState(HeroDefinition hero, IEnumerable<CardDefinition> deck, int seed, int initialHp = -1,
            int enemyHp = 45, int enemyDamage = 8, string enemyName = "Espectro", int extraDraw = 0)
        {
            Hero = hero ?? throw new ArgumentNullException(nameof(hero));
            if (deck == null) throw new ArgumentNullException(nameof(deck));
            foreach (var card in deck) draw.Add(card ?? throw new ArgumentException("Null card"));
            if (draw.Count == 0) throw new ArgumentException("Deck must not be empty", nameof(deck));
            Hand = hand.AsReadOnly(); random = new Random(seed);
            if (initialHp < -1 || initialHp > hero.MaxHp || enemyHp < 1 || enemyDamage < 0 || extraDraw < 0 || extraDraw > 100) throw new ArgumentOutOfRangeException();
            Hp = initialHp == -1 ? hero.MaxHp : initialHp; Mana = hero.MaxMana;
            EnemyHp = EnemyMaxHp = enemyHp; EnemyName = enemyName; enemyBaseDamage = enemyDamage; handSize = 5 + extraDraw;
            Shuffle(); DrawHand();
        }

        public bool CanPlay(int index) => !Finished && index >= 0 && index < hand.Count && hand[index].Cost <= Mana;

        public bool PlayCard(int index)
        {
            if (!CanPlay(index)) return false;
            var card = hand[index];
            var endure = Block > 0;
            Mana -= card.Cost;
            if (card.IsAttack)
            {
                var damage = card.Damage.AddFlat(Hero.AttackBonus).AddFlat(Strength).Value;
                var absorbed = BigInteger.Min(EnemyBlock, damage);
                EnemyBlock -= (int)absorbed;
                damage -= absorbed;
                EnemyHp = damage >= EnemyHp ? 0 : EnemyHp - (int)damage;
            }
            var blockGain = (long)card.Block + (card.Block > 0 ? Hero.Dexterity : 0);
            if (cardsPlayed > 0 && card.ComboBlock > 0) blockGain += card.ComboBlock + Hero.Dexterity;
            Block = (int)Math.Min(int.MaxValue, Block + blockGain);
            if (endure) Strength = (int)Math.Min(int.MaxValue, (long)Strength + card.EndureStrength);
            Fire = (int)Math.Min(int.MaxValue, (long)Fire + card.FireStacks);
            cardsPlayed++;
            hand.RemoveAt(index);
            Mana = (int)Math.Min(int.MaxValue, (long)Mana + card.ManaGain);
            DrawCards(card.Draw);
            discard.Add(card);
            return true;
        }

        public bool EndTurn()
        {
            if (Finished) return false;
            discard.AddRange(hand); hand.Clear();
            EnemyBlock = 0;
            if (EnemyIntendsBlock) EnemyBlock = 8;
            else Hp = Math.Max(0, Hp - Math.Max(0, EnemyDamage - Block));
            if (!Defeat) EnemyHp = Math.Max(0, EnemyHp - Fire);
            Block = 0; cardsPlayed = 0;
            if (!Finished) { Turn++; Mana = Hero.MaxMana; DrawHand(); }
            return true;
        }

        private void DrawHand() => DrawCards(handSize - hand.Count);
        private void DrawCards(int count)
        {
            var target = (int)Math.Min(10, (long)hand.Count + count);
            while (hand.Count < target)
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
