using System;

namespace LastWish.Domain
{
    /// <summary>
    /// Combat rules of the floor-3 expansion: Congelación at the end of the hero's turn, Agotamiento at its start, Cólera
    /// spent after the enemy acts, the Autómatas that wind up El Relojero and Rebobinar. Perforante lives in the enemy's
    /// hits (<see cref="HitHero"/>), Cólera's gain in <see cref="EnemyState.Damage"/> and Pendón / Inmortal in
    /// <see cref="EnemyPatterns.Floor3Auras"/>. With no floor-3 enemy in the fight every hook here is a no-op.
    /// </summary>
    public sealed partial class CombatState
    {
        /// <summary>HP the hero lost to Congelación at the end of the last turn (cards left in the hand).</summary>
        public int FrostbiteLost { get; private set; }
        /// <summary>Mana Agotamiento took at the start of this turn.</summary>
        public int ManaExhausted { get; private set; }

        /// <summary>After a card: the Strength each enemy gained from Cólera.</summary>
        private void AfterCardFloor3(PlayResult result)
        {
            foreach (var enemy in enemies)
            {
                if (enemy.RageGained > 0) result.RageGains[enemy.Id] = enemy.RageGained;
                enemy.RageGained = 0;
            }
        }

        /// <summary>Congelación: 1 HP per card left in the hand, through block (not an attack).</summary>
        private int FrostbiteHero(int cards)
        {
            var lost = Invincible ? 0 : Math.Min(Hp, Math.Max(0, cards)); Hp -= lost; OnHpLost(lost, true); Revive();
            return lost;
        }

        /// <summary>End of an enemy's turn (not frozen): it spends its Cólera Strength; an Autómata winds up its Relojero.</summary>
        private void AfterActFloor3(EnemyState enemy, EnemyAction action)
        {
            if (!enemy.Alive) return;
            if (enemy.RageStrength > 0)
            {
                var spent = Math.Min(enemy.RageStrength, enemy.Statuses[StatusKind.Strength]);
                enemy.Statuses.Set(StatusKind.Strength, enemy.Statuses[StatusKind.Strength] - spent);
                action.RageSpent = spent; enemy.RageStrength = 0;
            }
            if (action.Stunned || enemy.Definition.Ai != "automata" || string.IsNullOrEmpty(enemy.SummonerId)) return;
            foreach (var maker in enemies)
                if (maker.Id == enemy.SummonerId && maker.Alive && maker.Definition.Ai == "relojero")
                { maker.Statuses.Add(StatusKind.Spring, 1); action.WoundUp = maker.Id; break; }
        }

        /// <summary>Rebobinar: it heals all the HP it lost since the hero's turn began.</summary>
        private void Rewind(EnemyState enemy, EnemyAction action)
        {
            var back = enemy.Heal(Math.Max(0, enemy.HpAtTurnStart - enemy.Hp));
            action.Rewound = back; action.Healed += back;
        }
    }
}
