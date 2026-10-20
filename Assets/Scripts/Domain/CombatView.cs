using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace LastWish.Domain
{
    /// <summary>What a play did, cast by cast (play_card.PlayResult): the screen replays golden casts from it.</summary>
    public sealed class PlayResult
    {
        public bool Success; public string Message = "";
        public bool Combo, Singular, Void, Spoil, Reaction, Fury, Endure;
        public int Casts = 1;
        /// <summary>Cards each cast put in the hand.</summary>
        public readonly List<int> CastDrawn = new List<int>();
        /// <summary>HP each enemy lost during each cast.</summary>
        public readonly List<int[]> CastHits = new List<int[]>();
        public readonly List<int[]> CastEnemyHp = new List<int[]>();
        public readonly List<int[]> CastEnemyBlock = new List<int[]>();
        public readonly List<int> CastPlayerBlock = new List<int>();
        /// <summary>Hits the hero took after the card: Pinchos ("thorns") and Réplica ("riposte"), HP lost each.</summary>
        public readonly List<(string enemyId, int amount, string kind)> HeroHits = new List<(string, int, string)>();
        /// <summary>Hits each enemy parried during the card (Desvío).</summary>
        public readonly Dictionary<string, int> Parried = new Dictionary<string, int>();
        /// <summary>Enemies that gained 1 Strength from Vigilancia with this card.</summary>
        public readonly List<string> VigilGains = new List<string>();
    }

    /// <summary>One enemy's action in the last enemy turn (combat.EnemyAction).</summary>
    public sealed class EnemyAction
    {
        public int Index; public string MoveId = "";
        /// <summary>HP the hero lost on each hit (0 when the block took it).</summary>
        public readonly List<int> Hits = new List<int>();
        public readonly List<StatusAmount> Debuffs = new List<StatusAmount>();
        public readonly List<StatusCardSpawn> Cards = new List<StatusCardSpawn>();
        public int Healed; public bool Exploded;
        /// <summary>Ids of the minions or fragments it added (they arrive at the end of the enemy turn).</summary>
        public readonly List<string> Summoned = new List<string>();
        public bool SummonBlocked, Split, Stunned;
        public int StrengthStolen, StrengthGained, BlockStolen, Cleansed;
        public readonly List<string> Swallowed = new List<string>();
        /// <summary>Id of the partner it brought back.</summary>
        public string Revived;
    }

    /// <summary>Read-only values the combat screen and the hero sheet show, plus previews (no rules here).</summary>
    public sealed partial class CombatState
    {
        public const int HandMaxSize = 10;
        private int drawnToHand;
        private PlayResult lastPlay;
        private List<EnemyAction> enemyLog = new List<EnemyAction>();
        public PlayResult LastPlay => lastPlay;
        /// <summary>Every enemy that acted in the last enemy turn, in order.</summary>
        public IReadOnlyList<EnemyAction> EnemyLog => enemyLog;

        /// <summary>The hero's Ataque now: character + Pruebas + what cards and relics added this combat.</summary>
        public int AttackBonusNow => Saturate((long)Hero.AttackBonus + bonusAttack);
        /// <summary>The hero's Destreza now (cards such as Reflejos raise it for the combat).</summary>
        public int DexterityNow => Saturate((long)Hero.Dexterity + bonusDexterity);
        public int CardsPlayedThisTurn => cardsPlayed;
        public int CardsPerTurn => handSize;
        public IReadOnlyList<CardDefinition> DrawPile => draw.AsReadOnly();
        public IReadOnlyList<CardDefinition> DiscardPile => discard.AsReadOnly();
        public IReadOnlyList<CardDefinition> ExhaustPile => exhausted.AsReadOnly();
        /// <summary>Catalejo: the next cards of the draw pile, first one first.</summary>
        public List<string> SpyglassCards()
        {
            var n = RelicPower("spyglass", 3); var list = new List<string>();
            for (var i = draw.Count - 1; i >= 0 && list.Count < n; i--) list.Add(draw[i].Name);
            return list;
        }

        /// <summary>Keyword layers that would resolve if <paramref name="card"/> were played now (at <paramref name="target"/>).</summary>
        public uint ReadyMask(CardDefinition card, int? target = null) => ActiveConditions(card, target);

        /// <summary>The bonus damage / block a card in hand shows (hero stats, Fuerza, Afilar, relics that do not depend on the target).</summary>
        public (BigInteger damage, long block) CardBonuses(CardDefinition card)
        {
            var savedCard = resolvingCard; var savedMask = resolvingMask;
            resolvingCard = card; resolvingMask = ActiveConditions(card);
            try
            {
                BigInteger damage = (BigInteger)Hero.AttackBonus + Strength + nextDamage + MainRelicDamage(-1);
                long block = (long)Hero.Dexterity + bonusDexterity + MainRelicBlock();
                return (damage, block);
            }
            finally { resolvingCard = savedCard; resolvingMask = savedMask; }
        }

        /// <summary>Play <paramref name="index"/> at <paramref name="target"/> and say what happened.</summary>
        public PlayResult Play(int index, int? target = null)
        {
            if (target.HasValue && target.Value >= 0 && target.Value < enemies.Count && enemies[target.Value].Alive) SelectedEnemy = target.Value;
            if (index < 0 || index >= hand.Count) return new PlayResult { Message = "Carta no válida" };
            if (!CanPlay(index))
                return new PlayResult { Message = Finished ? "El combate terminó" : CardCost(hand[index]) > Mana ? "Maná insuficiente" : "Esta carta no se puede jugar" };
            PlayCard(index);
            return lastPlay;
        }

        private void RecordCast(PlayResult result, int[] hpBefore, int drawnBefore)
        {
            var hp = enemies.Select(e => e.Hp).ToArray();
            var hits = new int[hp.Length];
            for (var i = 0; i < hp.Length; i++) hits[i] = Math.Max(0, (i < hpBefore.Length ? hpBefore[i] : hp[i]) - hp[i]);
            result.CastHits.Add(hits); result.CastEnemyHp.Add(hp); result.CastEnemyBlock.Add(enemies.Select(e => e.Block).ToArray());
            result.CastPlayerBlock.Add(Block); result.CastDrawn.Add(drawnToHand - drawnBefore);
        }

        /// <summary>Relics that trigger after the play (Combo relics, Garfio…) belong to the last cast.</summary>
        private void MergeTail(PlayResult result, int[] hpBefore, int drawnBefore)
        {
            var last = result.CastHits.Count - 1; if (last < 0) return;
            var hp = enemies.Select(e => e.Hp).ToArray();
            for (var i = 0; i < hp.Length && i < result.CastHits[last].Length; i++) result.CastHits[last][i] += Math.Max(0, hpBefore[i] - hp[i]);
            result.CastEnemyHp[last] = hp; result.CastEnemyBlock[last] = enemies.Select(e => e.Block).ToArray();
            result.CastPlayerBlock[last] = Block; result.CastDrawn[last] += drawnToHand - drawnBefore;
        }

        /// <summary>An independent copy for previews: playing a card on it never touches this combat (same random stream, copied).</summary>
        public CombatState Clone()
        {
            var copy = (CombatState)MemberwiseClone();
            var map = new Dictionary<EnemyState, EnemyState>();
            copy.enemies = enemies.Select(e => map[e] = e.Clone()).ToList();
            copy.draw = new List<CardDefinition>(draw); copy.discard = new List<CardDefinition>(discard);
            copy.hand = new List<CardDefinition>(hand); copy.exhausted = new List<CardDefinition>(exhausted);
            copy.powers = new List<CardDefinition>(powers); copy.reactions = new List<ReactionEvent>(reactions);
            copy.enemyLog = new List<EnemyAction>(enemyLog); copy.fading = new HashSet<CardDefinition>(fading);
            copy.HeroStatuses = new StatusSet(); copy.HeroStatuses.CopyFrom(HeroStatuses);
            copy.relicsByTag = relicsByTag.ToDictionary(p => p.Key, p => p.Value.Select(r => r.Clone()).ToList());
            copy.markedSeen = new HashSet<EnemyState>(markedSeen.Where(map.ContainsKey).Select(e => map[e]));
            copy.checkedDeaths = new HashSet<EnemyState>(checkedDeaths.Where(map.ContainsKey).Select(e => map[e]));
            copy.lastRandomTarget = lastRandomTarget != null && map.TryGetValue(lastRandomTarget, out var t) ? t : null;
            copy.random = random.Clone();
            copy.Hand = copy.hand.AsReadOnly(); copy.Enemies = copy.enemies.AsReadOnly(); copy.Reactions = copy.reactions.AsReadOnly();
            copy.lastPlay = null;
            copy.deathsHandled = new HashSet<string>(deathsHandled); copy.arrivals = new List<QueuedArrival>();
            copy.deathEvents = new List<EnemyDeathEvent>(deathEvents);
            return copy;
        }
    }
}
