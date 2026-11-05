using System;
using System.Collections.Generic;
using System.Linq;

namespace LastWish.Presentation
{
    /// <summary>What the shopkeeper reacts to.</summary>
    public enum MerchantCue { Enter, Idle, HoverRelic, HoverPack, HoverGolden, HoverPoor, BuyRelic, BuyPack, BuyGolden, BuyFree, Poor, Sold, SoldOut }

    /// <summary>The shopkeeper's lines (on screen, so in Spanish). <c>{item}</c> is the offer's name, <c>{short}</c> the gold
    /// that is missing. Lines never refer to the item with a gendered word, so any relic or pack name fits.</summary>
    public static class MerchantLines
    {
        public static readonly IReadOnlyDictionary<MerchantCue, string[]> All = new Dictionary<MerchantCue, string[]>
        {
            [MerchantCue.Enter] = new[]
            {
                "¡Bienvenido, viajero! Todo lo que ves está a la venta.", "Ah, un cliente. Las criptas son malas para el negocio.",
                "Pasa, pasa. Mis precios son casi honrados.", "Mira sin miedo: mirar todavía es gratis.",
                "¿Sigues vivo? Entonces puedes comprar algo.",
            },
            [MerchantCue.Idle] = new[]
            {
                "Este farol no se apaga ni con la lluvia de la cripta.", "Llevo tres pisos cargando esta mochila. Compra algo y aligérame.",
                "Los muertos no compran nada. Tú sí, ¿verdad?", "Una vez le vendí una espada a un esqueleto. Bueno... se la llevó.",
                "No regateo. Bueno, casi nunca.", "¿Te decides o le pongo precio a tu indecisión?",
            },
            [MerchantCue.HoverRelic] = new[]
            {
                "¿{item}? Tienes buen ojo.", "Esa la saqué de un cofre que respiraba.", "Tiene historia. Y precio.",
                "Con eso las criptas te temerán un poco más.",
            },
            [MerchantCue.HoverPack] = new[] { "Cinco cartas y una sorpresa.", "Los sobres nunca mienten. Casi nunca.", "¿{item}? Ábrelo y elige con calma." },
            [MerchantCue.HoverGolden] = new[] { "¡Esa brilla! Efecto doble, placer doble.", "Oro de verdad. Lo mordí para comprobarlo." },
            [MerchantCue.HoverPoor] = new[] { "Te faltan {short} monedas. Puedo esperar.", "Mirar es gratis. Comprar ya es otra historia." },
            [MerchantCue.BuyRelic] = new[]
            {
                "¡Trato hecho! Cuídala bien.", "¡{item}! No acepto devoluciones.", "Excelente elección. Mi bolsa te lo agradece.",
                "Que te dé suerte allá abajo.",
            },
            [MerchantCue.BuyPack] = new[] { "¡Ábrelo! A ver qué te toca.", "Que salga algo bueno...", "Cinco cartas, un destino." },
            [MerchantCue.BuyGolden] = new[] { "¡La dorada! Esa la voy a extrañar.", "Brilla más en tus manos que en las mías." },
            [MerchantCue.BuyFree] = new[] { "Con ese vale... un trato es un trato.", "Gratis. Me duele, pero cumplo mi palabra." },
            [MerchantCue.Poor] = new[]
            {
                "Sin oro no hay trato, amigo.", "Te faltan {short} de oro. Vuelve con la bolsa llena.", "Mi generosidad tiene un límite: el precio.",
            },
            [MerchantCue.Sold] = new[] { "Ya lo vendí. Ni yo tengo dos.", "Agotado. Haber madrugado." },
            [MerchantCue.SoldOut] = new[] { "¡Me dejaste sin nada! Hasta la próxima, viajero." },
        };

        public static string Fill(string line, string item, int shortBy) =>
            line.Replace("{item}", item ?? "Eso").Replace("{short}", shortBy.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", " "));
    }

    /// <summary>
    /// What the shopkeeper says and when (<see cref="NpcSpeech{TCue}"/>): purchases, refusals and greetings always speak;
    /// hovering an offer only sometimes; after a quiet spell he chats.
    /// </summary>
    public sealed class MerchantSpeech : NpcSpeech<MerchantCue>
    {
        public MerchantSpeech(int seed) : base(seed, MerchantLines.All, MerchantCue.Idle, MerchantLines.Fill) { }

        /// <summary>The animation a line starts with: he tips his hat when you walk in, bows after a sale (and when you empty
        /// the shop), shakes his head at a refusal; other lines have none (he just talks).</summary>
        public static string ReactionOf(MerchantCue cue)
        {
            switch (cue)
            {
                case MerchantCue.Enter: return "greet";
                case MerchantCue.BuyRelic: case MerchantCue.BuyPack: case MerchantCue.BuyGolden: case MerchantCue.BuyFree: case MerchantCue.SoldOut: return "thanks";
                case MerchantCue.Poor: case MerchantCue.Sold: return "no";
                default: return null;
            }
        }
    }

    /// <summary>
    /// Which animation of El Mercader plays (<see cref="NpcPose"/>): greet, thanks and no follow his lines, he talks while a
    /// line is typed and flips a coin when the shop has been quiet a while.
    /// </summary>
    public sealed class MerchantPose : NpcPose
    {
        public MerchantPose(int seed) : base(seed, "coins") { }
        public void Update(float dt, MerchantSpeech speech, NpcSheet sheet) => Update(dt, speech.Lines, MerchantSpeech.ReactionOf(speech.Cue), speech.Talking, sheet);
    }

    /// <summary>El Mercader's sheet: Tools/VfxLab/merchant.html → export_merchant_art.py → Resources/Game/Shop.</summary>
    public static class MerchantSheet
    {
        public static NpcSheet Load(Art art) => NpcSheet.Load(art, "Shop/merchant");
    }
}
