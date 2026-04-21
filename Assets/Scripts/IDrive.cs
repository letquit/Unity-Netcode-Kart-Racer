using UnityEngine;

namespace Kart
{
    /// <summary>
    /// 驾驶输入接口。
    /// 定义了任何驾驶控制器（玩家、AI、网络幽灵）必须实现的标准。
    /// 这使得车辆物理控制器可以通用化，不依赖具体的输入源。
    /// </summary>
    public interface IDrive
    {
        /// <summary>
        /// 移动输入向量。
        /// 通常：
        /// - x 轴：转向 (-1 左转, 1 右转)
        /// - y 轴：油门/刹车 (0 空闲, 1 加速, -1 倒车/刹车)
        /// </summary>
        Vector2 Move { get; }

        /// <summary>
        /// 刹车状态。
        /// 用于区分单纯的松油门和强制刹车（例如手刹或漂移刹车）。
        /// </summary>
        bool IsBraking { get; }

        /// <summary>
        /// 启用驾驶者。
        /// 当该驾驶者被激活（例如切换控制角色）时调用，用于重置状态或启动协程。
        /// </summary>
        void Enable();
    }
}