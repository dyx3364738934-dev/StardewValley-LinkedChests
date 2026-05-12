using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Pathfinding;
using StardewValley.TerrainFeatures;
using StardewValley.Monsters;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 参考源码: kimjosell/helpfulSpouses
/// 
/// 核心功能:
///   1. 按 G 键切换 NPC 跟随/停止
///   2. 跟随使用位置历史队列 (20帧延迟)
///   3. 玩家种种子后 NPC 自动浇水
///   4. Harmony 拦截 returnHomeFromFarmPosition 防止 NPC 回家
/// 
/// 可复用技术:
///   - Queue<Vector2> 位置跟随
///   - PathFindController + endBehaviorFunction 自动化路径
///   - HoeDirt.state.Value = 1 直接浇水
/// </summary>
