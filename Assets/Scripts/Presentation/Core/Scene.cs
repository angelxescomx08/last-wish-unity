using System;

namespace LastWish.Presentation
{
    /// <summary>Persistence the runtime provides (preferences.json, dev_settings.json, checkpoints).</summary>
    public interface IStorage
    {
        string Read(string name);
        void Write(string name, string text);
        void Delete(string name);
    }

    /// <summary>infrastructure/preferences.UserPreferences</summary>
    public sealed class Preferences
    {
        public bool ShowFps;
        public float SfxVolume = .7f, MusicVolume = .35f;
        public const string File = "preferences.json";

        public static Preferences Load(IStorage storage)
        {
            var prefs = new Preferences();
            try
            {
                var data = Json.Obj(Json.Parse(storage?.Read(File) ?? ""));
                if (data == null) return prefs;
                prefs.ShowFps = data.Get("show_fps") is bool b && b;
                prefs.SfxVolume = Valid(data.Get("sfx_volume"), .7f);
                prefs.MusicVolume = Valid(data.Get("music_volume"), .35f);
            }
            catch (Exception) { }
            return prefs;
        }
        private static float Valid(object value, float fallback)
        {
            if (!(value is long) && !(value is double)) return fallback;
            var v = Json.Num(value);
            return double.IsNaN(v) || double.IsInfinity(v) ? fallback : (float)Math.Max(0, Math.Min(1, v));
        }
        public void Save(IStorage storage)
        {
            storage?.Write(File, Json.Write(new System.Collections.Generic.Dictionary<string, object> {
                { "show_fps", ShowFps }, { "sfx_volume", (double)SfxVolume }, { "music_volume", (double)MusicVolume } }));
        }
    }

    /// <summary>Everything a scene needs (fonts, art, sound, live input, preferences).</summary>
    public sealed class GameContext
    {
        public Canvas Canvas { get; }
        public Art Art { get; }
        public FontRegistry Fonts => Art.Fonts;
        public SoundPlayer Sound { get; }
        public InputState Input { get; } = new InputState();
        public Preferences Prefs { get; }
        public IStorage Storage { get; }
        /// <summary>Exported presentation tables (card faces, glossary, relics, enemies).</summary>
        public GameData Data { get; set; }
        /// <summary>Seconds since start (wall clock of the presentation).</summary>
        public float Time { get; set; }
        public float Fps { get; set; }
        public Action Quit { get; set; }
        public GameContext(Canvas canvas, Art art, SoundPlayer sound, Preferences prefs, IStorage storage)
        { Canvas = canvas; Art = art; Sound = sound; Prefs = prefs ?? new Preferences(); Storage = storage; }
    }

    /// <summary>A screen on the scene stack (handle_event / update / draw).</summary>
    public abstract class Scene
    {
        protected GameContext Game { get; }
        protected Art Art => Game.Art;
        protected FontRegistry Fonts => Game.Art.Fonts;
        protected SoundPlayer Sound => Game.Sound;
        protected Scene(GameContext game) { Game = game ?? throw new ArgumentNullException(nameof(game)); }
        public virtual void HandleEvent(in InputEvent e) { }
        public virtual void Update(float dt) { }
        public abstract void Draw(Canvas canvas);
        /// <summary>Gold counter visible on this screen (scene attribute show_gold_hud).</summary>
        public virtual bool ShowGoldHud => true;
        /// <summary>gold_hud_pos (anchor, x, y); null = the default top right.</summary>
        public virtual (Anchor anchor, float x, float y)? GoldHudPos => null;
        /// <summary>pause_button_rect: null = default place, Hidden = no button.</summary>
        public virtual RectF? PauseButtonRect => null;
        public virtual bool HidePauseButton => false;
        /// <summary>An overlay (collection, confirmation) that consumes Escape first.</summary>
        public virtual bool HasOverlay => false;
    }
}
