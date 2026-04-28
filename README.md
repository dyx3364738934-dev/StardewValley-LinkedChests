# Linked Chests 🔗

> English | [中文](#linked-chests-1)

A **Stardew Valley** mod that links adjacent chests together. Click the **organize button** on any linked chest, and ALL adjacent chests will be sorted as one unified inventory — items consolidate, stacks merge, and overflow fills the next chest automatically.

## Features

- 🔗 **Auto-link**: Chests placed next to each other (8-direction adjacency) are automatically linked
- 🗂️ **Unified Sort**: Click "Organize" on any chest → all linked chests sort together
- 📦 **Smart Consolidation**: Same items merge stacks, overflow fills chests in left→right, top→bottom order
- 🔄 **Seamless**: Replaces the vanilla organize button — no extra keys needed
- 🤝 **QuickStack Integration**: Works with [QuickStack](https://github.com/Koko/QuickStack) — auto-sorts after stacking
- ⚙️ Configurable notifications

## How It Works

```
Before:                           After clicking "Organize":
┌──────┬──────┐                   ┌──────┬──────┐
│Stone │Stone │                   │Stone │Wood  │
│  ×10 │  ×50 │                   │  ×60 │  ×30 │
│Wood  │empty │     ──────▶       │empty │empty │
│  ×30 │      │                   │  ... │  ... │
└──────┴──────┘                   └──────┴──────┘
```

Chests fill in grid order: **#1** (top-left) → **#2** → **#3** → **#4**

## Configuration

```json
{
  "EnableLinkedSort": true,     // Master switch
  "ShowNotification": true      // Show HUD message after sorting
}
```

## Install

1. Install [SMAPI](https://smapi.io) (4.0+)
2. Download and extract to `Mods/LinkedChests/`
3. Start the game!
4. Place chests next to each other, click organize ✨

## API

Other mods can trigger linked sorting via SMAPI's mod API:

```csharp
var api = helper.ModRegistry.GetApi<ILinkedChestsApi>("Koko.LinkedChests");
api?.TriggerSort(chest);
```

## Compatibility

- ✅ Stardew Valley 1.6+
- ✅ Regular Chests (36 slots) & Big Chests (70 slots)
- ✅ Multiplayer

---

# Linked Chests 箱子链接 🔗

一个 **星露谷** mod，相邻箱子自动链接。点任意箱子的**整理按钮**，所有贴在一起的箱子会作为一个整体统一排序——同种物品合并堆叠，前箱满了自动溢出到后箱。

## 功能

- 🔗 相邻箱子自动链接（8方向）
- 🗂️ 一键联排：点整理按钮 → 全部链接箱子一起排序
- 📦 智能合并：同种物品堆叠合并，按左上→右上→左下→右下顺序装填
- 🤝 联动 [QuickStack](https://github.com/Koko/QuickStack)：一键堆叠后自动触发联排

## 工作原理

```
整理前：              整理后：
┌──────┬──────┐      ┌──────┬──────┐
│石头10│石头50│      │石头60│木头30│
│木头30│  空  │  →   │  空  │  空  │
└──────┴──────┘      └──────┴──────┘
```

按网格顺序装填：#1(左上) → #2 → #3 → #4
