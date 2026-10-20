using System;
using System.Collections.Generic;
using System.Linq;

namespace LastWish.Domain
{
    /// <summary>
    /// Combat rules of the floor-1 expansion: arrivals (summons, splits, revivals) with the on-screen cap, Pinchos,
    /// Desvío/Réplica, Vigilancia, stolen Strength, siphoned block, Muda, swallowed cards and death effects.
    /// With only original enemies every hook here is a no-op.
    /// </summary>
    public sealed partial class CombatState
    {
        private sealed class QueuedArrival
        {
            public EnemyDefinition Definition; public string SummonerId; public double HpFactor; public bool PaysGold;
            public int? ExactHp; public bool Revive;
        }
        private List<QueuedArrival> arrivals = new List<QueuedArrival>();   // empty outside EndTurn
        private int summonCounter;
        private HashSet<string> deathsHandled = new HashSet<string>();
        private List<EnemyDeathEvent> deathEvents = new List<EnemyDeathEvent>();

        /// <summary>Deaths handled since the last card or end of turn (returned cards, Carroña).</summary>
        public IReadOnlyList<EnemyDeathEvent> DeathEvents => deathEvents;

        private (int hp, int hits)[] ExpansionSnapshot()
        {
            var snapshot = new (int, int)[enemies.Count];
            for (var i = 0; i < enemies.Count; i++) { enemies[i].ParriedHits = 0; snapshot[i] = (enemies[i].Hp, enemies[i].DamagingHits); }
            deathEvents.Clear();
            return snapshot;
        }

        /// <summary>After a card: Vigilancia, then Pinchos and Réplica (skipped when the card left no enemy alive).</summary>
        private void AfterCardExpansion(PlayResult result, (int hp, int hits)[] snapshot)
        {
            foreach (var enemy in enemies)
            {
                var vigil = enemy.Statuses[StatusKind.Vigil];
                if (enemy.Alive && vigil > 0 && cardsPlayed > vigil) { enemy.Statuses.Add(StatusKind.Strength, 1); result.VigilGains.Add(enemy.Id); }
            }
            if (!enemies.Any(e => e.Alive)) return;
            for (var i = 0; i < enemies.Count && i < snapshot.Length && !Defeat; i++)
            {
                var enemy = enemies[i]; var thorns = enemy.Statuses[StatusKind.Thorns];
                if (thorns > 0 && (enemy.Hp < snapshot[i].hp || enemy.DamagingHits > snapshot[i].hits))
                    result.HeroHits.Add((enemy.Id, HitHeroOwnTurn(thorns), "thorns"));
            }
            for (var i = 0; i < enemies.Count && !Defeat; i++)
            {
                var enemy = enemies[i];
                if (enemy.ParriedHits <= 0) continue;
                result.Parried[enemy.Id] = enemy.ParriedHits;
                for (var k = 0; k < enemy.ParriedHits && !Defeat; k++) result.HeroHits.Add((enemy.Id, HitHeroOwnTurn(enemy.Riposte), "riposte"));
                enemy.ParriedHits = 0;
            }
            if (pendingDraws > 0) { var draws = pendingDraws; pendingDraws = 0; DrawCards(draws); }   // Venda Empapada after a Pinchos hit
        }

        /// <summary>Pinchos / Réplica: not an attack (no decoys, defence relics or Corona de Espinas); block absorbs it.</summary>
        private int HitHeroOwnTurn(int damage)
        {
            if (damage <= 0) return 0;
            var absorbed = Math.Min(Block, damage); Block -= absorbed;
            var lost = Invincible ? 0 : Math.Min(Hp, damage - absorbed); Hp -= lost; OnHpLost(lost, true); Revive();
            return lost;
        }

        private static readonly StatusKind[] Cleansed = {
            StatusKind.Poison, StatusKind.Marked, StatusKind.Weak, StatusKind.Vulnerable, StatusKind.Frail, StatusKind.Entangled,
            StatusKind.Fire, StatusKind.Water, StatusKind.Earth, StatusKind.Air, StatusKind.Frozen };

        /// <summary>Effects that happen before the hits of the same intent (Muda, Confiscación, Juicio).</summary>
        private void BeforeHitsExpansion(EnemyState enemy, IntentExtras x, EnemyAction action)
        {
            if (x.Cleanse)
            {
                var stacks = 0L;
                foreach (var kind in Cleansed) { stacks += enemy.Statuses[kind]; enemy.Statuses.Remove(kind); }
                action.Cleansed = Saturate(stacks);
                enemy.GainBlock(Saturate(Math.Min(x.CleanseBlockCap, stacks * x.CleanseBlockPerStack)));
            }
            if (x.StealStrength > 0)
            {
                var stolen = Math.Min(Strength, x.StealStrength);
                if (stolen > 0)
                {
                    Strength -= stolen; HeroStatuses.Set(StatusKind.Strength, Strength);
                    enemy.Statuses.Add(StatusKind.Strength, stolen); action.StrengthStolen = stolen;
                }
                else if (x.StrengthFallback > 0) { enemy.Statuses.Add(StatusKind.Strength, x.StrengthFallback); action.StrengthGained = x.StrengthFallback; }
            }
            if (x.SiphonBlockPercent > 0)
            {
                var taken = (int)((long)Block * x.SiphonBlockPercent / 100);
                Block -= taken; enemy.GainBlock(taken); action.BlockStolen = taken;
            }
        }

        /// <summary>Effects after the usual ones: own statuses, healing, swallowing, summons and revivals.</summary>
        private void AfterEffectsExpansion(EnemyState enemy, IntentExtras x, EnemyAction action)
        {
            foreach (var s in x.SetSelf) { enemy.Statuses.Set(s.Kind, s.Amount); if (s.Kind == StatusKind.Focus) enemy.FocusBroken = false; }
            if (x.HealSelf > 0) action.Healed += enemy.Heal(x.HealSelf);
            for (var n = 0; n < x.Swallow; n++) Swallow(enemy, action);
            foreach (var spec in x.Summons) QueueSummons(enemy, spec, action);
            if (x.ReviveAi != null) QueueRevive(enemy, x.ReviveAi, action);
            if (x.ConsumesFocus) enemy.Statuses.Remove(StatusKind.Focus);
        }

        private IEnumerable<EnemyDefinition> QueuedDefinitions() => arrivals.Select(a => a.Definition);

        private void QueueSummons(EnemyState summoner, SummonSpec spec, EnemyAction action)
        {
            var definition = EnemyCatalog.Definition(spec.Ai);
            var profile = EnemyCatalog.Profile(summoner.Definition.Ai);
            var budget = profile == null ? spec.Count : Math.Max(0, profile.SummonBudget - summoner.SummonsMade);
            var n = Math.Min(Math.Min(spec.Count, budget), EnemyField.Free(enemies, QueuedDefinitions(), EnemyField.IsLarge(definition)));
            if (n <= 0) { action.SummonBlocked = true; return; }
            if (n < spec.Count) action.SummonBlocked = true;
            for (var i = 0; i < n; i++)
                arrivals.Add(new QueuedArrival { Definition = definition, SummonerId = summoner.Id, HpFactor = spec.HpFactor, PaysGold = spec.PaysGold });
            summoner.SummonsMade += n;
        }

        private void QueueRevive(EnemyState summoner, string ai, EnemyAction action)
        {
            summoner.ReviveUsed = true; summoner.AwaitingExhume = false;
            var definition = EnemyCatalog.Definition(ai);
            if (EnemyField.Free(enemies, QueuedDefinitions(), EnemyField.IsLarge(definition)) < 1) { action.SummonBlocked = true; return; }
            arrivals.Add(new QueuedArrival { Definition = definition, SummonerId = summoner.Id, HpFactor = 1.0, PaysGold = true, Revive = true });
        }

        /// <summary>División: the enemy dies and its fragments (half of its HP each) arrive at the end of the enemy turn.</summary>
        private void Split(EnemyState enemy, IntentExtras x, EnemyAction action)
        {
            var hp = Math.Max(1, enemy.Hp / 2);
            enemy.LoseHp(enemy.Hp); enemy.SplitDone = true; action.Split = true;
            var definition = EnemyCatalog.Definition(x.SplitInto);
            var n = Math.Min(Math.Max(1, x.SplitCount), EnemyField.Free(enemies, QueuedDefinitions(), EnemyField.IsLarge(definition)));
            if (n < x.SplitCount) action.SummonBlocked = true;
            for (var i = 0; i < n; i++)
                arrivals.Add(new QueuedArrival { Definition = definition, SummonerId = enemy.Id, HpFactor = 1.0, PaysGold = true, ExactHp = hp });
        }

        private void Swallow(EnemyState enemy, EnemyAction action)
        {
            bool Edible(CardDefinition c) => !c.Id.StartsWith("status_", StringComparison.Ordinal) && c.Id != "t_daga_oculta";
            var pile = discard.Any(Edible) ? discard : draw.Any(Edible) ? draw : null;
            if (pile == null) return;
            var candidates = pile.Where(Edible).ToList();
            var card = candidates[random.Next(candidates.Count)];
            pile.Remove(card); enemy.Stomach.Add(card); enemy.Statuses.Add(StatusKind.Swallowed, 1);
            action.Swallowed.Add(card.Name);
        }

        /// <summary>Arrivals join the fight in queue order with ids "&lt;summoner&gt;_s&lt;n&gt;"; they are planned (step 0) right after.</summary>
        private void AppendQueuedSummons()
        {
            if (arrivals.Count == 0) return;
            foreach (var a in arrivals)
            {
                var summoner = enemies.FirstOrDefault(e => e.Id == a.SummonerId);
                var id = a.SummonerId + "_s" + (++summonCounter);
                var floor = summoner?.Floor ?? 1;
                var arrived = EnemyCatalog.CreateSummon(a.Definition.Ai, floor, id, a.SummonerId, a.HpFactor, a.PaysGold, a.ExactHp);
                if (a.Revive && summoner != null)
                {
                    arrived.LoseHp(arrived.MaxHp / 2);
                    var original = enemies.FirstOrDefault(e => e.Id == summoner.PartnerId);
                    if (original != null) original.PartnerId = null;
                    arrived.PartnerId = summoner.Id; arrived.PairName = summoner.PairName;
                    summoner.PartnerId = id; summoner.PartnerHandled = false; summoner.Step = 0;
                }
                else if (summoner != null) arrived.PairName = summoner.PairName;
                enemies.Add(arrived);
                foreach (var enemy in enemies)
                    if (enemy.Id == a.SummonerId) { var log = enemyLog.FirstOrDefault(l => l.Index == enemies.IndexOf(enemy)); if (log != null) { if (a.Revive) log.Revived = id; else log.Summoned.Add(id); } break; }
            }
            arrivals.Clear();
        }

        /// <summary>Deaths not handled yet, in list order: swallowed cards return to the discard pile and Carroña feeds.</summary>
        private void HandleEnemyDeaths()
        {
            foreach (var dead in enemies)
            {
                if (dead.Alive || !deathsHandled.Add(dead.Id)) continue;
                var ev = new EnemyDeathEvent { EnemyId = dead.Id };
                foreach (var card in dead.Stomach) { discard.Add(card); ev.ReturnedCards.Add(card.Name); }
                dead.Stomach.Clear(); dead.Statuses.Remove(StatusKind.Swallowed);
                if (dead.PaysGold)   // summoned minions do not feed Carroña (no farming)
                    foreach (var feeder in enemies)
                    {
                        var carrion = feeder.Statuses[StatusKind.Carrion];
                        if (feeder == dead || !feeder.Alive || carrion <= 0) continue;
                        feeder.Statuses.Add(StatusKind.Strength, carrion);
                        var healed = feeder.Heal(Saturate(3L * carrion));
                        ev.Carrion.Add((feeder.Id, carrion, healed));
                    }
                if (ev.ReturnedCards.Count > 0 || ev.Carrion.Count > 0 || EnemyCatalog.IsExpansion(dead.Definition.Ai)) deathEvents.Add(ev);
            }
        }
    }
}
