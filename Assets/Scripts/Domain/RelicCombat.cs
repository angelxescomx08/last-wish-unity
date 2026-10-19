using System;
using System.Collections.Generic;
using System.Linq;
namespace LastWish.Domain
{
    public enum ChromaKind { None, Golden, Diamond }
    public sealed class OwnedRelic
    {
        public RelicDefinition Definition { get; }
        public ChromaKind Chroma { get; }
        public int Multiplier => (int)Chroma + 1;
        public bool Active { get; private set; } = true;
        public int TimesTriggered { get; private set; }
        public OwnedRelic(RelicDefinition definition, ChromaKind chroma = ChromaKind.None)
        { Definition = definition ?? throw new ArgumentNullException(nameof(definition)); if (!Enum.IsDefined(typeof(ChromaKind), chroma)) throw new ArgumentOutOfRangeException(); Chroma = chroma; }
        public void Restore(bool active, int uses)
        { if (uses < 0) throw new ArgumentOutOfRangeException(); Active = active; TimesTriggered = uses; }
        internal void SpendCharge() { TimesTriggered++; if (TimesTriggered >= Multiplier) Active = false; }
        internal OwnedRelic Clone() { var copy = new OwnedRelic(Definition, Chroma); copy.Restore(Active, TimesTriggered); return copy; }
    }
    public sealed partial class CombatState
    {
        private Dictionary<string, List<OwnedRelic>> relicsByTag = new Dictionary<string, List<OwnedRelic>>();
        private int manaMaximum;
        public int ManaMaximum => manaMaximum;
        public int HpLostCombat { get; private set; }
        private int RelicPower(string tag, int amount = 1)
        {
            if (!relicsByTag.TryGetValue(tag, out var items)) return 0;
            long total = 0; foreach (var relic in items) if (relic.Active) total += (long)amount * relic.Multiplier;
            return Saturate(total);
        }
        private bool HasRelic(string tag) => RelicPower(tag) > 0;
        private void BindRelics(IEnumerable<OwnedRelic> relics)
        {
            if (relics == null) return;
            foreach (var relic in relics)
            {
                if (relic == null) throw new ArgumentException("Null relic");
                var tag = relic.Definition.Tag;
                if (!relicsByTag.TryGetValue(tag, out var group)) relicsByTag[tag] = group = new List<OwnedRelic>();
                group.Add(relic);
            }
        }
        private bool Revive()
        {
            if (Hp > 0) return false;
            foreach (var tag in new[] { "spectral_shield", "ankh" })
                if (relicsByTag.TryGetValue(tag, out var relics))
                    foreach (var relic in relics) if (relic.Active)
                    { relic.SpendCharge(); Hp = tag == "ankh" ? Hero.MaxHp : 1; return true; }
            return false;
        }
        private void StartingRelics()
        {
            GainStrength(RelicPower("scar")); AddBlock(RelicPower("war_bracer", 8));
        }
        private bool BlocksDebuff(StatusKind status)
        {
            if (HasRelic("panacea") || status == StatusKind.Poison && HasRelic("antidote")) return true;
            if (!debuffIgnored && HasRelic("monk_wraps")) { debuffIgnored=true;return true; }
            return false;
        }
    }
}
