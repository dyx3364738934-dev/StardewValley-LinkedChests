# Linked Chests

星露谷物语 SMAPI Mod —— 相邻箱子链接，一键联排整理 + 滚轮切箱 + 工作台全场景覆盖。

## 功能

- **🔗 联排整理**：8 方向相邻箱子自动链接，点击整理按钮跨箱子合并堆叠、原版规则排序、重新装填
- **🔄 滚轮切箱**：打开链接箱子后鼠标滚轮切换查看同组其他箱子，大小箱子自动适配 UI
- **🔧 工作台全场景覆盖**：工作台读取当前场景内所有箱子物品（已修复消耗 bug）
- **📦 大小混搭**：大箱子(70格) 和 小箱子(36格) 混搭正确识别容量，分配正确
- **📋 Quick Stack 联动**：检测到 Quick Stack 后自动触发联排整理
- **🔌 API 开放**：`ILinkedChestsApi.TriggerSort(chest)`

## 安装

1. 安装 [SMAPI](https://smapi.io/) 4.0.0+
2. 下载最新 [Release](https://github.com/dyx3364738934-dev/StardewValley-LinkedChests/releases)
3. 解压到 `Stardew Valley/Mods/LinkedChests/`
4. 启动游戏

## 配置

`Mods/LinkedChests/config.json`：

| 配置项 | 说明 | 默认 |
|--------|------|------|
| `EnableLinkedSort` | 联排整理 | `true` |
| `ShowNotification` | HUD 通知 | `true` |
| `EnableWorkbenchRangeBoost` | 工作台全场景 | `true` |
| `EnableScrollSwitch` | 滚轮切箱 | `true` |

## 兼容性

- Stardew Valley 1.6+
- SMAPI 4.0.0+
- Quick Stack mod（自动联动）

## 版本历史

- **v1.3.2** — 按箱子滚轮，大小箱子 UI 自动切换
- **v1.3.1** — GetChestCapacity 修复（GetActualCapacity）
- **v1.3.0** — 虚拟滚轮切箱 + 工作台消耗修复
- **v1.2.0** — 工作台全场景覆盖
