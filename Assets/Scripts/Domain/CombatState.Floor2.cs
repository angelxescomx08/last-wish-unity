using System;
using System.Collections.Generic;

namespace LastWish.Domain
{
    /// <summary>
    /// Combat rules of the floor-2 expansion: Reflejo and Cabezas after a card, Sangrado, Ansia de Sangre, Rito and
    /// Cicatrización at the end of an enemy's action, Espejo Maldito, Ofrenda Oscura, Rencor and the Ilusiones that vanish
    /// with their summoner. Amparo lives in <see cref="EnemyState.Damage"/> and Necrosis in the hero's healing.
    /// With no floor-2 enemy in the fight every hook here is a no-op.
    /// </summary>
    public sealed partial class CombatState
    {
        private bool thirstFed;
        private List<(string enemyId, int strength)> thirstFeeds = new List<(string, int)>();
        private HashSet<string> dispelled = new HashSet<string>();

        /// <summary>Ansia de Sangre since the last card or end of turn: who gained how much Strength.</summary>
        public IReadOnlyList<(string enemyId, int strength)> ThirstFeeds => thirstFeeds;

        private static readonly StatusKind[] Mirrored = { StatusKind.Poison, StatusKind.Weak, StatusKind.Vulnerable };

        /// <summary>After a card (enemies still alive): heads cut, Reflejo, Sangrado, then Ansia de Sangre.</summary>
        private void AfterCardFloor2(PlayResult result)
        {
            foreach (var enemy in enemies)
            {
                if (enemy.HeadsCut > 0) result.HeadsCut[enemy.Id] = enemy.HeadsCut;
                enemy.HeadsCut = 0;
            }
            foreach (var enemy in enemies)
            {
                if (enemy.ReflectPending <= 0) continue;
                var damage = enemy.ReflectPending; enemy.ReflectPending = 0;
                if (!Defeat) result.HeroHits.Add((enemy.Id, HitHeroDirect(damage, true), "reflect"));
            }
            if (HeroStatuses[StatusKind.Bleed] > 0 && !Defeat) result.HeroHits.Add(("", BleedHero(), "bleed"));
            FeedThirst();
        }

        /// <summary>Rencor, Reflejo: not an attack (no decoys, defence relics or Corona de Espinas); block absorbs it.</summary>
        private int HitHeroDirect(int damage, bool ownTurn)
        {
            if (damage <= 0) return 0;
            var absorbed = Math.Min(Block, damage); Block -= absorbed;
            var lost = Invincible ? 0 : Math.Min(Hp, damage - absorbed); Hp -= lost; OnHpLost(lost, ownTurn); Revive();
            return lost;
        }

        /// <summary>Sangrado: 1 HP per card played, through block.</summary>
        private int BleedHero()
        {
            var lost = Invincible ? 0 : Math.Min(Hp, 1); Hp -= lost; OnHpLost(lost, true); Revive();
            return lost;
        }

        /// <summary>Ansia de Sangre: the first time in a turn the hero has lost HP, each thirsty enemy gains its stacks of Strength.</summary>
        private void FeedThirst()
        {
            if (thirstFed || HpLostThisTurn <= 0) return;
            thirstFed = true;
            foreach (var enemy in enemies)
            {
                var n = enemy.Statuses[StatusKind.Thirst];
                if (!enemy.Alive || n <= 0) continue;
                enemy.Statuses.Add(StatusKind.Strength, n);
                thirstFeeds.Add((enemy.Id, n));
            }
        }

        /// <summary>End of an enemy's action (it acted and is alive): Rito gives Strength, Cicatrización heals unless it burns or is poisoned.</summary>
        private void AfterActFloor2(EnemyState enemy, EnemyAction action, int ritual)
        {
            if (!enemy.Alive) return;
            if (ritual > 0) { enemy.Statuses.Add(StatusKind.Strength, ritual); action.RitualStrength = ritual; }
            var regen = enemy.Statuses[StatusKind.Regen];
            if (regen > 0 && enemy.Statuses[StatusKind.Fire] == 0 && enemy.Statuses[StatusKind.Poison] == 0)
                action.Regenerated = enemy.Heal(regen);
        }

        /// <summary>Espejo Maldito: its Veneno, Débil and Vulnerable move to the hero (relics that block debuffs still work).</summary>
        private void ReturnDebuffs(EnemyState enemy, EnemyAction action)
        {
            foreach (var kind in Mirrored)
            {
                var n = enemy.Statuses[kind];
                if (n <= 0) continue;
                enemy.Statuses.Remove(kind);
                action.DebuffsReturned = Saturate((long)action.DebuffsReturned + n);
                if (!BlocksDebuff(kind)) { HeroStatuses.Add(kind, n); action.Debuffs.Add(new StatusAmount(kind, n)); }
            }
            enemy.Statuses.Remove(StatusKind.Marked);
        }

        /// <summary>Ofrenda Oscura: its living minion with the most Strength dies; it takes that Strength plus a bonus and heals.</summary>
        private void Devour(EnemyState enemy, IntentExtras x, EnemyAction action)
        {
            var victim = EnemyPatterns.DevourVictim(enemy, enemies);
            if (victim == null) return;
            var strength = victim.Statuses[StatusKind.Strength];
            victim.LoseHp(victim.Hp);
            var gain = Saturate((long)strength + x.DevourStrength);
            enemy.Statuses.Add(StatusKind.Strength, gain);
            action.Healed += enemy.Heal(x.DevourHeal);
            action.Devoured = victim.Id; action.DevouredStrength = gain;
        }

        /// <summary>A death: Rencor hits the hero, the summoner counts a lost minion, a vanished Ilusión is marked.</summary>
        private void OnDeathFloor2(EnemyState dead, EnemyDeathEvent ev, bool ownTurn)
        {
            if (dispelled.Contains(dead.Id)) ev.Dispelled = true;
            var grudge = dead.Statuses[StatusKind.Grudge];
            if (grudge > 0 && !Defeat) { ev.GrudgeDamage = grudge; ev.GrudgeLost = HitHeroDirect(grudge, ownTurn); }
            if (dead.Summoned && !string.IsNullOrEmpty(dead.SummonerId))
                foreach (var e in enemies) if (e.Id == dead.SummonerId) { e.MinionsLost++; break; }
        }

        /// <summary>The bound minions of a dead summoner vanish. True if any did (they die now and are handled next).</summary>
        private bool DispelBound(EnemyState dead)
        {
            var any = false;
            foreach (var e in enemies)
                if (e.Alive && e.Bound && e.SummonerId == dead.Id) { dispelled.Add(e.Id); e.LoseHp(e.Hp); any = true; }
            return any;
        }
    }
}
