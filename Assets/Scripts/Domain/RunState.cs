using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace LastWish.Domain
{
    public sealed partial class RunState
    {
        private readonly List<CardDefinition> deck = new List<CardDefinition>();
        private readonly List<string> relics = new List<string>();
        private readonly HashSet<string> sold = new HashSet<string>();
        public HeroKind HeroKind { get; }
        public HeroDefinition Hero => HeroDefinition.OriginalFor(HeroKind);
        public int Seed { get; }
        public int Floor { get; private set; } = 1;
        public int Gold { get; private set; } = 2000;
        public int Hp { get; private set; }
        public int MaxHp => Hero.MaxHp + RelicCount("iron") * 15 + RelicCount("vitality") * 10;
        public int Pulls { get; private set; }
        public int Removed { get; private set; }
        public RunMap Map { get; private set; }
        public string CurrentRoomId { get; private set; }
        public bool RoomCompleted { get; private set; } = true;
        public bool IsDead => Hp <= 0;
        public ReadOnlyCollection<CardDefinition> Deck { get; }
        public ReadOnlyCollection<string> Relics { get; }
        public int PurgePrice => ShopPrice(checked(75 + 25 * Removed));
        public int RelicCount(string id) => relics.Count(r => r == id);
        public int ShopPrice(int value) => Math.Max(1, (int)Math.Round(value * (1 - Math.Min(.5, RelicCount("mask") * .1))));

        public RunState(HeroKind hero, int seed)
        {
            HeroKind = hero; Seed = seed; Hp = Hero.MaxHp;
            Deck = deck.AsReadOnly(); Relics = relics.AsReadOnly();
            deck.AddRange(RunContent.StarterDeck(hero)); Map = RunMap.Generate(seed, Floor);
        }
        public bool Enter(string id)
        {
            var node = Map.Find(id);
            if (IsDead || !RoomCompleted || node == null || node.Visited || !node.Available) return false;
            node.Visited = true; node.Available = false; CurrentRoomId = id; RoomCompleted = false; return true;
        }
        public bool CompleteRoom()
        {
            var node = Map.Find(CurrentRoomId);
            if (node == null || RoomCompleted || IsDead) return false;
            RoomCompleted = true; Map.Unlock(node); return true;
        }
        public bool AdvanceFloor()
        {
            if (IsDead || !RoomCompleted || CurrentRoomId != Map.BossId || Floor == int.MaxValue) return false;
            Floor++; Map = RunMap.Generate(Seed, Floor); CurrentRoomId = null; sold.Clear(); return true;
        }
        public void GainGold(int value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            Gold = (int)Math.Min(int.MaxValue, (long)Gold + value);
        }
        public bool Spend(int value)
        { if (value < 0 || value > Gold || IsDead) return false; Gold -= value; return true; }
        public void Heal(int value)
        { if (value < 0) throw new ArgumentOutOfRangeException(nameof(value)); if (!IsDead) Hp = (int)Math.Min(MaxHp, (long)Hp + value); }
        public void ApplyCombatResult(int hp, int reward)
        {
            if (hp < 0 || hp > MaxHp || reward < 0) throw new ArgumentOutOfRangeException();
            Hp = hp;
            if (!IsDead) { GainGold((int)Math.Min(int.MaxValue, Math.Round((reward + RelicCount("gold") * 15.0) * (1 + .25 * RelicCount("mask"))))); Heal(RelicCount("blood") * 8); }
        }
        public void AddCard(CardDefinition card) => deck.Add(card ?? throw new ArgumentNullException(nameof(card)));
        public bool BuyCard(string offer, CardDefinition card, int price)
        {
            if (string.IsNullOrEmpty(offer) || card == null || sold.Contains(offer) || !Spend(price)) return false;
            sold.Add(offer); deck.Add(card); return true;
        }
        public bool BuyPack(string offer, int price)
        {
            if (string.IsNullOrEmpty(offer) || sold.Contains(offer) || !Spend(price)) return false;
            sold.Add(offer); return true;
        }
        public bool IsSold(string offer) => sold.Contains(offer);
        public bool AcquireRelic(string id)
        {
            if (!RunContent.Relics.Any(r => r.Id == id)) return false;
            var before = MaxHp; relics.Add(id); Hp += MaxHp - before; return true;
        }
        public bool BuyRelic(string offer, string id, int price)
        {
            if (sold.Contains(offer) || !RunContent.Relics.Any(r => r.Id == id) || !Spend(price)) return false;
            sold.Add(offer); AcquireRelic(id); return true;
        }
        public bool Purge(int index)
        {
            if (index < 0 || index >= deck.Count || deck.Count <= 1 || !Spend(PurgePrice)) return false;
            deck.RemoveAt(index); Removed++; return true;
        }
        public int UpgradePrice(int index) => index < 0 || index >= deck.Count ? int.MaxValue : new[] { 50, 75, 100, 150, 200 }[Math.Max(1, Math.Min(5, deck[index].Rarity)) - 1];
        public bool Upgrade(int index)
        {
            if (index < 0 || index >= deck.Count || deck[index].Upgraded || !Spend(UpgradePrice(index))) return false;
            deck[index] = deck[index].Upgrade(); return true;
        }
        public static int PullPrice(bool stellar, int pulls)
        {
            var raw = (stellar ? 190.0 : 80.0) * Math.Pow(1.5, Math.Max(0, pulls));
            return raw >= int.MaxValue - 5 ? int.MaxValue : Math.Max(5, (int)Math.Round(raw / 5) * 5);
        }
        public RelicDefinition Pull(bool stellar)
        {
            var price = ShopPrice(PullPrice(stellar, Pulls));
            if (Gold < price || IsDead) return null;
            var pool = RunContent.Relics.Where(r => !relics.Contains(r.Id)).ToArray();
            if (pool.Length == 0) pool = RunContent.Relics.ToArray();
            if (pool.Length == 0 || !Spend(price)) return null;
            var random = new Random(unchecked(Seed * 397 + Pulls * 7919));
            var weights = stellar ? new[] { 0.0, 40, 34, 18, 8 } : new[] { 50.0, 28, 14, 6, 2 };
            var tiers = pool.Select(r => r.Rarity).Distinct().OrderBy(t => t).ToArray();
            var luck = HeroKind == HeroKind.Warrior ? 2 : HeroKind == HeroKind.Mage ? 5 : 8;
            var total = tiers.Sum(t => weights[t - 1] * (1 + .1 * luck * (t - 1)));
            var roll = random.NextDouble() * total; var tier = tiers[0];
            foreach (var candidate in tiers) { roll -= weights[candidate - 1] * (1 + .1 * luck * (candidate - 1)); if (roll < 0) { tier = candidate; break; } }
            var candidates = pool.Where(r => r.Rarity == tier).ToArray();
            Pulls++; return candidates[random.Next(candidates.Length)];
        }
        public void RestoreProgress(string completedRoom, IEnumerable<string> visited)
        {
            if (completedRoom != null && Map.Find(completedRoom) == null) throw new ArgumentException(nameof(completedRoom));
            foreach (var id in visited)
            {
                var node = Map.Find(id) ?? throw new ArgumentException(nameof(visited));
                node.Visited = true; node.Available = false; Map.Unlock(node);
            }
            CurrentRoomId = completedRoom; RoomCompleted = true;
            if (completedRoom != null) { Map.Find(completedRoom).Visited = true; Map.Find(completedRoom).Available = false; Map.Unlock(Map.Find(completedRoom)); }
            foreach (var node in Map.Nodes.Where(n => n.Visited)) node.Available = false;
        }
        public CombatState CreateBattle()
        {
            var room = Map.Find(CurrentRoomId) ?? throw new InvalidOperationException("No active room");
            var elite = room.Kind == RoomKind.Elite; var boss = room.Kind == RoomKind.Boss;
            var hp = (int)Math.Min(1000000, (boss ? 120L : elite ? 70L : 45L) + (Floor - 1L) * 12);
            return new CombatState(Hero.WithBonuses(RelicCount("fire") * 2, RelicCount("amulet"), MaxHp - Hero.MaxHp), deck,
                unchecked(Seed * 397 ^ Floor * 997 ^ room.Row * 31 ^ room.Column), Hp, hp,
                (int)Math.Min(1000000, (boss ? 18L : elite ? 12L : 8L) + Floor - 1L),
                boss ? "Guardián de la Cripta" : elite ? "Espectro de élite" : "Espectro", RelicCount("totem"));
        }
    }

    public sealed class RelicDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public int Rarity { get; }
        public RelicDefinition(string id, string name, string description, int rarity)
        { Id = id; Name = name; Description = description; Rarity = rarity; }
    }
    public static class RunContent
    {
        public static readonly IReadOnlyList<RelicDefinition> Relics = Array.AsReadOnly(new[] {
            new RelicDefinition("amulet", "Amuleto de Combate", "+1 maná máximo en combate", 4),
            new RelicDefinition("fire", "Orbe de Fuego", "+2 de daño en ataques", 2),
            new RelicDefinition("totem", "Tótem Roto", "+1 carta al robar cada turno", 3),
            new RelicDefinition("gold", "Anillo de Oro", "+15 de oro por victoria", 1),
            new RelicDefinition("iron", "Corazón de Hierro", "+15 de vida máxima", 2),
            new RelicDefinition("vitality", "Amuleto de Vitalidad", "+10 de vida máxima", 1),
            new RelicDefinition("blood", "Poción de Sangre", "Recupera 8 de vida tras vencer", 1),
            new RelicDefinition("mask", "Máscara del Ladrón", "Tienda -10 % y oro de combate +25 %", 1)
        });
        public static List<CardDefinition> StarterDeck(HeroKind kind)
        {
            var cards = new List<CardDefinition>();
            var owner = kind.ToString().ToLowerInvariant();
            var rogue = kind == HeroKind.Rogue;
            var attackId = rogue ? "punalada_base" : kind == HeroKind.Mage ? "proyectil_base" : "espadazo_base";
            var attackName = rogue ? "Puñalada" : kind == HeroKind.Mage ? "Proyectil Arcano" : "Espadazo";
            var blockId = rogue ? "esquiva_base" : kind == HeroKind.Mage ? "egida_base" : "bloqueo_base";
            var blockName = rogue ? "Esquiva" : kind == HeroKind.Mage ? "Égida" : "Bloqueo";
            for (var i = 0; i < (kind == HeroKind.Warrior ? 5 : 4); i++) cards.Add(new CardDefinition(attackId, attackName, 1, 6, 0, classId: owner));
            for (var i = 0; i < (kind == HeroKind.Mage ? 5 : 4); i++) cards.Add(new CardDefinition(blockId, blockName, 1, 0, rogue ? 6 : 5, classId: owner));
            if (rogue) cards.Add(new CardDefinition("finta_base", "Finta", 1, 4, 0, classId: owner, comboBlock: 4));
            else if (kind == HeroKind.Mage) cards.Add(new CardDefinition("chispa_ignea_base", "Chispa Ígnea", 1, 4, 0, classId: owner, fireStacks: 2));
            else cards.Add(new CardDefinition("guardia_ferrea_base", "Guardia Férrea", 1, 0, 5, classId: owner, endureStrength: 1));
            return cards;
        }
        public static CardDefinition[] ForHero(HeroKind kind) => Cards.Where(c => c.ClassId == "neutral" || c.ClassId == kind.ToString().ToLowerInvariant()).ToArray();
        public static readonly IReadOnlyList<CardDefinition> Cards = Array.AsReadOnly(new[] {
            new CardDefinition("a_golpe_ferreo", "Golpe Férreo", 1, 9, 0, 0, 0, false, 2, "neutral", "acero"),
            new CardDefinition("a_arremetida", "Arremetida", 2, 14, 0, 0, 0, false, 3, "mage", "acero"),
            new CardDefinition("a_furia", "Furia", 3, 20, 0, 0, 0, false, 4, "mage", "acero"),
            new CardDefinition("a_punio", "Puñetazo", 0, 5, 0, 0, 0, false, 1, "neutral", "acero"),
            new CardDefinition("a_golpe_pesado", "Golpe Pesado", 2, 16, 0, 0, 0, false, 3, "warrior", "acero"),
            new CardDefinition("a_patada", "Patada", 1, 7, 0, 0, 0, false, 2, "neutral", "acero"),
            new CardDefinition("a_embestida", "Embestida", 2, 12, 0, 0, 0, false, 3, "mage", "acero"),
            new CardDefinition("a_gran_golpe", "Gran Golpe", 3, 26, 0, 0, 0, false, 4, "warrior", "acero"),
            new CardDefinition("r_pinchazo", "Pinchazo", 0, 3, 0, 0, 0, false, 1, "rogue", "acero"),
            new CardDefinition("r_corte_y_guardia", "Corte y Guardia", 1, 3, 3, 0, 0, false, 1, "rogue", "acero"),
            new CardDefinition("e_guardia_solida", "Guardia Sólida", 1, 0, 8, 0, 0, false, 2, "neutral", "escudo"),
            new CardDefinition("e_escudo_solido", "Escudo Sólido", 1, 0, 5, 1, 0, false, 2, "neutral", "escudo"),
            new CardDefinition("e_fortaleza", "Fortaleza", 2, 0, 14, 0, 0, false, 3, "neutral", "escudo"),
            new CardDefinition("e_parada", "Parada", 0, 0, 4, 0, 0, false, 1, "neutral", "escudo"),
            new CardDefinition("e_capa_hierro", "Capa de Hierro", 2, 0, 11, 1, 0, false, 3, "mage", "escudo"),
            new CardDefinition("e_torre", "Torre", 2, 0, 16, 0, 0, false, 3, "warrior", "escudo"),
            new CardDefinition("e_gran_muralla", "Gran Muralla", 3, 0, 28, 0, 0, false, 4, "warrior", "escudo"),
            new CardDefinition("r_paso_atras", "Paso Atrás", 0, 0, 3, 0, 0, false, 1, "rogue", "escudo"),
            new CardDefinition("r_muro_de_humo", "Muro de Humo", 2, 0, 14, 0, 0, false, 2, "rogue", "escudo"),
            new CardDefinition("m_chispa", "Chispa", 0, 3, 0, 1, 0, false, 1, "mage", "magia"),
            new CardDefinition("m_rayo", "Rayo", 2, 11, 0, 0, 0, false, 3, "mage", "magia"),
            new CardDefinition("m_absorcion", "Absorción", 1, 0, 0, 3, 0, false, 2, "neutral", "magia"),
            new CardDefinition("m_impulso", "Impulso", 1, 0, 6, 1, 0, false, 2, "neutral", "magia"),
            new CardDefinition("m_barrera_magica", "Barrera Mágica", 2, 0, 10, 1, 0, false, 3, "mage", "magia"),
            new CardDefinition("m_vision", "Visión", 0, 0, 0, 2, 0, false, 1, "neutral", "magia"),
            new CardDefinition("m_hechizo_menor", "Hechizo Menor", 1, 6, 0, 0, 0, false, 2, "mage", "magia"),
            new CardDefinition("m_conjuro", "Conjuro de Combate", 2, 10, 6, 0, 0, false, 3, "warrior", "magia"),
            new CardDefinition("r_astucia", "Astucia", 1, 0, 0, 2, 0, false, 4, "rogue", "magia"),
            new CardDefinition("ep_golpe_mortal", "Golpe Mortal", 2, 22, 0, 0, 0, false, 5, "neutral", "epico"),
            new CardDefinition("ep_tormenta", "Tormenta", 3, 16, 12, 0, 0, false, 5, "mage", "epico"),
            new CardDefinition("ep_bastion", "Bastión", 3, 0, 30, 0, 0, false, 5, "warrior", "epico"),
            new CardDefinition("ep_descarga", "Descarga", 2, 18, 0, 2, 0, false, 5, "mage", "epico"),
            new CardDefinition("ep_escudo_arcano", "Escudo Arcano", 2, 0, 14, 2, 0, false, 5, "mage", "epico"),
        });
    }
}
