using System.Collections;
using System.Linq;
using LastWish.Domain;
using LastWish.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LastWish.Tests
{
    public sealed class InputTests
    {
        [UnityTest]
        public IEnumerator PointerAndKeyboardDriveTheActualGame()
        {
            var previousEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                yield return SceneManager.LoadSceneAsync("Bootstrap");
                var deadline = Time.realtimeSinceStartup + 30;
                while ((GameBootstrap.Instance == null || !GameBootstrap.Instance.IsReady) && Time.realtimeSinceStartup < deadline) yield return null;
                var game = GameBootstrap.Instance;
                Assert.That(game.IsReady, Is.True);
                Assert.That(EventSystem.current.GetComponent<InputSystemUIInputModule>(), Is.Not.Null);
                yield return null;
                Assert.That(EventSystem.current.GetComponent<InputSystemUIInputModule>().point.action.enabled, Is.True);
                var rect = GameObject.Find("PlayButton").GetComponent<RectTransform>();
                var position = RectTransformUtility.WorldToScreenPoint(null, rect.position);
                var hits = new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
                Assert.That(hits.Any(h => h.gameObject.name == "PlayButton"), Is.True, "Pointer must target the actual button");
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
                InputSystem.Update();
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left));
                InputSystem.Update();
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
                InputSystem.Update();
                yield return null;
                Assert.That(game.ScreenName, Is.EqualTo("Selection"), "A real pointer click must open character selection");
                game.StartBattle(HeroKind.Warrior);
                var cardRect = GameObject.Find("CardButton0").GetComponent<RectTransform>();
                var cardPoint = RectTransformUtility.WorldToScreenPoint(null, cardRect.position);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = cardPoint }); InputSystem.Update();
                yield return null;
                Assert.That(GameObject.Find("CardTooltip"), Is.Not.Null, "Hover must show a readable card description");
                BootstrapTests.CaptureUi(System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../TestResults/tooltip.png")));
                var mana = game.Battle.Mana;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit1));
                InputSystem.Update();
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                deadline = Time.realtimeSinceStartup + 5;
                while (game.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(game.Battle.Mana, Is.LessThan(mana));
                var turn = game.Battle.Turn;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                InputSystem.Update();
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                deadline = Time.realtimeSinceStartup + 5;
                while (game.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(game.Battle.Turn, Is.EqualTo(turn + 1));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                InputSystem.Update();
                yield return null;
                Assert.That(game.ScreenName, Is.EqualTo("Menu"));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                var run = new RunState(HeroKind.Mage, 42) { ForcedEncounter = 12 };
                typeof(GameBootstrap).GetProperty("Run").SetValue(game, run);
                game.EnterRoom(run.Map.Nodes.Single(n => n.Available).Id);
                deadline = Time.realtimeSinceStartup + 30;
                while (game.ScreenName == "Loading" && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(game.Battle.Enemies.Count, Is.EqualTo(2));
                var targetRect = GameObject.Find("EnemyTarget1").GetComponent<RectTransform>();
                var targetPoint = RectTransformUtility.WorldToScreenPoint(null, targetRect.position);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = targetPoint }); InputSystem.Update(); yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = targetPoint }.WithButton(MouseButton.Left)); InputSystem.Update(); yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = targetPoint }); InputSystem.Update(); yield return null;
                Assert.That(game.Battle.SelectedEnemy, Is.EqualTo(1), "A real pointer click must select the second enemy");
            }
            finally { InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse); InputSystem.settings.backgroundBehavior = previousBackground; InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorBehavior; }
        }
    }
}
