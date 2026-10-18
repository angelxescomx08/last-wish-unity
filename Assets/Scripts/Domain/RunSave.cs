using System;
using System.Linq;
using System.Numerics;

namespace LastWish.Domain
{
    [Serializable] public sealed class SavedCard
    {
        public string id, name, damage;
        public int cost, block, draw, mana, rarity, comboBlock, endureStrength, fireStacks;
        public string classId, theme;
        public bool upgraded;
    }
    [Serializable] public sealed class RunSave
    {
        public int version = 1;
        public int hero, seed, floor, gold, hp, pulls, removed;
        public string currentRoom;
        public string[] visited, relics, sold;
        public SavedCard[] cards;
    }
    public sealed partial class RunState
    {
        public RunSave CreateSave()
        {
            if (!RoomCompleted || IsDead) throw new InvalidOperationException("Only save between rooms");
            return new RunSave {
                hero = (int)HeroKind, seed = Seed, floor = Floor, gold = Gold, hp = Hp, pulls = Pulls, removed = Removed,
                currentRoom = CurrentRoomId, visited = Map.Nodes.Where(n => n.Visited).Select(n => n.Id).ToArray(),
                relics = relics.ToArray(), sold = sold.ToArray(),
                cards = deck.Select(c => new SavedCard { id = c.Id, name = c.Name, cost = c.Cost, damage = c.Damage.Value.ToString(),
                    block = c.Block, draw = c.Draw, mana = c.ManaGain, upgraded = c.Upgraded, rarity = c.Rarity, classId = c.ClassId, theme = c.Theme, comboBlock = c.ComboBlock, endureStrength = c.EndureStrength, fireStacks = c.FireStacks }).ToArray()
            };
        }
        public static RunState FromSave(RunSave save)
        {
            if (save == null || save.version != 1 || !Enum.IsDefined(typeof(HeroKind), save.hero)
                || save.floor < 1 || save.floor > 100000 || save.gold < 0 || save.hp < 1 || save.pulls < 0 || save.pulls > 1000
                || save.removed < 0 || save.removed > 10000 || save.cards == null || save.cards.Length < 1 || save.cards.Length > 10000
                || save.relics == null || save.relics.Length > 1000 || save.visited == null || save.visited.Length > 96
                || save.sold == null || save.sold.Length > 1000) throw new ArgumentException("Invalid checkpoint");
            var run = new RunState((HeroKind)save.hero, save.seed);
            run.Floor = save.floor; run.Map = RunMap.Generate(save.seed, save.floor);
            run.deck.Clear();
            foreach (var card in save.cards)
            {
                if (card == null || string.IsNullOrWhiteSpace(card.id) || card.id.Length > 128 || string.IsNullOrWhiteSpace(card.name)
                    || card.name.Length > 160 || card.damage == null || card.damage.Length > 4096
                    || !BigInteger.TryParse(card.damage, out var damage) || damage < 0 || card.block < 0 || card.draw < 0 || card.draw > 100
                    || card.mana < 0 || card.mana > 100 || card.cost < 0 || card.cost > 100)
                    throw new ArgumentException("Invalid saved card");
                run.deck.Add(new CardDefinition(card.id, card.name, card.cost, damage, card.block, card.draw, card.mana, card.upgraded, Math.Max(1, Math.Min(5, card.rarity)), card.classId ?? "neutral", card.theme ?? "acero", card.comboBlock, card.endureStrength, card.fireStacks));
            }
            foreach (var id in save.relics) if (!run.AcquireRelic(id)) throw new ArgumentException("Unknown saved relic");
            if (save.hp > run.MaxHp) throw new ArgumentException("Invalid saved HP");
            run.Hp = save.hp; run.Gold = save.gold; run.Pulls = save.pulls; run.Removed = save.removed;
            foreach (var offer in save.sold)
            {
                if (string.IsNullOrEmpty(offer) || offer.Length > 256) throw new ArgumentException("Invalid offer");
                run.sold.Add(offer);
            }
            run.RestoreProgress(string.IsNullOrEmpty(save.currentRoom) ? null : save.currentRoom, save.visited);
            return run;
        }
    }
}
