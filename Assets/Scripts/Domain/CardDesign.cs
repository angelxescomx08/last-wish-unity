using System.Collections.Generic;
using System.Linq;

namespace LastWish.Domain
{
    /// <summary>Card design changes of the Unity version on top of the cards exported from the Python original.
    /// NumericCards.Generated.cs and Data/cards.json stay as exported; RunContent.Cards and the card faces apply these.</summary>
    public static class CardDesign
    {
        /// <summary>A power keeps acting between turns. "Powers" that only did something once are skills.</summary>
        public static readonly IReadOnlyDictionary<string, CardKind> KindChanges = new Dictionary<string, CardKind>
        {
            ["ep_poder_oculto"] = CardKind.Skill,   // draws 4 once
            ["m_concentracion"] = CardKind.Skill,   // draws 2 once
        };

        /// <summary>Card text changes (Spanish, lower-case like the exported texts).</summary>
        public static readonly IReadOnlyDictionary<string, string> TextChanges = new Dictionary<string, string>
        {
            ["r_sombra_gemela"] = "tu primer Combo de cada turno se duplica",
        };

        public static IReadOnlyList<CardDefinition> Apply(IReadOnlyList<CardDefinition> cards) => cards.Select(Apply).ToList().AsReadOnly();

        public static CardDefinition Apply(CardDefinition card) =>
            card != null && KindChanges.TryGetValue(card.Id, out var kind) && card.Kind != kind ? card.WithKind(kind) : card;

        /// <summary>The type name the card faces use ("ATTACK", "SKILL", "POWER", "STATUS").</summary>
        public static string TypeName(CardKind kind)
        {
            switch (kind) { case CardKind.Attack: return "ATTACK"; case CardKind.Power: return "POWER"; case CardKind.Status: return "STATUS"; default: return "SKILL"; }
        }
    }
}
