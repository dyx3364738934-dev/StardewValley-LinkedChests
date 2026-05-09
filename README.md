# Linked Chests

星露谷物语 SMAPI Mod —— 相邻箱子自动链接，一键整理跨箱子智能排序，工作台全场景覆盖，滚轮虚拟切箱。

## 功能

- **🔗 相邻箱子联排**：8 方向相邻的箱子自动链接为一组，点击整理按钮时按原版规则（类别 → 品质降序 → ID）跨箱子合并堆叠、排序、重新装填
- **🔧 工作台全场景覆盖**：工作台不再仅限于贴着的箱子，可读取当前场景内所有箱子物品。**已修复 v1.2.0 中制作不消耗材料的严重 bug**
- **🔄 滚轮虚拟切箱**：打开链接箱子后，鼠标滚轮即可切换查看同组其他箱子内容。**纯虚拟，真实物品完全不动，多人互不影响**
- **📦 智能排序**：大箱子和小箱子混搭也正确分配槽位，按左上→右下网格顺序装填
- **🔌 API 开放**：其他 mod 可通过 `ILinkedChestsApi.TriggerSort()` 触发联排整理
- **📋 Quick Stack 联动**：检测到 Quick Stack 后，一键堆叠自动触发联排整理

## 安装

1. 安装 [SMAPI](https://smapi.io/) (v4.0.0+)
2. 下载最新 [Release](https://github.com/dyx3364738934-dev/StardewValley-LinkedChests/releases)
3. 解压到 `Stardew Valley/Mods/LinkedChests/`
4. 启动游戏

## 配置

运行一次游戏后，在 `Mods/LinkedChests/config.json` 中修改：

```json
{
  "EnableLinkedSort": true,
  "ShowNotification": true,
  "EnableWorkbenchRangeBoost": true,
  "EnableScrollSwitch": true
}
```

| 配置项 | 说明 | 默认值 |
|--------|------|--------|
| `EnableLinkedSort` | 启用相邻箱子联排整理 | `true` |
| `ShowNotification` | 整理时显示 HUD 通知（含箱子数和物品数） | `true` |
| `EnableWorkbenchRangeBoost` | 启用工作台全场景覆盖 | `true` |
| `EnableScrollSwitch` | 启用滚轮虚拟切箱 | `true` |

## API

其他 mod 可调用联排整理：

```csharp
var api = helper.ModRegistry.GetApi<ILinkedChestsApi>("Koko.LinkedChests");
api?.TriggerSort(chest);
```

## 兼容性

- Stardew Valley 1.6+
- SMAPI 4.0.0+
- 兼容 Quick Stack mod（自动联动）

## 版本历史

- **v1.3.0** — 滚轮虚拟切箱（替换 context/callback，真实物品不动，多人安全）+ 工作台消耗 bug 修复
- **v1.2.0** — 工作台全场景覆盖 + 代码优化重构
- **v1.1.0** — 新增工作台范围增强、API 接口
- **v1.0.0** — 初始版本：相邻箱子联排整理
