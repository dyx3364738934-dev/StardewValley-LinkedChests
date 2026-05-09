using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Inventories;
using StardewValley.Menus;
using StardewValley.Objects;

namespace LinkedChests
{
    /// <summary>
    /// Harmony 补丁：整理按钮联排 + 工作台全场景覆盖
    /// </summary>
    internal static class HarmonyPatches
    {
        public static bool IsPatched { get; private set; }

        // 运行时反射缓存的 CraftingPage 字段信息
        private static FieldInfo? _craftingListField;

        // 虚拟滚轮
        private static List<(Chest chest, Vector2 tile)>? _linkedGroup;
        private static int _groupIndex;

        public static void Apply(Harmony harmony)
        {
            // ════════════════════════════════════════════
            // 补丁 1：整理按钮 → 跨箱子联排整理
            // ════════════════════════════════════════════
            ApplyOrganizeButtonPatch(harmony);

            // ════════════════════════════════════════════
            // 补丁 2：工作台 → 当前场景箱子全覆盖
            // ════════════════════════════════════════════
            ApplyWorkbenchPatch(harmony);
            ApplyScrollPatch(harmony);

            // 运行时发现 CraftingPage 中的箱子列表字段
            DiscoverCraftingField();
        }

        private static void DiscoverCraftingField()
        {
            try
            {
                var type = typeof(CraftingPage);
                foreach (var f in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    if (!f.FieldType.IsGenericType) continue;
                    var gen = f.FieldType.GetGenericTypeDefinition();
                    if (gen != typeof(List<>)) continue;
                    var elem = f.FieldType.GetGenericArguments()[0];
                    // SDV 1.6 中 _materialContainers 类型为 List<IInventory>
                    if (elem == typeof(IInventory) || elem == typeof(Chest))
                    {
                        _craftingListField = f;
                        ModEntry.Instance.Monitor.Log(
                            $"[Harmony] 发现 CraftingPage 箱子列表字段: {f.Name} ({elem.Name})",
                            StardewModdingAPI.LogLevel.Info);
                        return;
                    }
                }
                ModEntry.Instance.Monitor.Log(
                    "[Harmony] ⚠️ 未发现 CraftingPage 箱子列表字段，工作台功能不可用",
                    StardewModdingAPI.LogLevel.Warn);
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"[Harmony] 发现 CraftingPage 字段失败: {ex.Message}",
                    StardewModdingAPI.LogLevel.Debug);
            }
        }

        // ────────────────────────────────────────────────
        //  补丁 1：整理按钮联排
        // ────────────────────────────────────────────────

        private static void ApplyOrganizeButtonPatch(Harmony harmony)
        {
            var targetType = typeof(ItemGrabMenu);
            var paramSets = new[]
            {
                new Type[] { typeof(int), typeof(int), typeof(bool) },
                new Type[] { typeof(int), typeof(int) },
            };

            foreach (var paramSet in paramSets)
            {
                try
                {
                    var method = AccessTools.Method(targetType, "receiveLeftClick", paramSet);
                    if (method != null)
                    {
                        harmony.Patch(method,
                            postfix: new HarmonyMethod(typeof(HarmonyPatches),
                                nameof(Postfix_OrganizeButton)));
                        ModEntry.Instance.Monitor.Log(
                            $"[Harmony] 整理补丁已应用: ItemGrabMenu.receiveLeftClick({string.Join(", ", paramSet.Select(p => p.Name))})",
                            StardewModdingAPI.LogLevel.Info);
                        IsPatched = true;
                        return;
                    }
                }
                catch (Exception ex)
                {
                    ModEntry.Instance.Monitor.Log(
                        $"[Harmony] 整理补丁尝试失败 ({paramSet.Length}参数): {ex.Message}",
                        StardewModdingAPI.LogLevel.Debug);
                }
            }

            // 兜底
            try
            {
                var method = AccessTools.Method(targetType, "receiveLeftClick");
                if (method != null)
                {
                    harmony.Patch(method,
                        postfix: new HarmonyMethod(typeof(HarmonyPatches),
                            nameof(Postfix_OrganizeButton)));
                    ModEntry.Instance.Monitor.Log(
                        "[Harmony] 整理补丁已应用: ItemGrabMenu.receiveLeftClick (无参数约束)",
                        StardewModdingAPI.LogLevel.Info);
                    IsPatched = true;
                    return;
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"[Harmony] 整理补丁通用失败: {ex.Message}",
                    StardewModdingAPI.LogLevel.Debug);
            }

            ModEntry.Instance.Monitor.Log(
                "[Harmony] ⚠️ 未能找到 receiveLeftClick，整理联排功能不可用",
                StardewModdingAPI.LogLevel.Warn);
        }

        private static void Postfix_OrganizeButton(object __instance, int x, int y)
        {
            var organizeButton = AccessTools.Field(typeof(ItemGrabMenu), "organizeButton")
                ?.GetValue(__instance) as ClickableTextureComponent;
            if (organizeButton == null || !organizeButton.containsPoint(x, y))
                return;

            var context = AccessTools.Field(typeof(ItemGrabMenu), "context")
                ?.GetValue(__instance);
            if (context is not Chest chest)
                return;

            if (!ModEntry.Instance.Config.EnableLinkedSort)
                return;

            var location = Game1.player?.currentLocation;
            if (location == null)
                return;

            try
            {
                // 使用 tile-aware 版本，一次遍历搞定
                var linkedWithTiles = ChestLinker.FindLinkedChestsWithTiles(chest, location);

                if (linkedWithTiles.Count <= 1)
                    return;

                // 统计物品总数（在排序前统计，因为排序会清空再填充）
                int totalItemStacks = linkedWithTiles.Sum(t => t.chest.Items.Count(i => i != null));

                ChestLinker.DoLinkedSort(linkedWithTiles);

                if (ModEntry.Instance.Config.ShowNotification)
                {
                    Game1.addHUDMessage(new HUDMessage(
                        $"整理了 {linkedWithTiles.Count} 个链接箱子，共 {totalItemStacks} 件物品", HUDMessage.newQuest_type)
                    { noIcon = true, timeLeft = 2000f });
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"链接整理出错: {ex}", StardewModdingAPI.LogLevel.Error);
            }
        }

        // ────────────────────────────────────────────────
        //  补丁 2：工作台当前场景全覆盖（getContainerContents postfix）
        // ────────────────────────────────────────────────

        private static void ApplyWorkbenchPatch(Harmony harmony)
        {
            var targetType = typeof(CraftingPage);

            // SDV 1.6 中 CraftingPage 只有一个获取容器内容的方法：
            //   IList<Item> getContainerContents()
            // 直接在其上做 postfix，替换返回值为全场景箱子物品，最可靠
            try
            {
                var method = AccessTools.Method(targetType, "getContainerContents");
                if (method != null)
                {
                    harmony.Patch(method,
                        postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_getContainerContents)));
                    ModEntry.Instance.Monitor.Log(
                        "[Harmony] 工作台补丁已应用: CraftingPage.getContainerContents()",
                        StardewModdingAPI.LogLevel.Info);
                    return;
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"[Harmony] 工作台 getContainerContents 补丁失败: {ex.Message}", StardewModdingAPI.LogLevel.Debug);
            }

            ModEntry.Instance.Monitor.Log(
                "[Harmony] ⚠️ 未能找到 getContainerContents，工作台功能不可用",
                StardewModdingAPI.LogLevel.Warn);
        }

        /// <summary>
        /// Postfix：工作台打开时注入全场景箱子物品，保留玩家背包
        /// </summary>
        private static void Postfix_getContainerContents(CraftingPage __instance, ref IList<Item> __result)
        {
            if (!ModEntry.Instance.Config.EnableWorkbenchRangeBoost) return;
            try
            {
                var sf = typeof(CraftingPage).GetField("_standaloneMenu", BindingFlags.Instance | BindingFlags.NonPublic);
                if (sf == null || !(bool)sf.GetValue(__instance)!) return;
                var cf = typeof(CraftingPage).GetField("cooking", BindingFlags.Instance | BindingFlags.NonPublic);
                if (cf != null && (bool)cf.GetValue(__instance)!) return;
                var loc = Game1.player?.currentLocation; if (loc == null) return;

                // 追加场景箱子到 _materialContainers（消耗用）
                if (_craftingListField != null)
                {
                    var mc = (System.Collections.IList)_craftingListField.GetValue(__instance)!;
                    var ex = new HashSet<object>(); foreach (var i in mc) ex.Add(i);
                    foreach (var p in loc.objects.Pairs) if (p.Value is Chest cc && ex.Add(cc.Items)) mc.Add(cc.Items);
                }
                // 追加场景箱子物品到 __result（显示用，每次重建）
                var seen = new HashSet<Item>(__result);
                foreach (var p in loc.objects.Pairs) if (p.Value is Chest cc) for (int i = 0; i < cc.Items.Count; i++) if (cc.Items[i] is Item it && seen.Add(it)) __result.Add(it);
            }
            catch { }
        }

        // ===== 补丁 3：虚拟滚轮（替换 context/callback，物品不动） =====
        private static void ApplyScrollPatch(Harmony harmony)
        {
            var showMenu = AccessTools.Method(typeof(Chest), "ShowMenu");
            if (showMenu != null) harmony.Patch(showMenu, postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_ShowMenu)));
            var scroll = AccessTools.Method(typeof(IClickableMenu), "receiveScrollWheelAction", new[] { typeof(int) });
            if (scroll != null) harmony.Patch(scroll, prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Prefix_ScrollWheel)));
            var close = AccessTools.Method(typeof(ItemGrabMenu), "emergencyShutDown")
                ?? AccessTools.Method(typeof(IClickableMenu), "exitThisMenuNoSound")
                ?? AccessTools.Method(typeof(IClickableMenu), "exitThisMenu");
            if (close != null) harmony.Patch(close, postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_CloseCleanup)));

            var draw = AccessTools.Method(typeof(ItemGrabMenu), "draw", new[] { typeof(SpriteBatch) });
            if (draw != null) harmony.Patch(draw, postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Postfix_DrawIndex)));
        }

        private static void Postfix_ShowMenu(Chest __instance)
        {
            if (!ModEntry.Instance.Config.EnableScrollSwitch) return;
            try
            {
                var loc = Game1.player?.currentLocation; if (loc == null) return;
                var group = ChestLinker.FindLinkedChestsWithTiles(__instance, loc);
                if (group.Count <= 1) return;
                group.Sort((a, b) => { int y = a.tile.Y.CompareTo(b.tile.Y); return y != 0 ? y : a.tile.X.CompareTo(b.tile.X); });
                _linkedGroup = group;
                _groupIndex = group.FindIndex(t => t.chest == __instance);
            }
            catch { }
        }

        private static bool Prefix_ScrollWheel(IClickableMenu __instance, int direction)
        {
            if (_linkedGroup == null || __instance is not ItemGrabMenu grab) return true;

            int n = _linkedGroup.Count;
            _groupIndex = (_groupIndex + (direction > 0 ? -1 : 1) + n) % n;
            var target = _linkedGroup[_groupIndex].chest;

            // 替换 context
            typeof(ItemGrabMenu).GetField("context", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                ?.SetValue(grab, target);

            // 替换 actualInventory
            var itemsMenu = grab.ItemsToGrabMenu;
            if (itemsMenu != null) itemsMenu.actualInventory = target.Items;

            // 替换 behavior 回调
            var bt = typeof(ItemGrabMenu).GetNestedType("behaviorOnItemSelect", BindingFlags.Public | BindingFlags.NonPublic);
            if (bt != null)
            {
                foreach (var f in typeof(ItemGrabMenu).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    if (f.FieldType == bt && f.GetValue(grab) is Delegate d && d.Target is Chest)
                        f.SetValue(grab, Delegate.CreateDelegate(bt, target, d.Method));
                }
            }
            return false;
        }

        private static void Postfix_CloseCleanup()
        {
            _linkedGroup = null; _groupIndex = 0;
        }

        private static void Postfix_DrawIndex(ItemGrabMenu __instance)
        {
            if (_linkedGroup == null) return;
            try
            {
                string text = $"{_groupIndex + 1}/{_linkedGroup.Count}";
                int x = __instance.ItemsToGrabMenu.xPositionOnScreen - 48;
                int y = __instance.ItemsToGrabMenu.yPositionOnScreen - 28;
                int w = (int)Game1.smallFont.MeasureString(text).X + 16;
                Game1.spriteBatch.Draw(Game1.fadeToBlackRect, new Rectangle(x, y, w, 24), Color.Black * 0.55f);
                Game1.spriteBatch.Draw(Game1.fadeToBlackRect, new Rectangle(x, y, w, 2), Color.Gold);
                Utility.drawTextWithShadow(Game1.spriteBatch, text, Game1.smallFont, new Vector2(x + 8, y + 3), Color.White, 1f, 1f, -1, -1, 0.5f);
            }
            catch { }
        }
    }
}
