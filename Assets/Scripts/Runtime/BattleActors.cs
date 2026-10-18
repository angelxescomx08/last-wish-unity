using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LastWish.Application;
using LastWish.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace LastWish.Presentation
{
    public sealed partial class GameBootstrap
    {
        private readonly List<AtlasAnimation> enemyViews = new List<AtlasAnimation>();
        private readonly List<Text> enemyLabels = new List<Text>();
        private readonly List<Button> enemyTargets = new List<Button>();
        private readonly List<bool> enemyDeathShown = new List<bool>();
        private IEnumerator BeginEncounter()
        {
            session = new BattleSession(Run.CreateBattle()); ShowLoading(); IsBusy = true;
            var loader = assets.LoadEnemies(session.State.Enemies.Select(e => e.Definition.AssetId), UpdateLoading);
            while (true)
            {
                bool more = false; object next = null; System.Exception failure = null;
                try { more = loader.MoveNext(); if (more) next = loader.Current; }
                catch (System.Exception error) { failure = error; }
                if (failure != null)
                {
                    IsBusy = false; loadingLabel.text = "No se pudo cargar el encuentro."; Debug.LogException(failure);
                    ui.Button(screen, "RetryLoading", "Reintentar", 640, 540, 360, 48, () => StartCoroutine(BeginEncounter()));
                    yield break;
                }
                if (!more) break;
                yield return next;
            }
            IsBusy = false; BuildBattle();
        }
        private void BuildEnemies()
        {
            enemyViews.Clear(); enemyLabels.Clear(); enemyTargets.Clear(); enemyDeathShown.Clear();
            var count = session.State.Enemies.Count;
            for (var i = 0; i < count; i++)
            {
                var index = i; var enemy = session.State.Enemies[i];
                var x = count == 1 ? 980 : count == 2 ? 770 + i * 300 : 660 + i * 230;
                var atlas = assets.Atlas(enemy.Definition.AssetId); var scale = 2f;
                var width = atlas.cellWidth * scale;
                var anchorX = atlas.anchorX > 0 ? atlas.anchorX : atlas.cellWidth / 2f;
                var anchorY = atlas.anchorY > 0 ? atlas.anchorY : atlas.cellHeight - 8;
                enemyViews.Add(Actor(enemy.Definition.AssetId, x + (atlas.cellWidth / 2f - anchorX) * scale,
                    430 + (atlas.cellHeight / 2f - anchorY) * scale, width));
                var target = ui.Rect(screen, "EnemyTarget" + i, x, 340, 220, 180);
                var image = target.gameObject.AddComponent<Image>(); image.color = Color.clear; image.raycastTarget = true;
                var button = target.gameObject.AddComponent<Button>(); button.targetGraphic = image;
                button.onClick.AddListener(() => SelectEnemy(index)); enemyTargets.Add(button);
                enemyLabels.Add(ui.Label(screen, "EnemyStats" + i, "", x, 195, count == 1 ? 550 : 225, 120, count == 1 ? 22 : 15));
                enemyDeathShown.Add(false);
            }
        }
        public void SelectEnemy(int index)
        {
            if (!IsBusy && session != null && session.State.SelectEnemy(index)) RefreshBattle();
        }
        private void CycleEnemy(int direction)
        {
            if (IsBusy || session == null || ScreenName != "Battle") return;
            var state = session.State;
            for (var offset = 1; offset <= state.Enemies.Count; offset++)
            {
                var index = (state.SelectedEnemy + state.Enemies.Count + direction * offset) % state.Enemies.Count;
                if (state.Enemies[index].Alive) { SelectEnemy(index); return; }
            }
        }
        private void RefreshEnemies()
        {
            var state = session.State;
            for (var i = 0; i < state.Enemies.Count; i++)
            {
                var enemy = state.Enemies[i]; var intent = enemy.Intent;
                var detail = enemy.Alive ? intent.Name + (intent.Kind == IntentKind.Attack ? "\n" + enemy.HitDamage(state.HeroStatuses) + " × " + intent.Hits + " de daño" : "") : "Derrotado";
                var statuses = DescribeStatuses(enemy.Statuses);
                if (statuses.Length > 0) detail += "\n" + statuses;
                enemyLabels[i].text = (i == state.SelectedEnemy && enemy.Alive ? "> " : "") + enemy.Definition.Name + "\nVida " + enemy.Hp + "/" + enemy.MaxHp + " · Escudo " + enemy.Block + "\n" + detail;
                enemyLabels[i].color = i == state.SelectedEnemy && enemy.Alive ? UiFactory.Gold : UiFactory.Ink;
                enemyTargets[i].interactable = enemy.Alive && !IsBusy;
            }
        }
        private static readonly string[] StatusNames = { "Veneno", "Marcado", "Débil", "Vulnerable", "Frágil", "Enredado", "Fuerza", "Espadas", "Mecha", "Fuego", "Agua", "Tierra", "Aire", "Congelado" };
        private static string DescribeStatuses(StatusSet statuses)
        {
            var names = StatusNames;
            System.Text.StringBuilder result = null;
            for (var i = 0; i < names.Length; i++)
            {
                var amount = statuses[(StatusKind)i]; if (amount == 0) continue;
                if (result == null) result = new System.Text.StringBuilder(); else result.Append(" · ");
                result.Append(names[i]).Append(' ').Append(amount);
            }
            return result == null ? "" : result.ToString();
        }
        private void PlayEnemyIntents()
        {
            for (var i = 0; i < session.State.Enemies.Count; i++)
            {
                var enemy = session.State.Enemies[i];
                if (enemy.Alive) enemyViews[i].PlayMove(enemy.Intent.Id, enemy.Intent.Kind == IntentKind.Attack ? "attack" : "cast");
            }
        }
        private void ShowEnemyHit(int index)
        {
            if (session.State.Enemies[index].Alive) enemyViews[index].Play("hurt");
            ShowEnemyDeaths();
        }
        private void ShowEnemyDeaths()
        {
            for (var i = 0; i < session.State.Enemies.Count; i++)
                if (!session.State.Enemies[i].Alive && !enemyDeathShown[i])
                { enemyDeathShown[i] = true; enemyViews[i].Play("death", false); }
        }
    }
}
