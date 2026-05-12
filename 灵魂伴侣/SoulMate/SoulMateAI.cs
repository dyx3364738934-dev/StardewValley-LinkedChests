using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Monsters;
using StardewValley.Pathfinding;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using SObject = StardewValley.Object;

namespace SoulMate
{
    /// <summary>
    /// SoulMateAI v0.5 — POSTFIX架构 + 距离速度缩放 + V字队形
    /// </summary>
    public static class SoulMateAI
    {
        private static float _periodic, _combat, _mirror;
        private static readonly Random _rng = new();

        // V字队形偏移表（玩家背后）
        private static readonly Point[] VFormation =
        {
            new(-1, 0),  new(-2, -1), new(-2, 1),
            new(-3, -2), new(-3, 2),  new(-4, -3), new(-4, 3),
        };

        public static void Update(NPC npc, CompanionState state, Queue<Vector2> q,
            ModConfig c, IMonitor m, GameTime t)
        {
            var loc = npc.currentLocation;
            if (loc == null) return;
            float dt = (float)t.ElapsedGameTime.TotalSeconds;

            // ── 跨场景：瞬移（和StardewSquad一样，这是行业标准） ──
            if (state == CompanionState.Following && npc.currentLocation != Game1.player.currentLocation)
            {
                Game1.warpCharacter(npc, Game1.player.currentLocation.Name, Game1.player.TilePoint);
                npc.controller = null;
                q.Clear();
                for (int i = 0; i < c.FollowDelayFrames; i++) q.Enqueue(npc.Position);
                return;
            }

            if (state == CompanionState.Idle) { DoIdle(npc, loc, c, dt); return; }

            // ── POSTFIX架构：必须逐帧清掉游戏日程塞进来的controller ──
            if (npc.controller != null)
            {
                npc.controller = null;
                npc.temporaryController = null;
            }

            // ── Following: 战斗 > 镜像 > 周期 > 跟随 ──
            _combat += dt;
            if (_combat >= 0.25f) { _combat = 0f; if (DoCombat(npc, loc, c)) return; }

            _mirror += dt;
            if (_mirror >= 0.4f) { _mirror = 0f; if (DoMirror(npc, loc, c, m)) return; }

            _periodic += dt;
            if (_periodic >= c.PeriodicScanInterval) { _periodic = 0f; DoScan(npc, loc, c, m); }

            DoFollowMove(npc, q, c);
        }

        // ═══════════════════ Idle ═══════════════════
        private static void DoIdle(NPC npc, GameLocation loc, ModConfig c, float dt)
        {
            if (npc.controller == null && _rng.NextDouble() < 0.008f)
                npc.controller = new PathFindController(npc, loc,
                    new Point(npc.TilePoint.X + _rng.Next(-6, 7), npc.TilePoint.Y + _rng.Next(-6, 7)), _rng.Next(4));

            _periodic += dt;
            if (_periodic >= c.PeriodicScanInterval * 2f)
            {
                _periodic = 0f;
                if (npc.controller == null) DoScan(npc, loc, c, null!);
            }
        }

        // ═══════════════════ 战斗 ═══════════════════
        private static bool DoCombat(NPC npc, GameLocation loc, ModConfig c)
        {
            float range = c.CombatRange * 64f;
            var m = loc.characters?.OfType<Monster>()
                .Where(x => x.Health > 0)
                .Where(x => Vector2.Distance(npc.Position, x.Position) < range ||
                            Vector2.Distance(Game1.player.Position, x.Position) < range)
                .OrderBy(x => Vector2.Distance(npc.Position, x.Position))
                .FirstOrDefault();
            if (m == null) return false;

            float d = Vector2.Distance(npc.Position, m.Position);
            Vector2 dir = m.Position - npc.Position;
            npc.FacingDirection = Math.Abs(dir.X) > Math.Abs(dir.Y) ? (dir.X > 0 ? 1 : 3) : (dir.Y > 0 ? 2 : 0);

            if (d < 48f)
            {
                m.Health -= c.CombatDamage;
                if (m.Health <= 0)
                {
                    Game1.playSound("monsterDie");
                    loc.characters?.Remove(m);
                    foreach (string id in m.objectsToDrop ?? [])
                    {
                        var drop = ItemRegistry.Create(id);
                        if (drop != null)
                        {
                            // 掉落物自动放进伴侣背包
                            var inv = Menus.InventoryStore.Get(npc.Name);
                            for (int i = 0; i < inv.Count; i++)
                                if (inv[i] == null) { inv[i] = drop; break; }
                        }
                    }
                }
                else Game1.playSound("hitEnemy");
            }
            else
            {
                npc.controller = new PathFindController(npc, loc, m.TilePoint, 2);
                // ★ 追击加速 ★
                npc.speed = Math.Max(3, (int)Game1.player.getMovementSpeed() + 2);
            }

            return true;
        }

        // ═══════════════════ 镜像 ═══════════════════
        private static bool DoMirror(NPC npc, GameLocation loc, ModConfig c, IMonitor m)
        {
            var tool = Game1.player.CurrentTool;
            if (tool == null) return false;

            if (tool is WateringCan && c.MirrorWatering)
            {
                var dry = loc.terrainFeatures?.Pairs
                    .Where(p => p.Value is HoeDirt d && d.crop != null && d.state.Value != HoeDirt.watered)
                    .OrderBy(p => Vector2.Distance(Utility.PointToVector2(npc.TilePoint), p.Key))
                    .ToArray();
                if (dry.Length > 0)
                {
                    var key = dry[0].Key;
                    if (GoNear(npc, loc, key, 1.5f))
                    {
                        if (loc.terrainFeatures?.TryGetValue(key, out var tf) == true && tf is HoeDirt dirt)
                            dirt.state.Value = HoeDirt.watered;
                        npc.doEmote(20);
                    }
                    return true;
                }
            }

            if (tool is Axe && c.MirrorChopping)
            {
                var trees = loc.terrainFeatures?.Pairs
                    .Where(p => p.Value is Tree t && t.growthStage.Value >= 5 && !t.stump.Value && !t.tapped.Value)
                    .OrderBy(p => Vector2.Distance(Utility.PointToVector2(npc.TilePoint), p.Key))
                    .ToArray();
                if (trees.Length > 0)
                {
                    var key = trees[0].Key;
                    if (GoNear(npc, loc, key, 1.5f))
                        new Axe().DoFunction(loc, (int)key.X, (int)key.Y, 0, Game1.player);
                    return true;
                }
            }

            if (tool is Hoe && c.MirrorHoeing)
            {
                var spot = FindHoeSpot(loc, Game1.player.TilePoint, c.HoeRadius);
                if (spot != null)
                {
                    if (GoNear(npc, loc, spot.Value, 2f))
                        new Hoe().DoFunction(loc, (int)spot.Value.X, (int)spot.Value.Y, 0, Game1.player);
                    return true;
                }
            }

            if (tool is FishingRod && c.MirrorFishing)
            {
                var water = FindWater(loc, npc.TilePoint);
                if (water != null)
                {
                    if (GoNear(npc, loc, water.Value, 2f))
                    {
                        var fish = loc.getFish(0f, null, 0, Game1.player, 0, water.Value);
                        if (fish != null)
                        {
                            var inv = Menus.InventoryStore.Get(npc.Name);
                            for (int i = 0; i < inv.Count && fish != null; i++)
                            {
                                if (inv[i] == null) { inv[i] = fish; break; }
                                if (inv[i]!.canStackWith(fish)) { inv[i]!.Stack += fish.Stack; break; }
                            }
                            Game1.playSound("fishSlap");
                        }
                    }
                    return true;
                }
            }

            return false;
        }

        private static bool GoNear(NPC npc, GameLocation loc, Vector2 tile, float threshold)
        {
            float d = Vector2.Distance(Utility.PointToVector2(npc.TilePoint), tile);
            if (d < threshold)
            {
                Vector2 dir = tile - Utility.PointToVector2(npc.TilePoint);
                npc.FacingDirection = Math.Abs(dir.X) > Math.Abs(dir.Y) ? (dir.X > 0 ? 1 : 3) : (dir.Y > 0 ? 2 : 0);
                return true;
            }
            npc.controller = new PathFindController(npc, loc, new Point((int)tile.X, (int)tile.Y), 2);
            return false;
        }

        // ═══════════════════ 周期扫描 ═══════════════════
        private static void DoScan(NPC npc, GameLocation loc, ModConfig c, IMonitor m)
        {
            if (npc.controller != null) return;
            if (c.PetAnimals && loc.IsFarm && TryPet(npc, loc)) return;
            if (c.CollectForage && TryForage(npc, loc)) return;
        }

        private static bool TryPet(NPC npc, GameLocation loc)
        {
            var farm = loc as Farm ?? Game1.getFarm();
            var a = farm?.animals?.Values
                .Where(x => x.currentLocation == loc && !x.wasPet.Value)
                .OrderBy(x => Vector2.Distance(npc.Position, x.Position))
                .FirstOrDefault();
            if (a == null) return false;
            if (GoNear(npc, loc, new Vector2(a.TilePoint.X, a.TilePoint.Y), 2f)) a.pet(Game1.player);
            return true;
        }

        private static bool TryForage(NPC npc, GameLocation loc)
        {
            if (loc.objects == null) return false;
            var f = loc.objects.Pairs.Where(p => p.Value?.isForage() == true)
                .OrderBy(p => Vector2.Distance(npc.Position, p.Key * 64f)).FirstOrDefault();
            if (f.Value == null) return false;
            var tile = f.Key;
            if (GoNear(npc, loc, tile, 2f))
            {
                if (loc.objects.TryGetValue(tile, out var o) && o?.isForage() == true)
                {
                    var inv = Menus.InventoryStore.Get(npc.Name);
                    for (int i = 0; i < inv.Count; i++)
                    {
                        if (inv[i] == null) { inv[i] = o; break; }
                        if (inv[i]!.canStackWith(o)) { inv[i]!.Stack += o.Stack; break; }
                    }
                    loc.objects.Remove(tile);
                    Game1.playSound("pickUpItem");
                }
            }
            return true;
        }

        // ═══════════════════ 跟随移动 (V字队形 + 速度缩放) ═══════════════════
        private static void DoFollowMove(NPC npc, Queue<Vector2> q, ModConfig c)
        {
            // ★ 队形偏移 ★
            if (!HarmonyPatches.FormationIndexes.TryGetValue(npc.Name, out int fi))
                fi = 0;
            Point offset = fi < VFormation.Length ? VFormation[fi] : VFormation[^1];

            Vector2 playerPos = Game1.player.Position;
            Vector2 formTarget = playerPos + new Vector2(
                offset.X * 64f * (Game1.player.FacingDirection == 3 ? -1 : 1),
                offset.Y * 64f);

            q.Enqueue(formTarget);
            if (q.Count <= c.FollowDelayFrames) return;

            Vector2 tgt = q.Dequeue();
            float dist = Vector2.Distance(npc.Position, playerPos);

            // ★ 速度缩放（抄StardewSquad） ★
            float playerSpeed = Game1.player.getMovementSpeed();
            if (dist > 5f * 64f) npc.speed = Math.Max(3, (int)playerSpeed + 3);
            else if (dist < 2.8f * 64f) npc.speed = Math.Max(2, (int)playerSpeed - 1);
            else npc.speed = (int)playerSpeed + 1;

            if (dist < c.FollowMinDistance) { npc.Halt(); return; }

            Vector2 dir = new Vector2(tgt.X, tgt.Y - 16f) - npc.Position;
            float ax = Math.Abs(dir.X), ay = Math.Abs(dir.Y);
            npc.FacingDirection = ax > ay * 1.5f ? (dir.X > 0 ? 1 : 3) : (ay > ax * 1.5f ? (dir.Y > 0 ? 2 : 0) : npc.FacingDirection);
            int sf = npc.FacingDirection * 4;

            if (dir.Length() > 1f)
            {
                npc.Sprite.Animate(Game1.currentGameTime, sf, 4, 100f);
                Vector2 mv = dir;
                if (mv.Length() > npc.speed) mv = Vector2.Normalize(mv) * npc.speed;
                npc.Position += mv;
            }
        }

        // ═══════════════════ 工具 ═══════════════════
        private static Vector2? FindHoeSpot(GameLocation loc, Point center, int r)
        {
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    int tx = center.X + dx, ty = center.Y + dy;
                    var k = new Vector2(tx, ty);
                    if (loc.terrainFeatures?.ContainsKey(k) == true && loc.terrainFeatures[k] is HoeDirt) continue;
                    if (loc.doesTileHaveProperty(tx, ty, "Diggable", "Back") == "T") return k;
                }
            return null;
        }

        private static Vector2? FindWater(GameLocation loc, Point from)
        {
            Vector2? best = null; float bd = 20f;
            for (int dx = -10; dx <= 10; dx++)
                for (int dy = -10; dy <= 10; dy++)
                {
                    int tx = from.X + dx, ty = from.Y + dy;
                    if (loc.isWaterTile(tx, ty))
                    {
                        float d = Vector2.Distance(Utility.PointToVector2(from), new Vector2(tx, ty));
                        if (d < bd) { bd = d; best = new Vector2(tx, ty); }
                    }
                }
            return best;
        }
    }
}
