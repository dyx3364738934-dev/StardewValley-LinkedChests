using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace SoulMate.Menus
{
    /// <summary>
    /// 伴侣箱子面板 — 基于 ItemGrabMenu（原生箱子交互）
    /// 左侧箱子图标替换为 NPC 像素头像
    /// </summary>
    public class CompanionChest : ItemGrabMenu
    {
        private readonly NPC _npc;
        private readonly CompanionManager _mgr;
        private CompanionState _state;
        private ClickableTextureComponent _followBtn;
        private ClickableTextureComponent _idleBtn;

        public static void Open(NPC npc, CompanionState state, CompanionManager mgr)
        {
            Game1.activeClickableMenu?.exitThisMenu();
            Game1.activeClickableMenu = new CompanionChest(npc, state, mgr);
        }

        public CompanionChest(NPC npc, CompanionState state, CompanionManager mgr)
            : base(
                inventory: InventoryStore.Get(npc.Name),
                reverseGrab: false,
                showReceivingMenu: true,
                highlightFunction: _ => true,
                behaviorOnItemSelectFunction: null,
                message: null,
                behaviorOnItemGrab: null,
                snapToBottom: false,
                canBeExitedWithKey: true,
                context: npc
            )
        {
            _npc = npc;
            _mgr = mgr;
            _state = state;

            // 如果 ItemsToGrabMenu 是 null（可能发生），保底
            if (this.ItemsToGrabMenu != null)
            {
                this.ItemsToGrabMenu.rows = 2;
                this.ItemsToGrabMenu.capacity = 12;
                this.ItemsToGrabMenu.showGrayedOutSlots = false;
            }

            // 按钮区域（紧贴箱子右侧）
            int bx = this.ItemsToGrabMenu?.xPositionOnScreen != 0
                ? this.ItemsToGrabMenu.xPositionOnScreen - 72
                : this.xPositionOnScreen + 32;
            int by = this.ItemsToGrabMenu?.yPositionOnScreen != 0
                ? this.ItemsToGrabMenu.yPositionOnScreen - 72
                : this.yPositionOnScreen + 32;

            _followBtn = new ClickableTextureComponent(
                new Rectangle(bx, by, 56, 56),
                Game1.mouseCursors, new Rectangle(294, 428, 21, 11), 2.6f);

            _idleBtn = new ClickableTextureComponent(
                new Rectangle(bx, by + 64, 56, 56),
                Game1.mouseCursors, new Rectangle(0, 428, 21, 11), 2.6f);

            // 高亮当前状态
            if (_state == CompanionState.Following)
                _followBtn.scale = 3.2f;
            else
                _idleBtn.scale = 3.2f;
        }

        // ══════════════════════════════════════

        public override void draw(SpriteBatch b)
        {
            base.draw(b);

            // ── NPC 像素头像（覆盖箱子图标位置） ──
            // ItemGrabMenu 的箱子图标大约在 xPositionOnScreen 左侧偏移处
            int px = this.xPositionOnScreen + 48;
            int py = this.yPositionOnScreen - 64;

            // 背景框
            IClickableMenu.drawTextureBox(b, px - 4, py - 4, 72, 72, Color.White * 0.8f);

            // 绘制 NPC 精灵（放大到64x128 → 缩小显示头像部分）
            try
            {
                // 取 NPC 精灵的"面部"帧（通常是 frame 0, 面朝下）
                var sourceRect = new Rectangle(0, 0, 16, 24);
                b.Draw(_npc.Sprite.Texture,
                    new Vector2(px + 20, py + 4),
                    sourceRect,
                    Color.White,
                    0f, Vector2.Zero,
                    2.5f,
                    SpriteEffects.None,
                    0.99f);
            }
            catch
            {
                b.Draw(Game1.mouseCursors, new Vector2(px + 16, py + 8),
                    new Rectangle(0, 0, 0, 0), Color.White);
            }

            // ── NPC 名字 ──
            b.DrawString(Game1.smallFont, _npc.displayName,
                new Vector2(px, py + 72), Color.White);

            // ── 状态按钮 ──
            _followBtn.draw(b);
            _idleBtn.draw(b);

            // ── 提示 ──
            if (_followBtn.containsPoint(Game1.getMouseX(), Game1.getMouseY()))
                IClickableMenu.drawToolTip(b, "切换跟随模式", _state == CompanionState.Following ? "当前: 跟随中" : "点击开始跟随", null!);
            if (_idleBtn.containsPoint(Game1.getMouseX(), Game1.getMouseY()))
                IClickableMenu.drawToolTip(b, "暂停伴侣", _state == CompanionState.Idle ? "当前: 待定" : "点击暂停游走", null!);

            if (!Game1.options.hardwareCursor)
                drawMouse(b);
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (_followBtn.containsPoint(x, y))
            {
                _state = _state == CompanionState.Following ? CompanionState.Idle : CompanionState.Following;
                _mgr.SetState(_npc, _state);
                Game1.playSound("drumkit6");
                _followBtn.scale = _state == CompanionState.Following ? 3.2f : 2.6f;
                _idleBtn.scale = _state == CompanionState.Idle ? 3.2f : 2.6f;
                return;
            }
            if (_idleBtn.containsPoint(x, y))
            {
                _state = CompanionState.Idle;
                _mgr.SetState(_npc, CompanionState.Idle);
                Game1.playSound("trashcan");
                _followBtn.scale = 2.6f;
                _idleBtn.scale = 3.2f;
                return;
            }

            base.receiveLeftClick(x, y, playSound);
        }

        public override void performHoverAction(int x, int y)
        {
            base.performHoverAction(x, y);
            _followBtn.tryHover(x, y, 0.05f);
            _idleBtn.tryHover(x, y, 0.05f);
        }

        public override void receiveKeyPress(Keys key)
        {
            if (key == Keys.Escape || (Game1.options.doesInputListContain(Game1.options.menuButton, key) && readyToClose()))
            {
                exitThisMenu(true);
                return;
            }
            base.receiveKeyPress(key);
        }

        public override void emergencyShutDown()
        {
            if (Game1.player.CursorSlotItem != null)
            {
                Game1.player.addItemToInventoryBool(Game1.player.CursorSlotItem);
                Game1.player.CursorSlotItem = null;
            }
            base.emergencyShutDown();
        }
    }

    /// <summary>伴侣背包存储（存档持久化）</summary>
    public static class InventoryStore
    {
        private static readonly Dictionary<string, List<Item>> _store = new();
        private const string KEY = "soulmate_inv";

        public static List<Item> Get(string name)
        {
            if (!_store.TryGetValue(name, out var l))
            {
                l = new List<Item>(12);
                for (int i = 0; i < 12; i++) l.Add(null!);
                _store[name] = l;
            }
            while (l.Count < 12) l.Add(null!);
            return l;
        }

        public static Dictionary<string, List<Item>> All => _store;

        public static void Save(IModHelper h)
        {
            var d = new Dictionary<string, List<SlotData>>();
            foreach (var kv in _store)
                d[kv.Key] = kv.Value.Select(item => item == null ? new SlotData()
                    : new SlotData { Id = item.QualifiedItemId, Stack = item.Stack, Quality = item is StardewValley.Object o ? o.Quality : 0 }).ToList();
            h.Data.WriteSaveData(KEY, d);
        }

        public static void Load(IModHelper h)
        {
            _store.Clear();
            try
            {
                var d = h.Data.ReadSaveData<Dictionary<string, List<SlotData>>>(KEY);
                if (d == null) return;
                foreach (var kv in d)
                {
                    var l = new List<Item>();
                    foreach (var s in kv.Value)
                        l.Add(!string.IsNullOrEmpty(s.Id) ? ItemRegistry.Create(s.Id, s.Stack, s.Quality)! : null!);
                    while (l.Count < 12) l.Add(null!);
                    _store[kv.Key] = l;
                }
            }
            catch { }
        }

        public class SlotData { public string? Id { get; set; } public int Stack { get; set; } public int Quality { get; set; } }
    }
}
