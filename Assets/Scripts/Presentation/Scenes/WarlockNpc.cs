using System.Collections.Generic;

namespace LastWish.Presentation
{
    /// <summary>What El Brujo reacts to.</summary>
    public enum WarlockCue { Enter, Idle, HoverCard, HoverRare, HoverUpgraded, HoverPoor, Select, Upgrade, Poor, Maxed, Cancel, AllDone }

    /// <summary>El Brujo's lines (on screen, so in Spanish). <c>{item}</c> is a card's name, <c>{short}</c> the gold that is
    /// missing. He only ever talks about cards, so "esa" (carta) always agrees.</summary>
    public static class WarlockLines
    {
        public static readonly IReadOnlyDictionary<WarlockCue, string[]> All = new Dictionary<WarlockCue, string[]>
        {
            [WarlockCue.Enter] = new[]
            {
                "Ah... un alma con cartas. Acércate, que no muerdo.", "El fuego verde me dijo que vendrías.",
                "Mis runas tienen hambre. ¿Qué carta les das?", "Pasa. Aquí el oro se vuelve poder.",
                "¿Otra vez vivo? Bien. Los muertos no pagan.",
            },
            [WarlockCue.Idle] = new[]
            {
                "Este caldero lleva tres siglos hirviendo. Más o menos.", "Las cartas susurran. Algunas piden a gritos un poco de poder.",
                "No toques el caldero. Lo digo por tus dedos.", "¿Dudas? La duda también tiene precio.",
                "Mi bastón recuerda cada carta que tocó.", "Un poco de oro, un poco de magia... y nada vuelve a ser igual.",
            },
            [WarlockCue.HoverCard] = new[]
            {
                "¿{item}? Puedo hacerla brillar.", "Esa tiene potencial. Lo huelo.", "Un toque de runas y {item} será otra cosa.",
                "Buena carta. Podría ser mejor.",
            },
            [WarlockCue.HoverRare] = new[] { "Esa guarda magia antigua. Te costará.", "Rara... me gusta trabajar con cosas raras." },
            [WarlockCue.HoverUpgraded] = new[] { "Esa ya probó mi magia. Más no aguanta.", "Ya la mejoré. Pedirle más sería romperla." },
            [WarlockCue.HoverPoor] = new[] { "Te faltan {short} monedas para esa.", "Las runas no se pagan con promesas." },
            [WarlockCue.Select] = new[] { "Así quedará. ¿Lo hacemos?", "Buena elección. Mira lo que será.", "Una vez hecho, no hay vuelta atrás." },
            [WarlockCue.Upgrade] = new[]
            {
                "¡Despierta, {item}!", "¡Runas, aceptad el oro!", "¡Por el fuego verde!", "¡Que crezca su poder!",
            },
            [WarlockCue.Poor] = new[] { "Sin oro, no hay magia.", "Te faltan {short} de oro. Las runas no fían.", "Vuelve cuando tu bolsa pese más." },
            [WarlockCue.Maxed] = new[] { "Esa ya está mejorada. Ni yo hago milagros dos veces.", "Una mejora por carta. Son las reglas de la magia." },
            [WarlockCue.Cancel] = new[] { "¿Te lo piensas? Sabio... o cobarde.", "Como quieras. El caldero esperará." },
            [WarlockCue.AllDone] = new[] { "Ya no queda nada que mejorar. Tu mazo brilla.", "Todas tus cartas probaron mi magia. ¡Vete a usarlas!" },
        };

        public static string Fill(string line, string item, int shortBy) =>
            line.Replace("{item}", item ?? "esa carta").Replace("{short}", shortBy.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", " "));
    }

    /// <summary>What El Brujo says and when (<see cref="NpcSpeech{TCue}"/>).</summary>
    public sealed class WarlockSpeech : NpcSpeech<WarlockCue>
    {
        public WarlockSpeech(int seed) : base(seed, WarlockLines.All, WarlockCue.Idle, WarlockLines.Fill) { }

        /// <summary>The animation a line starts with: a green flame in his palm when you walk in (and when nothing is left to
        /// upgrade), the staff raised for the upgrade itself (he shouts the spell), a shaken head at a refusal; the other lines
        /// have none (he just talks).</summary>
        public static string ReactionOf(WarlockCue cue)
        {
            switch (cue)
            {
                case WarlockCue.Enter: case WarlockCue.AllDone: return "greet";
                case WarlockCue.Upgrade: return "cast";
                case WarlockCue.Poor: case WarlockCue.Maxed: return "no";
                default: return null;
            }
        }
    }

    /// <summary>Which animation of El Brujo plays (<see cref="NpcPose"/>); when the room is quiet he strokes his beard.</summary>
    public sealed class WarlockPose : NpcPose
    {
        public WarlockPose(int seed) : base(seed, "ponder") { }
        public void Update(float dt, WarlockSpeech speech, NpcSheet sheet) => Update(dt, speech.Lines, WarlockSpeech.ReactionOf(speech.Cue), speech.Talking, sheet);
    }

    /// <summary>El Brujo's sheet: Tools/VfxLab/warlock.html → export_warlock_art.py → Resources/Game/Warlock.</summary>
    public static class WarlockSheet
    {
        public static NpcSheet Load(Art art) => NpcSheet.Load(art, "Warlock/warlock");
    }
}
