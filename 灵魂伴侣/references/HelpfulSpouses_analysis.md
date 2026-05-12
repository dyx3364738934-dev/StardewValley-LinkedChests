# HelpfulSpouses (kimjosell) 源码分析

## 来源
GitHub: https://github.com/kimjosell/helpfulSpouses
语言: C#, MIT 许可证

## 文件结构
```
helpfulSpouses/
├── Class1.cs                    # 占位类（空）
├── ModEntry.cs                  # 入口类（空壳）
├── manifest.json                # SMAPI manifest
├── README.md                    # 说明文档
└── stardew/
    ├── CompanionState.cs        # 状态枚举
    ├── ModEntry.cs              # 核心逻辑（~14KB）
    ├── helpfulSpouses.csproj    # 项目文件
    └── manifest.json            # SMAPI manifest
```

## 核心架构分析

### 1. 状态机设计 (CompanionState)
```csharp
public enum CompanionState
{
    Idle,       // 待机（NPC正常生活）
    Following,  // 跟随玩家
    Working,    // 工作模式（预留）
    Watering,   // 浇水模式
}
```

### 2. 跟随系统 (Following Behavior)
- 使用 `Queue<Vector2>` 记录玩家位置历史（20帧延迟）
- 每帧 (`UpdateTicked`) 从队列取出目标位置，NPC向该位置移动
- NPC 到达距离 < 48px 时停止
- 方向根据移动方向自动切换（上下左右）
- 玩家跨地图时自动 `warpCharacter` 跟随
- 使用 Harmony Patch 阻止 `returnHomeFromFarmPosition`（防止NPC自动回家）

### 3. 浇水系统 (Watering Behavior)
- 触发条件：玩家使用种子后（`InventoryChanged` 检测 SeedsCategory 消耗）
- 通过 `FindUnwateredCrops()` 查找所有已种植但未浇水的 `HoeDirt`
- 使用 `PathFindController` 控制 NPC 走到最近的未浇水地块
- `endBehaviorFunction` 回调中直接设置 `dirt.state.Value = 1`（浇水）
- 播放 `doEmote(20)`（水滴表情）作为视觉反馈

### 4. 控制方法
- `DeactivateControllers()` - 清除 NPC 的 controller 和 temporaryController
- `FreeNpc()` - 释放 NPC（walkToHome + 恢复 Idle）
- `walkToHome()` - 使用 PathFindController 导航回家

### 5. Harmony 补丁
```csharp
// 阻止跟随中的配偶自动回家
harmony.Patch(AccessTools.Method(typeof(NPC), "returnHomeFromFarmPosition"), prefix: ...);

// 阻止跟随中与NPC的交互（为后续菜单预留）
harmony.Patch(AccessTools.Method(typeof(NPC), "checkAction", new[] { typeof(Farmer), typeof(GameLocation) }), prefix: ...);
```

### 6. 按键交互
- 按 G 键：开始/停止跟随（需距离 < 2 格）

## 优缺点分析

### 优点
✅ 跟随系统简洁高效（位置队列 + 延迟跟随）
✅ 浇水逻辑直接（PathFindController + HoeDirt.state）
✅ 状态机设计清晰
✅ Harmony 补丁精准（只拦截需要的）
✅ HUD 状态显示

### 缺点
❌ 只支持配偶（`Game1.player.getSpouse()`）
❌ 浇水触发不够智能（只在玩家用种子后触发）
❌ 没有收获/播种功能
❌ 没有战斗系统
❌ 没有采矿/砍树功能
❌ 没有多 NPC 支持
❌ 没有对话系统
❌ 配置项少

## 可复用的技术点
1. `Queue<Vector2>` 位置历史跟随 → 通用跟随系统
2. `FindUnwateredCrops()` + `PathFindController` → 农活自动化
3. `dirt.state.Value = 1` → 直接浇水
4. Harmony prefix 拦截 → 自定义 NPC 行为
5. 状态机枚举 → 可扩展的任务系统
