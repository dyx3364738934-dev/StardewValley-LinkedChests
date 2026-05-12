using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Objects;

namespace LinkedChests
{
    /// <summary>
    /// 箱子链接核心逻辑 —�?邻居发现、物品收集、堆叠合并、排序、重分配
    /// </summary>
    public static class ChestLinker
    {
        /// <summary>8方向相邻偏移</summary>
        public static readonly Vector2[] AdjacentOffsets = new[]
        {
            new Vector2(-1, -1), new Vector2(0, -1), new Vector2(1, -1),
            new Vector2(-1,  0),                      new Vector2(1,  0),
            new Vector2(-1,  1), new Vector2(0,  1), new Vector2(1,  1),
        };

        /// <summary>
        /// 获取箱子实际槽位数（BigChest=70, 普通箱=36, 祝尼魔箱=9 等）
        /// 使用 Chest 内置�?GetActualCapacity()，而非类型名字符串比较
        /// </summary>
        internal static int GetChestCapacity(Chest chest)
        {
            return chest.GetActualCapacity();
        }

        /// <summary>
        /// 构建场景 tile �?Chest 映射字典
        /// </summary>
        private static Dictionary<Vector2, Chest> BuildTileChestMap(GameLocation location)
        {
            var map = new Dictionary<Vector2, Chest>();
            foreach (var pair in location.objects.Pairs)
            {
                if (pair.Value is Chest chest)
                    map[pair.Key] = chest;
            }
            return map;
        }

        /// <summary>
        /// BFS 扫描 8 方向相邻的所有箱子，返回链接组（�?tile 位置�?
        /// </summary>
        public static List<(Chest chest, Vector2 tile)> FindLinkedChestsWithTiles(Chest sourceChest, GameLocation location)
        {
            if (sourceChest == null || location == null)
                return new List<(Chest, Vector2)>();

            var tileToChest = BuildTileChestMap(location);

            Vector2? startTile = null;
            foreach (var kv in tileToChest)
            {
                if (kv.Value == sourceChest) { startTile = kv.Key; break; }
            }

            if (startTile == null)
                return new List<(Chest, Vector2)> { (sourceChest, Vector2.Zero) };

            var visited = new HashSet<Vector2>();
            var queue = new Queue<Vector2>();
            var result = new List<(Chest, Vector2)>();

            queue.Enqueue(startTile.Value);

            while (queue.Count > 0)
            {
                var tile = queue.Dequeue();
                if (!visited.Add(tile)) continue;
                if (!tileToChest.TryGetValue(tile, out var chest)) continue;

                result.Add((chest, tile));
                foreach (var offset in AdjacentOffsets)
                {
                    var neighbor = tile + offset;
                    if (!visited.Contains(neighbor))
                        queue.Enqueue(neighbor);
                }
            }

            return result;
        }

        /// <summary>
        /// 执行链接整理：收�?�?合并堆叠 �?排序 �?重分�?
        /// （接受预�?tile 位置，避免重复遍历场景物体）
        /// </summary>
        public static void DoLinkedSort(List<(Chest chest, Vector2 tile)> chestsWithTiles)
        {
            if (chestsWithTiles == null || chestsWithTiles.Count <= 1) return;

            // ════════════════════════════════════════════
            // 第一步：收集所有物�?
            // ════════════════════════════════════════════
            var allItems = new List<Item>();
            int chestCount = chestsWithTiles.Count;

            for (int c = 0; c < chestCount; c++)
            {
                var chest = chestsWithTiles[c].chest;
                for (int i = 0; i < chest.Items.Count; i++)
                {
                    var item = chest.Items[i];
                    if (item != null)
                        allItems.Add(item);
                }
                chest.Items.Clear();
            }

            if (allItems.Count == 0) return;

            // ════════════════════════════════════════════
            // 第二步：合并同种物品的堆�?
            // ════════════════════════════════════════════
            var merged = MergeStacks(allItems);

            // ════════════════════════════════════════════
            // 第三步：排序
            // ════════════════════════════════════════════
            SortItems(merged);

            // ════════════════════════════════════════════
            // 第四步：重分�?—�?按格子位置排序（使用预置 tile，不重复遍历�?
            // ════════════════════════════════════════════
            // 复制一份避免修改原始列�?
            var sortedList = chestsWithTiles
                .Select(t => (chest: t.chest, tile: t.tile, capacity: GetChestCapacity(t.chest)))
                .ToList();

            // �?Y (�? 排序，再�?X (�? 排序 �?左上恒定�?#1
            sortedList.Sort((a, b) =>
            {
                int yCmp = a.tile.Y.CompareTo(b.tile.Y);
                if (yCmp != 0) return yCmp;
                return a.tile.X.CompareTo(b.tile.X);
            });

            var orderedChests = sortedList.Select(t => t.chest).ToList();
            var orderedCaps = sortedList.Select(t => t.capacity).ToList();

            DistributeItems(merged, orderedChests, orderedCaps);
        }

        /// <summary>
        /// 合并同种物品的堆�?
        /// </summary>
        private static List<Item> MergeStacks(List<Item> items)
        {
            var groups = new Dictionary<(string Id, int Quality), (Item Template, int TotalCount)>();

            foreach (var item in items)
            {
                int quality = (item is StardewValley.Object obj) ? obj.Quality : 0;
                var key = (item.QualifiedItemId, quality);

                if (groups.TryGetValue(key, out var entry))
                    groups[key] = (entry.Template, entry.TotalCount + item.Stack);
                else
                    groups[key] = (item, item.Stack);
            }

            var result = new List<Item>();
            foreach (var (key, (template, initialCount)) in groups)
            {
                if (template == null || initialCount <= 0) continue;

                int maxStack = template.maximumStackSize();
                int remaining = initialCount;

                while (remaining > 0)
                {
                    int stackSize = Math.Min(remaining, maxStack);
                    var newStack = template.getOne();
                    newStack.Stack = stackSize;
                    result.Add(newStack);
                    remaining -= stackSize;
                }
            }

            return result;
        }

        /// <summary>
        /// 模拟原版整理排序：类�?�?品质降序 �?物品ID
        /// </summary>
        private static void SortItems(List<Item> items)
        {
            items.RemoveAll(item => item == null || item.Stack <= 0);

            items.Sort((a, b) =>
            {
                int catCompare = a.Category.CompareTo(b.Category);
                if (catCompare != 0) return catCompare;

                int qualityA = (a is StardewValley.Object objA) ? objA.Quality : 0;
                int qualityB = (b is StardewValley.Object objB) ? objB.Quality : 0;
                if (qualityA != qualityB) return qualityB.CompareTo(qualityA);

                return string.Compare(a.QualifiedItemId, b.QualifiedItemId, StringComparison.OrdinalIgnoreCase);
            });
        }

        /// <summary>
        /// 将排序好的物品列表按箱子顺序装填（Clear + Add 方式，安全无越界�?
        /// </summary>
        private static void DistributeItems(List<Item> sortedItems, List<Chest> orderedChests, List<int> slotCounts)
        {
            // 构建扁平槽位数组（物品在前，null 在后�?
            int totalSlots = 0;
            for (int i = 0; i < slotCounts.Count; i++)
                totalSlots += slotCounts[i];

            var flatSlots = new Item[totalSlots];
            for (int i = 0; i < sortedItems.Count && i < totalSlots; i++)
                flatSlots[i] = sortedItems[i];

            // 按箱子逐个填装（Clear + Add 方式，自动扩充到正确槽位�?
            int globalIndex = 0;
            for (int c = 0; c < orderedChests.Count; c++)
            {
                var chest = orderedChests[c];
                int slotCount = slotCounts[c];

                chest.Items.Clear();
                for (int i = 0; i < slotCount; i++)
                {
                    chest.Items.Add(flatSlots[globalIndex]);
                    globalIndex++;
                }
            }
        }

    }
}
