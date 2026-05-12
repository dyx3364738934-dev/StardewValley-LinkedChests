namespace SoulMate
{
    /// <summary>
    /// SoulMate v0.3 配置 — 简化二状态 + 镜像AI
    /// </summary>
    public class ModConfig
    {
        // ── 通用 ──
        public string RecruitKey { get; set; } = "G";
        public bool ShowStateLabel { get; set; } = true;
        public bool ShowNotifications { get; set; } = true;

        // ── 跟随 ──
        public int FollowDelayFrames { get; set; } = 15;
        public float FollowMinDistance { get; set; } = 48f;

        // ── 战斗 ──
        /// <summary>索敌范围（格，10 = 10 tile）</summary>
        public int CombatRange { get; set; } = 10;
        /// <summary>NPC攻击间隔（秒）</summary>
        public float CombatInterval { get; set; } = 0.8f;
        /// <summary>NPC攻击力（每击伤害）</summary>
        public int CombatDamage { get; set; } = 25;

        // ── 镜像AI ──
        /// <summary>锄地半径（格，以玩家为中心）</summary>
        public int HoeRadius { get; set; } = 4;
        /// <summary>是否启用镜像砍树</summary>
        public bool MirrorChopping { get; set; } = true;
        /// <summary>是否启用镜像浇水</summary>
        public bool MirrorWatering { get; set; } = true;
        /// <summary>是否启用镜像锄地</summary>
        public bool MirrorHoeing { get; set; } = true;
        /// <summary>是否启用镜像钓鱼</summary>
        public bool MirrorFishing { get; set; } = true;

        // ── 周期检测 ──
        /// <summary>周期检测间隔（秒）</summary>
        public float PeriodicScanInterval { get; set; } = 2f;
        /// <summary>是否抚摸动物</summary>
        public bool PetAnimals { get; set; } = true;
        /// <summary>是否收集凋落物</summary>
        public bool CollectForage { get; set; } = true;

        // ── 背包 ──
        public int InventorySize { get; set; } = 12;
    }
}
