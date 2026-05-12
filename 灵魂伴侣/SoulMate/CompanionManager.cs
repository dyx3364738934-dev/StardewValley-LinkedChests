using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace SoulMate
{
    public class CompanionManager
    {
        public class Data { public NPC Npc = null!; public CompanionState State = CompanionState.Following; }
        public Dictionary<string, Data> Companions { get; } = new();
        private readonly ModConfig _c;
        private readonly IMonitor _m;

        public CompanionManager(ModConfig c, IMonitor m) { _c = c; _m = m; }

        public bool Recruit(NPC n)
        {
            if (Companions.ContainsKey(n.Name)) return false;
            Companions[n.Name] = new Data { Npc = n };
            n.ignoreScheduleToday = true;
            n.farmerPassesThrough = true;
            n.controller = null;
            if (_c.ShowNotifications)
                Game1.addHUDMessage(new HUDMessage($"{n.displayName} ♥", HUDMessage.newQuest_type) { noIcon = true, timeLeft = 3000f });
            return true;
        }

        public void Release(NPC n)
        {
            if (!Companions.Remove(n.Name)) return;
            n.ignoreScheduleToday = false;
            n.farmerPassesThrough = false;
            n.controller = null;
            HarmonyPatches.Queues.Remove(n.Name);
            if (_c.ShowNotifications)
                Game1.addHUDMessage(new HUDMessage($"{n.displayName} 自由了", HUDMessage.newQuest_type) { noIcon = true, timeLeft = 3000f });
        }

        public void ReleaseAll()
        {
            foreach (var k in Companions.Keys.ToList())
                if (Companions.TryGetValue(k, out var d)) Release(d.Npc);
            Companions.Clear();
            HarmonyPatches.Cleanup();
        }

        public void OnWarp(GameLocation loc)
        {
            foreach (var d in Companions.Values)
                if (d.Npc.currentLocation != loc)
                {
                    Game1.warpCharacter(d.Npc, loc.Name, Game1.player.TilePoint);
                    d.Npc.controller = null;
                    // 重置位置队列
                    if (HarmonyPatches.Queues.TryGetValue(d.Npc.Name, out var q))
                    {
                        q.Clear();
                        for (int i = 0; i < _c.FollowDelayFrames; i++) q.Enqueue(d.Npc.Position);
                    }
                }
        }

        public Data? GetData(NPC n) => Companions.TryGetValue(n.Name, out var d) ? d : null;
        public CompanionState GetState(NPC n) => Companions.TryGetValue(n.Name, out var d) ? d.State : CompanionState.Idle;
        public void SetState(NPC n, CompanionState s) { if (Companions.TryGetValue(n.Name, out var d)) d.State = s; }
    }
}
