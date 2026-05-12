# SoulMate — 目标功能规格书 (v1.0 目标)

---

## 一、入口与配置层

### ModEntry.cs (~150行)
**职责**: 模组入口，初始化 Harmony 补丁、事件订阅、管理器。

**事件绑定**:
| 事件 | 处理 | 用途 |
|------|------|------|
| `GameLoop.DayStarted` | 从存档加载背包 + 释放所有伴侣 | 新一天重置 |
| `GameLoop.DayEnding` | 保存背包到存档 + 释放所有伴侣 | 防吞物品 |
| `GameLoop.SaveLoaded` | 从存档恢复背包 | 读档恢复 |
| `Input.ButtonPressed` | 检测招募键(G) | 招募/释放 |
| `Player.Warped` | 传送伴侣跨场景 | 场景跟随 |
| `Display.RenderedHud` | 绘制头顶状态标签 | [♥]/[idle] |

**配置项**: 见 `ModConfig.cs`

---

### ModConfig.cs (~50行)
| 配置 | 默认值 | 说明 |
|------|--------|------|
| `RecruitKey` | `"G"` | 招募/释放快捷键 |
| `ShowStateLabel` | `true` | 头顶状态标签 |
| `ShowNotifications` | `true` | HUD通知 |
| `FollowDelayFrames` | `15` | 跟随队列延迟帧数 |
| `FollowMinDistance` | `48.0` | 最小跟随距离(像素) |
| `CombatRange` | `10` | 索敌范围(格) |
| `CombatInterval` | `0.8` | 攻击间隔(秒) |
| `CombatDamage` | `25` | 每击伤害 |
| `MirrorChopping` | `true` | 镜像砍树 |
| `MirrorWatering` | `true` | 镜像浇水 |
| `MirrorHoeing` | `true` | 镜像锄地 |
| `MirrorFishing` | `true` | 镜像钓鱼 |
| `HoeRadius` | `4` | 锄地半径(格) |
| `PeriodicScanInterval` | `2.0` | 周期扫描间隔(秒) |
| `PetAnimals` | `true` | 抚摸动物 |
| `CollectForage` | `true` | 收集凋落物 |
| `InventorySize` | `12` | 伴侣背包格子数 |

---

## 二、Harmony 补丁层

### HarmonyPatches.cs (~80行)
**目标: 减少补丁数量，只拦截必要方法**

| 原版方法 | 补丁类型 | 功能 |
|----------|---------|------|
| `NPC.update(GameTime, GameLocation)` | **Postfix** | 清除原版日程控制器，注入AI行为 |
| `NPC.returnHomeFromFarmPosition()` | Prefix | 阻止伴侣NPC自动回家 |
| `NPC.checkAction(Farmer, GameLocation)` | Prefix | 右键打开伴侣箱子面板 |

**为什么用 Postfix？**
- 原版 `NPC.update()` 先跑完整流程（精灵动画、呼吸、朝向等）
- Postfix 中我们清除原版设置的 `controller`，再注入自己的AI
- 不破坏原版视觉表现

**多人隔离**: 每个 Prefix/Postfix 入口检查 `!Context.IsMainPlayer → return`

---

## 三、NPC管理

### CompanionManager.cs (~80行)
**职责**: 招募/释放/场景传送

**`Recruit(NPC)`**:
1. 检查是否已在伴侣列表
2. 设置 `ignoreScheduleToday = true` + `farmerPassesThrough = true`
3. 清除所有原版控制器
4. HUD通知

**`Release(NPC)`**:
1. 从伴侣列表移除
2. 恢复原版属性
3. 清除位置队列
4. NPC走回家

**`OnWarp(GameLocation)`**:
- 检测所有伴侣是否在玩家所在场景
- 不在 → `Game1.warpCharacter()` 传送过来
- 重置位置历史队列

**状态管理**:
- 二状态: `Following` / `Idle`
- 通过 `CompanionState` 枚举跟踪

---

## 四、AI引擎

### SoulMateAI.cs (~390行) — 帧驱动核心

**主循环** `Update()`:
```
每帧:
  1. 跨场景检测 → warp
  2. 清除原版controller (POSTFIX架构需要)
  3. Idle状态 → DoIdle()
  4. Following状态 → 优先级: 战斗 > 镜像 > 周期 > 跟随
```

---

### 4.1 Idle状态 `DoIdle()`
| 行为 | 频率 | 说明 |
|------|------|------|
| 随机游走 | ~1%/帧 | 在6格范围内随机选点，用 `PathFindController` 走过去 |
| 周期扫描 | 每4秒 | 抚摸动物 > 收集凋落物（无controller时） |

---

### 4.2 跟随状态 — 战斗 `DoCombat()`
| 条件 | 行为 |
|------|------|
| 检测频率 | 每0.25秒 |
| 索敌范围 | `CombatRange` × 64像素（以玩家+NPC为中心） |
| NPC无血量限制 | ✅ |
| 攻击伤害 | `CombatDamage` 直接扣 `monster.Health` |
| 击杀 | 播放音效 + 移除怪物 + 掉落物自动进伴侣背包 |
| 追敌 | `PathFindController` + 速度+3 |
| 近战(48px内) | 面朝怪物 → 直接扣血 → 击退 |

---

### 4.3 镜像系统 `DoMirror()` — 每0.4秒检测玩家行为
| 玩家行为 | NPC行为 | 特殊规则 |
|----------|---------|----------|
| 拿**洒水壶** | 搜索未浇水耕地 → 走过去浇水 | 直接用 `HoeDirt.state.Value = HoeDirt.watered` |
| 拿**斧头** | 搜索成熟树木 → 走过去砍 | ⚠️ `tree.tapped.Value == false` 排除树液采集器 |
| 拿**锄头** | 半径4格搜索可锄地 → 走过去锄 | 已有耕地的跳过 |
| 拿**鱼竿** | 搜索10格内水域 → 走过去秒上钩 | `loc.getFish()` 直接入背包 |

**`GoNear()` 统一方法**: 距离 < 阈值 → 执行动作；距离 > 阈值 → `PathFindController` 走过去

---

### 4.4 周期扫描 `DoScan()` — 每2秒
| 优先级 | 行为 | 条件 |
|:--:|------|------|
| 1 | **抚摸动物** | 场景是Farm + `FarmAnimal.wasPet == false` |
| 2 | **收集凋落物** | `isForage() == true` 的地面物品 → 自动进背包 |

---

### 4.5 跟随移动 `DoFollowMove()` — V字队形 + 速度缩放
| 条件 | NPC速度 |
|------|--------|
| NPC离玩家 > 5格 | `playerSpeed + 3` |
| NPC离玩家 < 2.8格 | `playerSpeed - 1` |
| 正常范围 | `playerSpeed + 1` |

**V字队形偏移表**:
```
Player →
         [-1, 0]   ← NPC#1 (正后方)
    [-2,-1]  [-2,1] ← NPC#2/#3
[-3,-2] [-3,0] [-3,2] ...
```

**移动方式**: `Queue<Vector2>` 位置历史队列 → 15帧延迟 → 逐帧手动移动 → `Sprite.Animate()`

---

## 五、伴侣箱子 UI

### CompanionChest.cs — 继承 `ItemGrabMenu`
**触发**: 右键伴侣NPC → 打开箱子面板

**布局**:
```
┌──────┬──────────────────────────────┐
│[头像]│ Abigail · 状态: ♥跟随中     │
│名字  │ [▶跟随] [⏸暂停]             │
├──────┴──────────────────────────────┤
│  ┌──┬──┬──┬──┬──┬──┐               │
│  │  │  │  │  │  │  │ ← 12格背包    │
│  ├──┼──┼──┼──┼──┼──┤   和箱子一样  │
│  │  │  │  │  │  │  │               │
│  └──┴──┴──┴──┴──┴──┘               │
├─────────────────────────────────────┤
│  玩家背包 (3×12=36格)               │
└─────────────────────────────────────┘
```

**交互**:
| 操作 | 行为 |
|------|------|
| 左键物品槽 | 取出物品 |
| 右键物品槽 | 取出半组 |
| 手持物品左键 | 存入 |
| 拖拽 | 自由交换 |
| 状态按钮 | 切换跟随/待定 |
| Esc | 关闭 |

---

## 六、物品持久化

### InventoryStore.cs
| 方法 | 调用时机 | 行为 |
|------|----------|------|
| `Get(name)` | 运行时 | 返回NPC的12格背包List |
| `Save(helper)` | DayEnding | 序列化为 `SlotData` DTO → `WriteSaveData` |
| `Load(helper)` | DayStarted / SaveLoaded | `ReadSaveData` → 反序列化 → 恢复背包 |

**SlotData DTO**:
```json
{"Id": "(O)128", "Stack": 5, "Quality": 2}
```

**不会丢物品的场景**:
- 睡觉 → DayEnding保存 + DayStarted恢复 ✅
- 释放NPC → 背包保留在存档 ✅
- 重新招募 → 从存档恢复 ✅
- 返回标题 → 背包保存 ✅

---

## 七、操作手册（用户视角）

### 按键
| 按键 | 条件 | 效果 |
|------|------|------|
| **G** | 站在配偶4格内 | 招募/释放灵魂伴侣 |
| **右键NPC** | 已招募 | 打开伴侣箱子面板 |

### 自动行为（招募后全自动）
- 砍树 → NPC帮砍（跳过采集器树木）
- 浇水 → NPC帮浇
- 锄地 → NPC帮锄（4格半径）
- 钓鱼 → NPC帮钓（秒上钩）
- 下矿 → NPC无敌打怪（10格范围）
- 农场闲逛 → NPC抚摸动物 + 拾取凋落物

### 箱子面板
- 和原版箱子一模一样操作
- 左侧显示NPC像素头像
- 12格独立背包，物品持久化不丢失
