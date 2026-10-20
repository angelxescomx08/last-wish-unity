using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>A small glossary box under the main panel.</summary>
    public sealed class TooltipPanel
    {
        public string Title; public List<string> Lines; public string Icon; public Rgba Color; public string Tag = ""; public string Key = "";
        public TooltipPanel(string title, IEnumerable<string> lines, string icon, Rgba color, string tag = "", string key = "")
        { Title = title; Lines = new List<string>(lines ?? Array.Empty<string>()); Icon = icon; Color = color; Tag = tag ?? ""; Key = key ?? ""; }
    }

    public sealed class TooltipContent
    {
        public string Title; public List<string> Lines = new List<string>(); public string Icon; public string Subtitle = ""; public string Tag = "";
        public Rgba? Accent; public List<TooltipPanel> Panels = new List<TooltipPanel>();
        public TooltipContent(string title) { Title = title ?? ""; }

        /// <summary>Every visible string (searches and the layout cache key).</summary>
        public string AllText()
        {
            var sb = new StringBuilder();
            void Add(string s) { if (!string.IsNullOrEmpty(s)) sb.Append(Tooltip.StripIcon(s)).Append('\n'); }
            Add(Title); Add(Subtitle); Add(Tag); foreach (var l in Lines) Add(l);
            foreach (var p in Panels) { Add(p.Title); Add(p.Tag); foreach (var l in p.Lines) Add(l); }
            return sb.ToString();
        }

        internal string Key()
        {
            var sb = new StringBuilder();
            sb.Append(Title).Append('\u0001').Append(Icon).Append('\u0001').Append(Subtitle).Append('\u0001').Append(Tag).Append('\u0001').Append(Accent?.ToString());
            foreach (var l in Lines) sb.Append('\u0002').Append(l);
            foreach (var p in Panels)
            {
                sb.Append('\u0003').Append(p.Title).Append('\u0001').Append(p.Icon).Append('\u0001').Append(p.Color.ToString()).Append('\u0001').Append(p.Tag);
                foreach (var l in p.Lines) sb.Append('\u0002').Append(l);
            }
            return sb.ToString();
        }
    }

    /// <summary>ui/tooltip.py: content builders and the stacked renderer (layouts cached by content).</summary>
    public static class Tooltip
    {
        private static readonly Regex IconPrefix = new Regex(@"^\[\[(\w+)\]\]\s*");
        public static string StripIcon(string line) => IconPrefix.Replace(line, "");

        public static readonly Dictionary<string, string> CardClassLabel = new Dictionary<string, string>
        { ["neutral"] = "Neutral", ["warrior"] = "La Guerrera", ["mage"] = "El Mago", ["rogue"] = "La Pícara" };
        private static readonly Dictionary<string, string> TypeName = new Dictionary<string, string> { ["ATTACK"] = "Ataque", ["SKILL"] = "Habilidad", ["POWER"] = "Poder", ["STATUS"] = "Estado" };
        private static readonly Dictionary<string, string> TypeIcon = new Dictionary<string, string> { ["ATTACK"] = "damage", ["SKILL"] = "block", ["POWER"] = "ritual", ["STATUS"] = "junk_card" };
        private static readonly string[] HeroTimedDebuffs = { "Débil", "Vulnerable", "Frágil" };

        private static TooltipPanel TermPanel(Term term, string tag = "")
        {
            var lines = new List<string> { term.Rule }; lines.AddRange(term.Lines ?? Array.Empty<string>());
            return new TooltipPanel(term.Name, lines, term.Icon, term.Color, tag);
        }

        private static void AddGlossary(Glossary g, TooltipContent content, bool includeShield = false)
        {
            var have = new HashSet<string>();
            foreach (var p in content.Panels) have.Add(string.IsNullOrEmpty(p.Key) ? p.Title : p.Key);
            var texts = new List<string> { content.Title }; texts.AddRange(content.Lines);
            foreach (var p in content.Panels) texts.AddRange(p.Lines);
            foreach (var term in g.TermsIn(texts, have, includeShield))
            {
                if (have.Contains(term.Name)) continue;
                content.Panels.Add(TermPanel(term)); have.Add(term.Name);
            }
        }

        private static string Sentence(string text) => char.ToUpperInvariant(text[0]) + text.Substring(1) + (text.EndsWith(".") ? "" : ".");

        // ------------------------------------------------------------------ cards
        private static readonly (string key, EffectCondition cond)[] KeywordOrder =
        { ("combo", EffectCondition.Combo), ("endure", EffectCondition.Endure), ("fury", EffectCondition.Fury), ("reaction", EffectCondition.Reaction),
          ("singular", EffectCondition.Singular), ("spoil", EffectCondition.Spoil), ("void", EffectCondition.Void) };

        public static TooltipContent Card(GameContext game, CardDefinition card, BigInteger bonusDamage, long bonusBlock = 0, uint active = 0)
        {
            var g = game.Data.Glossary; var info = game.Data.Card(card);
            var mask = CardTotals.Normalize(card, active);
            var type = CardWidget.TypeOf(game, card);
            var cost = info.Unplayable ? "Injugable" : "Coste: " + card.Cost + " maná";
            var content = new TooltipContent(ChromaFx.Title(card.Name, card.Chroma))
            {
                Icon = TypeIcon.TryGetValue(type, out var ti) ? ti : null,
                Subtitle = (TypeName.TryGetValue(type, out var tn) ? tn : type) + "  ·  " + cost + "  ·  " + (CardClassLabel.TryGetValue(card.ClassId ?? "neutral", out var cl) ? cl : "Neutral"),
            };
            var lines = content.Lines;
            var dmg = CardTotals.Damage(card, mask); var blk = CardTotals.Block(card, mask);
            if (dmg > 0)
            {
                lines.Add("Inflige " + Numbers.FormatInt(dmg + bonusDamage) + " de daño a un enemigo.");
                if (bonusDamage > 0) lines.Add("  (" + Numbers.FormatInt(dmg) + " base + " + bonusDamage + " bonus)");
            }
            if (blk > 0)
            {
                lines.Add("Gana " + Numbers.FormatInt(blk + bonusBlock) + " de escudo.");
                if (bonusBlock > 0) lines.Add("  (" + Numbers.FormatInt(blk) + " base + " + bonusBlock + " destreza)");
            }
            var draw = CardTotals.Draw(card, mask);
            if (draw > 0) lines.Add("Roba " + draw + " carta" + (draw > 1 ? "s" : "") + ".");
            var texts = info.Texts ?? Array.Empty<string>();
            foreach (var (element, stacks, all) in info.Chips)
            {
                var named = false; foreach (var t in texts) if (t.Contains("de " + element)) named = true;
                if (named) continue;
                lines.Add("[[" + g.StatusIconOf(element) + "]]Aplica " + stacks + " de " + element + (all ? " a todos los enemigos" : "") + ".");
            }
            var anyText = false;
            foreach (var t in texts) if (!string.IsNullOrEmpty(t)) { lines.Add(Sentence(t)); anyText = true; }
            if (info.PlayOnDraw || card.PlayOnDraw) lines.Add("Se juega sola al robarla y desaparece.");
            if (card.Innate) lines.Add("Marcada: empieza cada combate en tu mano.");
            if (dmg == 0 && blk == 0 && draw == 0 && !anyText && info.Chips.Length == 0)
                lines.Add(type == "POWER" ? "Poder permanente: efecto especial." : "Efecto especial.");

            foreach (var (key, cond) in KeywordOrder)
            {
                if (!info.KeywordText.TryGetValue(key, out var kt) || !g.KeywordTerms.TryGetValue(key, out var term)) continue;
                var on = (mask & CardTotals.Bit(cond)) != 0;
                content.Panels.Add(new TooltipPanel(term.Name, new[] { term.Rule, "Efecto extra: " + kt + "." }, term.Icon, on ? new Rgba(150, 240, 140) : term.Color, on ? "¡Activo!" : ""));
            }
            if (info.Exhaust) content.Panels.Add(TermPanel(g.Exhaust));
            if (info.Unplayable) content.Panels.Add(TermPanel(g.Unplayable));
            if (info.Ethereal) content.Panels.Add(TermPanel(g.Ethereal));
            if (card.Chroma != ChromaKind.None)
            {
                var st = ChromaFx.Style(card.Chroma);
                content.Panels.Add(new TooltipPanel(st.Name, new[] { st.CardNote }, "singular", new Rgba(255, 222, 120)));
            }
            AddGlossary(g, content);
            return content;
        }

        /// <summary>card_rule_lines: the card's own effect sentences (status-card panels).</summary>
        public static List<string> CardRuleLines(GameContext game, CardDefinition card)
        {
            var list = new List<string>();
            foreach (var t in game.Data.Card(card).Texts) if (!string.IsNullOrEmpty(t)) list.Add(Sentence(t));
            return list;
        }

        // ------------------------------------------------------------------ enemies and intents
        private static readonly Dictionary<string, string> CardPile = new Dictionary<string, string> { ["draw"] = "tu pila de robo", ["discard"] = "tu pila de descarte", ["hand"] = "tu mano" };

        public static string AttackIcon(long total) => total < 8 ? "attack_1" : total < 16 ? "attack_2" : total < 26 ? "attack_3" : "attack_4";
        public static string IntentIcon(EnemyIntent it, int? hitDamage = null)
        {
            if (it.Kind == IntentKind.Attack) return AttackIcon((long)(hitDamage ?? it.Value) * Math.Max(1, it.Hits));
            switch (it.Kind)
            {
                case IntentKind.Block: return "defend"; case IntentKind.Buff: return "buff"; case IntentKind.Debuff: return "debuff";
                case IntentKind.Summon: return "ritual"; case IntentKind.Stun: return "unplayable"; default: return "unknown";
            }
        }
        public static Rgba IntentAccent(EnemyIntent it)
        {
            switch (it.Kind)
            {
                case IntentKind.Attack: return new Rgba(255, 120, 100); case IntentKind.Block: return new Rgba(120, 180, 255);
                case IntentKind.Buff: return new Rgba(130, 200, 255); case IntentKind.Debuff: return new Rgba(220, 150, 255);
                default: return new Rgba(190, 190, 200);
            }
        }
        private static string StatusNameOf(Glossary g, StatusKind k) => g.StatusName.TryGetValue(k, out var n) ? n : k.ToString();

        private static string DamageBreakdown(Glossary g, EnemyState enemy, StatusSet hero)
        {
            var parts = new List<string>();
            var strength = enemy.Statuses[StatusKind.Strength];
            if (strength != 0) parts.Add("+" + strength + " Fuerza");
            if (enemy.Statuses[StatusKind.Weak] > 0) parts.Add("Débil −25 %");
            var water = enemy.Statuses[StatusKind.Water];
            if (water != 0) parts.Add("−" + water + " Agua");
            if (enemy.Statuses[StatusKind.Frozen] > 0) parts.Add(enemy.Definition.Boss || enemy.Definition.Elite ? "Congelado: mitad" : "Congelado: no actúa");
            if (hero != null && hero[StatusKind.Vulnerable] > 0) parts.Add("tu Vulnerable +50 %");
            return parts.Count > 0 ? "  Base " + enemy.Intent.Value + " por golpe · " + string.Join(" · ", parts) : "";
        }

        public static List<string> IntentLines(GameContext game, EnemyState enemy, int? hitDamage, CombatState player)
        {
            var g = game.Data.Glossary; var it = enemy.Intent; var lines = new List<string>();
            if (it.Kind == IntentKind.Attack)
            {
                long dmg = hitDamage ?? it.Value; var hits = Math.Max(1, it.Hits); var total = dmg * hits;
                var icon = AttackIcon(total);
                lines.Add(hits > 1 ? "[[" + icon + "]]Te atacará " + hits + " veces de " + dmg + " de daño (" + total + " de daño en total)." : "[[" + icon + "]]Te atacará por " + dmg + " de daño.");
                var note = DamageBreakdown(g, enemy, player?.HeroStatuses);
                if (note.Length > 0) lines.Add(note);
                if (player != null)
                {
                    var stopped = Math.Min(player.Block, total); var lost = total - stopped;
                    if (stopped > 0) lines.Add("  Tu escudo para " + stopped + ": perderías " + lost + " de vida.");
                    if (player.Hp > 0 && lost >= player.Hp) lines.Add("[[lethal]]¡Este ataque te matará si no te proteges!");
                }
            }
            else if (it.Kind == IntentKind.Block) lines.Add("[[defend]]Ganará " + it.Value + " de escudo.");
            else if (it.Kind == IntentKind.Buff && (it.Buffs == null || it.Buffs.Length == 0)) lines.Add("[[buff]]Va a fortalecerse.");
            else if (it.Kind == IntentKind.Debuff && (it.Debuffs == null || it.Debuffs.Length == 0)) lines.Add("[[debuff]]Va a debilitarte.");
            else if (it.Kind == IntentKind.Unknown) lines.Add("[[unknown]]No se sabe qué hará.");
            else if (it.Kind == IntentKind.Stun) lines.Add("[[unplayable]]Está Aturdido: pierde su turno.");
            ExpansionIntentLines(game, enemy, player, lines);
            if (it.Block > 0) lines.Add("[[defend]]Ganará " + it.Block + " de escudo.");
            if (it.Buffs != null) foreach (var b in it.Buffs) lines.Add("[[buff]]Ganará " + b.Amount + " de " + StatusNameOf(g, b.Kind) + ".");
            if (it.Debuffs != null) foreach (var d in it.Debuffs) lines.Add("[[debuff]]Te aplicará " + d.Amount + " de " + StatusNameOf(g, d.Kind) + ".");
            if (it.Cards != null)
                foreach (var c in it.Cards)
                {
                    var label = CombatState.StatusCard(c.Id)?.Name ?? c.Id;
                    if (c.Count > 1 && !label.EndsWith("s")) label += "s";
                    lines.Add("[[cards]]Meterá " + c.Count + " " + label + " en " + (CardPile.TryGetValue(c.Pile, out var pile) ? pile : c.Pile) + ".");
                }
            if (it.Lifesteal) lines.Add("[[heal]]Se curará tanta vida como te quite.");
            if (it.AllyBlock > 0) lines.Add("[[defend]]Dará " + it.AllyBlock + " de escudo a su compañero.");
            if (it.AllyBuffs != null) foreach (var b in it.AllyBuffs) lines.Add("[[buff]]Dará " + b.Amount + " de " + StatusNameOf(g, b.Kind) + " a sus aliados.");
            if (it.HealAllies > 0) lines.Add("[[heal]]Curará " + it.HealAllies + " de vida a cada aliado (y a sí mismo).");
            if (it.SelfDestruct) lines.Add("[[fuse]]Después de atacar, explota y muere.");
            return lines;
        }

        private static string EnemyName(string ai, int count)
        {
            var name = EnemyCatalog.Definition(ai).Name;
            if (count == 1) return name;
            var words = name.Split(' ');
            words[0] = words[0].EndsWith("s") ? words[0] : words[0] + "s";
            return string.Join(" ", words);
        }

        /// <summary>Lines of the expansion intent extras (summons, Strength theft, block siphon, molt, swallow, split, revival, Carga).</summary>
        private static void ExpansionIntentLines(GameContext game, EnemyState enemy, CombatState player, List<string> lines)
        {
            var it = enemy.Intent; var x = it.Extras;
            if (!x.Any) return;
            foreach (var s in x.Summons)
                lines.Add("[[ritual]]Invocará " + (s.Count == 1 ? "1 " : s.Count + " ") + EnemyName(s.Ai, s.Count) + " (máximo 3 enemigos en pantalla).");
            if (x.SplitInto != null) lines.Add("[[ritual]]Se partirá en dos " + EnemyName(x.SplitInto, 2) + " con la mitad de su vida cada uno.");
            if (x.ReviveAi != null) lines.Add("[[ritual]]Desenterrará a " + EnemyCatalog.Definition(x.ReviveAi).Name + " con la mitad de su vida.");
            if (x.StealStrength > 0)
            {
                var have = player?.Strength ?? 0;
                lines.Add(have > 0 ? "[[strength]]Te robará " + (x.StealStrength == int.MaxValue ? "toda tu Fuerza" : Math.Min(have, x.StealStrength) + " de Fuerza") + " (ahora " + have + ")."
                    : "[[strength]]Te robaría Fuerza; como no tienes, ganará " + x.StrengthFallback + ".");
            }
            if (x.SiphonBlockPercent > 0)
            {
                var taken = (player?.Block ?? 0) * x.SiphonBlockPercent / 100;
                lines.Add("[[block]]Te arrancará el " + x.SiphonBlockPercent + " % de tu escudo y se lo quedará" + (player != null ? " (ahora " + taken + ")." : "."));
            }
            if (x.Cleanse) lines.Add(x.CleanseBlockPerStack > 0
                ? "[[buff]]Se librará de sus perjuicios y elementos: +" + x.CleanseBlockPerStack + " de escudo por acumulación (máx. " + x.CleanseBlockCap + ")."
                : "[[buff]]Se librará de sus perjuicios y elementos.");
            if (x.HealSelf > 0) lines.Add("[[heal]]Se curará " + x.HealSelf + " de vida.");
            if (x.Swallow > 0) lines.Add("[[cards]]Se tragará " + x.Swallow + " carta" + (x.Swallow > 1 ? "s" : "") + " de tu descarte (vuelven al matarlo).");
            foreach (var s in x.SetSelf)
                if (s.Kind == StatusKind.Focus) lines.Add("[[hourglass]]Preparará un golpe con Carga " + s.Amount + ".");
                else lines.Add("[[buff]]Pasará a tener " + s.Amount + " de " + StatusNameOf(game.Data.Glossary, s.Kind) + ".");
            if (x.ConsumesFocus)
            {
                var focus = enemy.Statuses[StatusKind.Focus];
                lines.Add(focus > 0 ? "[[hourglass]]Quítale " + focus + " de vida antes de que actúe para aturdirlo." : "[[hourglass]]Golpe preparado.");
            }
        }

        private static List<TooltipPanel> IntentPanels(GameContext game, EnemyState enemy)
        {
            var list = new List<TooltipPanel>();
            if (enemy.Intent.Cards == null) return list;
            foreach (var c in enemy.Intent.Cards)
            {
                var card = CombatState.StatusCard(c.Id);
                if (card != null) list.Add(new TooltipPanel(card.Name, CardRuleLines(game, card), "cards", new Rgba(170, 230, 110), "Carta de estado"));
            }
            return list;
        }

        private static readonly Dictionary<IntentKind, string> IntentTitle = new Dictionary<IntentKind, string>
        { [IntentKind.Attack] = "Va a atacar", [IntentKind.Block] = "Va a defenderse", [IntentKind.Buff] = "Va a fortalecerse", [IntentKind.Debuff] = "Va a debilitarte", [IntentKind.Unknown] = "Intención desconocida",
          [IntentKind.Summon] = "Va a invocar", [IntentKind.Stun] = "Está aturdido" };

        public static TooltipContent Intent(GameContext game, EnemyState enemy, int? hitDamage, CombatState player)
        {
            var it = enemy.Intent;
            var content = new TooltipContent(!string.IsNullOrEmpty(it.Name) ? it.Name : IntentTitle[it.Kind])
            {
                Icon = IntentIcon(it, hitDamage), Subtitle = "Intención de " + enemy.Definition.Name + "  ·  ocurre al terminar tu turno",
                Lines = IntentLines(game, enemy, hitDamage, player), Accent = IntentAccent(it),
            };
            if (!string.IsNullOrEmpty(it.Description)) content.Lines.Add("  " + it.Description);
            content.Panels.AddRange(IntentPanels(game, enemy));
            AddGlossary(game.Data.Glossary, content);
            return content;
        }

        public static TooltipPanel StatusPanel(GameContext game, string name, int stacks, bool onPlayer = false)
        {
            var g = game.Data.Glossary; var buff = g.IsBuffStatus(name);
            var term = g.StatusTerm(name, buff);
            var lines = new List<string>();
            if (!string.IsNullOrEmpty(term.Rule)) lines.Add(term.Rule);
            lines.AddRange(term.Lines ?? Array.Empty<string>());
            if (onPlayer && Array.IndexOf(HeroTimedDebuffs, name) >= 0) lines.Add("  Dura " + stacks + " turno" + (stacks != 1 ? "s" : "") + " (baja 1 al final de tu turno).");
            return new TooltipPanel(name + " " + stacks, lines, term.Icon, buff ? g.BuffTitle : g.DebuffTitle, buff ? "Mejora" : "Perjuicio", name);
        }

        public static TooltipContent Status(GameContext game, string name, int stacks, bool onPlayer = false)
        {
            var p = StatusPanel(game, name, stacks, onPlayer);
            return new TooltipContent(p.Title) { Lines = p.Lines, Icon = p.Icon, Subtitle = game.Data.Glossary.IsBuffStatus(name) ? "Mejora" : "Perjuicio", Accent = p.Color };
        }

        public static string Identity(GameContext game, EnemyState enemy)
        {
            var profile = EnemyCatalog.Profile(enemy.Definition.Ai);
            if (profile != null) return profile.Identity.Length > 0 ? profile.Identity + ": " + profile.Title : profile.Title;
            if (enemy.Definition.Boss) return "";
            return game.Data.Enemies.TryGetValue(enemy.Definition.Ai, out var e) && !string.IsNullOrEmpty(e.Identity) ? e.Identity + ": " + e.Title : "";
        }

        /// <summary>Pair / group / minion line of the inspect tooltip.</summary>
        public static string PairLine(EnemyState enemy)
        {
            if (!enemy.PaysGold) return "  Esbirro invocado: no da oro.";
            if (string.IsNullOrEmpty(enemy.PairName)) return null;
            if (!EnemyCatalog.IsExpansion(enemy.Definition.Ai)) return "  Pareja: " + enemy.PairName + ". Si cae su pareja, buscará Venganza.";
            var fight = EnemyCatalog.BossEncounters.FirstOrDefault(b => b.Name == enemy.PairName);
            if (fight != null) return "  Pareja: " + fight.Name + ". " + fight.Synergy;
            return "  Grupo: " + enemy.PairName + ".";
        }

        public static TooltipContent Enemy(GameContext game, EnemyState enemy, int? hitDamage, CombatState player)
        {
            var g = game.Data.Glossary; var it = enemy.Intent;
            var lines = new List<string> { "Vida: " + enemy.Hp + " / " + enemy.MaxHp };
            var identity = Identity(game, enemy);
            if (identity.Length > 0) lines.Add(identity);
            var profile = EnemyCatalog.Profile(enemy.Definition.Ai);
            if (profile != null) foreach (var m in profile.Mechanics) lines.Add("  " + m);
            var pairLine = PairLine(enemy);
            if (pairLine != null) lines.Add(pairLine);
            if (enemy.Block > 0) lines.Add("Bloqueo: " + enemy.Block + " (absorbe el próximo daño)");
            var intentLines = IntentLines(game, enemy, hitDamage, player);
            if (!string.IsNullOrEmpty(it.Description)) intentLines.Add("  " + it.Description);
            var panels = new List<TooltipPanel> { new TooltipPanel(!string.IsNullOrEmpty(it.Name) ? "Intención: " + it.Name : "Intención", intentLines, IntentIcon(it, hitDamage), IntentAccent(it)) };
            foreach (var k in enemy.Statuses.Active) panels.Add(StatusPanel(game, StatusNameOf(g, k), enemy.Statuses[k]));
            panels.AddRange(IntentPanels(game, enemy));
            var boss = enemy.Definition.Boss; var elite = enemy.Definition.Elite;
            var content = new TooltipContent(enemy.Definition.Name)
            {
                Lines = lines, Tag = boss ? "JEFE" : elite ? "ÉLITE" : "", Subtitle = boss ? "Jefe del piso" : elite ? "Élite del piso: suelta una reliquia" : "Enemigo", Panels = panels,
            };
            AddGlossary(g, content);
            return content;
        }

        public const int QuickMaxStatuses = 5;
        public const string InspectHint = "  Clic derecho (o mantener pulsado): ver todo";

        public static string IntentSummary(GameContext game, EnemyState enemy, int? hitDamage)
        {
            var g = game.Data.Glossary; var it = enemy.Intent; var parts = new List<string>();
            if (it.Kind == IntentKind.Attack)
            {
                long dmg = hitDamage ?? it.Value; var hits = Math.Max(1, it.Hits);
                parts.Add(hits > 1 ? "ataca " + dmg + "×" + hits + " (" + dmg * hits + " de daño)" : "ataca " + dmg + " de daño");
            }
            else if (it.Kind == IntentKind.Block && it.Value > 0) parts.Add("+" + it.Value + " de escudo");
            else if (it.Kind == IntentKind.Buff && (it.Buffs == null || it.Buffs.Length == 0)) parts.Add("se fortalece");
            else if (it.Kind == IntentKind.Debuff && (it.Debuffs == null || it.Debuffs.Length == 0)) parts.Add("te debilita");
            else if (it.Kind == IntentKind.Unknown) parts.Add("¿?");
            if (it.Block > 0) parts.Add("+" + it.Block + " de escudo");
            // The original formats these pairs as "{n} {k}" over (name, n) tuples: name first, then the number.
            if (it.Buffs != null && it.Buffs.Length > 0) { var s = new List<string>(); foreach (var b in it.Buffs) s.Add(StatusNameOf(g, b.Kind) + " " + b.Amount); parts.Add("gana " + string.Join(", ", s)); }
            if (it.Debuffs != null && it.Debuffs.Length > 0) { var s = new List<string>(); foreach (var b in it.Debuffs) s.Add(StatusNameOf(g, b.Kind) + " " + b.Amount); parts.Add("te aplica " + string.Join(", ", s)); }
            if (it.Cards != null && it.Cards.Length > 0) { var s = new List<string>(); foreach (var c in it.Cards) s.Add(c.Count + " carta(s)"); parts.Add("mete " + string.Join(", ", s)); }
            if (it.HealAllies > 0 || it.Lifesteal) parts.Add("se cura");
            if (it.SelfDestruct) parts.Add("¡explota!");
            var head = !string.IsNullOrEmpty(it.Name) ? it.Name + ": " : "";
            return "[[" + IntentIcon(it, hitDamage) + "]]" + head + (parts.Count > 0 ? string.Join(" · ", parts) : "actúa");
        }

        private static List<string> QuickStatusLines(GameContext game, StatusSet statuses)
        {
            var g = game.Data.Glossary;
            var kinds = new List<StatusKind>(statuses.Active);
            var elements = new List<StatusKind>(); var rest = new List<StatusKind>();
            foreach (var k in kinds) (g.ElementColor.ContainsKey(StatusNameOf(g, k)) ? elements : rest).Add(k);
            elements.AddRange(rest);
            var lines = new List<string>();
            for (var i = 0; i < Math.Min(QuickMaxStatuses, elements.Count); i++)
            {
                var name = StatusNameOf(g, elements[i]);
                lines.Add("[[" + g.StatusIconOf(name, g.IsBuffStatus(name)) + "]]" + g.StatusShortText(name, statuses[elements[i]]));
            }
            if (elements.Count > QuickMaxStatuses) lines.Add("  … y " + (elements.Count - QuickMaxStatuses) + " más");
            return lines;
        }

        public static TooltipContent EnemyQuick(GameContext game, EnemyState enemy, int? hitDamage)
        {
            var lines = new List<string> { "Vida " + enemy.Hp + "/" + enemy.MaxHp + (enemy.Block > 0 ? "  ·  Escudo " + enemy.Block : ""), IntentSummary(game, enemy, hitDamage) };
            lines.AddRange(QuickStatusLines(game, enemy.Statuses)); lines.Add(InspectHint);
            var boss = enemy.Definition.Boss; var elite = enemy.Definition.Elite;
            return new TooltipContent(enemy.Definition.Name)
            {
                Lines = lines, Tag = boss ? "JEFE" : elite ? "ÉLITE" : "",
                Subtitle = boss ? "Jefe del piso" : elite ? "Élite" : !enemy.PaysGold ? "Esbirro"
                    : !string.IsNullOrEmpty(enemy.PairName) ? (EnemyCatalog.IsExpansion(enemy.Definition.Ai) ? "Grupo: " : "Pareja: ") + enemy.PairName : "Enemigo",
            };
        }

        public static TooltipContent PlayerQuick(GameContext game, CombatState state, int incoming)
        {
            var lines = new List<string> { "Vida " + state.Hp + "/" + state.Hero.MaxHp + (state.Block != 0 ? "  ·  Escudo " + state.Block : "") };
            if (incoming != 0)
            {
                var lost = Math.Max(0, incoming - state.Block);
                lines.Add("[[" + AttackIcon(incoming) + "]]Daño entrante: " + incoming + " → pierdes " + lost);
                if (state.Hp > 0 && lost >= state.Hp) lines.Add("[[lethal]]¡Letal! Consigue escudo o mata a quien ataca.");
            }
            lines.AddRange(QuickStatusLines(game, state.HeroStatuses)); lines.Add(InspectHint);
            return new TooltipContent(state.Hero.Name) { Lines = lines, Subtitle = "Tu héroe" };
        }

        public static TooltipContent Player(GameContext game, CombatState state, int incoming, int luck)
        {
            var g = game.Data.Glossary;
            var lines = new List<string> { "Vida: " + state.Hp + " / " + state.Hero.MaxHp };
            lines.Add(state.Block > 0 ? "[[block]]Bloqueo: " + state.Block + " (absorbe el próximo daño recibido)." : "Sin bloqueo activo.");
            if (incoming != 0)
            {
                var lost = Math.Max(0, incoming - state.Block);
                lines.Add("[[" + AttackIcon(incoming) + "]]Daño entrante: " + incoming + " de daño; perderías " + lost + " de vida.");
                if (state.Hp > 0 && lost >= state.Hp) lines.Add("[[lethal]]¡Letal! Consigue escudo o mata a quien ataca.");
            }
            if (luck > 0) lines.Add("Suerte: " + luck + " -- más cartas y reliquias doradas, y reliquias de mayor rareza.");
            var content = new TooltipContent(state.Hero.Name) { Lines = lines, Subtitle = "Tu héroe" };
            foreach (var k in state.HeroStatuses.Active) content.Panels.Add(StatusPanel(game, StatusNameOf(g, k), state.HeroStatuses[k], true));
            AddGlossary(g, content);
            return content;
        }

        // ------------------------------------------------------------------ relics, piles, mana
        public static TooltipContent Relic(GameContext game, RelicDefinition relic, ChromaKind chroma, bool active = true)
        {
            var lines = new List<string> { relic.Description, "Rareza: " + RunContent.RarityLabel(relic.Rarity) };
            if (relic.ClassId != "neutral" && CardClassLabel.TryGetValue(relic.ClassId, out var cl)) lines.Add("Solo para " + cl);
            if (chroma != ChromaKind.None) lines.Add(ChromaFx.Style(chroma).RelicNote);
            lines.Add(""); lines.Add(active ? "Estado: Activo" : "Estado: Agotado");
            var content = new TooltipContent(ChromaFx.Title(relic.Name, chroma)) { Lines = lines, Subtitle = "Reliquia" };
            AddGlossary(game.Data.Glossary, content);
            return content;
        }
        public static TooltipContent Relic(GameContext game, OwnedRelic relic) => Relic(game, relic.Definition, relic.Chroma, relic.Active);

        public static TooltipContent Pile(string label, int count, bool isDraw, IList<string> nextCards = null)
        {
            if (isDraw)
            {
                var lines = new List<string> { count + " carta(s) restantes.", "Haz clic para ver las cartas.", "", "Cuando se agota se baraja la pila de descarte y se convierte en la nueva pila." };
                if (nextCards != null && nextCards.Count > 0)
                {
                    lines.Add(""); lines.Add("Catalejo · próximas cartas:");
                    for (var i = 0; i < nextCards.Count; i++) lines.Add("  " + (i + 1) + ". " + nextCards[i]);
                }
                return new TooltipContent("Pila de Robo") { Icon = "draw", Lines = lines };
            }
            return new TooltipContent("Pila de Descarte")
            {
                Icon = "spoil", Lines = new List<string> { count + " carta(s) descartadas.", "Haz clic para ver las cartas.", "", "Se baraja en la pila de robo cuando ésta se queda sin cartas." },
            };
        }

        public static TooltipContent Mana(int current, int maximum) => new TooltipContent("Maná")
        { Icon = "mana", Lines = new List<string> { "Disponible: " + current + " / " + maximum, "", "Recurso para jugar cartas. Se recarga completamente al inicio de cada turno." } };

        // ------------------------------------------------------------------ renderer
        public const int MainW = 318, PanelW = 318, Pad = 9, Gap = 3, StackGap = 5, Corner = 5, OffX = 18, OffY = -6, IconW = 28;
        private static readonly Rgba Bg = new Rgba(16, 13, 26, 238), PanelBg = new Rgba(22, 18, 34, 236), Accent0 = new Rgba(215, 180, 75);

        private struct Row { public Sprite Icon; public bool HasIcon; public RichLine Line; public int Indent; }
        private sealed class Head { public Sprite Icon; public bool HasIcon; public int TitleX; public string Tag; public FontFace TagFont; public List<string> Lines = new List<string>(); public FontFace Font; public Rgba Color; public int H; }
        private sealed class Block { public bool Main; public Head Head; public List<RichLine> Sub; public List<Row> Rows; public int H; public Rgba Accent; public Rgba Color; }
        private sealed class Layout { public List<List<Block>> Columns = new List<List<Block>>(); public int W, H; }

        private static readonly Dictionary<string, Layout> layouts = new Dictionary<string, Layout>();
        private static readonly Queue<string> layoutOrder = new Queue<string>();

        private static List<Row> Body(GameContext game, List<string> lines, FontFace font, int width, Rgba baseColor)
        {
            var g = game.Data.Glossary; var rows = new List<Row>();
            foreach (var original in lines)
            {
                if (original == "") { rows.Add(new Row()); continue; }
                var raw = original; Sprite icon = default; var hasIcon = false;
                var m = IconPrefix.Match(raw);
                if (m.Success) { icon = game.Art.Icon(m.Groups[1].Value); hasIcon = icon.Valid; raw = raw.Substring(m.Length); }
                var dim = raw.StartsWith("  ");
                var indent = hasIcon ? icon.W + 5 : dim ? 8 : 0;
                var parts = RichText.Lines(g, raw.Trim(), font, width - indent, dim ? g.Dim : baseColor);
                for (var k = 0; k < parts.Length; k++) rows.Add(new Row { Icon = icon, HasIcon = hasIcon && k == 0, Line = parts[k], Indent = indent });
            }
            return rows;
        }
        private static int RowH(Row r) => Math.Max(r.Line.Height, r.HasIcon ? r.Icon.H - 2 : 0);
        private static int BodyHeight(List<Row> rows) { var h = 0; foreach (var r in rows) h += r.Line == null ? Gap * 2 : RowH(r) + Gap; return h; }

        private static List<string> WordWrap(string text, FontFace font, int maxW)
        {
            var lines = new List<string>(); var current = "";
            foreach (var word in text.Split(new[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var test = (current + " " + word).Trim();
                if (font.Width(test) <= maxW) current = test;
                else { if (current.Length > 0) lines.Add(current); current = word; }
            }
            if (current.Length > 0) lines.Add(current);
            if (lines.Count == 0) lines.Add(text);
            return lines;
        }

        private static Head MakeHead(GameContext game, string title, string icon, string tag, Rgba color, FontFace titleFont, FontFace tagFont, int width)
        {
            var h = new Head { Font = titleFont, Color = color, TagFont = tagFont, Tag = string.IsNullOrEmpty(tag) ? null : tag };
            if (!string.IsNullOrEmpty(icon)) { h.Icon = game.Art.Icon(icon); h.HasIcon = h.Icon.Valid; }
            h.TitleX = h.HasIcon ? IconW + 6 : 0;
            var room = width - h.TitleX - (h.Tag != null ? tagFont.Width(h.Tag) + 8 : 0);
            if (!string.IsNullOrEmpty(title)) h.Lines = WordWrap(title, titleFont, room);
            h.H = Math.Max(h.HasIcon ? IconW : 0, h.Lines.Count * titleFont.Height);
            return h;
        }

        private static Rgba Dim(Rgba c, double k) => new Rgba((int)(c.R * k), (int)(c.G * k), (int)(c.B * k));

        private static Block MeasureMain(GameContext game, TooltipContent content)
        {
            var f = game.Fonts; var inner = MainW - Pad * 2;
            var accent = content.Accent ?? Accent0;
            var b = new Block { Main = true, Accent = accent };
            b.Head = MakeHead(game, content.Title, content.Icon, content.Tag, accent, f.Get(16), f.Get(11), inner);
            b.Sub = string.IsNullOrEmpty(content.Subtitle) ? new List<RichLine>() : new List<RichLine>(RichText.Lines(game.Data.Glossary, content.Subtitle, f.Get(11), inner, new Rgba(160, 152, 138)));
            b.Rows = Body(game, content.Lines, f.Get(13), inner, game.Data.Glossary.Body);
            var h = Pad * 2 + b.Head.H; foreach (var s in b.Sub) h += s.Height; if (b.Sub.Count > 0) h += 2;
            if (b.Rows.Count > 0) h += 10 + BodyHeight(b.Rows);
            b.H = h;
            return b;
        }

        private static Block MeasurePanel(GameContext game, TooltipPanel panel)
        {
            var f = game.Fonts; var inner = PanelW - Pad * 2;
            var b = new Block { Color = panel.Color };
            b.Head = MakeHead(game, panel.Title, panel.Icon, panel.Tag, panel.Color, f.Get(14), f.Get(11), inner);
            b.Rows = Body(game, panel.Lines, f.Get(12), inner, new Rgba(214, 208, 196));
            b.H = Pad * 2 - 2 + b.Head.H + (b.Rows.Count > 0 ? 4 + BodyHeight(b.Rows) : 0);
            return b;
        }

        private static Layout Build(GameContext game, TooltipContent content, int screenH)
        {
            var key = content.Key() + "\u0004" + screenH;
            if (layouts.TryGetValue(key, out var hit)) return hit;
            var main = MeasureMain(game, content);
            var layout = new Layout();
            layout.Columns.Add(new List<Block> { main });
            var colH = main.H; var hidden = 0; var limit = screenH - 8;
            foreach (var p in content.Panels)
            {
                var b = MeasurePanel(game, p);
                if (colH + StackGap + b.H > limit && layout.Columns.Count < 2) { layout.Columns.Add(new List<Block>()); colH = -StackGap; }
                if (colH + StackGap + b.H > limit - 30) { hidden++; continue; }
                layout.Columns[layout.Columns.Count - 1].Add(b); colH += StackGap + b.H;
            }
            if (hidden > 0) layout.Columns[layout.Columns.Count - 1].Add(MeasurePanel(game, new TooltipPanel("+" + hidden + " más", new[] { "Clic derecho para verlo todo." }, "unknown", new Rgba(176, 170, 160))));
            foreach (var col in layout.Columns) { var h = 0; foreach (var b in col) h += b.H; h += StackGap * (col.Count - 1); layout.H = Math.Max(layout.H, h); }
            layout.W = MainW * layout.Columns.Count + 6 * (layout.Columns.Count - 1);
            layouts[key] = layout; layoutOrder.Enqueue(key);
            if (layoutOrder.Count > 48) layouts.Remove(layoutOrder.Dequeue());
            return layout;
        }

        public static (int w, int h) Size(GameContext game, TooltipContent content) { var l = Build(game, content, Canvas.Height); return (l.W, l.H); }

        private static void DrawBox(Canvas canvas, RectF rect, Rgba bg, Rgba border)
        {
            canvas.Rect(rect, bg, 0, Corner);
            canvas.Rect(rect, border, 1, Corner);
            var hl = new Rgba(Math.Min(255, border.R + 40), Math.Min(255, border.G + 40), Math.Min(255, border.B + 40));
            canvas.Line(rect.X + Corner, rect.Y + 1, rect.Right - Corner - 1, rect.Y + 1, hl);
        }

        private static int DrawHead(Canvas canvas, Head head, int x, int y, int width)
        {
            if (head.HasIcon)
            {
                int iw = head.Icon.W * 2, ih = head.Icon.H * 2;
                canvas.BlitRect(head.Icon, new RectF(x - 2, y + head.H / 2 - ih / 2, iw, ih), Rgba.White);
            }
            var ty = y + (head.H - head.Lines.Count * head.Font.Height) / 2;
            foreach (var s in head.Lines) { head.Font.Draw(canvas, s, x + head.TitleX, ty, head.Color); ty += head.Font.Height; }
            if (head.Tag != null) head.TagFont.Draw(canvas, head.Tag, x + width - head.TagFont.Width(head.Tag), y + head.H / 2 - head.TagFont.Height / 2, new Rgba(210, 200, 180));
            return y + head.H;
        }

        private static void DrawRows(Canvas canvas, List<Row> rows, int x, int y)
        {
            foreach (var r in rows)
            {
                if (r.Line == null) { y += Gap * 2; continue; }
                var rh = RowH(r);
                if (r.HasIcon) canvas.Blit(r.Icon, x - 1, y + rh / 2 - r.Icon.H / 2);
                r.Line.Draw(canvas, x + r.Indent, y + (rh - r.Line.Height) / 2);
                y += rh + Gap;
            }
        }

        private static void DrawLayout(Canvas canvas, Layout layout, int tx, int ty)
        {
            for (var c = 0; c < layout.Columns.Count; c++)
            {
                var x = tx + c * (MainW + 6); var y = ty;
                foreach (var b in layout.Columns[c])
                {
                    if (b.Main)
                    {
                        DrawBox(canvas, new RectF(x, y, MainW, b.H), Bg, Dim(b.Accent, 0.85));
                        var cy = DrawHead(canvas, b.Head, x + Pad, y + Pad, MainW - Pad * 2);
                        foreach (var s in b.Sub) { s.Draw(canvas, x + Pad, cy + 1); cy += s.Height; }
                        if (b.Rows.Count > 0)
                        {
                            cy += 5;
                            canvas.Line(x + Pad, cy, x + MainW - Pad, cy, Dim(b.Accent, 0.45));
                            cy += 5;
                            DrawRows(canvas, b.Rows, x + Pad, cy);
                        }
                        y += b.H + StackGap;
                    }
                    else
                    {
                        DrawBox(canvas, new RectF(x, y, PanelW, b.H), PanelBg, Dim(b.Color, 0.55));
                        var cy = DrawHead(canvas, b.Head, x + Pad, y + Pad - 2, PanelW - Pad * 2);
                        if (b.Rows.Count > 0) DrawRows(canvas, b.Rows, x + Pad, cy + 4);
                        y += b.H + StackGap;
                    }
                }
            }
        }

        /// <summary>draw_tooltip: near the cursor or beside <paramref name="beside"/>, kept on screen; returns its rect.</summary>
        public static RectF Draw(Canvas canvas, GameContext game, TooltipContent content, (float x, float y) mouse, RectF? beside = null)
        {
            int sw = Canvas.Width, sh = Canvas.Height;
            var layout = Build(game, content, sh);
            int tx, ty;
            if (beside.HasValue)
            {
                var b = beside.Value;
                tx = b.Right + 8 + layout.W <= sw - 4 ? (int)b.Right + 8 : (int)b.X - layout.W - 8;
                ty = (int)Math.Min(b.Y, sh - 4 - layout.H);
            }
            else
            {
                tx = (int)mouse.x + OffX; ty = (int)mouse.y + OffY;
                if (tx + layout.W > sw - 4) tx = (int)mouse.x - layout.W - OffX;
                if (ty + layout.H > sh - 4) ty = sh - 4 - layout.H;
            }
            tx = Math.Max(4, Math.Min(tx, sw - 4 - layout.W)); ty = Math.Max(4, ty);
            DrawLayout(canvas, layout, tx, ty);
            return new RectF(tx, ty, layout.W, layout.H);
        }

        /// <summary>The whole stack drawn with its top-left at (x, y) (inspect panel / log detail).</summary>
        public static (int w, int h) DrawAt(Canvas canvas, GameContext game, TooltipContent content, int x, int y, int screenH = 1 << 20)
        {
            var layout = Build(game, content, screenH);
            DrawLayout(canvas, layout, x, y);
            return (layout.W, layout.H);
        }
    }
}
