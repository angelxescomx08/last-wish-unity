using System;
using System.Collections.Generic;
using System.Linq;

namespace LastWish.Presentation
{
    public interface IAssetSource
    {
        /// <summary>Text of a JSON asset (key without extension, e.g. "Ui/kit").</summary>
        string ReadText(string key);
    }

    /// <summary>One animation row (sprite_loader.HeroAnimation).</summary>
    public sealed class SpriteAnimation
    {
        public int Row { get; }
        public float[] Durations { get; }
        public bool Loop { get; }
        public float Total { get; }
        public SpriteAnimation(int row, float[] durations, bool loop)
        { Row = row; Durations = durations; Loop = loop; Total = durations.Sum(); }
        public int FrameAt(float elapsed)
        {
            var t = Math.Max(0f, elapsed);
            if (Loop && Total > 0) t %= Total;
            else if (t >= Total) return Durations.Length - 1;
            for (var i = 0; i < Durations.Length; i++)
            {
                if (t < Durations[i] - 1e-9f) return i;
                t -= Durations[i];
            }
            return Durations.Length - 1;
        }
        public float SecondsUntil(int frame) { float s = 0; for (var i = 0; i < Math.Min(frame, Durations.Length); i++) s += Durations[i]; return s; }
    }

    /// <summary>A hero's sheets (192 px combat, 96 px small) and timing events.</summary>
    public sealed class HeroSheets
    {
        public string Id { get; }
        public int Cell { get; }
        public Dictionary<string, SpriteAnimation> Animations { get; } = new Dictionary<string, SpriteAnimation>();
        public int? StrikeFrame { get; }
        public (float x, float y)? StrikePoint { get; }
        public int? CastFrame { get; }
        public (float x, float y)? CastPoint { get; }
        private readonly Dictionary<int, (Tex tex, int columns, int split)> sheets = new Dictionary<int, (Tex, int, int)>();

        public HeroSheets(string id, string json, Func<string, Tex> load)
        {
            Id = id;
            var meta = Json.Obj(Json.Parse(json));
            Cell = Json.Int(meta.Get("cell"), 192);
            var pack = Json.Obj(meta.Get("pack"));
            foreach (var pair in Json.Obj(meta.Get("sheets")))
            {
                var info = Json.Obj(pack.Get(pair.Key));
                sheets[int.Parse(pair.Key)] = (load(Json.Str(pair.Value)), Json.Int(info.Get("columns")), Json.Int(info.Get("split"), 1));
            }
            foreach (var pair in Json.Obj(meta.Get("animations")))
            {
                var a = Json.Obj(pair.Value);
                Animations[pair.Key] = new SpriteAnimation(Json.Int(a.Get("row")), Json.Nums(a.Get("durations_ms")).Select(ms => (float)(ms / 1000.0)).ToArray(), (bool)a.Get("loop"));
            }
            var events = Json.Obj(meta.Get("events"));
            var attack = Json.Obj(events.Get("attack")); var cast = Json.Obj(events.Get("cast"));
            if (attack?.Get("strike_frame") != null) StrikeFrame = Json.Int(attack.Get("strike_frame"));
            var sp = Json.Nums(attack?.Get("strike_point")); if (sp != null && sp.Length == 2) StrikePoint = ((float)sp[0], (float)sp[1]);
            if (cast?.Get("cast_frame") != null) CastFrame = Json.Int(cast.Get("cast_frame"));
            var cp = Json.Nums(cast?.Get("cast_point")); if (cp != null && cp.Length == 2) CastPoint = ((float)cp[0], (float)cp[1]);
        }

        public float Seconds(string name) => Animations.TryGetValue(name, out var a) ? a.Total : 0f;
        public float StrikeSeconds => StrikeFrame.HasValue && Animations.TryGetValue("attack", out var a) ? a.SecondsUntil(StrikeFrame.Value) : 0f;
        public float CastSeconds => CastFrame.HasValue && Animations.TryGetValue("cast", out var a) ? a.SecondsUntil(CastFrame.Value) : 0f;
        /// <summary>Weapon contact offset from the centre of the 192 px sprite (metadata uses the 96 px canvas).</summary>
        public (float x, float y)? StrikeOffset => StrikePoint.HasValue ? (2 * StrikePoint.Value.x - 96, 2 * StrikePoint.Value.y - 96) : ((float, float)?)null;
        public (float x, float y)? CastOffset => CastPoint.HasValue ? (2 * CastPoint.Value.x - 96, 2 * CastPoint.Value.y - 96) : ((float, float)?)null;

        /// <summary>The frame of <paramref name="animation"/> at <paramref name="elapsed"/>, from the sheet whose cell divides <paramref name="size"/>.</summary>
        public Sprite Frame(string animation, float elapsed, int size, out float scale)
        {
            var name = Animations.ContainsKey(animation) ? animation : "idle";
            var anim = Animations[name];
            var fit = sheets.Keys.Where(c => size % c == 0).ToList();
            var cell = fit.Count > 0 ? fit.Max() : Cell;
            var (tex, columns, split) = sheets[cell];
            var frame = anim.FrameAt(elapsed);
            var row = anim.Row * split + frame / columns; var col = frame % columns;
            scale = size / (float)cell;
            return new Sprite(tex, col * cell, row * cell, cell, cell);
        }
    }

    /// <summary>infrastructure/enemy_sprites.EnemySheet: native cells drawn ×scale.</summary>
    public sealed class EnemySheet
    {
        public string Id { get; }
        public int CellW { get; }
        public int CellH { get; }
        public int Scale { get; }
        public (int w, int h) Size => (CellW * Scale, CellH * Scale);
        public (int x, int y) Anchor { get; }
        public Dictionary<string, SpriteAnimation> Animations { get; } = new Dictionary<string, SpriteAnimation>();
        public Dictionary<string, int[]> Strikes { get; } = new Dictionary<string, int[]>();
        public Dictionary<string, bool> BladeEvents { get; } = new Dictionary<string, bool>();
        public Dictionary<string, string> Moves { get; } = new Dictionary<string, string>();
        public bool IsBoss { get; }
        public bool IsElite { get; }
        public int Top { get; }
        /// <summary>Opaque bounds of idle frame 0 in native px (x, y, w, h): the combat-log portrait.</summary>
        public (int x, int y, int w, int h) IdleBox { get; }
        public string[] Terminal { get; }
        public Tex Texture { get; }
        public Tex BladeTexture { get; }
        public int BladeSize { get; }
        public int BladeRotations { get; }
        public Dictionary<string, object> Meta { get; }

        public EnemySheet(string id, string json, Func<string, Tex> load)
        {
            Id = id; Meta = Json.Obj(Json.Parse(json));
            CellW = Json.Int(Meta.Get("cell_w")); CellH = Json.Int(Meta.Get("cell_h")); Scale = Math.Max(1, Json.Int(Meta.Get("scale"), 2));
            Texture = load("Enemies/" + System.IO.Path.GetFileNameWithoutExtension(Json.Str(Meta.Get("sheet"))));
            foreach (var pair in Json.Obj(Meta.Get("animations")))
            {
                var a = Json.Obj(pair.Value);
                Animations[pair.Key] = new SpriteAnimation(Json.Int(a.Get("row")), Json.Nums(a.Get("durations_ms")).Select(ms => (float)(ms / 1000.0)).ToArray(), (bool)a.Get("loop"));
            }
            var anchor = Json.Ints(Meta.Get("anchor"));
            Anchor = anchor != null ? (anchor[0] * Scale, anchor[1] * Scale) : (CellW / 2 * Scale, CellH * Scale);
            var events = Json.Obj(Meta.Get("events"));
            if (events != null)
                foreach (var pair in events)
                {
                    var ev = Json.Obj(pair.Value); if (ev == null) continue;
                    var s = Json.Ints(ev.Get("strikes")); if (s != null) Strikes[pair.Key] = s;
                    if (ev.Get("blades") is bool b) BladeEvents[pair.Key] = b;
                }
            var moves = Json.Obj(Meta.Get("moves"));
            if (moves != null) foreach (var pair in moves) Moves[pair.Key] = Json.Str(pair.Value);
            IsBoss = Meta.Get("boss") is bool boss && boss; IsElite = Meta.Get("elite") is bool elite && elite;
            Top = Json.Int(Meta.Get("top_native")) * Scale;
            var box = Json.Ints(Meta.Get("idle_bbox_native"));
            IdleBox = box != null && box.Length == 4 ? (box[0], box[1], box[2] - box[0], box[3] - box[1]) : (0, 0, CellW, CellH);
            var terminal = Json.Arr(Meta.Get("terminal"));
            Terminal = terminal == null ? Array.Empty<string>() : terminal.Select(Json.Str).Where(Animations.ContainsKey).ToArray();
            var blade = Json.Obj(Meta.Get("blade"));
            if (blade != null)
            {
                BladeTexture = load("Enemies/" + System.IO.Path.GetFileNameWithoutExtension(Json.Str(blade.Get("sheet"))));
                BladeSize = Json.Int(blade.Get("size")); BladeRotations = Json.Int(blade.Get("rotations"));
            }
        }

        public float Seconds(string animation) => Animations.TryGetValue(animation, out var a) ? a.Total : 0f;
        public Sprite Frame(string animation, float elapsed)
        {
            var name = Animations.ContainsKey(animation) ? animation : "idle";
            var anim = Animations[name];
            var frame = anim.FrameAt(elapsed);
            return new Sprite(Texture, frame * CellW, anim.Row * CellH, CellW, CellH);
        }
        public Sprite Portrait()
        {
            var idle = Animations.TryGetValue("idle", out var a) ? a.Row : 0;
            return new Sprite(Texture, IdleBox.x, idle * CellH + IdleBox.y, IdleBox.w, IdleBox.h);
        }
        public Sprite Blade(int rotation) => new Sprite(BladeTexture, (rotation % Math.Max(1, BladeRotations)) * BladeSize, 0, BladeSize, BladeSize);
        public float[] StrikeSeconds(string animation)
        {
            if (!Animations.TryGetValue(animation, out var anim)) return Array.Empty<float>();
            if (!Strikes.TryGetValue(animation, out var frames)) frames = animation == "attack" ? new[] { 3 } : Array.Empty<int>();
            return frames.Where(f => f >= 0 && f <= anim.Durations.Length).Select(anim.SecondsUntil).ToArray();
        }
        public string AnimationForMove(string moveId, string fallback) =>
            moveId != null && Moves.TryGetValue(moveId, out var name) && Animations.ContainsKey(name) ? name : fallback;
    }

    /// <summary>All art and metadata of the game, loaded once (the infrastructure layer of the original).</summary>
    public sealed class Art
    {
        private readonly IRenderBackend backend;
        private readonly IAssetSource source;
        private readonly Dictionary<string, Tex> textures = new Dictionary<string, Tex>();
        private readonly Dictionary<string, (int x, int y, int w, int h, int[] slice)> kit = new Dictionary<string, (int, int, int, int, int[])>();
        private readonly Dictionary<string, (int x, int y, int w, int h)> icons = new Dictionary<string, (int, int, int, int)>();
        private readonly Dictionary<string, (int x, int y, int w, int h)> relics = new Dictionary<string, (int, int, int, int)>();
        private readonly Dictionary<string, HeroSheets> heroes = new Dictionary<string, HeroSheets>();
        private readonly Dictionary<string, EnemySheet> enemies = new Dictionary<string, EnemySheet>();
        private readonly HashSet<string> cardArt = new HashSet<string>();
        public FontRegistry Fonts { get; private set; }
        public Dictionary<string, object> CardLayout { get; private set; }
        public Dictionary<string, object> Dungeon { get; private set; }
        public Dictionary<string, object> Gacha { get; private set; }
        public Tex KitTexture { get; private set; }
        public Tex IconTexture { get; private set; }
        public Tex RelicTexture { get; private set; }
        public int KitScale { get; private set; } = 2;

        public static readonly Dictionary<string, string> EnemySheetIds = new Dictionary<string, string> {
            { "Espectro", "wraith" }, { "Reina Micélida", "mycelid" }, { "La Tejedora", "weaver" }, { "Caballero Hueco", "knight" },
            { "Babosa Ácida", "slime" }, { "Gusano de Tumba", "worm" }, { "Ojo Vigilante", "eye" }, { "Cráneo Ígneo", "skull" },
            { "Murciélago Vampiro", "bat" }, { "Seta Explosiva", "bomb" }, { "Gólem de Musgo", "golem" }, { "Acólito de Ceniza", "acolyte" },
            { "Diablillo", "imp" }, { "Mímico", "mimic" }, { "El Verdugo", "executioner" }, { "Bruja del Pantano", "hag" },
            { "Gárgola", "gargoyle" }, { "Minotauro", "minotaur" }, { "Escorpión Rey", "scorpion" },
        };
        public static readonly Dictionary<string, string> HeroIds = new Dictionary<string, string> {
            { "La Guerrera", "warrior" }, { "El Guerrero", "warrior" }, { "El Mago", "mage" }, { "La Pícara", "rogue" }, { "El Pícaro", "rogue" },
        };

        public Art(IRenderBackend backend, IAssetSource source) { this.backend = backend; this.source = source; }

        public Tex Texture(string key)
        {
            if (!textures.TryGetValue(key, out var tex)) textures[key] = tex = backend.LoadTexture(key);
            return tex;
        }

        /// <summary>Loads the small always-needed assets (fonts, kit, icons, relics, metadata).</summary>
        public void LoadCore()
        {
            BitmapFont F(string key) => new BitmapFont(key, Texture("Fonts/" + key), source.ReadText("Fonts/" + key));
            Fonts = new FontRegistry(F("lw8"), F("lw16"), F("lw8b"), F("lw16b"), F("tiny5"));
            KitTexture = Texture("Ui/kit");
            var kitMeta = Json.Obj(Json.Parse(source.ReadText("Ui/kit")));
            KitScale = Json.Int(kitMeta.Get("scale"), 2);
            foreach (var pair in Json.Obj(kitMeta.Get("pieces")))
            {
                var p = Json.Obj(pair.Value); var r = Json.Ints(p.Get("rect"));
                kit[pair.Key] = (r[0], r[1], r[2], r[3], Json.Ints(p.Get("slice")));
            }
            IconTexture = Texture("Ui/icons");
            foreach (var pair in Json.Obj(Json.Obj(Json.Parse(source.ReadText("Ui/icons"))).Get("icons")))
            { var r = Json.Ints(pair.Value); icons[pair.Key] = (r[0], r[1], r[2], r[3]); }
            RelicTexture = Texture("Relics/relics");
            foreach (var pair in Json.Obj(Json.Parse(source.ReadText("Relics/relics"))))
            { var r = Json.Ints(pair.Value); relics[pair.Key] = (r[0], r[1], r[2], r[3]); }
            CardLayout = Json.Obj(Json.Parse(source.ReadText("Cards/layout")));
            foreach (var id in Json.Arr(CardLayout.Get("art"))) cardArt.Add(Json.Str(id));
            Dungeon = Json.Obj(Json.Parse(source.ReadText("Dungeon/dungeon")));
            Gacha = Json.Obj(Json.Parse(source.ReadText("Gacha/gacha")));
        }

        // ---------------------------------------------------------------- kit
        public bool HasKitPiece(string name) => kit.ContainsKey(name);
        public Sprite KitPiece(string name)
        {
            if (!kit.TryGetValue(name, out var p)) return default;
            return new Sprite(KitTexture, p.x, p.y, p.w, p.h);
        }
        public (int w, int h) KitSize(string name) => kit.TryGetValue(name, out var p) ? (p.w, p.h) : (0, 0);

        /// <summary>ui_kit.kit_slice drawn at <paramref name="dst"/>: corners kept, edges and centre tiled at native scale.</summary>
        public void DrawKitSlice(Canvas canvas, string name, RectF dst, int scale = 2, Rgba? tint = null)
        {
            if (!kit.TryGetValue(name, out var p) || dst.W <= 0 || dst.H <= 0) return;
            var color = tint ?? Rgba.White;
            var sl = p.slice ?? new[] { 0, 0, 0, 0 };
            int left = sl[0], top = sl[1], right = sl[2], bottom = sl[3];
            var nativeW = Math.Max(1, (int)Math.Round(dst.W / scale, MidpointRounding.ToEven));
            var threeSlice = top == 0 && bottom == 0;
            var builtW = Math.Max(nativeW, left + right);
            var builtH = threeSlice ? p.h : Math.Max(Math.Max(1, (int)Math.Round(dst.H / scale, MidpointRounding.ToEven)), top + bottom);
            var sx = dst.W / builtW; var sy = dst.H / builtH;
            int midW = p.w - left - right, midH = p.h - top - bottom;
            var cols = new[] { (0, left, 0, left), (left, midW, left, builtW - left - right), (p.w - right, right, builtW - right, right) };
            var rows = new[] { (0, top, 0, top), (top, midH, top, builtH - top - bottom), (p.h - bottom, bottom, builtH - bottom, bottom) };
            foreach (var (srcX, srcW, dstX, dstW) in cols)
                foreach (var (srcY, srcH, dstY, dstH) in rows)
                {
                    if (srcW <= 0 || srcH <= 0 || dstW <= 0 || dstH <= 0) continue;
                    for (var y = 0; y < dstH; y += srcH)
                        for (var x = 0; x < dstW; x += srcW)
                        {
                            var w = Math.Min(srcW, dstW - x); var h = Math.Min(srcH, dstH - y);
                            canvas.BlitRect(new Sprite(KitTexture, p.x + srcX, p.y + srcY, w, h),
                                new RectF(dst.X + (dstX + x) * sx, dst.Y + (dstY + y) * sy, w * sx, h * sy), color);
                        }
                }
        }

        // ---------------------------------------------------------------- icons / relics
        public bool HasIcon(string name) => name != null && icons.ContainsKey(name);
        public Sprite Icon(string name)
        {
            if (name == null || !icons.TryGetValue(name, out var r)) return default;
            return new Sprite(IconTexture, r.x, r.y, r.w, r.h);
        }
        public Sprite Relic(string name)
        {
            if (name == null || !relics.TryGetValue(name, out var r)) return default;
            return new Sprite(RelicTexture, r.x, r.y, r.w, r.h);
        }

        // ---------------------------------------------------------------- cards
        public static string RarityKey(int rarity)
        {
            switch (rarity) { case 2: return "UNCOMMON"; case 3: return "RARE"; case 4: return "EPIC"; case 5: return "LEGENDARY"; default: return "COMMON"; }
        }
        public Sprite CardFrame(int rarity)
        {
            var info = Json.Obj(Json.Obj(CardLayout.Get("frames")).Get(RarityKey(rarity)));
            return info == null ? default : new Sprite(Texture(Json.Str(info.Get("file"))));
        }
        public (double x0, double y0, double x1, double y1) CardArtZone(int rarity)
        {
            var art = Json.Obj(Json.Obj(Json.Obj(CardLayout.Get("frames")).Get(RarityKey(rarity))).Get("art"));
            return (Json.Num(art.Get("x0")), Json.Num(art.Get("y0")), Json.Num(art.Get("x1")), Json.Num(art.Get("y1")));
        }
        public Dictionary<string, object> CardZone(string name) => Json.Obj(Json.Obj(CardLayout.Get("zones")).Get(name));
        public Sprite PackArt(string theme)
        {
            var rel = Json.Str(Json.Obj(CardLayout.Get("packs")).Get(theme));
            return rel == null ? default : new Sprite(Texture(rel));
        }
        public Sprite CardBack => new Sprite(Texture("Cards/back"));
        public bool HasCardArt(string cardId) => cardId != null && cardArt.Contains(cardId);
        public Sprite CardArt(string cardId) => HasCardArt(cardId) ? new Sprite(Texture("Cards/Art/" + cardId)) : default;
        public Sprite CardPlaceholder(string type) => new Sprite(Texture("Cards/Placeholder/" + type.ToLowerInvariant()));

        // ---------------------------------------------------------------- sheets
        public HeroSheets Hero(string id)
        {
            if (id == null) return null;
            if (!heroes.TryGetValue(id, out var sheets)) heroes[id] = sheets = new HeroSheets(id, source.ReadText("Heroes/" + id), Texture);
            return sheets;
        }
        public static string HeroIdFor(string playerName) => playerName != null && HeroIds.TryGetValue(playerName, out var id) ? id : null;
        public EnemySheet EnemySheetById(string id)
        {
            if (id == null) return null;
            if (!enemies.TryGetValue(id, out var sheet)) enemies[id] = sheet = new EnemySheet(id, source.ReadText("Enemies/" + id + "_sheet"), Texture);
            return sheet;
        }
        public EnemySheet SheetForEnemy(string name) => name != null && EnemySheetIds.TryGetValue(name, out var id) ? EnemySheetById(id) : null;

        /// <summary>Procedural texture cache (soft glows, vignettes, etc.).</summary>
        public Tex Procedural(string key, int w, int h, Func<Rgba[]> build, bool smooth = false)
        {
            if (!textures.TryGetValue(key, out var tex)) textures[key] = tex = backend.CreateTexture(key, w, h, build(), smooth);
            return tex;
        }
    }
}
