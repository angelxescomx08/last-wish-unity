using System;
using System.Linq;
using LastWish.Application;
using LastWish.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace LastWish.Presentation
{
    public sealed partial class GameBootstrap
    {
        public RunState Run { get; private set; }
        private RelicDefinition pendingRelic;
        private int inventoryPage;
        private bool pickingPack;
        private void StartRun(HeroKind kind)
        {
            Run = new RunState(kind, unchecked(Environment.TickCount));
            selectedHero = kind; ShowMap();
        }
        private void RunHeading(string name, string heading)
        {
            ClearScreen(name); Backdrop();
            ui.Panel(screen, "RunShade", 640, 360, 1280, 720, new Color(.02f, .025f, .04f, .88f));
            ui.Label(screen, "Heading", heading, 640, 60, 1000, 65, 36, UiFactory.Gold);
            ui.Label(screen, "RunStats", $"Piso {Run.Floor}  |  Vida {Run.Hp}/{Run.MaxHp}  |  Oro {Run.Gold}  |  Cartas {Run.Deck.Count}", 640, 115, 1200, 42, 20);
        }
        public void ShowMap()
        {
            if (Run == null) { ShowMenu(); return; }
            if (Run.RoomCompleted && !Run.IsDead) RunPersistence.Save(Run);
            RunHeading("Map", "RUTAS DE LA MAZMORRA");
            var map = Run.Map;
            float X(MapRoom n) => 240 + n.Column * (800f / Math.Max(1, map.Columns - 1));
            float Y(MapRoom n) => 600 - n.Row * (410f / Math.Max(1, map.Rows - 1));
            foreach (var node in map.Nodes)
                foreach (var next in node.Connections.Select(map.Find))
                    ui.Panel(screen, "Corridor", (X(node) + X(next)) / 2, (Y(node) + Y(next)) / 2,
                        Math.Max(3, Math.Abs(X(node) - X(next))), Math.Max(3, Math.Abs(Y(node) - Y(next))), new Color(.32f, .30f, .23f));
            foreach (var node in map.Nodes)
            {
                var room = node;
                var button = ui.Button(screen, "Room_" + room.Id, RoomLabel(room.Kind), X(room), Y(room), 106, 36, () => EnterRoom(room.Id));
                button.interactable = room.Available && !room.Visited && Run.RoomCompleted;
                button.GetComponent<Image>().color = room.Visited ? new Color(.12f, .23f, .18f) : room.Available ? new Color(.38f, .29f, .08f) : new Color(.09f, .10f, .15f);
                button.GetComponentInChildren<Text>().fontSize = 14;
            }
            ui.Button(screen, "DeckButton", "Mazo y reliquias", 350, 672, 340, 44, () => { inventoryPage = 0; ShowInventory(false, false); });
            ui.Button(screen, "MapMenuButton", "Menú", 930, 672, 240, 44, ShowMenu);
        }
        private static string RoomLabel(RoomKind kind)
        {
            switch (kind)
            {
                case RoomKind.Combat: return "Combate"; case RoomKind.Treasure: return "Tesoro";
                case RoomKind.Shop: return "Tienda"; case RoomKind.Event: return "Suceso";
                case RoomKind.Boss: return "Jefe"; case RoomKind.Warlock: return "El Brujo";
                case RoomKind.Gacha: return "Gachapón"; case RoomKind.Purge: return "Purga";
                default: return "Élite";
            }
        }
        public void EnterRoom(string id)
        {
            if (Run == null || !Run.Enter(id)) return;
            Confirm(); ShowCurrentRoom();
        }
        private void ShowCurrentRoom()
        {
            var room = Run.Map.Find(Run.CurrentRoomId);
            switch (room.Kind)
            {
                case RoomKind.Combat: case RoomKind.Elite: case RoomKind.Boss:
                    StartCoroutine(BeginEncounter()); break;
                case RoomKind.Shop: ShowShop(); break;
                case RoomKind.Warlock: inventoryPage = 0; ShowInventory(true, false); break;
                case RoomKind.Purge: inventoryPage = 0; ShowInventory(false, true); break;
                case RoomKind.Gacha: ShowGacha(); break;
                case RoomKind.Event: ShowEvent(); break;
                default: ShowTreasure(); break;
            }
        }
        private void FinishRoom()
        {
            if (Run.CompleteRoom()) ShowMap();
        }
        private int RoomSeed(int salt) => unchecked(Run.Seed * 397 ^ Run.Floor * 7919 ^ Run.Map.Find(Run.CurrentRoomId).Row * 31 ^ Run.Map.Find(Run.CurrentRoomId).Column ^ salt);
        private void ShowShop()
        {
            RunHeading("Shop", "EL MERCADER");
            var random = new System.Random(RoomSeed(12));
            var pool = RunContent.ForHero(Run.HeroKind);
            var themes = new[] { "acero", "escudo", "magia", "epico" }.OrderBy(_ => random.Next()).Take(3).ToArray();
            for (var i = 0; i < 3; i++)
            {
                var theme = themes[i]; var offer = Run.CurrentRoomId + "_pack_" + i;
                var price = Run.ShopPrice(theme == "epico" ? 150 : 75);
                var button = ui.Button(screen, "ShopPack" + i, $"Sobre de {theme}\n{price} de oro", 280 + i * 360, 280, 320, 105,
                    () => { if (Run.BuyPack(offer, price)) ShowPack(theme); });
                button.interactable = !Run.IsSold(offer) && Run.Gold >= price;
            }
            var relicStock = RunContent.Relics.OrderBy(_ => random.Next()).Take(3).ToArray();
            for (var i = 0; i < 3; i++)
            {
                var relic = relicStock[i];
                var offer = Run.CurrentRoomId + "_relic_" + i; var price = Run.ShopPrice(150);
                var button = ui.Button(screen, "ShopRelic" + i, $"{relic.Name}\n{price} de oro", 280 + i * 360, 465, 320, 100,
                    () => { if (Run.BuyRelic(offer, relic.Id, price)) ShowShop(); });
                button.interactable = !Run.IsSold(offer) && Run.Gold >= price;
                ui.Label(screen, "RelicEffect", relic.Description, 280 + i * 360, 550, 320, 65, 16);
            }
            ui.Button(screen, "LeaveRoomButton", "Continuar", 640, 655, 340, 48, FinishRoom);
        }
        private void ShowPack(string theme)
        {
            pickingPack = true; RunHeading("Pack", "ELIGE UNA CARTA DEL SOBRE");
            var random = new System.Random(RoomSeed(theme.Length * 17));
            var pool = RunContent.ForHero(Run.HeroKind);
            var choices = pool.Where(c => c.Theme == theme).OrderBy(_ => random.Next()).Take(5).ToList();
            choices.AddRange(pool.Where(c => !choices.Contains(c)).OrderBy(_ => random.Next()).Take(5 - choices.Count));
            for (var i = 0; i < choices.Count; i++)
            {
                var card = choices[i];
                var button = ui.Button(screen, "PackCard" + i, $"{card.Name}\nManá {card.Cost}\nDaño {card.Damage}\nEscudo {card.Block}\nRoba {card.Draw}",
                    150 + i * 245, 380, 225, 240, () =>
                    { if (!pickingPack) return; pickingPack = false; Run.AddCard(card); ShowShop(); });
                button.GetComponentInChildren<Text>().fontSize = 18;
            }
        }
        private void ShowTreasure()
        {
            RunHeading("Treasure", "UN TESORO OLVIDADO");
            var relic = RunContent.Relics[new System.Random(RoomSeed(24)).Next(RunContent.Relics.Count)];
            ui.Label(screen, "TreasureName", relic.Name, 640, 300, 1000, 70, 38, UiFactory.Gold);
            ui.Label(screen, "TreasureEffect", relic.Description, 640, 390, 1050, 70, 24);
            ui.Button(screen, "TakeRelicButton", "Recoger", 640, 535, 380, 52, () => { Run.AcquireRelic(relic.Id); FinishRoom(); });
        }
        private void ShowEvent()
        {
            RunHeading("Event", "UN RESPIRO EN LA OSCURIDAD");
            ui.Label(screen, "EventText", "Una fuente brilla junto a un cofre abandonado.\nSolo puedes elegir uno.", 640, 300, 1100, 130, 28);
            ui.Button(screen, "EventHeal", "Beber: recuperar 20 de vida", 640, 460, 800, 52, () => { Run.Heal(20); FinishRoom(); });
            ui.Button(screen, "EventGold", "Abrir el cofre: ganar 50 de oro", 640, 550, 800, 52, () => { Run.GainGold(50); FinishRoom(); });
        }
        private void ShowInventory(bool upgrading, bool purging)
        {
            RunHeading(upgrading ? "Warlock" : purging ? "Purge" : "Inventory", upgrading ? "EL BRUJO" : purging ? "ALTAR DE PURGA" : "TU MAZO Y RELIQUIAS");
            var pages = Math.Max(1, (Run.Deck.Count + 7) / 8); inventoryPage = Math.Min(inventoryPage, pages - 1);
            for (var slot = 0; slot < 8; slot++)
            {
                var index = inventoryPage * 8 + slot; if (index >= Run.Deck.Count) break;
                var card = Run.Deck[index]; var price = upgrading ? Run.UpgradePrice(index) : Run.PurgePrice;
                var detail = $"{card.Name}\nManá {card.Cost} · Daño {card.Damage} · Escudo {card.Block}";
                if (upgrading || purging) detail += $"\n{(upgrading ? "Mejorar" : "Eliminar")}: {price} de oro";
                var button = ui.Button(screen, "DeckCard" + index, detail, 325 + slot % 2 * 630, 205 + slot / 2 * 93, 590, 80, () =>
                {
                    if (upgrading && Run.Upgrade(index)) ShowInventory(true, false);
                    if (purging && Run.Purge(index)) FinishRoom();
                });
                button.GetComponentInChildren<Text>().fontSize = 18;
                button.interactable = upgrading ? !card.Upgraded && Run.Gold >= price : purging && Run.Deck.Count > 1 && Run.Gold >= price;
            }
            var owned = string.Join(" · ", Run.Relics.Select(id => RunContent.Relics.First(r => r.Id == id).Name));
            ui.Label(screen, "OwnedRelics", "Reliquias: " + (owned.Length == 0 ? "Ninguna" : owned), 640, 575, 1200, 55, 16);
            ui.Button(screen, "PreviousPage", "<", 160, 635, 80, 40, () => { inventoryPage = Math.Max(0, inventoryPage - 1); ShowInventory(upgrading, purging); });
            ui.Label(screen, "Page", $"{inventoryPage + 1}/{pages}", 320, 635, 160, 40, 20);
            ui.Button(screen, "NextPage", ">", 470, 635, 80, 40, () => { inventoryPage = Math.Min(pages - 1, inventoryPage + 1); ShowInventory(upgrading, purging); });
            ui.Button(screen, "LeaveRoomButton", "Volver", 960, 650, 320, 48, () => { if (upgrading || purging) FinishRoom(); else ShowMap(); });
        }
        private void ShowGacha()
        {
            RunHeading("Gacha", "GACHAPÓN");
            ui.Label(screen, "GachaDescription", "Cada tirada aumenta el precio de las siguientes.\nLa tirada estelar nunca ofrece una reliquia común.", 640, 260, 1100, 110, 24);
            for (var i = 0; i < 2; i++)
            {
                var stellar = i == 1; var price = Run.ShopPrice(RunState.PullPrice(stellar, Run.Pulls));
                var button = ui.Button(screen, "Pull" + i, $"{(stellar ? "Estelar" : "Normal")}\n{price} de oro", 400 + i * 480, 425, 380, 100, () =>
                {
                    pendingRelic = Run.Pull(stellar); if (pendingRelic != null) ShowCapsule();
                });
                button.interactable = Run.Gold >= price;
            }
            ui.Button(screen, "LeaveRoomButton", "Continuar", 640, 630, 340, 48, FinishRoom);
        }
        private void ShowCapsule()
        {
            RunHeading("Capsule", pendingRelic.Name);
            ui.Label(screen, "RelicDescription", pendingRelic.Description, 640, 320, 1100, 100, 28);
            ui.Button(screen, "AcceptRelic", "Conservar", 420, 500, 340, 52, () => { if (pendingRelic != null) Run.AcquireRelic(pendingRelic.Id); pendingRelic = null; ShowGacha(); });
            ui.Button(screen, "DeclineRelic", "Descartar", 860, 500, 340, 52, () => { pendingRelic = null; ShowGacha(); });
        }
        private void ShowRunReward()
        {
            var room = Run.Map.Find(Run.CurrentRoomId);
            Run.ApplyCombatResult(session.State.Hp, session.State.GoldEarned + session.State.Enemies.Sum(e => Math.Max(1, e.MaxHp / 8)));
            RunHeading("Reward", "VICTORIA");
            ui.Label(screen, "RewardText", "Elige una carta para tu mazo o continúa.", 640, 235, 1100, 60, 28);
            var random = new System.Random(RoomSeed(48));
            var pool = RunContent.ForHero(Run.HeroKind);
            for (var i = 0; i < 3; i++)
            {
                var card = pool[random.Next(pool.Length)];
                ui.Button(screen, "RewardCard" + i, $"{card.Name}\nDaño {card.Damage} · Escudo {card.Block}", 280 + i * 360, 400, 320, 120, () => { Run.AddCard(card); CompleteReward(room); });
            }
            ui.Button(screen, "SkipReward", "Continuar sin carta", 640, 620, 600, 50, () => CompleteReward(room));
        }
        private void CompleteReward(MapRoom room)
        {
            if (room.Kind == RoomKind.Elite || room.Kind == RoomKind.Boss)
                Run.AcquireRelic(RunContent.Relics[new System.Random(RoomSeed(96)).Next(RunContent.Relics.Count)].Id);
            Run.CompleteRoom();
            if (room.Kind == RoomKind.Boss) Run.AdvanceFloor();
            ShowMap();
        }
    }
}
