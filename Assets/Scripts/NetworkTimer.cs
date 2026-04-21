namespace Kart
{
    /// <summary>
    /// 网络计时器。
    /// 用于按照服务器设定的固定频率（Tick Rate）触发逻辑，常用于比赛倒计时或网络状态同步。
    /// </summary>
    public class NetworkTimer
    {
        private float timer; // 累积的时间缓存
        
        // 两次 Tick 之间的最小时间间隔 (例如 60Hz -> 0.016s)
        public float MinTimeBetweenTicks { get; }
        
        // 当前已经触发的 Tick 次数
        public int CurrentTick { get; private set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="serverTickRate">服务器频率（例如 30 或 60）</param>
        public NetworkTimer(float serverTickRate)
        {
            MinTimeBetweenTicks = 1f / serverTickRate;
        }

        /// <summary>
        /// 每一帧调用，累积时间
        /// </summary>
        /// <param name="deltaTime">上一帧到现在的耗时</param>
        public void Update(float deltaTime)
        {
            timer += deltaTime;
        }

        /// <summary>
        /// 检查是否应该执行一次 Tick 逻辑
        /// 如果累积时间超过了阈值，则返回 true 并扣除相应时间
        /// </summary>
        /// <returns>是否应该执行 Tick</returns>
        public bool ShouldTick()
        {
            if (timer >= MinTimeBetweenTicks)
            {
                // 扣除一个周期的时间（注意不是清零，防止低帧率下丢失 Tick）
                timer -= MinTimeBetweenTicks;
                CurrentTick++;
                return true;
            }
            
            return false;
        }
    }
}