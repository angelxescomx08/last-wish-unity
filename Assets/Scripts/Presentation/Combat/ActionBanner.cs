using System;
using System.Collections.Generic;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>ui/action_banner.py: one banner per acting enemy ("Caballero Hueco usa Danza de Espadas — 2 golpes · …").</summary>
    public sealed class ActionBanners
    {
        public const float FadeIn = 0.15f, Hold = 2.1f, FadeOut = 0.35f;
        public const int Width = 560;
        private static readonly Dictionary<string, string> PileName = new Dictionary<string, string> { ["draw"] = "tu pila de robo", ["discard"] = "tu descarte", ["hand"] = "tu mano" };
        private static readonly Dictionary<IntentKind, string> Generic = new Dictionary<IntentKind, string>
        { [IntentKind.Attack] = "ataca", [IntentKind.Block] = "se defiende", [IntentKind.Buff] = "se fortalece", [IntentKind.Debuff] = "te debilita", [IntentKind.Unknown] = "actúa" };

        public sealed class Banner
        {
            public string Icon, Title, Detail; public float Delay, Age;
            public float Alpha
            {
                get
                {
                    var t = Age - Delay;
                    if (t < 0) return 0f;
                    if (t < FadeIn) return t / FadeIn;
                    if (t < FadeIn + Hold) return 1f;
                    return Math.Max(0f, 1f - (t - FadeIn - Hold) / FadeOut);
                }
            }
            public bool Done => Age - Delay >= FadeIn + Hold + FadeOut;
        }

        private static string Plural(string word, int n) => n == 1 || word.EndsWith("s") ? word : word + "s";
        private static string NameOf(Glossary g, StatusKind k) => g.StatusName.TryGetValue(k, out var n) ? n : k.ToString();

        /// <summary>describe_action: (icon, title, detail) for what one enemy just did.</summary>
        public static (string icon, string title, string detail) Describe(Glossary g, string enemyName, EnemyIntent intent, EnemyAction action)
        {
            var move = intent.Name;
            var title = !string.IsNullOrEmpty(move) ? enemyName + " usa " + move : enemyName + " " + (Generic.TryGetValue(intent.Kind, out var v) ? v : "actúa");
            var parts = new List<string>();
            if (intent.Kind == IntentKind.Attack && action.Hits.Count > 0)
            {
                var n = action.Hits.Count; var lost = 0; foreach (var h in action.Hits) lost += h;
                var hits = n + " " + (n == 1 ? "golpe" : "golpes");
                parts.Add(lost == 0 ? hits + " · ¡todo bloqueado!" : hits + " · pierdes " + lost + " de vida");
            }
            else if (intent.Kind == IntentKind.Block && intent.Value > 0) parts.Add("gana " + intent.Value + " de escudo");
            if (intent.Block > 0) parts.Add("gana " + intent.Block + " de escudo");
            if (intent.Buffs != null) foreach (var b in intent.Buffs) parts.Add("gana " + b.Amount + " de " + NameOf(g, b.Kind));
            foreach (var d in action.Debuffs) parts.Add("te aplica " + d.Amount + " de " + NameOf(g, d.Kind));
            foreach (var c in action.Cards)
            {
                var label = Plural(CombatState.StatusCard(c.Id)?.Name ?? c.Id, c.Count);
                parts.Add("mete " + c.Count + " " + label + " en " + (PileName.TryGetValue(c.Pile, out var p) ? p : c.Pile));
            }
            if (intent.AllyBlock > 0) parts.Add("da " + intent.AllyBlock + " de escudo a su compañero");
            if (intent.AllyBuffs != null) foreach (var b in intent.AllyBuffs) parts.Add("da " + b.Amount + " de " + NameOf(g, b.Kind) + " a sus aliados");
            if (action.Healed > 0) parts.Add("recupera " + action.Healed + " de vida");
            if (action.Exploded) parts.Add("¡explota y muere!");
            return (Tooltip.IntentIcon(intent), title, string.Join(" · ", parts));
        }

        public readonly int Top;
        private readonly List<Banner> items = new List<Banner>();
        public ActionBanners(int top = 372) { Top = top; }
        public IReadOnlyList<Banner> Active => items;
        public void Clear() => items.Clear();
        public void Add(string icon, string title, string detail = "", float delay = 0f) => items.Add(new Banner { Icon = icon, Title = title, Detail = detail ?? "", Delay = delay });

        public void Update(float dt)
        {
            foreach (var b in items) b.Age += dt;
            items.RemoveAll(b => b.Done);
        }

        public void Draw(Canvas canvas, Art art, Glossary g)
        {
            var y = Top; const int cx = Canvas.Width / 2;
            var titleFont = art.Fonts.Get(16); var detailFont = art.Fonts.Get(13);
            foreach (var b in items)
            {
                var a = b.Alpha;
                if (a <= 0) continue;
                var hasIcon = art.HasIcon(b.Icon);
                var icon = hasIcon ? art.Icon(b.Icon) : default;
                var iconW = hasIcon ? icon.W * 2 + 6 : 0;
                var textMax = Width - iconW - 24;
                var title = TextFit.Fit(titleFont, b.Title, textMax);
                RichLine[] details = b.Detail.Length > 0 ? RichText.Lines(g, b.Detail, detailFont, textMax, new Rgba(230, 224, 212), true) : Array.Empty<RichLine>();
                var shownLines = Math.Min(3, details.Length);
                var textW = titleFont.Width(title);
                for (var k = 0; k < shownLines; k++) textW = Math.Max(textW, details[k].Width);
                var w = Math.Min(Width, Math.Max(260, textW + iconW + 24));
                var lineH = detailFont.LineSize;
                var h = shownLines > 0 ? 36 + lineH * shownLines - 2 : 36;
                var slide = (int)((1 - Math.Min(1.0, a * 1.2)) * -10);
                var panel = new IRect(cx - w / 2, y + slide, w, h);
                canvas.PushAlpha(a);
                canvas.Rect(panel, new Rgba(12, 8, 20, 215), 0, 6);
                canvas.Rect(panel, new Rgba(150, 110, 70), 1, 6);
                var x = panel.X + 12;
                if (hasIcon)
                {
                    var ih = icon.H * 2;
                    canvas.Blit(icon, x - 2, panel.Y + h / 2 - ih / 2, 2);
                    x += icon.W * 2 + 6;
                }
                titleFont.Draw(canvas, title, x, panel.Y + (shownLines > 0 ? 5 : (h - titleFont.Height) / 2), new Rgba(246, 220, 150));
                for (var k = 0; k < shownLines; k++) details[k].Draw(canvas, x, panel.Y + 5 + titleFont.Height + 3 + k * lineH);
                canvas.PopAlpha();
                y += h + 6;
            }
        }
    }
}
