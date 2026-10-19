using System;
using System.Collections.Generic;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>An overlay that takes every event while open (collections, confirmations, sheets).</summary>
    public interface IOverlay
    {
        bool Dismissed { get; }
        void HandleEvent(in InputEvent e);
        void Update(float dt);
        void Draw(Canvas canvas);
    }

    /// <summary>ui/collection_viewer.py: scrollable modal grid shared by cards and relics.</summary>
    public abstract class CollectionViewer<T> : IOverlay
    {
        protected virtual int TileWidth => 180;
        protected virtual int TileHeight => 254;
        public virtual string CloseLabel => "Cerrar ×";
        public virtual string FooterText => "ESC o clic fuera para cerrar";
        public virtual string HintText => "Pasa el cursor para ver detalles  ·  Rueda / ↑ ↓ / Re Pág / Av Pág";
        public virtual bool CloseOnOutsideClick => true;

        protected readonly GameContext Game;
        protected readonly string Title, Noun;
        protected readonly List<T> Items;
        protected IRect Panel = new IRect(50, 30, 1180, 660), Viewport = new IRect(78, 124, 1100, 520), CloseRect = new IRect(1108, 48, 96, 34), Track = new IRect(1190, 124, 12, 520);
        protected int ScrollY;
        private int? dragOffset;
        protected (float x, float y) MousePos = (-1, -1);
        protected readonly Dictionary<int, IRect> ItemRects = new Dictionary<int, IRect>();
        private int columns, contentHeight, maxScroll;
        public bool Dismissed { get; set; }

        protected CollectionViewer(GameContext game, string title, IEnumerable<T> items, string noun)
        {
            Game = game; Title = title; Noun = noun; Items = new List<T>(items);
            Recount();
        }

        protected void Recount()
        {
            columns = Math.Max(1, Viewport.W / TileWidth);
            var rows = (Items.Count + columns - 1) / columns;
            contentHeight = rows * TileHeight;
            maxScroll = Math.Max(0, contentHeight - Viewport.H);
            ScrollY = Math.Max(0, Math.Min(maxScroll, ScrollY));
        }

        private IRect Thumb
        {
            get
            {
                var height = Math.Max(32, (int)(Track.H * Viewport.H / (double)Math.Max(contentHeight, 1)));
                height = Math.Min(height, Track.H);
                var travel = Track.H - height;
                var top = Track.Y + MathX.Round(travel * ScrollY / (double)Math.Max(maxScroll, 1));
                return new IRect(Track.X, top, Track.W, height);
            }
        }

        protected void ScrollTo(int value) { ScrollY = Math.Max(0, Math.Min(maxScroll, value)); ItemRects.Clear(); }

        public virtual void HandleEvent(in InputEvent e)
        {
            switch (e.Type)
            {
                case EventType.KeyDown:
                    if (e.Key == Key.Escape) Dismissed = true;
                    switch (e.Key)
                    {
                        case Key.Home: ScrollTo(0); break;
                        case Key.End: ScrollTo(maxScroll); break;
                        case Key.Up: ScrollTo(ScrollY - 60); break;
                        case Key.Down: ScrollTo(ScrollY + 60); break;
                        case Key.PageUp: ScrollTo(ScrollY - Viewport.H); break;
                        case Key.PageDown: ScrollTo(ScrollY + Viewport.H); break;
                    }
                    break;
                case EventType.Wheel: ScrollTo(ScrollY - (int)(e.Wheel * 60)); break;
                case EventType.MouseDown:
                    if (e.Button == 4 || e.Button == 5) ScrollTo(ScrollY + (e.Button == 4 ? -60 : 60));
                    else if (e.Button == 1)
                    {
                        if (CloseRect.Contains(e.X, e.Y) || CloseOnOutsideClick && !Panel.Contains(e.X, e.Y)) Dismissed = true;
                        else if (maxScroll > 0 && Track.Contains(e.X, e.Y))
                        {
                            var thumb = Thumb;
                            if (thumb.Contains(e.X, e.Y)) dragOffset = (int)e.Y - thumb.Y;
                            else ScrollTo(ScrollY + (e.Y < thumb.Y ? -1 : 1) * Viewport.H);
                        }
                        else OnClick(e);
                    }
                    break;
                case EventType.MouseUp: if (e.Button == 1) dragOffset = null; break;
                case EventType.MouseMove:
                    MousePos = e.Pos;
                    if (dragOffset.HasValue)
                    {
                        var travel = Track.H - Thumb.H;
                        var offset = (int)e.Y - Track.Y - dragOffset.Value;
                        ScrollTo(MathX.Round(offset * maxScroll / (double)Math.Max(travel, 1)));
                    }
                    break;
            }
        }

        /// <summary>A left click that is not the close button or the scrollbar (subclasses pick items).</summary>
        protected virtual void OnClick(in InputEvent e) { }

        public virtual void Update(float dt) { }

        public virtual void Draw(Canvas canvas)
        {
            var f = Game.Fonts;
            canvas.Fill(new RectF(0, 0, Canvas.Width, Canvas.Height), new Rgba(0, 0, 0, 190));
            canvas.Rect(Panel, Colors.BgPanel, 0, 12);
            canvas.Rect(Panel, Colors.BorderBright, 1, 12);
            f.Get(24).Draw(canvas, Title + "  ·  " + Items.Count + " " + Noun, 78, 48, Colors.TextAccent);
            f.Get(13).Draw(canvas, HintText, 78, 90, Colors.TextSecondary);
            canvas.Rect(CloseRect, Colors.BgDark, 0, 6);
            f.Get(15).DrawCentered(canvas, CloseLabel, CloseRect.CenterX, CloseRect.CenterY, Colors.TextPrimary);
            ItemRects.Clear();
            canvas.PushClip(Viewport);
            var gap = (Viewport.W - columns * TileWidth) / Math.Max(columns - 1, 1);
            for (var i = 0; i < Items.Count; i++)
            {
                var rect = new IRect(Viewport.X + i % columns * (TileWidth + gap), Viewport.Y + i / columns * TileHeight - ScrollY, TileWidth, TileHeight);
                if (rect.Bottom <= Viewport.Y || rect.Y >= Viewport.Bottom) continue;
                ItemRects[i] = rect;
                DrawItem(canvas, Items[i], rect, i);
            }
            if (Items.Count == 0) f.Get(22).DrawCentered(canvas, "No hay objetos en esta colección", Viewport.CenterX, Viewport.CenterY, Colors.TextSecondary);
            canvas.PopClip();
            if (maxScroll > 0)
            {
                canvas.Rect(Track, Colors.BgDark, 0, 6);
                canvas.Rect(Thumb, Colors.TextSecondary, 0, 6);
            }
            f.Get(12).DrawCentered(canvas, FooterText, 640, 668, Colors.TextSecondary);
            DrawExtra(canvas);
            if (Viewport.Contains(MousePos.x, MousePos.y) && !dragOffset.HasValue)
                foreach (var pair in ItemRects)
                    if (pair.Value.Contains(MousePos.x, MousePos.y)) { Tooltip.Draw(canvas, Game, TooltipOf(Items[pair.Key]), MousePos); break; }
        }

        protected virtual void DrawExtra(Canvas canvas) { }
        protected abstract void DrawItem(Canvas canvas, T item, IRect rect, int index);
        protected abstract TooltipContent TooltipOf(T item);
    }

    /// <summary>ui/pile_viewer.py: a pile or the deck, with the hero's bonuses on every card.</summary>
    public class PileViewer : CollectionViewer<CardDefinition>
    {
        protected readonly CardBonus Bonus;
        public PileViewer(GameContext game, string title, IEnumerable<CardDefinition> cards, CardBonus bonus = default) : base(game, title, cards, "cartas") { Bonus = bonus; }
        protected override void DrawItem(Canvas canvas, CardDefinition item, IRect rect, int index) => CardWidget.Draw(canvas, Game, item, rect.X + 20, rect.Y + 44, Bonus.For(item));
        protected override TooltipContent TooltipOf(CardDefinition item) { var s = Bonus.For(item); return Tooltip.Card(Game, item, s.BonusDamage, s.BonusBlock); }
    }

    /// <summary>ui/relic_viewer.py: every owned relic, its effect and state.</summary>
    public sealed class RelicViewer : CollectionViewer<OwnedRelic>
    {
        protected override int TileWidth => 530;
        protected override int TileHeight => 154;
        public RelicViewer(GameContext game, IEnumerable<OwnedRelic> relics) : base(game, "Tus reliquias", relics, "reliquias") { }

        public static void DrawRelicIcon(Canvas canvas, Art art, string name, int size, float cx, float cy, float alpha = 1f)
        {
            var s = art.Relic(name); if (!s.Valid) return;
            canvas.BlitRect(s, RectF.CenteredInt(cx, cy, size, size), Rgba.White.WithAlpha((int)(255 * alpha)));
        }

        protected override void DrawItem(Canvas canvas, OwnedRelic item, IRect rect, int index)
        {
            var f = Game.Fonts; var def = item.Definition;
            var box = rect.Inflate(-8, -12);
            canvas.Rect(box, Colors.BgDark, 0, 8);
            canvas.Rect(box, Colors.RelicBorder, 1, 8);
            DrawRelicIcon(canvas, Game.Art, def.Name, 64, box.X + 48, box.CenterY);
            f.Get(18).Draw(canvas, ChromaFx.Title(def.Name, item.Chroma), box.X + 96, box.Y + 12, Colors.TextAccent);
            var font = f.Get(14);
            var lines = CardWrap(def.Description, font, box.W - 115);
            var room = Math.Max(1, (box.Bottom - 28 - (box.Y + 43)) / 19);
            if (lines.Count > room) { lines = lines.GetRange(0, room); lines[lines.Count - 1] = TextFit.Fit(font, lines[lines.Count - 1] + "…", box.W - 115); }
            for (var i = 0; i < lines.Count; i++) font.Draw(canvas, lines[i], box.X + 96, box.Y + 43 + i * 19, Colors.TextPrimary);
            var status = item.Active ? "Activa" : "Agotada";
            if (item.Chroma != ChromaKind.None) status += "  ·  " + ChromaFx.Style(item.Chroma).ShortNote;
            f.Get(12).Draw(canvas, status, box.X + 96, box.Bottom - 24, Colors.TextSecondary);
            if (item.Chroma != ChromaKind.None) ChromaFx.DrawBox(canvas, Game.Art, box, item.Chroma, Game.Time, 8);
        }
        protected override TooltipContent TooltipOf(OwnedRelic item) => Tooltip.Relic(Game, item);

        /// <summary>card_widget._wrap: word wrap that truncates only a single over-long word.</summary>
        public static List<string> CardWrap(string text, FontFace font, int maxW)
        {
            var lines = new List<string>(); var current = "";
            foreach (var word in (text ?? "").Split(new[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = (current + " " + word).Trim();
                if (font.Width(candidate) <= maxW) current = candidate;
                else
                {
                    if (current.Length > 0) lines.Add(current);
                    current = font.Width(word) <= maxW ? word : FitWord(word, font, maxW);
                }
            }
            if (current.Length > 0) lines.Add(current);
            if (lines.Count == 0) lines.Add("");
            return lines;
        }
        private static string FitWord(string text, FontFace font, int maxW)
        {
            if (font.Width(text) <= maxW) return text;
            while (text.Length > 1 && font.Width(text + "…") > maxW) text = text.Substring(0, text.Length - 1);
            return text + "…";
        }
    }
}
