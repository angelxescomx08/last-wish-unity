using System;
using System.Collections.Generic;
using System.Linq;

namespace LastWish.Domain
{
    public sealed class RelicDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public int Rarity { get; }
        public string Tag { get; }
        public string ClassId { get; }
        public IReadOnlyList<string> CardClasses { get; }
        public RelicDefinition(string id, string name, string description, int rarity, string tag = "", string classId = "neutral", string[] cardClasses = null)
        { Id = id; Name = name; Description = description; Rarity = rarity; Tag = tag; ClassId = classId; CardClasses = Array.AsReadOnly(cardClasses ?? Array.Empty<string>()); }
    }

    public enum PackTheme { Acero, Escudo, Magia, Epico }

    public sealed class PackDefinition
    {
        public PackTheme Theme { get; }
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public int Cost { get; }
        public PackDefinition(PackTheme theme, string id, string name, string description, int cost)
        { Theme = theme; Id = id; Name = name; Description = description; Cost = cost; }
    }

    /// <summary>A card offered by a reward or a pack (Card.lucky_drop marks luck's extra cards).</summary>
    public sealed class OfferedCard
    {
        public CardDefinition Card { get; }
        public bool Lucky { get; }
        public OfferedCard(CardDefinition card, bool lucky = false) { Card = card ?? throw new ArgumentNullException(nameof(card)); Lucky = lucky; }
    }

    /// <summary>A relic offered by a treasure, elite, boss, shop or gachapón, with its rolled chroma.</summary>
    public sealed class RelicOffer
    {
        public RelicDefinition Definition { get; }
        public ChromaKind Chroma { get; }
        public RelicOffer(RelicDefinition definition, ChromaKind chroma = ChromaKind.None) { Definition = definition ?? throw new ArgumentNullException(nameof(definition)); Chroma = chroma; }
    }

    public sealed class PackOffer
    {
        public PackDefinition Definition { get; }
        public ChromaKind Chroma { get; }
        public PackOffer(PackDefinition definition, ChromaKind chroma = ChromaKind.None) { Definition = definition; Chroma = chroma; }
    }

    public static class RunContent
    {
        public static readonly IReadOnlyList<RelicDefinition> Relics = RelicCatalog.All;
        public static readonly IReadOnlyList<CardDefinition> Cards = NumericCards.All;
        public static readonly IReadOnlyList<PackDefinition> Packs = Array.AsReadOnly(new[] {
            new PackDefinition(PackTheme.Acero, "acero", "Sobre de Acero", "Cartas de ataque", 75),
            new PackDefinition(PackTheme.Escudo, "escudo", "Sobre de Escudo", "Cartas defensivas", 75),
            new PackDefinition(PackTheme.Magia, "magia", "Sobre de Magia", "Cartas de magia mixta", 75),
            new PackDefinition(PackTheme.Epico, "epico", "Sobre Épico", "Cartas poderosas", 150),
        });
        public static readonly string[] ClassIds = { "neutral", "warrior", "mage", "rogue" };
        public static PackDefinition Pack(PackTheme theme) => Packs[(int)theme];
        public static string ClassOf(HeroKind kind) => kind.ToString().ToLowerInvariant();
        public static RelicDefinition RelicByTag(string tag) => Relics.FirstOrDefault(r => r.Tag == tag);
        public static RelicDefinition RelicById(string id) => Relics.FirstOrDefault(r => r.Id == id);

        public static string RarityLabel(int rarity)
        {
            switch (rarity) { case 1: return "Común"; case 2: return "Poco común"; case 3: return "Rara"; case 4: return "Épica"; default: return "Legendaria"; }
        }
        public static string ClassLabel(string classId)
        {
            switch (classId) { case "warrior": return "Guerrera"; case "mage": return "Mago"; case "rogue": return "Pícara"; default: return "Neutral"; }
        }

        public static List<CardDefinition> StarterDeck(HeroKind kind)
        {
            var cards = new List<CardDefinition>();
            var owner = ClassOf(kind);
            var rogue = kind == HeroKind.Rogue;
            var attackId = rogue ? "punalada_base" : kind == HeroKind.Mage ? "proyectil_base" : "espadazo_base";
            var attackName = rogue ? "Puñalada" : kind == HeroKind.Mage ? "Proyectil Arcano" : "Espadazo";
            var blockId = rogue ? "esquiva_base" : kind == HeroKind.Mage ? "egida_base" : "bloqueo_base";
            var blockName = rogue ? "Esquiva" : kind == HeroKind.Mage ? "Égida" : "Bloqueo";
            for (var i = 0; i < (kind == HeroKind.Warrior ? 5 : 4); i++) cards.Add(new CardDefinition(attackId, attackName, 1, 6, 0, classId: owner));
            for (var i = 0; i < (kind == HeroKind.Mage ? 5 : 4); i++) cards.Add(new CardDefinition(blockId, blockName, 1, 0, rogue ? 6 : 5, classId: owner));
            if (rogue) cards.Add(new CardDefinition("finta_base", "Finta", 1, 4, 0, classId: owner, comboBlock: 4, effects: new[] { new EffectLayer(EffectCondition.Always, 4), new EffectLayer(EffectCondition.Combo, 0, 4) }));
            else if (kind == HeroKind.Mage) cards.Add(new CardDefinition("chispa_ignea_base", "Chispa Ígnea", 1, 4, 0, classId: owner, fireStacks: 2, effects: new[] { new EffectLayer(EffectCondition.Always, 4, element: ElementKind.Fire, stacks: 2) }));
            else cards.Add(new CardDefinition("guardia_ferrea_base", "Guardia Férrea", 1, 0, 5, classId: owner, endureStrength: 1, effects: new[] { new EffectLayer(EffectCondition.Always, 0, 5), new EffectLayer(EffectCondition.Endure, 0, commands: new[] { new CardCommand(CommandKind.Strength, 1) }) }));
            return cards;
        }
        public static CardDefinition[] ForHero(HeroKind kind) => Cards.Where(c => c.ClassId == "neutral" || c.ClassId == ClassOf(kind)).ToArray();
    }
}
