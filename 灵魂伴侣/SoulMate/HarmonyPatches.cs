using System;
using System.Collections.Generic;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace SoulMate
{
    /// <summary>
    /// SoulMate v0.5 — 融合StardewSquad架构精华
    /// POSTFIX补丁 + 距离速度缩放 + V字队形偏移
    /// </summary>
    internal static class HarmonyPatches
    {
        internal static ModConfig? Config;
        internal static Dictionary<string, Queue<Vector2>> Queues = new();
        internal static Dictionary<string, int> FormationIndexes = new(); // NPC→队形序号
        private static CompanionManager? _mgr;
        private static IMonitor? _mon;

        public static void Apply(Harmony h, CompanionManager m, ModConfig c, IMonitor mon)
        {
            _mgr = m; _mon = mon; Config = c;

            // ★ POSTFIX — 原版精灵动画照常跑，我们只注入行为覆盖 ★
            var updateMethod = AccessTools.Method(typeof(NPC), nameof(NPC.update), [typeof(GameTime), typeof(GameLocation)]);
            if (updateMethod != null)
            {
                h.Patch(updateMethod, postfix: new(typeof(HarmonyPatches), nameof(Pfx_Update)));
                mon.Log("[Harmony] NPC.update POSTFIX 就绪", LogLevel.Info);
            }
            else mon.Log("[Harmony] ⚠️ NPC.update 找不到!", LogLevel.Warn);

            h.Patch(AccessTools.Method(typeof(NPC), "returnHomeFromFarmPosition"),
                prefix: new(typeof(HarmonyPatches), nameof(Pfx_Home)));

            var ca = AccessTools.Method(typeof(NPC), nameof(NPC.checkAction), [typeof(Farmer), typeof(GameLocation)]);
            if (ca != null)
                h.Patch(ca, prefix: new(typeof(HarmonyPatches), nameof(Pfx_CheckAction)));

            mon.Log("[Harmony] OK", LogLevel.Debug);
        }

        // ★ POSTFIX: 原版跑完后再覆盖移动 ★
        private static void Pfx_Update(NPC __instance, GameTime time, GameLocation location)
        {
            if (_mgr == null || Config == null || !Context.IsMainPlayer) return;
            var d = _mgr.GetData(__instance);
            if (d == null) return;

            // 清除原版日程可能设置的控制器
            if (__instance.controller != null
                || __instance.temporaryController != null)
            {
                __instance.controller = null;
                __instance.temporaryController = null;
            }

            if (!Queues.TryGetValue(__instance.Name, out var q))
            {
                q = new Queue<Vector2>();
                for (int i = 0; i < Config.FollowDelayFrames; i++) q.Enqueue(__instance.Position);
                Queues[__instance.Name] = q;
            }
            if (!FormationIndexes.ContainsKey(__instance.Name))
                FormationIndexes[__instance.Name] = _mgr.Companions.Count - 1;

            SoulMateAI.Update(__instance, d.State, q, Config, _mon!, time);
        }

        private static bool Pfx_Home(NPC __instance)
        {
            if (_mgr?.GetData(__instance) != null) return false;
            return true;
        }

        private static bool Pfx_CheckAction(NPC __instance, Farmer who, GameLocation l)
        {
            if (_mgr == null) return true;
            var d = _mgr.GetData(__instance);
            if (d != null && who == Game1.player)
            {
                Menus.CompanionChest.Open(__instance, d.State, _mgr);
                return false;
            }
            return true;
        }

        public static void Cleanup()
        {
            foreach (var kv in Queues) kv.Value.Clear();
            Queues.Clear();
            FormationIndexes.Clear();
        }
    }
}
