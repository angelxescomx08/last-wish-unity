using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;

namespace LastWish.Domain
{
    public enum PullKind { Normal, Stellar }

    public sealed class PullDefinition
    {
        public PullKind Kind { get; }
        public string Name { get; }
        public int BasePrice { get; }
        public double[] Weights { get; }
        public double ChromaBoost { get; }
        public string Note { get; }
        public PullDefinition(PullKind kind, string name, int basePrice, double[] weights, double boost, string note)
        { Kind = kind; Name = name; BasePrice = basePrice; Weights = weights; ChromaBoost = boost; Note = note; }
    }

    public sealed class PullResult
    {
        public bool Success { get; internal set; }
        public string Message { get; internal set; } = "";
        public RelicOffer Relic { get; internal set; }
        public PullKind Kind { get; internal set; }
        public int Price { get; internal set; }
        public bool Duplicate { get; internal set; }
        public bool Settled { get; internal set; }
        public bool Accepted { get; internal set; }
        public int Rarity => Relic?.Definition.Rarity ?? 0;
    }

    /// <summary>A deck choice a relic asked for: "remove" (Tijeras del Sastre) or "innate" (Cartas Marcadas).</summary>
    public sealed class DeckChoice
    {
        public string Kind { get; }
        public int Count { get; }
        public bool Remove => Kind == "remove";
        public DeckChoice(string kind, int count)
        { if (kind != "remove" && kind != "innate") throw new ArgumentException(nameof(kind)); if (count < 0 || count > 10000) throw new ArgumentOutOfRangeException(); Kind = kind; Count = count; }
    }

    /// <summary>
    /// Everything that survives between rooms (domain/run.py) and the run use cases
    /// (run_manager, card_rewards, gacha, warlock, purge, deck_choice), ported with the
    /// original seeded generator so a seed replays the same run.
    /// </summary>
    public sealed partial class RunState
    {
        public const int MinDeck = 1, PurgeBase = 75, PurgeStep = 25, RewardCount = 3, PackSize = 5, EliteCardLuck = 5;
        private static readonly BigInteger Mask64 = (BigInteger.One << 64) - 1;
        private static readonly BigInteger Mask48 = (BigInteger.One << 48) - 1;
        private const long EnemyPrime = 2654435761L, RewardPrime = 3141592653L;
        private const long ChromaSalt = 0x5EEDC0DE, LuckySalt = 0x7C10FE11, GachaPrime = 0x9E3779B1;
        private static readonly int[] UpgradePrices = { 50, 75, 100, 150, 200 };
        public static readonly IReadOnlyList<PullDefinition> PullDefinitions = Array.AsReadOnly(new[] {
            new PullDefinition(PullKind.Normal, "Tirada normal", 80, new[] { 50.0, 28.0, 14.0, 6.0, 2.0 }, 1.0, "Probabilidades de siempre."),
            new PullDefinition(PullKind.Stellar, "Tirada estelar", 190, new[] { 0.0, 40.0, 34.0, 18.0, 8.0 }, 2.0, "Nunca Común · doble de dorada."),
        });
        public static readonly double[] BaseRarityWeight = { 50.0, 28.0, 14.0, 6.0, 2.0 };

        private readonly List<CardDefinition> deck = new List<CardDefinition>();
        private readonly List<OwnedRelic> ownedRelics = new List<OwnedRelic>();
        private readonly List<DeckChoice> choices = new List<DeckChoice>();

        public HeroKind HeroKind { get; }
        public HeroDefinition Hero => HeroDefinition.OriginalFor(HeroKind);
        public long Seed { get; }
        public int Floor { get; private set; } = 1;
        public int Gold { get; private set; }
        public int Hp { get; private set; }
        public RunMap Map { get; private set; }
        public string CurrentRoomId { get; private set; }
        public int Pulls { get; private set; }
        public int Removed { get; private set; }
        public int InterestEarned { get; private set; }
        public int LastInterest { get; private set; }
        public int BonusMaxHp { get; private set; }
        public int BonusLuck { get; private set; }
        public int FreeGachaFloor { get; private set; }
        public ReadOnlyCollection<CardDefinition> Deck { get; }
        public ReadOnlyCollection<OwnedRelic> OwnedRelics { get; }
        public ReadOnlyCollection<DeckChoice> PendingChoices { get; }
        public bool IsDead => Hp <= 0;
        public MapRoom CurrentRoom => Map.Find(CurrentRoomId);

        /// <summary>Enemy content of this run: <see cref="EnemyRoster.Original"/> (the Python game, parity tests) unless the menu asks for the expansion.</summary>
        public EnemyRoster Roster { get; }

        public RunState(HeroKind hero, long seed, EnemyRoster roster = EnemyRoster.Original)
        {
            if (seed < 0) throw new ArgumentOutOfRangeException(nameof(seed));
            if (!Enum.IsDefined(typeof(EnemyRoster), roster)) throw new ArgumentOutOfRangeException(nameof(roster));
            HeroKind = hero; Seed = seed; Roster = roster;
            Deck = deck.AsReadOnly(); OwnedRelics = ownedRelics.AsReadOnly(); PendingChoices = choices.AsReadOnly();
            Gold = Tuning.Current.StartingGold; Hp = Hero.MaxHp + Tuning.Current.ExtraMaxHp;
            deck.AddRange(RunContent.StarterDeck(hero)); Map = RunMap.Generate(seed, Floor);
        }

        private static int Clamp(long value) => (int)Math.Min(int.MaxValue, Math.Max(0, value));
        private static int Clamp(BigInteger value) => value > int.MaxValue ? int.MaxValue : value < 0 ? 0 : (int)value;

        // ------------------------------------------------------------ relic queries
        public int RelicEffect(string tag, int amount = 1)
        {
            long total = 0;
            foreach (var r in ownedRelics) if (r.Active && r.Definition.Tag == tag) total += (long)amount * r.Multiplier;
            return Clamp(total);
        }
        public bool HasRelic(string tag) => RelicEffect(tag) > 0;
        public int RelicMaxHpBonus => Clamp((long)RelicEffect("iron_heart", 15) + RelicEffect("vitality_amulet", 10) + RelicEffect("wild_fruit", 7) + RelicEffect("stone_heart", 30) + Tuning.Current.ExtraMaxHp);
        public int MaxHp => Clamp((long)Hero.MaxHp + RelicMaxHpBonus + BonusMaxHp);
        public int RelicLuck => Clamp((long)RelicEffect("seven_leaf_clover", 100) + RelicEffect("silver_horseshoe", 30) + RelicEffect("rabbit_foot", 15));
        /// <summary>run_manager.run_luck: character + Pruebas + relics + what the run won.</summary>
        public int Luck => Clamp((long)Tuning.HeroLuck(Hero.Luck) + RelicLuck + BonusLuck);
        public int TreasureChoices => 1 + RelicEffect("master_key");

        public HashSet<string> AllowedCardClasses()
        {
            if (Tuning.Current.AllClassCards) return new HashSet<string>(RunContent.ClassIds);
            var result = new HashSet<string> { "neutral", RunContent.ClassOf(HeroKind) };
            foreach (var r in ownedRelics) if (r.Active) foreach (var c in r.Definition.CardClasses) result.Add(c);
            return result;
        }

        // ------------------------------------------------------------ rarity / chroma
        public static double RarityWeight(int rarity, int luck) => BaseRarityWeight[rarity - 1] * (1.0 + 0.10 * Math.Max(0, luck) * (rarity - 1));

        public static List<T> WeightedSample<T>(IEnumerable<T> items, int count, PyRandom rng, Func<T, int> rarityOf, int luck)
        {
            var remaining = items.ToList(); var chosen = new List<T>();
            while (remaining.Count > 0 && chosen.Count < count)
            {
                var tiers = remaining.Select(rarityOf).Distinct().OrderBy(t => t).ToList();
                var weights = tiers.Select(t => RarityWeight(t, luck)).ToList();
                var tier = rng.WeightedChoice(tiers, weights);
                var options = remaining.Where(i => rarityOf(i) == tier).ToList();
                var pick = rng.Choice(options);
                remaining.Remove(pick); chosen.Add(pick);
            }
            return chosen;
        }

        public static double LuckChromaMultiplier(int luck) => 1.0 + 0.10 * Math.Max(0, luck);

        /// <summary>chroma.roll_chroma: one rng call; golden is the only droppable chroma.</summary>
        public static ChromaKind RollChroma(PyRandom rng, string kind, int luck)
        {
            var boost = LuckChromaMultiplier(luck);
            var roll = rng.Random();
            var acc = Math.Min(1.0, Tuning.ChromaChance(ChromaKind.Golden, kind) * boost);
            return roll < acc ? ChromaKind.Golden : ChromaKind.None;
        }

        public static (double first, double second) LuckyCardChances(int luck)
        {
            luck = Math.Max(0, luck);
            return (Math.Min(1.0, luck / 100.0), Math.Min(1.0, Math.Max(0, luck - 100) / 200.0));
        }

        public static int LuckyCardCount(int luck, PyRandom rng)
        {
            var (first, second) = LuckyCardChances(luck);
            double a = rng.Random(), b = rng.Random();
            if (a >= first) return 0;
            return b < second ? 2 : 1;
        }

        // ------------------------------------------------------------ seeds
        private static uint Crc(string text) => PyRandom.Crc32(text ?? "None");
        public BigInteger EnemySeed(string roomId) => ((BigInteger)Seed * EnemyPrime + (BigInteger)Floor * 997 + Crc(roomId)) & Mask64;
        public BigInteger RewardSeed(string roomId) => ((BigInteger)Seed * RewardPrime + (BigInteger)Floor * 1999 + Crc(roomId)) & Mask64;

        // ------------------------------------------------------------ rooms
        /// <summary>Map click: mark the room visited (unlocking its neighbours), then the room relics.</summary>
        public MapRoom EnterRoom(string id)
        {
            var node = Map.Find(id);
            if (node == null || IsDead || choices.Count > 0 || node.Visited || !node.Available) return null;
            Map.MarkVisited(id); CurrentRoomId = id; EnterRoomRelics(node.Kind);
            return node;
        }

        /// <summary>run_manager.enter_room: Bolsa Agujereada, Hucha, Pico de Minero, Ganzúa.</summary>
        public int EnterRoomRelics(RoomKind kind)
        {
            long gold = RelicEffect("leaky_purse", 5);
            if (kind == RoomKind.Shop) gold += Math.Min(RelicEffect("piggy_bank", 15), (long)(Gold / 10) * RelicEffect("piggy_bank"));
            if (kind == RoomKind.Treasure) gold += (long)RelicEffect("miner_pick", 25) + RelicEffect("lockpick", 20);
            if (gold > 0) GainGold(Clamp(gold));
            return Clamp(gold);
        }

        public void AdvanceFloor()
        {
            if (Floor == int.MaxValue) return;
            Floor++; Map = RunMap.Generate(Seed, Floor); CurrentRoomId = null;
        }

        // ------------------------------------------------------------ enemies
        public List<EnemyState> GenerateEnemies(string roomId)
        {
            if (Tuning.Current.BossRooms) return GenerateBoss();
            if (Tuning.Current.EliteRooms) return GenerateElite(roomId);
            return EnemyCatalog.RollEncounter(new PyRandom(EnemySeed(roomId)), Floor, roomId, Tuning.Current.ForcedEncounter, Roster);
        }

        public List<EnemyState> GenerateElite(string roomId) =>
            EnemyCatalog.RollElite(new PyRandom(EnemySeed(roomId) ^ 0xE117E), Floor, roomId, Tuning.Current.ForcedElite, Roster);

        public string FloorBossAi()
        {
            if (Floor != 1) return null;
            var forced = Tuning.Current.ForcedBoss;
            if (forced >= 1 && forced <= EnemyCatalog.Floor1Bosses.Count) return EnemyCatalog.Floor1Bosses[forced - 1];
            return new PyRandom(((BigInteger)Seed * EnemyPrime) ^ 0xB055).Choice(EnemyCatalog.Floor1Bosses);
        }

        /// <summary>The floor-1 boss fight of the expanded roster (same seeded choice, over 6 fights), or null on later floors.</summary>
        public string FloorBossKey()
        {
            if (Floor != 1) return null;
            if (Roster == EnemyRoster.Original) return FloorBossAi();
            var keys = EnemyCatalog.BossKeys(Roster);
            var forced = Tuning.Current.ForcedBoss;
            if (forced >= 1 && forced <= keys.Count) return keys[forced - 1];
            return new PyRandom(((BigInteger)Seed * EnemyPrime) ^ 0xB055).Choice(keys.ToList());
        }

        public List<EnemyState> GenerateBoss()
        {
            if (Roster == EnemyRoster.Expanded && Floor == 1) return EnemyCatalog.CreateBoss(FloorBossKey(), Floor);
            var ai = FloorBossAi();
            return new List<EnemyState> { ai != null ? EnemyCatalog.Create(ai, Floor, "boss_f" + Floor) : EnemyCatalog.CreateCryptLord(Floor) };
        }

        /// <summary>combat_factory.create_combat_from_run: run deck, relics, HP and the Pruebas stats.</summary>
        public CombatState CreateCombat(IEnumerable<EnemyState> enemies, int? combatSeed = null)
        {
            var t = Tuning.Current;
            var hero = Hero.WithBonuses(t.ExtraDamage, t.ExtraMana, MaxHp - Hero.MaxHp, Luck - Hero.Luck, t.ExtraDexterity);
            var seed = combatSeed ?? unchecked((int)(uint)(EnemySeed(CurrentRoomId ?? "combat") & uint.MaxValue));
            return new CombatState(hero, deck, seed, Math.Min(Hp, hero.MaxHp), enemies, Math.Max(0, t.ExtraDraw), ownedRelics) { Invincible = t.Invincible };
        }

        // ------------------------------------------------------------ victory, gold, events
        public int CombatGold(IEnumerable<EnemyState> enemies)
        {
            long worth = 0; foreach (var e in enemies) if (e.PaysGold) worth += Math.Max(1, e.MaxHp / 8);
            worth += RelicEffect("gold_ring", 15);
            var multiplier = 1.0 + 0.25 * RelicEffect("thief_mask");
            return Clamp((long)Math.Round(worth * multiplier * Tuning.Current.GoldMultiplier, MidpointRounding.ToEven));
        }

        /// <summary>run_manager.apply_combat_victory. Returns the gold earned (before interest).</summary>
        public int ApplyCombatVictory(int hpAfter, IEnumerable<EnemyState> enemies, int bonusGold = 0, int? hpLost = null, int? turns = null, bool elite = false, bool boss = false)
        {
            var list = enemies.ToList();
            long hp = hpAfter;
            if (elite && HasRelic("veteran_medal")) { BonusMaxHp = Clamp((long)BonusMaxHp + RelicEffect("veteran_medal", 3)); hp += RelicEffect("veteran_medal", 3); }
            var newHp = Math.Min(hp + RelicEffect("blood_potion", 8), MaxHp);
            if (boss && HasRelic("laurel_leaf")) newHp = MaxHp;
            Hp = Math.Max(1, Clamp(newHp));
            long gold = (long)CombatGold(list) + Math.Max(0, bonusGold);
            if (hpLost.HasValue && hpLost.Value <= 0) { gold += RelicEffect("canteen", 15); BonusLuck = Clamp((long)BonusLuck + RelicEffect("rascal_boots", 2)); }
            if (turns.HasValue && turns.Value <= 3) gold += RelicEffect("hunting_trophy", 20);
            if (elite) gold += RelicEffect("grinning_skull", 30);
            GainGold(Clamp(gold));
            return Clamp(gold);
        }

        /// <summary>The run ended in a defeat: the hero is dead.</summary>
        public void ApplyDefeat() { Hp = 0; }

        /// <summary>run_manager.gain_gold (the single entry point of gold gains). Returns the interest paid.</summary>
        public int GainGold(int amount)
        {
            LastInterest = 0;
            if (amount <= 0) return 0;
            Gold = Clamp((long)Gold + amount);
            var interest = Clamp((long)Gold * RelicEffect("compound_interest", 10) / 100);
            Gold = Clamp((long)Gold + interest);
            LastInterest = interest; InterestEarned = Clamp((long)InterestEarned + interest);
            return interest;
        }

        public bool Spend(int amount)
        { if (amount < 0 || amount > Gold) return false; Gold -= amount; return true; }

        public void Heal(int amount) { if (amount > 0 && !IsDead) Hp = (int)Math.Min(MaxHp, (long)Hp + amount); }

        public int EventGold(string roomId)
        {
            var rng = new PyRandom(EnemySeed(roomId) ^ 0xABCD);
            return Clamp((long)Math.Round(rng.RandInt(10 + Floor * 2, 25 + Floor * 3) * Tuning.Current.GoldMultiplier, MidpointRounding.ToEven));
        }

        // ------------------------------------------------------------ cards
        public void AddCard(CardDefinition card)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));
            deck.Add(card.Chroma == ChromaKind.Golden && HasRelic("diamond_cards") ? card.WithChroma(ChromaKind.Diamond) : card);
        }

        public int RewardCardCount(int count = RewardCount) => count + RelicEffect("appraiser_lens");

        private List<CardDefinition> PickCards(IEnumerable<CardDefinition> pool, int count, PyRandom rng, int luck)
        {
            var list = pool.ToList();
            if (HasRelic("collector_seal")) { var high = list.Where(c => c.Rarity >= 3).ToList(); if (high.Count > 0) list = high; }
            return WeightedSample(list, count, rng, c => c.Rarity, luck);
        }

        private static List<OfferedCard> WithChromas(IEnumerable<CardDefinition> cards, BigInteger seed, int luck, bool lucky = false)
        {
            var rng = new PyRandom(seed ^ ChromaSalt);
            return cards.Select(c => new OfferedCard(c.WithChroma(RollChroma(rng, "card", luck)), lucky)).ToList();
        }

        private IEnumerable<CardDefinition> PoolForClasses(HashSet<string> allowed) => RunContent.Cards.Where(c => allowed.Contains(c.ClassId));

        public List<OfferedCard> PickRewardCards(string roomId, int count = RewardCount, int luckBonus = 0)
        {
            var seed = RewardSeed(roomId);
            var rng = new PyRandom(seed);
            var chosen = PickCards(PoolForClasses(AllowedCardClasses()), RewardCardCount(count), rng, Luck + Math.Max(0, luckBonus));
            var cards = WithChromas(chosen, seed, Luck);
            cards.AddRange(LuckyCards(seed, new HashSet<string>(cards.Select(c => c.Card.Id))));
            return cards;
        }

        public List<OfferedCard> PickPackCards(PackTheme theme, int count = PackSize, int reroll = 0)
        {
            var themeId = RunContent.Pack(theme).Id;
            var seed = RewardSeed("pack_" + themeId + "_" + Floor + (reroll != 0 ? "_r" + reroll : ""));
            var rng = new PyRandom(seed);
            var allowed = AllowedCardClasses();
            var chosen = PickCards(RunContent.Cards.Where(c => c.Theme == themeId && allowed.Contains(c.ClassId)), count, rng, Luck);
            if (chosen.Count < count)
            {
                var extra = PoolForClasses(allowed).Where(c => c.Theme != themeId && !chosen.Contains(c));
                chosen.AddRange(PickCards(extra, count - chosen.Count, rng, Luck));
            }
            var cards = WithChromas(chosen, seed, Luck);
            cards.AddRange(LuckyCards(seed, new HashSet<string>(cards.Select(c => c.Card.Id))));
            return cards;
        }

        /// <summary>card_rewards.lucky_cards: luck's extra "Cartas de la suerte" (usually none).</summary>
        public List<OfferedCard> LuckyCards(BigInteger seed, HashSet<string> exclude)
        {
            var luck = Luck;
            var rng = new PyRandom(seed ^ LuckySalt);
            var n = LuckyCardCount(luck, rng);
            if (n == 0) return new List<OfferedCard>();
            var pool = PoolForClasses(AllowedCardClasses()).Where(c => !exclude.Contains(c.Id)).ToList();
            var rare = pool.Where(c => c.Rarity >= 3).ToList();
            var chosen = PickCards(rare.Count >= n ? rare : pool, n, rng, luck);
            return WithChromas(chosen, seed ^ LuckySalt, luck, true);
        }

        /// <summary>Cards you keep from a pack: its chroma multiplier; Bolsa sin Fondo makes golden keep 3.</summary>
        public int PackPicks(ChromaKind chroma)
        {
            var picks = (int)chroma + 1;
            if (chroma == ChromaKind.Golden && HasRelic("bottomless_bag")) picks++;
            return picks;
        }

        // ------------------------------------------------------------ relics
        public List<RelicDefinition> RelicPool(int minimum = 1)
        {
            var own = RunContent.ClassOf(HeroKind);
            var allowed = RunContent.Relics.Where(r => r.ClassId == "neutral" || r.ClassId == own).ToList();
            if (HasRelic("antiquary_seal")) { var high = allowed.Where(r => r.Rarity >= 3).ToList(); if (high.Count > 0) allowed = high; }
            var owned = new HashSet<string>(ownedRelics.Select(r => r.Definition.Tag));
            var pool = allowed.Where(r => !owned.Contains(r.Tag)).ToList();
            return pool.Count >= minimum ? pool : allowed;
        }

        private static List<RelicOffer> WithRelicChroma(IEnumerable<RelicDefinition> relics, PyRandom rng, int luck) =>
            relics.ToList().Select(r => new RelicOffer(r, RollChroma(rng, "relic", luck))).ToList();

        private static List<RelicDefinition> PickRelics(List<RelicDefinition> pool, int count, PyRandom rng, int luck) => WeightedSample(pool, count, rng, r => r.Rarity, luck);

        public List<RelicOffer> PickTreasureRelics(string roomId)
        {
            var count = TreasureChoices;
            var pool = RelicPool(count);
            var rng = new PyRandom(EnemySeed(roomId) ^ 0x1234);
            return WithRelicChroma(PickRelics(pool, Math.Min(count, pool.Count), rng, Luck), rng, Luck);
        }

        public RelicOffer PickEliteRelic(string roomId)
        {
            var rng = new PyRandom(EnemySeed(roomId) ^ 0xE1173);
            return WithRelicChroma(PickRelics(RelicPool(1), 1, rng, Luck), rng, Luck)[0];
        }

        public List<RelicOffer> PickBossRelics(int count = 3)
        {
            var rng = new PyRandom((BigInteger)Seed * 37 + (BigInteger)Floor * 13);
            return WithRelicChroma(PickRelics(RelicPool(count), count, rng, Luck), rng, Luck);
        }

        public int ShopPrice(int value)
        {
            var discount = Math.Min(0.5, 0.10 * RelicEffect("thief_mask"));
            return Math.Max(1, (int)Math.Round(value * (1.0 - discount), MidpointRounding.ToEven));
        }

        /// <summary>run_manager.pick_shop_stock: three packs and three relics, stable for this room.</summary>
        public (List<PackOffer> packs, List<RelicOffer> relics) PickShopStock()
        {
            var rng = new PyRandom("shop:" + Seed + ":" + Floor + ":" + (CurrentRoomId ?? "None"));
            var pool = RelicPool(3);
            var packs = rng.Sample(RunContent.Packs, 3);
            var relics = WithRelicChroma(PickRelics(pool, 3, rng, Luck), rng, Luck);
            var packOffers = packs.Select(p => new PackOffer(p, RollChroma(rng, "pack", Luck))).ToList();
            return (packOffers, relics);
        }

        /// <summary>Shop price now: Máscara del Ladrón, Tarjeta de Socio (first purchase half), Vale de Descuento.</summary>
        public int ShopOfferPrice(int basePrice, bool relic, int purchases)
        {
            if (relic && HasRelic("voucher")) return 0;
            var price = ShopPrice(basePrice);
            if (purchases == 0 && HasRelic("member_card")) price = Math.Max(1, price / 2);
            return price;
        }

        public bool UseVoucher()
        {
            var voucher = ownedRelics.FirstOrDefault(r => r.Active && r.Definition.Tag == "voucher");
            if (voucher == null) return false;
            voucher.Restore(false, voucher.TimesTriggered); return true;
        }

        public OwnedRelic AcquireRelic(RelicOffer offer)
        {
            if (offer == null) throw new ArgumentNullException(nameof(offer));
            var relic = new OwnedRelic(offer.Definition, offer.Chroma);
            ownedRelics.Add(relic); ApplyPickup(relic); return relic;
        }
        public OwnedRelic AcquireRelic(string idOrTag, ChromaKind chroma = ChromaKind.None)
        {
            var definition = RunContent.RelicById(idOrTag) ?? RunContent.RelicByTag(idOrTag);
            return definition == null ? null : AcquireRelic(new RelicOffer(definition, chroma));
        }

        private void ApplyPickup(OwnedRelic relic)
        {
            Hp = Math.Min(Hp, MaxHp);
            var times = relic.Multiplier;
            switch (relic.Definition.Tag)
            {
                case "singular_mirror":
                    var best = new Dictionary<string, int>(); var order = new List<string>();
                    for (var i = 0; i < deck.Count; i++)
                    {
                        if (!best.TryGetValue(deck[i].Id, out var previous)) { best[deck[i].Id] = i; order.Add(deck[i].Id); }
                        else if (deck[previous].Chroma == ChromaKind.None && deck[i].Chroma != ChromaKind.None) best[deck[i].Id] = i;
                    }
                    var kept = order.Select(id => deck[best[id]]).ToList(); deck.Clear(); deck.AddRange(kept); break;
                case "diamond_cards":
                    for (var i = 0; i < deck.Count; i++) if (deck[i].Chroma == ChromaKind.Golden) deck[i] = deck[i].WithChroma(ChromaKind.Diamond);
                    break;
                case "portable_anvil":
                    var rng = new PyRandom((BigInteger)Seed * 31 + ownedRelics.Count);
                    for (var t = 0; t < times; t++)
                    {
                        var options = Enumerable.Range(0, deck.Count).Where(i => CanUpgrade(deck[i])).ToList();
                        if (options.Count > 0) { var index = rng.Choice(options); deck[index] = deck[index].Upgrade(); }
                    }
                    break;
                case "iron_file":
                    for (var t = 0; t < times; t++) { var index = deck.FindIndex(c => c.Id.EndsWith("_base", StringComparison.Ordinal)); if (index >= 0 && deck.Count > 1) deck.RemoveAt(index); }
                    break;
                case "tailor_scissors": choices.Add(new DeckChoice("remove", 3 * times)); break;
                case "marked_cards": choices.Add(new DeckChoice("innate", times)); break;
            }
        }

        // ------------------------------------------------------------ deck choices
        public int ChoiceLimit(string kind, int count) =>
            kind == "remove" ? Math.Max(0, Math.Min(count, deck.Count - MinDeck)) : Math.Max(0, Math.Min(count, deck.Count));

        /// <summary>deck_choice.apply_choice: invalid or repeated indices are ignored. Returns the cards affected.</summary>
        public int ApplyChoice(string kind, IEnumerable<int> indices)
        {
            var valid = indices.Where(i => i >= 0 && i < deck.Count).Distinct().OrderBy(i => i).ToList();
            valid = valid.Take(ChoiceLimit(kind, valid.Count)).ToList();
            if (kind == "remove") for (var k = valid.Count - 1; k >= 0; k--) deck.RemoveAt(valid[k]);
            else if (kind == "innate") foreach (var i in valid) deck[i] = deck[i].WithInnate();
            return valid.Count;
        }

        public DeckChoice NextChoice()
        {
            if (choices.Count == 0) return null;
            var next = choices[0]; choices.RemoveAt(0); return next;
        }

        // ------------------------------------------------------------ warlock / purge
        public static bool CanUpgrade(CardDefinition card) => !card.Upgraded;
        public int UpgradePrice(CardDefinition card)
        {
            var price = UpgradePrices[Math.Max(1, Math.Min(5, card.Rarity)) - 1] * (card.Upgraded ? 2 : 1);
            return HasRelic("smith_hammer") ? (price + 1) / 2 : price;
        }
        public bool CanBuyUpgrade(CardDefinition card) => CanUpgrade(card) && Gold >= UpgradePrice(card);
        public bool BuyUpgrade(int index)
        {
            if (index < 0 || index >= deck.Count || !CanBuyUpgrade(deck[index])) return false;
            Gold -= UpgradePrice(deck[index]); deck[index] = deck[index].Upgrade(); return true;
        }

        public int PurgePrice => ShopPrice(PurgeBase + PurgeStep * Math.Max(0, Removed));
        public string PurgeBlockReason()
        {
            if (deck.Count <= MinDeck) return "Es tu última carta: tu mazo no puede quedarse vacío";
            if (Gold < PurgePrice) return "No tienes suficiente oro";
            return "";
        }
        public bool CanPurge => PurgeBlockReason() == "";
        public CardDefinition PurgeCard(int index)
        {
            if (index < 0 || index >= deck.Count || !CanPurge) return null;
            Gold -= PurgePrice; Removed++;
            var card = deck[index]; deck.RemoveAt(index); return card;
        }

        // ------------------------------------------------------------ gachapón
        public static int PullPrice(PullKind kind, int pullsDone)
        {
            var raw = PullDefinitions[(int)kind].BasePrice * Math.Pow(1.5, Math.Max(0, pullsDone));
            if (raw >= int.MaxValue - 5) return int.MaxValue;
            return Math.Max(5, (int)Math.Round(raw / 5.0, MidpointRounding.ToEven) * 5);
        }

        public static double TierWeight(PullKind kind, int rarity, int luck) =>
            PullDefinitions[(int)kind].Weights[rarity - 1] * (1.0 + 0.10 * Math.Max(0, luck) * (rarity - 1));

        public static Dictionary<int, double> PullOdds(PullKind kind, int luck, IEnumerable<int> tiers = null)
        {
            var list = (tiers ?? new[] { 1, 2, 3, 4, 5 }).ToList();
            var weights = list.ToDictionary(t => t, t => TierWeight(kind, t, luck));
            var total = weights.Values.Sum();
            if (total <= 0) { weights = list.ToDictionary(t => t, t => 1.0); total = list.Count == 0 ? 1.0 : list.Count; }
            return weights.ToDictionary(p => p.Key, p => p.Value / total);
        }

        public bool FreePullReady(PullKind kind) => kind == PullKind.Normal && FreeGachaFloor != Floor && HasRelic("two_faced_coin");
        public int GachaPrice(PullKind kind) => FreePullReady(kind) ? 0 : ShopPrice(PullPrice(kind, Pulls));
        public bool CanPull(PullKind kind) => Gold >= GachaPrice(kind);
        public Dictionary<int, double> GachaOdds(PullKind kind) => PullOdds(kind, Luck);

        public PullResult Pull(PullKind kind)
        {
            var price = GachaPrice(kind);
            if (Gold < price) return new PullResult { Success = false, Message = "¡No tienes oro suficiente!", Kind = kind, Price = price };
            var rng = new PyRandom(((BigInteger)Seed * GachaPrime + 7919L * (Pulls + 1)) & Mask48);
            var owned = new HashSet<string>(ownedRelics.Select(r => r.Definition.Tag));
            var pool = RelicPool();
            var luck = Luck;
            if (pool.Count == 0) return new PullResult { Success = false, Message = "La máquina está vacía.", Kind = kind, Price = price };
            var present = pool.Select(r => r.Rarity).Distinct().OrderBy(t => t).ToList();
            var odds = PullOdds(kind, luck, present);
            var tier = rng.WeightedChoice(present, present.Select(t => odds[t]).ToList());
            var relic = rng.Choice(pool.Where(r => r.Rarity == tier).ToList());
            if (FreePullReady(kind)) FreeGachaFloor = Floor;
            Gold -= price; Pulls++;
            var boost = PullDefinitions[(int)kind].ChromaBoost;
            var chroma = RollChroma(rng, "relic", (int)(luck * boost + 10 * (boost - 1)));
            return new PullResult { Success = true, Message = "¡" + relic.Name + "!", Relic = new RelicOffer(relic, chroma), Kind = kind, Price = price, Duplicate = owned.Contains(relic.Tag) };
        }

        public bool AcceptPull(PullResult result)
        {
            if (result == null || !result.Success || result.Relic == null || result.Settled) return false;
            AcquireRelic(result.Relic); result.Settled = result.Accepted = true; return true;
        }

        public static bool DeclinePull(PullResult result)
        {
            if (result == null || !result.Success || result.Relic == null || result.Settled) return false;
            result.Settled = true; result.Accepted = false; return true;
        }
    }
}
