using LastWish.Domain;
namespace LastWish.Application
{
    public sealed class BattleSession
    {
        public CombatState State { get; }
        public BattleSession(HeroKind hero, int seed) { State = DemoBattle.Create(hero, seed); }
        public BattleSession(CombatState state) { State = state ?? throw new System.ArgumentNullException(nameof(state)); }
        public bool CanPlay(int index) => State.CanPlay(index);
        public bool PlayCard(int index) => State.PlayCard(index);
        public bool EndTurn() => State.EndTurn();
    }
}
