using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace LastWish.Domain
{
    /// <summary>
    /// Power telemetry of one run: counts what cards, relics and encounters do while the run is played,
    /// so the server can compare them (docs/telemetry.md). Only adds integers to in-memory lists during
    /// play; nothing here touches files, the network or the rules, and a finished tracker ignores
    /// everything. The scene manager feeds it; a headless simulator can feed it the same way.
    /// </summary>
    public sealed class PowerTracker
    {
        // Limits shared with public.ingest_run (supabase/migrations): past them the run is still sent, just shorter.
        public const int MaxCombats = 5000, MaxChoices = 10000, MaxCardsPerCombat = 1000, MaxOffered = 100;

        private readonly Dictionary<string, CardUse> uses = new Dictionary<string, CardUse>();
        private readonly List<RelicRecord> gains = new List<RelicRecord>();

        public RunRecord Record { get; }
        /// <summary>The fight in progress, or null.</summary>
        public CombatRecord Combat { get; private set; }
        public bool Finished => Record.Outcome.HasValue;
        /// <summary>Something was recorded since the last <see cref="Checkpoint"/>.</summary>
        public bool Dirty { get; private set; }

        public PowerTracker(RunState run, string runId, string gameVersion = "dev", string playerId = null, string startedAt = null)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (string.IsNullOrEmpty(runId)) throw new ArgumentException("A run id is required", nameof(runId));
            Record = new RunRecord
            {
                RunId = runId, PlayerId = playerId, GameVersion = string.IsNullOrEmpty(gameVersion) ? "dev" : gameVersion,
                ContentHash = ContentHash, StartedAt = startedAt, Hero = run.HeroKind, Seed = run.Seed, Roster = run.Roster,
                Tuned = !IsDefault(Tuning.Current),
            };
            Sync(run, 0);
        }

        // ------------------------------------------------------------------ rooms and fights
        public void RoomEntered()
        {
            if (Finished) return;
            Record.Rooms = Saturate((long)Record.Rooms + 1); Dirty = true;
        }

        /// <summary>A fight starts: who it is against and what the hero brings. A fight left open is closed as abandoned.</summary>
        public void BeginCombat(RunState run, CombatState state, RoomKind room, IEnumerable<EnemyState> enemies)
        {
            if (Finished || run == null || state == null || enemies == null) return;
            if (Combat != null) Close(CombatOutcome.Abandoned, Combat.Turns, Combat.HpStart, Combat.HpLost);
            var list = enemies.Where(e => e != null).ToList();
            var combat = new CombatRecord
            {
                Floor = run.Floor, Room = room, Encounter = EncounterKey(run, room, list),
                EnemyHp = Saturate(list.Sum(e => (long)e.MaxHp)), HpStart = state.Hp, MaxHp = Math.Max(1, state.Hero.MaxHp),
            };
            combat.Enemies.AddRange(list.Select(e => e.Definition.Ai));
            combat.Deck.AddRange(run.Deck.Select(c => c.Id));
            combat.Relics.AddRange(run.OwnedRelics.Select(r => r.Definition.Id));
            uses.Clear();
            Combat = combat;
            if (Record.Combats.Count < MaxCombats) Record.Combats.Add(combat);
            Dirty = true;
        }

        /// <summary>A card was played: <paramref name="manaPaid"/> is its cost before the play, <paramref name="blockGained"/> the hero's block change.</summary>
        public void CardPlayed(CardDefinition card, int manaPaid, PlayResult result, long blockGained)
        {
            var combat = Combat;
            if (Finished || combat == null || card == null || result == null || !result.Success) return;
            if (!uses.TryGetValue(card.Id, out var use))
            {
                if (combat.Cards.Count >= MaxCardsPerCombat) return;
                use = new CardUse { Id = card.Id }; uses[card.Id] = use; combat.Cards.Add(use);
            }
            long damage = 0;
            foreach (var hits in result.CastHits) foreach (var hit in hits) damage += Math.Max(0, hit);
            var mana = Math.Max(0, manaPaid);
            use.Plays = Saturate((long)use.Plays + 1); use.Mana += mana; use.Damage += damage; use.Block += Math.Max(0, blockGained);
            combat.CardsPlayed = Saturate((long)combat.CardsPlayed + 1); combat.Mana += mana; combat.Damage += damage;
            Dirty = true;
        }

        /// <summary>The fight ended (or the run left it): turns, HP at the end and HP lost in it.</summary>
        public void EndCombat(CombatState state, CombatOutcome outcome)
        {
            if (Finished || Combat == null || state == null) return;
            Close(outcome, state.Turn, state.Hp, state.HpLostCombat);
        }

        private void Close(CombatOutcome outcome, int turns, int hpEnd, int hpLost)
        {
            Combat.Outcome = outcome; Combat.Turns = Math.Max(0, turns); Combat.HpEnd = Math.Max(0, hpEnd); Combat.HpLost = Math.Max(0, hpLost);
            Combat = null; uses.Clear(); Dirty = true;
        }

        // ------------------------------------------------------------------ choices and relics
        /// <summary>Cards offered to choose from (rewards, packs). Returns the choice to <see cref="Pick"/> from, or null.</summary>
        public ChoiceRecord OfferCards(RunState run, string source, IEnumerable<CardDefinition> cards) =>
            Offer(run, source, false, cards?.Where(c => c != null).Select(c => c.Id));

        /// <summary>Relics offered to choose from (treasure, boss, shop, gachapón).</summary>
        public ChoiceRecord OfferRelics(RunState run, string source, IEnumerable<RelicDefinition> relics) =>
            Offer(run, source, true, relics?.Where(r => r != null).Select(r => r.Id));

        private ChoiceRecord Offer(RunState run, string source, bool relic, IEnumerable<string> ids)
        {
            if (Finished || run == null || ids == null || Record.Choices.Count >= MaxChoices) return null;
            var choice = new ChoiceRecord { Floor = run.Floor, Relic = relic, Source = string.IsNullOrEmpty(source) ? "unknown" : source };
            choice.Offered.AddRange(ids.Take(MaxOffered));
            if (choice.Offered.Count == 0) return null;
            Record.Choices.Add(choice); Dirty = true;
            return choice;
        }

        /// <summary>The offer was rerolled (Dado Cargado): the choice lists the new cards instead.</summary>
        public void ReplaceOffer(ChoiceRecord choice, IEnumerable<CardDefinition> cards)
        {
            if (Finished || choice == null || cards == null) return;
            choice.Offered.Clear(); choice.Offered.AddRange(cards.Where(c => c != null).Select(c => c.Id).Take(MaxOffered));
            Dirty = true;
        }

        /// <summary>The player took <paramref name="id"/> from <paramref name="choice"/> (call once per item taken).</summary>
        public void Pick(ChoiceRecord choice, string id)
        {
            if (Finished || choice == null || string.IsNullOrEmpty(id) || choice.Picked.Count >= MaxOffered) return;
            choice.Picked.Add(id); Dirty = true;
        }

        /// <summary>A relic reached the inventory: remembers the floor and the room (shown with the final relics).</summary>
        public void RelicGained(RunState run, RelicDefinition relic, string source)
        {
            if (Finished || run == null || relic == null) return;
            gains.Add(new RelicRecord { Id = relic.Id, Floor = run.Floor, Source = string.IsNullOrEmpty(source) ? "unknown" : source });
            Dirty = true;
        }

        // ------------------------------------------------------------------ run state
        /// <summary>Copy the run's current floor, gold, HP, deck and relics into the record and clear <see cref="Dirty"/>.</summary>
        public RunRecord Checkpoint(RunState run, double playedSeconds)
        {
            if (!Finished && run != null) Sync(run, playedSeconds);
            Dirty = false;
            return Record;
        }

        /// <summary>The run is over. An open fight is closed as lost (death) or abandoned. Later calls change nothing.</summary>
        public RunRecord Finish(RunState run, RunEnd end, double playedSeconds, CombatState combat = null)
        {
            if (Finished) return Record;
            if (Combat != null)
            {
                var outcome = end == RunEnd.Death ? CombatOutcome.Lost : CombatOutcome.Abandoned;
                if (combat != null) EndCombat(combat, outcome); else Close(outcome, Combat.Turns, Combat.HpStart, Combat.HpLost);
            }
            if (run != null) Sync(run, playedSeconds);
            if (end == RunEnd.Death) Record.Hp = 0;
            Record.Outcome = end; Dirty = true;
            return Record;
        }

        private void Sync(RunState run, double playedSeconds)
        {
            var r = Record;
            r.Floor = Math.Max(1, run.Floor); r.Gold = Math.Max(0, run.Gold); r.Hp = Math.Max(0, run.Hp); r.MaxHp = Math.Max(1, run.MaxHp);
            r.PlayedSeconds = double.IsNaN(playedSeconds) || playedSeconds < 0 ? 0 : Math.Min(playedSeconds, 1e7);
            r.Tuned |= !IsDefault(Tuning.Current);
            r.Deck.Clear();
            foreach (var card in run.Deck) r.Deck.Add(new DeckEntry { Id = card.Id, Upgraded = card.Upgraded, Chroma = card.Chroma });
            r.Relics.Clear();
            var used = new bool[gains.Count];
            foreach (var owned in run.OwnedRelics)
            {
                var entry = new RelicRecord { Id = owned.Definition.Id, Chroma = owned.Chroma };
                for (var i = 0; i < gains.Count; i++)
                    if (!used[i] && gains[i].Id == entry.Id) { used[i] = true; entry.Floor = gains[i].Floor; entry.Source = gains[i].Source; break; }
                r.Relics.Add(entry);
            }
        }

        // ------------------------------------------------------------------ keys
        /// <summary>Boss fights by their key (the floor-1 bosses, Los Hermanos Lápida), anything else by its enemy kinds.</summary>
        public static string EncounterKey(RunState run, RoomKind room, IReadOnlyList<EnemyState> enemies)
        {
            if (room == RoomKind.Boss && run != null)
            {
                var key = run.FloorBossKey();
                if (!string.IsNullOrEmpty(key)) return key;
            }
            if (enemies == null || enemies.Count == 0) return "none";
            return string.Join("+", enemies.Select(e => e.Definition.Ai));
        }

        /// <summary>True when the Pruebas screen left every rule as in normal play.</summary>
        public static bool IsDefault(Tuning t)
        {
            if (t == null) return true;
            foreach (var pair in t.ChromaChances)
            {
                var parts = pair.Key.Split(':');
                var chroma = parts[0] == "golden" ? ChromaKind.Golden : ChromaKind.Diamond;
                if (parts.Length != 2 || Math.Abs(pair.Value - Tuning.DefaultChromaChance(chroma, parts[1])) > 1e-12) return false;
            }
            return !t.AllClassCards && t.StartingGold == Tuning.DefaultStartingGold && t.GoldMultiplier == 1.0 && !t.Invincible
                && t.ExtraMana == 0 && t.ExtraDraw == 0 && t.ExtraMaxHp == 0 && t.ExtraLuck == 0 && t.ExtraDamage == 0 && t.ExtraDexterity == 0
                && t.ForcedBoss == 0 && t.ForcedEncounter == 0 && t.ForcedElite == 0 && !t.BossRooms && !t.EliteRooms && !t.GachaRooms;
        }

        private static string contentHash;
        /// <summary>
        /// 16 hex digits that change whenever a card, relic or enemy number changes, so the analytics can
        /// separate data from different balance versions (computed once, about a millisecond).
        /// </summary>
        public static string ContentHash => contentHash ??= ComputeContentHash();

        private static string ComputeContentHash()
        {
            var text = new StringBuilder();
            foreach (var c in RunContent.Cards)
            {
                text.Append(c.Id).Append('|').Append(c.Cost).Append('|').Append(c.Damage.Value.ToString(CultureInfo.InvariantCulture)).Append('|')
                    .Append(c.Block).Append('|').Append(c.Draw).Append('|').Append(c.ManaGain).Append('|').Append(c.Rarity).Append('|')
                    .Append((int)c.Kind).Append('|').Append(c.ClassId).Append('|').Append(c.ComboBlock).Append('|').Append(c.EndureStrength).Append('|').Append(c.FireStacks);
                if (c.Effects != null)
                    foreach (var e in c.Effects)
                    {
                        text.Append('[').Append((int)e.Condition).Append(',').Append(e.Damage.ToString(CultureInfo.InvariantCulture)).Append(',').Append(e.Block).Append(',')
                            .Append(e.Draw).Append(',').Append(e.Mana).Append(',').Append(e.Stacks).Append(',').Append(e.Text);
                        foreach (var command in e.Commands) text.Append(',').Append((int)command.Kind).Append(':').Append(command.Amount).Append('x').Append(command.Times);
                        foreach (var command in e.TurnCommands) text.Append(';').Append((int)command.Kind).Append(':').Append(command.Amount).Append('x').Append(command.Times);
                        text.Append(']');
                    }
                text.Append('\n');
            }
            foreach (var r in RunContent.Relics) text.Append(r.Id).Append('|').Append(r.Rarity).Append('|').Append(r.Tag).Append('|').Append(r.Description).Append('\n');
            foreach (var e in EnemyCatalog.AllDefinitions) text.Append(e.Ai).Append('|').Append(e.BaseHp).Append('|').Append(e.Boss).Append('|').Append(e.Elite).Append('\n');
            const ulong offset = 14695981039346656037UL, prime = 1099511628211UL;
            var hash = offset;
            foreach (var b in Encoding.UTF8.GetBytes(text.ToString())) { hash ^= b; hash *= prime; }
            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }

        private static int Saturate(long value) => (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, value));
    }
}
