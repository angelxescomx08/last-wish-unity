using System;
using System.Linq;
using System.Numerics;

namespace LastWish.Domain
{
    [Serializable] public sealed class SavedCommand { public int kind, amount, times = 1; }
    [Serializable] public sealed class SavedEffect
    {
        public string damage;
        public int condition, block, draw, mana, element = -1, stacks;
        public bool allEnemies;
        public string text;
        public SavedCommand[] commands, turnCommands;
    }
    [Serializable] public sealed class SavedCard
    {
        public string id, name, damage;
        public int cost, block, draw, mana, rarity, comboBlock, endureStrength, fireStacks;
        public string classId, theme;
        public bool upgraded;
        public int kind, chroma;
        public bool playOnDraw, innate;
        public SavedEffect[] effects;
    }
    [Serializable] public sealed class SavedRelic { public string id; public int chroma, uses; public bool active = true; }
    [Serializable] public sealed class SavedChoice { public string kind; public int count; }
    [Serializable] public sealed class RunSave
    {
        public int version = 2;
        public int hero, floor, gold, hp, pulls, removed;
        public long seed;
        public int bonusHp, bonusLuck, freeGachaFloor, interestEarned, lastInterest;
        /// <summary><see cref="EnemyRoster"/> of the run (0 = Original in older checkpoints).</summary>
        public int roster;
        public SavedRelic[] ownedRelics;
        public SavedChoice[] pendingChoices;
        public string currentRoom;
        public string[] visited, available;
        public SavedCard[] cards;
    }
    public sealed partial class RunState
    {
        /// <summary>Checkpoint taken on the map (between rooms).</summary>
        public RunSave CreateSave()
        {
            if (IsDead) throw new InvalidOperationException("A dead hero cannot be saved");
            return new RunSave {
                hero = (int)HeroKind, seed = Seed, roster = (int)Roster, floor = Floor, gold = Gold, hp = Hp, pulls = Pulls, removed = Removed,
                bonusHp = BonusMaxHp, bonusLuck = BonusLuck, freeGachaFloor = FreeGachaFloor, interestEarned = InterestEarned, lastInterest = LastInterest,
                ownedRelics = ownedRelics.Select(r => new SavedRelic { id = r.Definition.Id, chroma = (int)r.Chroma, uses = r.TimesTriggered, active = r.Active }).ToArray(),
                pendingChoices = choices.Select(c => new SavedChoice { kind = c.Kind, count = c.Count }).ToArray(),
                currentRoom = CurrentRoomId, visited = Map.Nodes.Where(n => n.Visited).Select(n => n.Id).ToArray(),
                available = Map.Nodes.Where(n => n.Available).Select(n => n.Id).ToArray(),
                cards = deck.Select(SaveCard).ToArray()
            };
        }
        public static SavedCard SaveCard(CardDefinition c) => new SavedCard { id = c.Id, name = c.Name, cost = c.Cost, damage = c.Damage.Value.ToString(),
            block = c.Block, draw = c.Draw, mana = c.ManaGain, upgraded = c.Upgraded, rarity = c.Rarity, classId = c.ClassId, theme = c.Theme, comboBlock = c.ComboBlock, endureStrength = c.EndureStrength, fireStacks = c.FireStacks, kind = (int)c.Kind, chroma = (int)c.Chroma, playOnDraw = c.PlayOnDraw, innate = c.Innate,
            effects = c.Effects == null ? null : c.Effects.Select(e => new SavedEffect { condition = (int)e.Condition, damage = e.Damage.ToString(), block = e.Block,
                draw = e.Draw, mana = e.Mana, element = e.Element.HasValue ? (int)e.Element.Value : -1, stacks = e.Stacks, allEnemies = e.AllEnemies, text = e.Text, commands = SaveCommands(e.Commands), turnCommands = SaveCommands(e.TurnCommands) }).ToArray() };
        private static SavedCommand[] SaveCommands(System.Collections.Generic.IEnumerable<CardCommand> commands) =>
            commands.Select(c => new SavedCommand { kind = (int)c.Kind, amount = c.Amount, times = c.Times }).ToArray();
        private static CardCommand[] LoadCommands(SavedCommand[] commands)
        {
            if (commands == null) return null;
            if (commands.Length > 64 || commands.Any(c => c == null)) throw new ArgumentException("Invalid saved commands");
            return commands.Select(c => new CardCommand((CommandKind)c.kind, c.amount, c.times)).ToArray();
        }
        public static RunState FromSave(RunSave save)
        {
            if (save == null || save.version != 2 || !Enum.IsDefined(typeof(HeroKind), save.hero) || save.seed < 0
                || save.floor < 1 || save.floor > 100000 || save.gold < 0 || save.hp < 1 || save.pulls < 0 || save.pulls > 100000
                || save.removed < 0 || save.removed > 10000 || save.cards == null || save.cards.Length < 1 || save.cards.Length > 10000
                || save.visited == null || save.visited.Length > 200 || save.available == null || save.available.Length > 200
                || save.ownedRelics == null || save.ownedRelics.Length > 1000 || !Enum.IsDefined(typeof(EnemyRoster), save.roster)) throw new ArgumentException("Invalid checkpoint");
            var run = new RunState((HeroKind)save.hero, save.seed, (EnemyRoster)save.roster);
            run.Floor = save.floor; run.Map = RunMap.Generate(save.seed, save.floor);
            run.deck.Clear();
            foreach (var card in save.cards) run.deck.Add(LoadCard(card));
            if (save.bonusHp < 0 || save.bonusLuck < 0 || save.freeGachaFloor < 0 || save.interestEarned < 0 || save.lastInterest < 0) throw new ArgumentException("Invalid saved bonuses");
            run.BonusMaxHp = save.bonusHp; run.BonusLuck = save.bonusLuck; run.FreeGachaFloor = save.freeGachaFloor; run.InterestEarned = save.interestEarned; run.LastInterest = save.lastInterest;
            foreach (var relic in save.ownedRelics)
            {
                var definition = relic == null ? null : RunContent.RelicById(relic.id);
                if (definition == null || relic.uses < 0 || !Enum.IsDefined(typeof(ChromaKind), relic.chroma)) throw new ArgumentException("Unknown saved relic");
                var owned = new OwnedRelic(definition, (ChromaKind)relic.chroma); owned.Restore(relic.active, relic.uses); run.ownedRelics.Add(owned);
            }
            if (save.pendingChoices != null)
            {
                if (save.pendingChoices.Length > 1000 || save.pendingChoices.Any(c => c == null)) throw new ArgumentException("Invalid saved choices");
                foreach (var choice in save.pendingChoices) run.choices.Add(new DeckChoice(choice.kind, choice.count));
            }
            if (save.hp > run.MaxHp) throw new ArgumentException("Invalid saved HP");
            run.Hp = save.hp; run.Gold = save.gold; run.Pulls = save.pulls; run.Removed = save.removed;
            if (!string.IsNullOrEmpty(save.currentRoom) && run.Map.Find(save.currentRoom) == null) throw new ArgumentException("Unknown current room");
            run.CurrentRoomId = string.IsNullOrEmpty(save.currentRoom) ? null : save.currentRoom;
            run.Map.Restore(save.visited, save.available);
            return run;
        }

        public static CardDefinition LoadCard(SavedCard card)
        {
            if (card == null || string.IsNullOrWhiteSpace(card.id) || card.id.Length > 128 || string.IsNullOrWhiteSpace(card.name)
                || card.name.Length > 160 || card.damage == null || card.damage.Length > 4096
                || !BigInteger.TryParse(card.damage, out var damage) || damage < 0 || card.block < 0 || card.draw < 0 || card.draw > 100
                || card.mana < 0 || card.mana > 100 || card.cost < 0 || card.cost > 100)
                throw new ArgumentException("Invalid saved card");
            if (!Enum.IsDefined(typeof(CardKind), card.kind) || !Enum.IsDefined(typeof(ChromaKind), card.chroma) || card.effects != null && card.effects.Length > 64) throw new ArgumentException("Invalid saved effects");
            var effects = card.effects == null || card.effects.Length == 0 ? null : card.effects.Select(e =>
            {
                if (e == null || e.damage == null || e.damage.Length > 4096 || !BigInteger.TryParse(e.damage, out var value)
                    || e.element < -1 || e.element > 3 || !Enum.IsDefined(typeof(EffectCondition), e.condition)) throw new ArgumentException("Invalid saved effect");
                return new EffectLayer((EffectCondition)e.condition, value, e.block, e.draw, e.mana, e.element < 0 ? (ElementKind?)null : (ElementKind)e.element, e.stacks, e.allEnemies, LoadCommands(e.commands), LoadCommands(e.turnCommands), e.text);
            }).ToArray();
            return new CardDefinition(card.id, card.name, card.cost, damage, card.block, card.draw, card.mana, card.upgraded, Math.Max(1, Math.Min(5, card.rarity)), card.classId ?? "neutral", card.theme ?? "acero", card.comboBlock, card.endureStrength, card.fireStacks, effects, (CardKind)card.kind, (ChromaKind)card.chroma, card.playOnDraw, card.innate);
        }
    }
}
