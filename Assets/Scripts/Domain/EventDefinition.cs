using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LastWish.Domain
{
    /// <summary>Immutable event data. Costs are validated before any outcome is applied.</summary>
    public sealed class EventEffect
    {
        public int Gold { get; }
        public int Heal { get; }
        public int MaxHp { get; }
        public int Luck { get; }
        public int Damage { get; }
        public bool Upgrade { get; }
        public bool Remove { get; }
        public bool Card { get; }
        public string Relic { get; }
        public string Text { get; }
        public EventEffect(string text, int gold = 0, int heal = 0, int maxHp = 0, int luck = 0,
            int damage = 0, bool upgrade = false, bool remove = false, bool card = false, string relic = "")
        {
            if (gold < 0 || heal < 0 || maxHp < 0 || luck < 0 || damage < 0) throw new ArgumentOutOfRangeException();
            Text = text; Gold = gold; Heal = heal; MaxHp = maxHp; Luck = luck; Damage = damage;
            Upgrade = upgrade; Remove = remove; Card = card; Relic = relic;
        }
    }

    public sealed class EventChoice
    {
        public string Label { get; }
        public int GoldCost { get; }
        public int HpCost { get; }
        public int MaxHpCost { get; }
        public int SuccessPercent { get; }
        public EventEffect Success { get; }
        public EventEffect Failure { get; }
        public string SetFlag { get; }
        public string NextEventId { get; }
        public int NextStage { get; }
        public bool IsExit { get; }
        public EventChoice(string label, EventEffect success, int goldCost = 0, int hpCost = 0, int maxHpCost = 0,
            int successPercent = 100, EventEffect failure = null, string setFlag = "", string nextEventId = "",
            int nextStage = -1, bool isExit = false)
        {
            if (goldCost < 0 || hpCost < 0 || maxHpCost < 0 || successPercent < 0 || successPercent > 100)
                throw new ArgumentOutOfRangeException();
            if (successPercent < 100 && failure == null) throw new ArgumentNullException(nameof(failure));
            Label = label; Success = success ?? throw new ArgumentNullException(nameof(success)); Failure = failure;
            GoldCost = goldCost; HpCost = hpCost; MaxHpCost = maxHpCost; SuccessPercent = successPercent;
            SetFlag = setFlag; NextEventId = nextEventId; NextStage = nextStage; IsExit = isExit;
        }
    }

    public sealed class EventStage
    {
        public string Body { get; }
        public ReadOnlyCollection<EventChoice> Choices { get; }
        public EventStage(string body, params EventChoice[] choices)
        { Body = body; Choices = Array.AsReadOnly((EventChoice[])choices.Clone()); }
    }

    public sealed class EventDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public int Rarity { get; }
        public int MinFloor { get; }
        public int MaxFloor { get; }
        public string RequiredFlag { get; }
        public string ArtStatus => "Pendiente";
        public string ArtKey => "";
        public ReadOnlyCollection<EventStage> Stages { get; }
        public EventDefinition(string id, string name, int rarity, int minFloor, int maxFloor, string requiredFlag, params EventStage[] stages)
        {
            Id = id; Name = name; Rarity = rarity; MinFloor = minFloor; MaxFloor = maxFloor;
            RequiredFlag = requiredFlag; Stages = Array.AsReadOnly((EventStage[])stages.Clone());
        }
    }

    /// <summary>An open story: its next chapter waits for an event room (<see cref="RunState.PendingStories"/>).</summary>
    public sealed class PendingStory
    {
        /// <summary>The next chapter's event.</summary>
        public string EventId { get; }
        /// <summary>The name of the story's first chapter (the event that started it).</summary>
        public string StoryName { get; }
        /// <summary>The chapter whose choice left it open ("" if unknown).</summary>
        public string FromName { get; }
        /// <summary>The number of the next chapter (2 = the second) and the chapters of the longest path through it.</summary>
        public int Chapter { get; }
        public int Chapters { get; }
        /// <summary>The first floor the next chapter can appear on.</summary>
        public int MinFloor { get; }
        /// <summary>The next event room on this floor opens it.</summary>
        public bool Ready { get; }
        public PendingStory(string eventId, string storyName, string fromName, int chapter, int chapters, int minFloor, bool ready)
        {
            EventId = eventId; StoryName = storyName; FromName = fromName; Chapter = chapter; Chapters = Math.Max(chapter, chapters);
            MinFloor = minFloor; Ready = ready;
        }
    }

    /// <summary>Linked events (a choice's NextEventId) read as stories: the first chapter, each chapter's number, the length.</summary>
    public static class EventStories
    {
        private static Dictionary<string, string> parent;
        private static Dictionary<string, List<string>> children;

        private static void Build()
        {
            if (parent != null) return;
            var up = new Dictionary<string, string>(); var down = new Dictionary<string, List<string>>();
            foreach (var e in EventCatalog.All)
                foreach (var stage in e.Stages)
                    foreach (var choice in stage.Choices)
                    {
                        var next = choice.NextEventId;
                        if (next.Length == 0 || next == e.Id) continue;
                        if (!up.ContainsKey(next)) up[next] = e.Id;
                        if (!down.TryGetValue(e.Id, out var list)) down[e.Id] = list = new List<string>();
                        if (!list.Contains(next)) list.Add(next);
                    }
            children = down; parent = up;
        }

        /// <summary>The event that starts the story <paramref name="id"/> belongs to (itself when nothing leads to it).</summary>
        public static string FirstChapter(string id)
        {
            Build(); var seen = new HashSet<string>();
            while (id != null && seen.Add(id) && parent.TryGetValue(id, out var up)) id = up;
            return id;
        }

        /// <summary>The chapter number of an event: 1 for an event nothing leads to.</summary>
        public static int Chapter(string id)
        {
            Build(); var n = 1; var seen = new HashSet<string>();
            while (id != null && seen.Add(id) && parent.TryGetValue(id, out var up)) { id = up; n++; }
            return n;
        }

        /// <summary>Chapters of the longest story path through an event.</summary>
        public static int Chapters(string id) => Chapter(id) + Below(id, new HashSet<string>());

        private static int Below(string id, HashSet<string> path)
        {
            Build();
            if (id == null || !path.Add(id)) return 0;
            var best = 0;
            if (children.TryGetValue(id, out var list)) foreach (var next in list) best = Math.Max(best, 1 + Below(next, path));
            path.Remove(id);
            return best;
        }
    }

    public sealed class EventRecord
    {
        public string EventId { get; }
        public string RoomId { get; }
        public int Floor { get; }
        public string Choice { get; }
        public string Result { get; }
        public EventRecord(string eventId, string roomId, int floor, string choice, string result)
        { EventId = eventId; RoomId = roomId; Floor = floor; Choice = choice; Result = result; }
    }

    /// <summary>A room-owned, resumable interaction. A completed choice cannot pay twice.</summary>
    public sealed class EventSession
    {
        private readonly RunState run;
        private readonly PyRandom random;
        public EventDefinition Definition { get; }
        public string RoomId { get; }
        public int Floor { get; }
        public int StageIndex { get; private set; }
        public EventStage Stage => Definition.Stages[StageIndex];
        public bool Completed { get; private set; }
        public string Message { get; private set; } = "";
        /// <summary>The last choice that was settled (null until one is) and whether its roll succeeded.</summary>
        public EventChoice LastChoice { get; private set; }
        public bool LastSucceeded { get; private set; }
        /// <summary>The effect the last choice applied (its success or its failure).</summary>
        public EventEffect LastEffect => LastChoice == null ? null : LastSucceeded ? LastChoice.Success : LastChoice.Failure ?? LastChoice.Success;
        internal EventSession(RunState run, string roomId, EventDefinition definition)
        {
            this.run = run; Definition = definition; RoomId = roomId; Floor = run.Floor;
            random = new PyRandom(run.EnemySeed(roomId) ^ 0x45A17);
        }
        public string BlockReason(int index)
        {
            if (Completed) return "Este suceso ya terminó.";
            if (Floor != run.Floor) return "Este suceso pertenece a otro piso.";
            if (index < 0 || index >= Stage.Choices.Count) return "Opción no válida.";
            return run.EventBlockReason(Stage.Choices[index]);
        }
        public bool Choose(int index)
        {
            var blocked = BlockReason(index);
            if (blocked.Length > 0) { Message = blocked; return false; }
            var choice = Stage.Choices[index];
            var success = choice.SuccessPercent == 100 || random.RandInt(1, 100) <= choice.SuccessPercent;
            var effect = success ? choice.Success : choice.Failure;
            Message = run.ApplyEventChoice(this, choice, effect, success);
            LastChoice = choice; LastSucceeded = success;
            if (choice.NextStage >= 0 && success) StageIndex = choice.NextStage;
            else Completed = true;
            return true;
        }
    }
}
