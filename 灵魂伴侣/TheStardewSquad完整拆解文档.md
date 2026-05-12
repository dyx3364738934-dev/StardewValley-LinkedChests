# The Stardew Squad — 完整代码拆解文档

> 源码: https://github.com/Isalda/the-stardew-squad (Isalda, C#, 700KB, SDV 1.6, Nexus #35341)
> "A Stardew Valley mod that lets you recruit NPCs and pets as companions who follow you and help with tasks."

---

## 1. 项目结构总览

```
TheStardewSquad/
├── ModEntry.cs                 (131行) 入口——依赖注入所有Manager
├── HarmonyPatches.cs           (1482行) ★23个Harmony补丁★
├── Config/
│   ├── ModConfig.cs            (85行)  所有用户配置
│   └── GenericModConfigMenuIntegration.cs (302行) GMCM绑定
├── Framework/
│   ├── FollowerManager.cs      (1190行) ★帧驱动核心★
│   ├── SquadManager.cs         (40行)  招募成员列表
│   ├── SquadMate.cs            (104行) 单个伴侣数据+行为接口
│   ├── SquadMateFactory.cs     (52行)  工厂创建SquadMate
│   ├── RecruitmentManager.cs   (260行) 招募/解散/等待逻辑
│   ├── InteractionManager.cs   (321行) 按键处理+手动命令
│   ├── FormationManager.cs     (162行) 螺旋队形+槽位分配
│   ├── WaitingNpcsManager.cs   (40行)  等待NPC列表
│   ├── TaskManager.cs          (3325行) ★任务执行引擎★(攻击/浇水/伐木/采矿/收获/采集/钓鱼)
│   ├── UnifiedTaskManager.cs   (222行) 统一任务查找(优先级)
│   ├── TaskPriorityManager.cs  (100行) 任务优先级表
│   ├── BehaviorManager.cs      (451行) 空闲动画+允许任务+招募条件
│   ├── DialogueManager.cs      (214行) 对话解析(CP驱动)
│   ├── SpriteManager.cs        (606行) 精灵动画/贴图管理
│   ├── VanillaSpriteDetector.cs (102行) SHA256检测原版贴图
│   ├── AssetManager.cs         (21行)  CP资产管道
│   ├── DebrisCollector.cs      (180行) 掉落物磁吸+物理飞行
│   ├── ServiceFactory.cs       (51行)  抽象层工厂
│   ├── BaselineContentLoader.cs (158行) 基线配置加载
│   ├── NpcConfigManager.cs     (188行) NPC配置管理器
│   ├── NpcConfig/              (6文件) 配置数据模型
│   ├── Tasks/                  SquadTask.cs (46行) 任务数据结构
│   ├── Behaviors/              (5文件) Strategy Pattern行为接口
│   ├── UI/
│   │   ├── SquadMemberMenu.cs       (451行) 招募/管理菜单
│   │   ├── SquadMemberPrompt.cs     (143行) 路由层
│   │   └── SquadInventoryMenu.cs    (67行)  小队背包(ItemGrabMenu)
│   ├── Gathering/
│   │   └── DebrisCollector.cs  (同上)
│   └── Wrappers/              (~15文件) IGameContext等接口实现
├── Pathfinding/
│   ├── AStarPathfinder.cs     (352行) ★自研A*寻路★
│   └── AStarNode.cs           (39行)  A*节点
├── Abstractions/              (~15文件) 依赖倒置接口层
│   ├── Character/             ISquadMate, ISquadMateStateHelper, IWarpService
│   ├── Core/                  IGameContext, IPlayerService, IRandomService
│   ├── Tasks/                 ITaskService, ITaskBehavior
│   ├── Location/              ILocationInfo (218行) + IMapInfo
│   └── UI/                    IUIService
├── Integrations/
│   └── IGenericModConfigMenuApi.cs (80行) GMCM API接口
└── i18n/                      (10种语言翻译)
```

---

## 2. Harmony 补丁全表

| # | 原版方法 | 类型 | 文件行 | 功能 |
|---|----------|------|--------|------|
| 1 | `NPC.IsVillager.get` | Postfix | :153 | 招募NPC时绕过村民阻挡 |
| 2 | `Game1.pressUseToolButton` | Pre+Post | :158 | 追踪玩家工具状态 |
| 3 | `NPC.checkAction` | Prefix | :164 | 战斗地图中阻止交互 |
| 4 | `Utility.checkForCharacterInteractionAtTile` | Prefix | :169 | 战斗地图隐藏交互光标 |
| 5 | `NPC.returnHomeFromFarmPosition` | Prefix | :174 | 阻止招募NPC回家 |
| 6 | `Pet.RunState` | Prefix | :179 | 阻止宠物AI接管 |
| 7 | `Pet.warpToFarmHouse` | Prefix | :184 | 阻止宠物传送回家 |
| 8 | `NPC.sayHiTo` | Prefix | :189 | 跳过对招募宠物的问候 |
| 9 | `GameLocation.CheckGarbage` | Transpiler | :194 | 过滤垃圾桶见证人 |
| 10 | `Debris.updateChunks` | Prefix | :199 | ★碎片飞向NPC |
| 11 | `NPC.getHitByPlayer` | Prefix | :204 | 防弹弓误伤 |
| 12 | `BasicProjectile.behaviorOnCollisionWithMonster` | Prefix | :209 | 防弹丸误伤 |
| 13 | `NPC.update` | **Postfix** | :217 | ★游泳+泳装切换★ |
| 14 | `Pet.draw` | Pre+Post | :222 | 骑乘偏移+游泳 |
| 15 | `BathHousePool.draw` | Pre+Post | :228 | 泳池游泳阴影修复 |
| 16 | `FishingRod.playerCaughtFishEndFunction` | Postfix | :234 | NPC同获鱼 |
| 17 | `FarmAnimal.pet` | Postfix | :239 | 检测玩家抚摸动物 |
| 18 | `Pet.checkAction` | Postfix | :244 | 检测玩家抚摸宠物 |
| 19 | `Crop.harvest` | Postfix | :249 | 检测玩家收获 |
| 20 | `Shears.DoFunction` | Pre+Post | :254 | 检测玩家剪毛 |
| 21 | `MilkPail.DoFunction` | Pre+Post | :260 | 检测玩家挤奶 |
| 22 | `NPC.draw` | Prefix | :266 | 骑乘绘制偏移 |
| 23 | `Farmer.StopSitting` | Prefix | :272 | ★阻止NPC靠近强制站立 |
| 24 | `Furniture.HasSittingFarmers` | Postfix | :289 | NPC坐姿渲染 |
| 25 | `MapSeat.HasSittingFarmers` | Postfix | :302 | NPC坐姿渲染 |
| 26 | `Horse.draw` | Pre+Post | :351 | 骑乘层级调整 |
| 27 | `Character.StandingPixel.get` | Postfix | :366 | 骑马站立像素 |
| 28 | `NPC.draw` (Transpiler) | Transpiler | :330 | 坐姿层级注入 |
| 29 | `Pet.draw` (Transpiler) | Transpiler | :340 | 宠物坐姿层级 |

---

## 3. 核心系统详细拆解

### 3.1 帧驱动跟随 (FollowerManager.cs)

**更新入口** `OnUpdateTicked()` :290:
```
┌─ 每帧调用 ──────────────────────────────────┐
│ 1. 切场结束? → WarpSquadToPlayer()          │
│ 2. DebrisCollector.Update()                 │
│ 3. HandleFestivalState() — 节日自动解散      │
│ 4. HandleFriendshipGain() — 每小时+好感      │
│    ┌─ SlowTick(15帧) ──────────────┐        │
│    │ UpdateMimickingTimers()        │        │
│    │ 检测玩家钓鱼状态               │        │
│    └────────────────────────────────┘        │
│ 5. UpdateRidingState() — 骑乘切换            │
│ 6. 对每个成员:                               │
│    有任务 → HandleTaskExecution()            │
│    无任务 → HandleFollowing()                │
│ 7. 空闲行为 + 对话                            │
└─────────────────────────────────────────────┘
```

**速度缩放** `HandleLocationAndSpeed()` :949:
```
距离 > 15f → 触发追赶模式 IsCatchingUp
距离 > 5f  → npc.speed = baseSpeed + 2
距离 < 2.8f → npc.speed = baseSpeed - 1
正常 → npc.speed = baseSpeed
```

**场景跟随** `HandleLocationAndSpeed()` :953:
```csharp
// ★ 也是直接warp！不是走门口 ★
if (npc.currentLocation != playerLocation)
{
    WarpCharacter(npc, playerLocation.Name, playerTilePoint);
    // 清空路径+任务+状态
}
```

---

### 3.2 A*寻路系统 (Pathfinding/AStarPathfinder.cs)

**不使用 `PathFindController`**，完全自研逐帧移动。

**`FindPath()` :22-81**:
- 最大500次迭代
- 8方向邻居（含对角线），防止墙角穿越
- G成本: 正交10 × speed, 对角14 × speed
- H成本: 曼哈顿距离
- 使用 `IMapInfo.IsTilePassable()` 检查通行性

**关键方法**:
| 方法 | 行 | 功能 |
|------|-----|------|
| `FindClosestPassableNeighbor()` | :102 | 找目标周围最近的可通行格 |
| `IsPathUnobstructed()` | :257 | 射线检查直线无障碍 |
| `IsDirectPathFullyPassable()` | :302 | Bresenham逐格检查 |
| `IsTilePassableForFollower()` | :206 | 忽略NPC碰撞的通行性 |

**逐帧移动** `ExecutePathMovement()` :879:
```csharp
// 手动每帧移动NPC
Vector2 target = pathNode * 64 + (32,32);
Vector2 velocity = getVelocityTowardPoint(npc.Position, target, npc.speed);
npc.Position += velocity;
npc.animateInFacingDirection(gameTime);
if (distance < npc.speed) → Pop节点
```

---

### 3.3 任务系统

**任务类型** `SquadTask.cs`:
```
Watering, Lumbering, Mining, Attacking, Harvesting, 
Foraging, Following, Fishing, Petting, Sitting, Shearing, Milking
```

**数据结构**:
```csharp
class SquadTask {
    TaskType Type;          // 任务类型
    Point Tile;             // 目标物所在格
    Point InteractionTile;  // NPC应该站的位置
    Character TargetCharacter; // 攻击/抚摸目标
    bool IsManual;          // 是否手动命令
    Vector2? SeatPosition;  // 坐姿坐标(支持小数)
}
```

**任务查找** `UnifiedTaskManager.FindUnifiedTask()` :78:
```
按 TaskPriorityManager 的顺序:
Harvesting → Shearing → Milking → Lumbering → Watering 
→ Petting → Foraging → Mining → Fishing → Sitting

每个类型:
  if (Mimicking模式 && 玩家在执行此任务)
    → 分配Mirror任务，40 tick持续(10秒)
  else if (Autonomous模式)
    → 搜索场景内的任务目标
```

**攻击任务** (TaskManager.cs :274-949):
- `CalculateAttackDamage()`: minDamage=CombatLevel×5, maxDamage=10+CombatLevel×11
- `CalculateCritChance()`: 基础2%, Scout +50%
- `CalculateCritMultiplier()`: 基础×3, Desperado ×2
- 动画: 四方向武器挥动精灵帧
- 宠物攻击: 跳向怪物+不同伤害公式

**执行流程** (每个任务):
1. `Find*()` → 搜索最近目标(带距离配置)
2. `npc走到InteractionTile` (A*路径 < 1.5f)
3. `Execute*()` → 执行一次行动
4. `Animate*()` → 播放工具动画
5. 设置冷却(48 tick = 0.8秒)
6. 10%概率说话

---

### 3.4 镜像(Mimicking)系统

**`UpdateMimickingTimers()`** :176:
```
每SlowTick(15帧):
  如果玩家执行任何Mimicking任务:
    → 所有NPC的MimickingTaskTimer重置为40 tick(10秒)
    → 所有NPC的MimickingTaskType设为该任务类型
  
  所有NPC的timer递减，过期清除任务
```

**使用方式**:
```
UnifiedTaskManager.FindUnifiedTask():
  if (MimickingMode && MimickingTaskType == thisType && timer > 0)
    → 返回对应类型的任务
    但只有当NPC在目标范围(InteractionRange)内时才分配
```

---

### 3.5 队形系统 (FormationManager.cs)

**螺旋生成** `GenerateSpiralOffsets()` :22:
```
优先级: (0,1)(-1,1)(1,1)(-1,0)(1,0)(-1,-1)(0,-1)(1,-1)(-2,1)(2,1)...
只保留 y >= 0 的位置（玩家后方）
最多支持150个槽位
```

**朝向旋转** `TryGetTargetTile()` :107:
```
根据玩家FacingDirection旋转偏移:
  Up   → 180° 旋转
  Right → 90° CW
  Left  → 90° CCW
  Down  → 不变
```

---

### 3.6 碎片收集 (DebrisCollector.cs)

**`Update()` :22**:
- 每30帧(0.5秒)扫描一次地面物品
- 检查玩家磁力范围 → 玩家优先
- 否则分配给最近的NPC
- 通过 `HarmonyPatches.Debris_UpdateChunks_Prefix` :394 注入物理

**碎片物理飞行** (HarmonyPatches :394-443):
```csharp
// 碎片飞向NPC而非玩家
Vector2 target = mate.Npc.Position;
chunk.xVelocity += (target.X > pos.X ? +0.8f : -0.8f);
chunk.yVelocity += (target.Y > pos.Y ? -0.8f : +0.8f);
// 距离 < 64px → 收集
```

---

### 3.7 坐姿系统

**触发**: `Farmer.StopSitting_Prefix` (HarmonyPatches :558)
```
检查是否有玩家输入 (wasd/c/click/手柄):
  无输入 → false (阻止站立，防NPC弹飞)
  有输入 → true (允许主动站起)
最短坐姿时间: 100ms
```

**NPC坐下**: TaskType.Sitting
- `LocationInfoWrapper.GetSittableFurniture()` :464 → 检查椅子占用
- `LocationInfoWrapper.GetMapSeats()` :515 → 按index排除已占座位
- `CalculateMapSeatSittingDirection()` :783 → 绕过原版方向bug

**层级深度**: Transpiler注入 (HarmonyPatches :1442)
```
在 NPC.draw() 的 IL 中找到 Y/10000f 模式
↓
替换为 AdjustSittingNpcDepth() → 家具前0.001，家具后-0.001
```

---

### 3.8 UI系统

**招募/管理菜单** `SquadMemberMenu.cs` :451:
```
720×500 IClickableMenu
布局:
  NPC名称(顶部)
  头像(128×128, 中心)
  状态文字
  按钮区(280×80):
    未招募: [招募]
    已招募: [背包] [等待] [解散] [解散全部]
    关闭X按钮
```

**小队背包** `SquadInventoryMenu.cs` :67:
- 继承 `ItemGrabMenu`: 36格全球背包
- 带整理按钮
- 键位: `LeftAlt+E`

---

## 4. 与 SoulMate 的关键差异

| 特性 | Stardew Squad | SoulMate (我们的方向) |
|------|:---:|:---:|
| NPC.update补丁 | POSTFIX(保留原版) | POSTFIX(保留原版) ✅ |
| 跟随方式 | 自研A*逐帧移动 | PathFindController + 手动移动混合 |
| 队形 | 螺旋队形+朝向旋转 | V字队形简化版 |
| 速度 | 距离自适应(+2/-1) | 距离自适应(+3/-1) |
| 任务系统 | Mimicking(40tick)+Autonomous | 直接镜像(检测CurrentTool) |
| 攻击 | 武器动画+暴击+职业加成 | 直接扣血(无敌) |
| 碎片收集 | 物理飞行+磁吸 | 自动入背包 |
| 坐姿 | 完整系统(Furniture+MapSeat) | 无 |
| 骑乘 | NPC骑在马上 | 无 |
| 游泳 | 泳装切换+泳池阴影 | 无 |
| 贴图 | CP驱动的精灵替换(12种任务) | 无 |
| 配置 | 全部任务都有Mode(Disabled/Mimick/Auto) | 简化为开关 |
| 招募限制 | 友谊度(4心默认) | 仅限配偶 |
| 对话 | CP驱动,支持条件判断 | 无 |
| GMCM | ✅ 完整集成 | 无 |
| 换场景 | warp瞬移 | warp瞬移 |
