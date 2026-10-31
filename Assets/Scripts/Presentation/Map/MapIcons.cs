using System.Collections.Generic;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>
    /// The room icons of the floor map (Tools/VfxLab/map_icons.py, docs/art-style.md): one atlas in native pixels, a second
    /// atlas with only their emissive bits (eyes, gems, flames, runes) to add on top as a halo, and the colour each icon glows.
    /// </summary>
    public sealed class MapIcons
    {
        public const string SheetKey = "Map/map_icons", HaloKey = "Map/map_icons_halo";
        public const string Boss = "Boss", Marker = "Marker";
        private static readonly Rgba DefaultGlow = new Rgba(80, 210, 255);

        private readonly Dictionary<string, (int x, int y, int w, int h)> rects = new Dictionary<string, (int, int, int, int)>();
        private readonly Dictionary<string, Rgba> glows = new Dictionary<string, Rgba>();
        public Tex Sheet { get; private set; }
        public Tex Halo { get; private set; }

        public static MapIcons Load(Art art)
        {
            var icons = new MapIcons();
            var meta = Json.Obj(Json.Parse(art.ReadText(SheetKey)));
            var rects = Json.Obj(meta.Get("icons"));
            if (rects != null)
                foreach (var pair in rects) { var r = Json.Ints(pair.Value); if (r != null && r.Length == 4) icons.rects[pair.Key] = (r[0], r[1], r[2], r[3]); }
            var glows = Json.Obj(meta.Get("glow"));
            if (glows != null)
                foreach (var pair in glows) { var c = Json.Ints(pair.Value); if (c != null && c.Length >= 3) icons.glows[pair.Key] = new Rgba(c[0], c[1], c[2]); }
            icons.Sheet = art.Texture(SheetKey);
            icons.Halo = art.Texture(HaloKey);
            return icons;
        }

        /// <summary>The icon of a room: the boss hall has its own, every other room the name of its kind.</summary>
        public static string NameOf(MapRoom room, bool boss) => boss || room.Kind == RoomKind.Boss ? Boss : room.Kind.ToString();

        public bool Has(string name) => name != null && rects.ContainsKey(name);
        public IEnumerable<string> Names => rects.Keys;

        public Sprite Icon(string name) => name != null && rects.TryGetValue(name, out var r) ? new Sprite(Sheet, r.x, r.y, r.w, r.h) : default;

        /// <summary>The emissive bits of an icon (black elsewhere), to draw additively over it.</summary>
        public Sprite HaloOf(string name) => name != null && rects.TryGetValue(name, out var r) ? new Sprite(Halo, r.x, r.y, r.w, r.h) : default;

        public Rgba Glow(string name) => name != null && glows.TryGetValue(name, out var c) ? c : DefaultGlow;
    }
}
