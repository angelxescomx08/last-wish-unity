using System;
using LastWish.Domain;

namespace LastWish.Presentation
{
    /// <summary>Scenes report to the run's <see cref="PowerTracker"/> through these, so telemetry can never break the game.</summary>
    public static class PowerTracking
    {
        /// <summary>Run <paramref name="action"/> on the tracker of the current run (nothing without one). An error switches telemetry off for the run.</summary>
        public static void Track(this GameContext game, Action<PowerTracker> action)
        {
            var power = game?.Power;
            if (power == null || action == null) return;
            try { action(power); }
            catch (Exception) { game.Power = null; }
        }

        /// <summary>The same, returning what the tracker returned (null without a tracker or on an error).</summary>
        public static T Track<T>(this GameContext game, Func<PowerTracker, T> func) where T : class
        {
            var power = game?.Power;
            if (power == null || func == null) return null;
            try { return func(power); }
            catch (Exception) { game.Power = null; return null; }
        }
    }
}
