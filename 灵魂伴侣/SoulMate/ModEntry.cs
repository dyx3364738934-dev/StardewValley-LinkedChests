using System;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using SoulMate.Menus;

namespace SoulMate
{
    public class ModEntry : Mod
    {
        internal static ModEntry? Instance { get; private set; }
        internal ModConfig Config { get; private set; } = null!;
        internal CompanionManager Mgr { get; private set; } = null!;
        private Harmony? _harmony;

        public override void Entry(IModHelper helper)
        {
            Instance = this;
            Config = helper.ReadConfig<ModConfig>();
            Mgr = new CompanionManager(Config, Monitor);

            _harmony = new Harmony("Koko.SoulMate");
            HarmonyPatches.Apply(_harmony, Mgr, Config, Monitor);

            helper.Events.Input.ButtonPressed += OnButton;
            helper.Events.GameLoop.DayStarted += OnDayStart;
            helper.Events.GameLoop.DayEnding += OnDayEnd;
            helper.Events.GameLoop.SaveLoaded += OnLoad;
            helper.Events.Player.Warped += (s, e) => { if (Context.IsMainPlayer) Mgr.OnWarp(e.NewLocation); };
            helper.Events.Display.RenderedHud += OnHud;

            Monitor.Log("[SoulMate] 就绪", LogLevel.Info);
        }

        private void OnLoad(object? s, SaveLoadedEventArgs e) => InventoryStore.Load(Helper);
        private void OnDayStart(object? s, DayStartedEventArgs e) { if (Context.IsMainPlayer) { InventoryStore.Load(Helper); Mgr.ReleaseAll(); } }
        private void OnDayEnd(object? s, DayEndingEventArgs e) { if (Context.IsMainPlayer) { InventoryStore.Save(Helper); Mgr.ReleaseAll(); } }

        private void OnButton(object? s, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady || !Context.IsMainPlayer) return;
            if (e.Button != SButton.G) return;

            var n = FindNearest();
            if (n == null) return;
            if (!IsSpouse(n)) { ShowMsg("只有配偶才行!"); return; }

            if (Mgr.Companions.ContainsKey(n.Name))
                Mgr.Release(n);
            else
                Mgr.Recruit(n);
        }

        private bool IsSpouse(NPC n) => Game1.player.getSpouse()?.Name == n.Name;

        private NPC? FindNearest()
        {
            NPC? b = null; float bd = 4f * 64f;
            foreach (var n in Game1.player.currentLocation?.characters?.OfType<NPC>() ?? [])
            {
                string tn = n.GetType().Name;
                if (tn.Contains("Monster") || tn == "Horse" || tn == "Pet") continue;
                float d = Vector2.Distance(Game1.player.Tile, n.Tile);
                if (d < bd) { bd = d; b = n; }
            }
            return b;
        }

        private void ShowMsg(string s) { if (Config.ShowNotifications) Game1.addHUDMessage(new HUDMessage(s, HUDMessage.error_type) { noIcon = true, timeLeft = 2500f }); }

        private void OnHud(object? s, RenderedHudEventArgs e)
        {
            if (!Config.ShowStateLabel) return;
            foreach (var p in Mgr.Companions)
            {
                var npc = p.Value.Npc;
                var label = p.Value.State == CompanionState.Following ? "[♥]" : "[idle]";
                Vector2 lp = npc.getLocalPosition(Game1.viewport) - new Vector2(0, npc.Sprite.SpriteHeight * 4 + 32);
                lp.X += npc.Sprite.SpriteWidth * 2;
                Vector2 up = Utility.ModifyCoordinatesForUIScale(lp);
                var sz = Game1.smallFont.MeasureString(label);
                e.SpriteBatch.DrawString(Game1.smallFont, label, up - sz / 2f, Color.HotPink);
            }
        }
    }
}
