using System;
using UnityEngine.InputSystem;

namespace LastWish.Presentation
{
    public sealed class GameInput : IDisposable
    {
        private readonly InputActionMap battle = new InputActionMap("Battle");
        public GameInput(Action<int> playCard, Action endTurn, Action cancel, Action<int> cycleEnemy)
        {
            for (var index = 0; index < 10; index++)
            {
                var slot = index;
                var digit = index == 9 ? 0 : index + 1;
                battle.AddAction("Card" + digit, InputActionType.Button, "<Keyboard>/" + digit)
                    .performed += _ => playCard(slot);
            }
            var next = battle.AddAction("NextEnemy", InputActionType.Button, "<Keyboard>/tab");
            next.AddBinding("<Gamepad>/rightShoulder"); next.performed += _ => cycleEnemy(1);
            var previous = battle.AddAction("PreviousEnemy", InputActionType.Button, "<Keyboard>/q");
            previous.AddBinding("<Gamepad>/leftShoulder"); previous.performed += _ => cycleEnemy(-1);
            var end = battle.AddAction("EndTurn", InputActionType.Button, "<Keyboard>/space");
            end.AddBinding("<Gamepad>/buttonWest"); end.performed += _ => endTurn();
            var back = battle.AddAction("Menu", InputActionType.Button, "<Keyboard>/escape");
            back.AddBinding("<Gamepad>/start"); back.performed += _ => cancel();
        }
        public void SetBattleActive(bool active)
        { if (active) battle.Enable(); else battle.Disable(); }
        public void Dispose() => battle.Dispose();
    }
}
