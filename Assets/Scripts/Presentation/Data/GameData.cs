using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>Static face data of a card exported from the original (texts, type, keyword summaries, element chips).</summary>
    public sealed class CardInfo
    {
        public string Id, Name, Type, Class, Theme, Target;
        public int Rarity, Cost;
        public string[] Texts;
        public Dictionary<string, string> KeywordText = new Dictionary<string, string>();
        public string[] Badges;
        public (string element, int stacks, bool all)[] Chips;
        public bool NeedsTarget, HitsAll, Unplayable, Exhaust, Ethereal, PlayOnDraw, PowerTurn;
        public string[] Keywords;
        public bool IsStatus => Type == "STATUS";
        public bool HasKeyword(string k) => KeywordText.ContainsKey(k);
    }

    public sealed class RelicInfo
    {
        public string Tag, Id, Name, Description, Class, Sprite;
        public int Rarity;
    }

    /// <summary>glossary.Term</summary>
    public sealed class Term
    {
        public string Name, Rule, Icon;
        public Rgba Color;
        public Regex Pattern;
        public string[] Lines;
    }

    public sealed class EnemyInfo { public string Ai, Name, Identity, Title, Kind; }

    /// <summary>Everything exported from the Python game by Tools/ExportPresentationData.py.</summary>
    public sealed class GameData
    {
        public Dictionary<string, CardInfo> Cards { get; } = new Dictionary<string, CardInfo>();
        public Dictionary<string, RelicInfo> RelicsByTag { get; } = new Dictionary<string, RelicInfo>();
        public Dictionary<string, EnemyInfo> Enemies { get; } = new Dictionary<string, EnemyInfo>();
        public Dictionary<string, (string name, string synergy)> Pairs { get; } = new Dictionary<string, (string, string)>();
        public Glossary Glossary { get; private set; }

        public static Rgba Color(object o) { var a = Json.Ints(o); return new Rgba(a[0], a[1], a[2]); }

        public static Term ParseTerm(object o)
        {
            var d = Json.Obj(o);
            return new Term
            {
                Name = Json.Str(d.Get("name")), Rule = Json.Str(d.Get("rule")), Icon = Json.Str(d.Get("icon")), Color = Color(d.Get("color")),
                Pattern = new Regex(Json.Str(d.Get("pattern")), RegexOptions.CultureInvariant),
                Lines = Json.Arr(d.Get("lines")).Select(Json.Str).ToArray(),
            };
        }

        public GameData(IAssetSource source)
        {
            foreach (var pair in Json.Obj(Json.Parse(source.ReadText("Data/cards"))))
            {
                var c = Json.Obj(pair.Value);
                var info = new CardInfo
                {
                    Id = Json.Str(c.Get("id")), Name = Json.Str(c.Get("name")), Type = Json.Str(c.Get("type")), Rarity = Json.Int(c.Get("rarity")),
                    Cost = Json.Int(c.Get("cost")), Class = Json.Str(c.Get("class")), Theme = Json.Str(c.Get("theme")), Target = Json.Str(c.Get("target")),
                    Texts = Json.Arr(c.Get("texts")).Select(Json.Str).ToArray(), Badges = Json.Arr(c.Get("badges")).Select(Json.Str).ToArray(),
                    Chips = Json.Arr(c.Get("chips")).Select(x => { var a = Json.Arr(x); return (Json.Str(a[0]), Json.Int(a[1]), (bool)a[2]); }).ToArray(),
                    NeedsTarget = (bool)c.Get("needs_target"), HitsAll = (bool)c.Get("hits_all"), Unplayable = (bool)c.Get("unplayable"),
                    Exhaust = (bool)c.Get("exhaust"), Ethereal = (bool)c.Get("ethereal"), PlayOnDraw = (bool)c.Get("play_on_draw"), PowerTurn = (bool)c.Get("power_turn"),
                    Keywords = Json.Arr(c.Get("keywords")).Select(Json.Str).ToArray(),
                };
                foreach (var k in new[] { "combo", "singular", "void", "spoil", "reaction", "fury", "endure" })
                    if (c.Get(k) is string text) info.KeywordText[k] = text;
                Cards[pair.Key] = info;
            }
            foreach (var pair in Json.Obj(Json.Parse(source.ReadText("Data/relics"))))
            {
                var r = Json.Obj(pair.Value);
                RelicsByTag[pair.Key] = new RelicInfo { Tag = pair.Key, Id = Json.Str(r.Get("id")), Name = Json.Str(r.Get("name")), Description = Json.Str(r.Get("description")),
                    Rarity = Json.Int(r.Get("rarity")), Class = Json.Str(r.Get("class")), Sprite = Json.Str(r.Get("sprite")) };
            }
            var enemies = Json.Obj(Json.Parse(source.ReadText("Data/enemies")));
            foreach (var pair in Json.Obj(enemies.Get("enemies")))
            {
                var e = Json.Obj(pair.Value);
                Enemies[pair.Key] = new EnemyInfo { Ai = pair.Key, Name = Json.Str(e.Get("name")), Identity = Json.Str(e.Get("identity")), Title = Json.Str(e.Get("title")), Kind = Json.Str(e.Get("kind")) };
            }
            foreach (var pair in Json.Obj(enemies.Get("pairs")))
            { var p = Json.Obj(pair.Value); Pairs[pair.Key] = (Json.Str(p.Get("name")), Json.Str(p.Get("synergy"))); }
            Glossary = new Glossary(Json.Obj(Json.Parse(source.ReadText("Data/glossary"))));
        }

        /// <summary>Face data of a deck card (status cards and starter cards included).</summary>
        public CardInfo Card(CardDefinition card)
        {
            if (card != null && Cards.TryGetValue(card.Id, out var info)) return info;
            return new CardInfo { Id = card?.Id ?? "?", Name = card?.Name ?? "?", Type = card != null && card.IsAttack ? "ATTACK" : "SKILL", Rarity = card?.Rarity ?? 1,
                Texts = Array.Empty<string>(), Badges = Array.Empty<string>(), Chips = Array.Empty<(string, int, bool)>(), Keywords = Array.Empty<string>(), Target = "SELF" };
        }
        public RelicInfo Relic(string tag) => tag != null && RelicsByTag.TryGetValue(tag, out var r) ? r : null;
    }

    /// <summary>ui/glossary.py data + rich_text rules.</summary>
    public sealed class Glossary
    {
        public Rgba Keyword, Damage, Block, PoisonInk, Mana, BuffTitle, DebuffTitle, FireInk, WaterInk, EarthInk, AirInk, IceInk, ReactionInk, Dim, Body;
        public List<Term> AllTerms { get; } = new List<Term>();
        public Term Shield, ElementsRule, Exhaust, Ethereal, Unplayable;
        public Dictionary<string, Term> StatusTerms { get; } = new Dictionary<string, Term>();
        public Dictionary<string, Term> KeywordTerms { get; } = new Dictionary<string, Term>();
        public Dictionary<string, Term> ReactionTerms { get; } = new Dictionary<string, Term>();
        public Dictionary<string, bool> StatusIsBuff { get; } = new Dictionary<string, bool>();
        public Dictionary<string, string> StatusIcon { get; } = new Dictionary<string, string>();
        public Dictionary<string, string> StatusShort { get; } = new Dictionary<string, string>();
        public Dictionary<string, string> StatusText { get; } = new Dictionary<string, string>();
        public Dictionary<string, string> ReactionIcon { get; } = new Dictionary<string, string>();
        public Dictionary<string, Rgba> ReactionColor { get; } = new Dictionary<string, Rgba>();
        public Dictionary<string, string> ReactionName { get; } = new Dictionary<string, string>();
        public Dictionary<string, string> ReactionText { get; } = new Dictionary<string, string>();
        public string ReactionRule;
        public List<(string a, string b, string reaction)> Reactions { get; } = new List<(string, string, string)>();
        public Dictionary<string, Rgba> ElementColor { get; } = new Dictionary<string, Rgba>();
        public Dictionary<string, (string name, string rule)> KeywordDefs { get; } = new Dictionary<string, (string, string)>();
        public List<(Regex pattern, Rgba color)> RichRules { get; } = new List<(Regex, Rgba)>();
        /// <summary>Spanish status name per engine status (POISON → "Veneno").</summary>
        public Dictionary<StatusKind, string> StatusName { get; } = new Dictionary<StatusKind, string>();
        private List<(Regex pattern, Rgba color)> highlights;

        public Glossary(Dictionary<string, object> d)
        {
            var c = Json.Obj(d.Get("colors"));
            Keyword = GameData.Color(c.Get("KEYWORD")); Damage = GameData.Color(c.Get("DAMAGE")); Block = GameData.Color(c.Get("BLOCK"));
            PoisonInk = GameData.Color(c.Get("POISON_INK")); Mana = GameData.Color(c.Get("MANA")); BuffTitle = GameData.Color(c.Get("BUFF_TITLE"));
            DebuffTitle = GameData.Color(c.Get("DEBUFF_TITLE")); FireInk = GameData.Color(c.Get("FIRE_INK")); WaterInk = GameData.Color(c.Get("WATER_INK"));
            EarthInk = GameData.Color(c.Get("EARTH_INK")); AirInk = GameData.Color(c.Get("AIR_INK")); IceInk = GameData.Color(c.Get("ICE_INK"));
            ReactionInk = GameData.Color(c.Get("REACTION_INK")); Dim = GameData.Color(c.Get("DIM")); Body = GameData.Color(c.Get("BODY"));
            foreach (var t in Json.Arr(d.Get("all_terms"))) AllTerms.Add(GameData.ParseTerm(t));
            Shield = GameData.ParseTerm(d.Get("shield")); ElementsRule = GameData.ParseTerm(d.Get("elements_rule"));
            Exhaust = GameData.ParseTerm(d.Get("exhaust")); Ethereal = GameData.ParseTerm(d.Get("ethereal")); Unplayable = GameData.ParseTerm(d.Get("unplayable"));
            foreach (var p in Json.Obj(d.Get("status_terms"))) StatusTerms[p.Key] = GameData.ParseTerm(p.Value);
            foreach (var p in Json.Obj(d.Get("keyword_terms"))) KeywordTerms[p.Key] = GameData.ParseTerm(p.Value);
            foreach (var p in Json.Obj(d.Get("reaction_terms"))) ReactionTerms[p.Key] = GameData.ParseTerm(p.Value);
            foreach (var p in Json.Obj(d.Get("status_kind"))) StatusIsBuff[p.Key] = (bool)p.Value;
            foreach (var p in Json.Obj(d.Get("status_icon"))) StatusIcon[p.Key] = Json.Str(p.Value);
            foreach (var p in Json.Obj(d.Get("status_short"))) StatusShort[p.Key] = Json.Str(p.Value);
            foreach (var p in Json.Obj(d.Get("status_text"))) StatusText[p.Key] = Json.Str(p.Value);
            foreach (var p in Json.Obj(d.Get("reaction_icon"))) ReactionIcon[p.Key] = Json.Str(p.Value);
            foreach (var p in Json.Obj(d.Get("reaction_color"))) ReactionColor[p.Key] = GameData.Color(p.Value);
            foreach (var p in Json.Obj(d.Get("reaction_names"))) ReactionName[p.Key] = Json.Str(p.Value);
            foreach (var p in Json.Obj(d.Get("reaction_text"))) ReactionText[p.Key] = Json.Str(p.Value);
            ReactionRule = Json.Str(d.Get("reaction_rule"));
            foreach (var r in Json.Arr(d.Get("reactions"))) { var a = Json.Arr(r); Reactions.Add((Json.Str(a[0]), Json.Str(a[1]), Json.Str(a[2]))); }
            foreach (var p in Json.Obj(d.Get("element_color"))) ElementColor[p.Key] = GameData.Color(p.Value);
            foreach (var p in Json.Obj(d.Get("keyword_defs"))) { var k = Json.Obj(p.Value); KeywordDefs[p.Key] = (Json.Str(k.Get("name")), Json.Str(k.Get("rule"))); }
            foreach (var r in Json.Arr(d.Get("rich_rules"))) { var a = Json.Arr(r); RichRules.Add((new Regex(Json.Str(a[0]), RegexOptions.CultureInvariant), GameData.Color(a[1]))); }
            var names = Json.Obj(d.Get("status_names"));
            var map = new Dictionary<string, StatusKind> {
                { "POISON", StatusKind.Poison }, { "MARKED", StatusKind.Marked }, { "WEAK", StatusKind.Weak }, { "VULNERABLE", StatusKind.Vulnerable },
                { "FRAIL", StatusKind.Frail }, { "ENTANGLED", StatusKind.Entangled }, { "STRENGTH", StatusKind.Strength }, { "BLADES", StatusKind.Blades },
                { "FUSE", StatusKind.Fuse }, { "FIRE", StatusKind.Fire }, { "WATER", StatusKind.Water }, { "EARTH", StatusKind.Earth }, { "AIR", StatusKind.Air }, { "FROZEN", StatusKind.Frozen } };
            foreach (var p in names) StatusName[map[p.Key]] = Json.Str(p.Value);
            GlossaryExpansion.Apply(this);
            highlights = AllTerms.Select(t => (t.Pattern, t.Color)).ToList();
        }

        public List<(Regex pattern, Rgba color)> HighlightWords => highlights;

        public string StatusIconOf(string name, bool isBuff = false) => StatusIcon.TryGetValue(name, out var icon) ? icon : isBuff ? "status_buff" : "status_debuff";
        public bool IsBuffStatus(string name, bool fallback = false) => StatusIsBuff.TryGetValue(name, out var b) ? b : fallback;

        public Term StatusTerm(string name, bool? isBuff = null)
        {
            if (StatusTerms.TryGetValue(name, out var term)) return term;
            var buff = StatusIsBuff.TryGetValue(name, out var b) ? b : isBuff ?? false;
            return new Term { Name = name, Rule = buff ? "Mejora temporal." : "Perjuicio temporal.", Icon = StatusIconOf(name, buff), Color = Keyword,
                Pattern = new Regex(Regex.Escape(name)), Lines = Array.Empty<string>() };
        }

        /// <summary>glossary.terms_in: terms mentioned in the texts, first mention first.</summary>
        public List<Term> TermsIn(IEnumerable<string> texts, ICollection<string> exclude = null, bool includeShield = false)
        {
            var joined = string.Join("\n", texts);
            var found = new List<(int pos, int order, Term term)>();
            var order = 0;
            foreach (var term in includeShield ? AllTerms.Concat(new[] { Shield }) : AllTerms)
            {
                order++;
                if (exclude != null && exclude.Contains(term.Name)) continue;
                var m = term.Pattern.Match(joined);
                if (m.Success) found.Add((m.Index, order, term));
            }
            return found.OrderBy(f => f.pos).ThenBy(f => f.order).Select(f => f.term).ToList();
        }

        /// <summary>glossary.status_short</summary>
        public string StatusShortText(string name, int stacks)
        {
            if (!StatusShort.TryGetValue(name, out var text))
            {
                var rule = StatusText.TryGetValue(name, out var r) ? r : "";
                text = rule.Length > 0 ? rule.Split(new[] { ". " }, StringSplitOptions.None)[0].TrimEnd('.') : (IsBuffStatus(name) ? "mejora" : "perjuicio");
                if (text.Length > 0) text = char.ToLowerInvariant(text[0]) + text.Substring(1);
            }
            return name + " " + stacks + ": " + text.Replace("{n}", stacks.ToString());
        }
    }
}
