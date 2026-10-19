using System;
using System.Collections.Generic;

namespace LastWish.Presentation
{
    /// <summary>play_card.TargetKind: what a card acts on.</summary>
    public enum TargetKind { Enemy, AllEnemies, Self }

    public enum PlayMode { Idle, Holding, Aiming }

    public readonly struct PlayRequest
    {
        public readonly int CardIndex; public readonly int? TargetIndex;
        public PlayRequest(int card, int? target) { CardIndex = card; TargetIndex = target; }
    }

    /// <summary>ui/card_play.py: Slay the Spire style card-play state machine (drag, aim, sticky click, keys).</summary>
    public sealed class CardPlayInput
    {
        public const float DragThreshold = 8f;
        public readonly int PlayLineY;
        public PlayMode Mode { get; private set; } = PlayMode.Idle;
        public int? Card { get; private set; }
        public TargetKind? Kind { get; private set; }
        public int? Target { get; private set; }
        public (float x, float y) Pointer { get; private set; }
        public bool Sticky { get; private set; }
        public bool KeyboardAim { get; private set; }
        public bool ByKey { get; private set; }
        private (float x, float y) press;
        private bool dragged;

        public CardPlayInput(int playLineY) { PlayLineY = playLineY; }
        public bool Active => Mode != PlayMode.Idle;
        public bool Aiming => Mode == PlayMode.Aiming;
        public bool AbovePlayLine => Pointer.y < PlayLineY;
        public bool Armed => Mode == PlayMode.Holding && Kind != TargetKind.Enemy && (ByKey || (dragged || Sticky) && AbovePlayLine);

        public void Pick(int index, TargetKind kind, (float x, float y) pos)
        {
            Mode = PlayMode.Holding; Card = index; Kind = kind; Target = null;
            Pointer = press = pos; Sticky = KeyboardAim = ByKey = dragged = false;
        }

        public void PickWithKey(int index, TargetKind kind, int? firstTarget)
        {
            Pick(index, kind, Pointer);
            Sticky = ByKey = true;
            if (kind == TargetKind.Enemy) { Mode = PlayMode.Aiming; Target = firstTarget; KeyboardAim = firstTarget.HasValue; }
        }

        public void Move((float x, float y) pos, int? enemyUnder)
        {
            if (Mode == PlayMode.Idle) { Pointer = pos; return; }
            if (pos != Pointer) KeyboardAim = ByKey = false;
            Pointer = pos;
            float dx = pos.x - press.x, dy = pos.y - press.y;
            if (!dragged && dx * dx + dy * dy > DragThreshold * DragThreshold) dragged = true;
            if (Kind == TargetKind.Enemy)
            {
                if (Sticky) Mode = PlayMode.Aiming;
                else if (dragged) Mode = AbovePlayLine ? PlayMode.Aiming : PlayMode.Holding;
                if (Mode == PlayMode.Holding) Target = null;
                else if (!KeyboardAim) Target = enemyUnder;
            }
        }

        public PlayRequest? Release((float x, float y) pos, int? enemyUnder)
        {
            if (Mode == PlayMode.Idle || Sticky) return null;
            Move(pos, enemyUnder);
            if (!dragged)
            {
                Sticky = true;
                if (Kind == TargetKind.Enemy) { Mode = PlayMode.Aiming; Target = enemyUnder; }
                return null;
            }
            return Resolve();
        }

        public PlayRequest? Click((float x, float y) pos, int? enemyUnder)
        {
            if (Mode == PlayMode.Idle) return null;
            Move(pos, enemyUnder);
            return Resolve();
        }

        public void CycleTarget(List<int> alive, int step)
        {
            if (Mode != PlayMode.Aiming || alive.Count == 0) return;
            var at = Target.HasValue ? alive.IndexOf(Target.Value) : -1;
            var n = alive.Count;
            if (at >= 0) Target = alive[((at + step) % n + n) % n];
            else Target = step >= 0 ? alive[0] : alive[alive.Count - 1];
            KeyboardAim = true;
        }

        public PlayRequest? Confirm()
        {
            if (Mode == PlayMode.Idle || !Card.HasValue) return null;
            if (Kind == TargetKind.Enemy && !Target.HasValue) return null;
            var request = new PlayRequest(Card.Value, Kind == TargetKind.Enemy ? Target : null);
            Cancel();
            return request;
        }

        public bool Cancel()
        {
            var was = Mode != PlayMode.Idle;
            Mode = PlayMode.Idle; Card = null; Kind = null; Target = null;
            Sticky = KeyboardAim = ByKey = dragged = false;
            return was;
        }

        private PlayRequest? Resolve()
        {
            var card = Card ?? -1;
            PlayRequest? request;
            if (Kind == TargetKind.Enemy) request = Aiming && Target.HasValue ? new PlayRequest(card, Target) : (PlayRequest?)null;
            else request = AbovePlayLine ? new PlayRequest(card, null) : (PlayRequest?)null;
            Cancel();
            return request;
        }
    }
}
