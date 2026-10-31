using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace LastWish.Domain
{
    public sealed partial class RunState
    {
        private int eventMaxHpPenalty;
        private readonly Dictionary<string, EventSession> eventSessions = new Dictionary<string, EventSession>();
        private readonly HashSet<string> seenEvents = new HashSet<string>();
        private readonly HashSet<string> eventFlags = new HashSet<string>();
        private readonly List<string> pendingEvents = new List<string>();
        private readonly List<EventRecord> eventHistory = new List<EventRecord>();
        public IReadOnlyCollection<string> EventFlags => Array.AsReadOnly(eventFlags.OrderBy(f => f, StringComparer.Ordinal).ToArray());
        public ReadOnlyCollection<EventRecord> EventHistory => eventHistory.AsReadOnly();
        public int PermanentMaxHpAdjustment => BonusMaxHp - eventMaxHpPenalty;

        private bool EventEligible(EventDefinition definition) => Floor >= definition.MinFloor && Floor <= definition.MaxFloor
            && (definition.RequiredFlag.Length == 0 || eventFlags.Contains(definition.RequiredFlag)) && !seenEvents.Contains(definition.Id);

        /// <summary>Stable room selection; optional id is a developer preview and still checks floor and prerequisites.</summary>
        public EventSession OpenEvent(string roomId, string eventId = null)
        {
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException(nameof(roomId));
            var key = Floor + ":" + roomId;
            if (eventSessions.TryGetValue(key, out var existing)) return existing;
            if (IsDead) throw new InvalidOperationException("La partida ha terminado.");
            EventDefinition definition;
            if (eventId != null)
            {
                definition = EventCatalog.Find(eventId);
                if (definition == null || !EventEligible(definition)) throw new InvalidOperationException("Suceso no disponible.");
            }
            else
            {
                definition = pendingEvents.Select(EventCatalog.Find).FirstOrDefault(e => EventEligible(e));
                if (definition == null)
                {
                    var pool = EventCatalog.All.Where(e => e.RequiredFlag.Length == 0 && EventEligible(e)).ToList();
                    if (pool.Count == 0) definition = EventCatalog.Find("scattered_coins");
                    else
                    {
                        var rng = new PyRandom(EnemySeed(roomId) ^ 0xE7E17);
                        var tiers = pool.Select(e => e.Rarity).Distinct().OrderBy(r => r).ToList();
                        var tier = rng.WeightedChoice(tiers, tiers.Select(r => BaseRarityWeight[r - 1]).ToList());
                        definition = rng.Choice(pool.Where(e => e.Rarity == tier).ToList());
                    }
                }
            }
            seenEvents.Add(definition.Id); pendingEvents.Remove(definition.Id);
            var session = new EventSession(this, roomId, definition); eventSessions.Add(key, session);
            return session;
        }

        internal string EventBlockReason(EventChoice choice)
        {
            if (IsDead) return "La partida ha terminado.";
            if (Gold < choice.GoldCost) return "No tienes suficiente oro.";
            if (Hp <= choice.HpCost) return "Necesitas conservar al menos 1 de vida.";
            if (MaxHp <= choice.MaxHpCost) return "Necesitas conservar al menos 1 de vida máxima.";
            foreach (var effect in new[] { choice.Success, choice.Failure }.Where(e => e != null))
            {
                if (effect.Remove && deck.Count <= MinDeck) return "No puedes perder tu última carta.";
                if (effect.Upgrade && !deck.Any(CanUpgrade)) return "Todas tus cartas están mejoradas.";
                if (effect.Relic.Length > 0 && HasRelic(effect.Relic)) return "Ya tienes esta reliquia.";
            }
            return "";
        }

        internal string ApplyEventChoice(EventSession session, EventChoice choice, EventEffect effect, bool success)
        {
            // Validation in EventSession precedes every mutation, including random consumption.
            Gold -= choice.GoldCost; Hp -= choice.HpCost;
            eventMaxHpPenalty = Clamp((long)eventMaxHpPenalty + choice.MaxHpCost);
            Hp = Math.Min(Hp, MaxHp);
            if (effect.MaxHp > 0) { BonusMaxHp = Clamp((long)BonusMaxHp + effect.MaxHp); }
            Hp = Math.Max(1, Hp - Math.Min(Hp - 1, effect.Damage));
            Heal(effect.Heal);
            BonusLuck = Clamp((long)BonusLuck + effect.Luck);
            GainGold(effect.Gold);
            var details = new List<string> { effect.Text };
            if (effect.Upgrade)
            {
                var index = deck.FindIndex(CanUpgrade); var card = deck[index]; deck[index] = card.Upgrade();
                details.Add("Mejorada: " + deck[index].Name + ".");
            }
            if (effect.Remove)
            { details.Add("Eliminada: " + deck[0].Name + "."); deck.RemoveAt(0); Removed++; }
            if (effect.Card)
            {
                var card = PickRewardCards("event_" + session.RoomId + "_" + session.StageIndex, 1)[0].Card;
                AddCard(card); details.Add("Recibiste: " + card.Name + ".");
            }
            if (effect.Relic.Length > 0)
            { var relic = AcquireRelic(effect.Relic); details.Add("Recibiste: " + relic.Definition.Name + "."); }
            if (success && choice.SetFlag.Length > 0) eventFlags.Add(choice.SetFlag);
            if (success && choice.NextEventId.Length > 0 && !pendingEvents.Contains(choice.NextEventId) && !seenEvents.Contains(choice.NextEventId))
                pendingEvents.Add(choice.NextEventId);
            var message = string.Join(" ", details);
            eventHistory.Add(new EventRecord(session.Definition.Id, session.RoomId, Floor, choice.Label, message));
            return message;
        }
    }
}
