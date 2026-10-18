using System;
using LastWish.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace LastWish.Presentation
{
    public sealed class CardView
    {
        private readonly RectTransform root;
        private readonly Button button;
        private readonly Text cost, name, kind, effect;
        private CardDefinition card;
        private int strength = -1, weak = -1, frail = -1;
        private float spacing;
        private int version = -1;
        private readonly CardHover hover;
        public CardView(UiFactory ui, Transform parent, Texture frame, int index, UnityEngine.Events.UnityAction onClick, TooltipPresenter tooltip)
        {
            root = ui.Rect(parent, "CardSlot" + index, 0, 110, 146, 194);
            button = ui.Button(root, "CardButton" + index, "", 73, 97, 146, 194, onClick);
            ui.Art(button.transform, "Frame", frame, 73, 97, 146, 194);
            cost = ui.Label(button.transform, "Cost", "", 20, 22, 34, 35, 24, UiFactory.Gold);
            name = ui.Label(button.transform, "Name", "", 86, 25, 104, 22, 16, Color.black);
            name.resizeTextForBestFit = true; name.resizeTextMinSize = 7; name.resizeTextMaxSize = 16;
            hover = button.gameObject.AddComponent<CardHover>(); hover.Configure(tooltip);
            kind = ui.Label(button.transform, "Kind", "", 73, 78, 120, 45, 16, UiFactory.Gold);
            effect = ui.Label(button.transform, "Effect", "", 73, 139, 115, 76, 14);
            effect.resizeTextForBestFit = true; effect.resizeTextMinSize = 9; effect.resizeTextMaxSize = 14;
        }
        public void SetVisible(bool visible) { if (root.gameObject.activeSelf != visible) root.gameObject.SetActive(visible); }
        public void Bind(CardDefinition value, CombatState state, int index, float newSpacing, bool playable)
        {
            SetVisible(true); button.interactable = playable;
            if (spacing != newSpacing)
            {
                spacing = newSpacing; root.anchoredPosition = new Vector2(55 + spacing / 2 + index * spacing, -110);
                var scale = Math.Min(1, spacing / 156); root.localScale = new Vector3(scale, scale, 1);
            }
            var newWeak = state.HeroStatuses[StatusKind.Weak]; var newFrail = state.HeroStatuses[StatusKind.Frail];
            if (ReferenceEquals(card, value) && strength == state.Strength && weak == newWeak && frail == newFrail && version == state.EffectVersion) return;
            version = state.EffectVersion; card = value; strength = state.Strength; weak = newWeak; frail = newFrail;
            cost.text = state.CardCost(card).ToString(); name.text = card.Name;
            kind.text = card.Id.StartsWith("status_") ? "ESTADO" : card.Kind == CardKind.Power ? "PODER" : card.IsAttack ? "ATAQUE" : card.Block > 0 ? "DEFENSA" : "HABILIDAD";
            var detail = "";
            if (card.IsAttack)
            {
                var damage = card.Damage.AddFlat(state.Hero.AttackBonus).AddFlat(strength).Value;
                if (weak > 0) damage = damage * 3 / 4;
                detail += "Inflige " + damage + "\nde daño.";
            }
            if (card.Block > 0)
            {
                var block = (long)card.Block + state.Hero.Dexterity;
                if (frail > 0) block = block * 3 / 4;
                detail += "\nGana " + block + " de escudo.";
            }
            if (card.ComboBlock > 0) detail += "\nCombo: +" + card.ComboBlock;
            if (card.EndureStrength > 0) detail += "\nAguante: Fuerza";
            if (card.FireStacks > 0) detail += "\nFuego +" + card.FireStacks;
            if (card.Draw > 0) detail += "\nRoba " + card.Draw;
            if (card.ManaGain > 0) detail += "\nManá +" + card.ManaGain;
            if (card.Id == "status_espora") detail = "Agotar.\nAl final del turno:\nVeneno +3.";
            if (card.Id == "status_moho") detail = "Injugable.\nAl robar: Maná -1.\nEtérea.";
            if (card.Id == "status_herida") detail = "Injugable.";
            if (card.Effects != null)
            {
                var shortText = new System.Text.StringBuilder();
                var fullText = new System.Text.StringBuilder("Maná: " + card.Cost + "\nDaño actual: " + state.PreviewDamage(card) + "\nEscudo actual: " + state.PreviewBlock(card) + "\n\n");
                if (state.PreviewDamage(card) > 0) shortText.Append("Daño ").Append(state.PreviewDamage(card)).Append('\n');
                if (state.PreviewBlock(card) > 0) shortText.Append("Escudo ").Append(state.PreviewBlock(card)).Append('\n');
                foreach (var layer in card.Effects)
                {
                    if (layer.Condition != EffectCondition.Always)
                    { shortText.Append(ConditionName(layer.Condition)).Append('\n'); fullText.Append(ConditionName(layer.Condition)).Append(": "); }
                    if (layer.Damage > 0) fullText.Append("Daño ").Append(layer.Damage).Append(". ");
                    if (layer.Block > 0) fullText.Append("Escudo ").Append(layer.Block).Append(". ");
                    if (layer.Draw > 0) { fullText.Append("Roba ").Append(layer.Draw).Append(". "); if (layer.Condition == EffectCondition.Always) shortText.Append("Roba ").Append(layer.Draw).Append('\n'); }
                    if (layer.Mana > 0) { fullText.Append("Maná +").Append(layer.Mana).Append(". "); if (layer.Condition == EffectCondition.Always) shortText.Append("Maná +").Append(layer.Mana).Append('\n'); }
                    if (layer.Element.HasValue)
                    {
                        var label = ElementName(layer.Element.Value) + " +" + layer.Stacks;
                        shortText.Append(label).Append('\n'); fullText.Append(label).Append(layer.AllEnemies ? " a todos. " : ". ");
                    }
                    if (layer.Text.Length > 0) { fullText.Append(layer.Text).Append(". "); shortText.Append(layer.Text).Append('\n'); }
                    fullText.Append('\n');
                }
                effect.text = shortText.ToString().Trim(); hover.SetText(card.Name, fullText.ToString().Trim());
            }
            else { effect.text = detail.Trim(); hover.SetText(card.Name, detail.Trim()); }
        }
        private static string ConditionName(EffectCondition condition)
        {
            switch (condition)
            {
                case EffectCondition.Combo: return "Combo"; case EffectCondition.Singular: return "Singular";
                case EffectCondition.Void: return "Vacío"; case EffectCondition.Spoil: return "Despojo";
                case EffectCondition.Reaction: return "Reacción"; case EffectCondition.Fury: return "Furia";
                case EffectCondition.Endure: return "Aguante"; default: return "";
            }
        }
        private static string ElementName(ElementKind element) => new[] { "Fuego", "Agua", "Tierra", "Aire" }[(int)element];
    }
}
