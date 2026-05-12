namespace SoulMate
{
    /// <summary>
    /// NPC伴侣二状态
    /// </summary>
    public enum CompanionState
    {
        Idle,       // 待定：游走 + 周期检测
        Following,  // 跟随：主动AI（战斗/镜像/周期）
    }
}
