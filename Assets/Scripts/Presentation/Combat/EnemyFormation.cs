using System.Collections.Generic;

namespace LastWish.Presentation
{
    /// <summary>
    /// Where each enemy stands on the 1280×720 canvas. The original fights keep their coordinates (three small slots,
    /// or the single boss spot); the expansion adds a large enemy with summoned small ones, and two large bosses.
    /// The domain cap (<c>EnemyField</c>) guarantees the alive enemies always fit the chosen formation.
    /// </summary>
    public sealed class EnemyFormation
    {
        public enum Kind { Line3, Boss1, Boss1Plus2, Boss2 }

        /// <summary>A small slot (X, Y = top-left of the 128×150 rect) or a large one (X = centre, Y = ground, W = HUD width).</summary>
        public readonly struct Place
        {
            public readonly bool Large; public readonly int X, Y, W;
            public Place(bool large, int x, int y, int w) { Large = large; X = x; Y = y; W = w; }
        }

        public const int SmallY = 125, Ground = 314;
        private static readonly Place[] Line3 = { new Place(false, 762, SmallY, 128), new Place(false, 942, SmallY, 128), new Place(false, 1122, SmallY, 128) };
        private static readonly Place[] Boss1 = { new Place(true, 905, Ground, 230) };
        // the HUD badges stick out of the rects (block ~14 px left, element ~35 px right): every gap here is ≥ 49 px
        private static readonly Place[] Boss1Plus2 = { new Place(false, 648, SmallY, 128), new Place(false, 826, SmallY, 128), new Place(true, 1106, Ground, 200) };
        private static readonly Place[] Boss2 = { new Place(true, 770, Ground, 230), new Place(true, 1080, Ground, 230) };

        public readonly Kind Shape;
        public readonly IReadOnlyList<Place> Places;

        public EnemyFormation(Kind shape)
        {
            Shape = shape;
            Places = shape == Kind.Line3 ? Line3 : shape == Kind.Boss1 ? Boss1 : shape == Kind.Boss1Plus2 ? Boss1Plus2 : (IReadOnlyList<Place>)Boss2;
        }

        public static IReadOnlyList<Place> PlacesOf(Kind shape) => new EnemyFormation(shape).Places;

        /// <summary>0 large ⇒ three slots; 1 large ⇒ the boss spot, or the boss with two small slots if it can bring small enemies; 2 large ⇒ side by side.</summary>
        public static Kind Choose(int large, bool bringsSmall) =>
            large <= 0 ? Kind.Line3 : large == 1 ? (bringsSmall ? Kind.Boss1Plus2 : Kind.Boss1) : Kind.Boss2;

        /// <summary>
        /// A free place of the right size: the preferred one if it is free, then one without a dying animator on it, then any
        /// free one; -1 if none (the caller then reuses one of its size).
        /// </summary>
        public int Pick(bool large, ICollection<int> taken, ICollection<int> dying, int preferred = -1)
        {
            if (preferred >= 0 && preferred < Places.Count && Places[preferred].Large == large && !taken.Contains(preferred)) return preferred;
            for (var pass = 0; pass < 2; pass++)
                for (var i = 0; i < Places.Count; i++)
                    if (Places[i].Large == large && !taken.Contains(i) && (pass == 1 || !dying.Contains(i))) return i;
            return -1;
        }

        /// <summary>The first place of that size (fallback when the domain somehow outgrew the formation).</summary>
        public int AnyOfSize(bool large)
        {
            for (var i = 0; i < Places.Count; i++) if (Places[i].Large == large) return i;
            return 0;
        }
    }
}
