namespace Kart
{
    /// <summary>
    /// 循环缓冲区（环形数组）。
    /// 用于存储固定数量的历史数据，常用于轨迹回放或幽灵车系统。
    /// 当缓冲区满时，新数据会覆盖旧数据。
    /// </summary>
    /// <typeparam name="T">存储的数据类型（如位置、旋转等）</typeparam>
    public class CircularBuffer<T>
    {
        private T[] buffer;       // 实际存储数据的数组
        private int bufferSize;   // 缓冲区的容量大小

        /// <summary>
        /// 构造函数，初始化指定大小的缓冲区
        /// </summary>
        /// <param name="bufferSize">缓冲区大小</param>
        public CircularBuffer(int bufferSize)
        {
            this.bufferSize = bufferSize;
            buffer = new T[bufferSize];
        }

        /// <summary>
        /// 添加数据到指定索引位置。
        /// 利用取模运算实现循环写入：当 index 超过 bufferSize 时，会自动回到数组开头覆盖旧数据。
        /// </summary>
        /// <param name="item">要存储的数据</param>
        /// <param name="index">逻辑索引（例如：Time.frameCount 或 累计帧数）</param>
        public void Add(T item, int index) => buffer[index % bufferSize] = item;

        /// <summary>
        /// 获取指定索引位置的数据。
        /// 同样利用取模运算映射到实际数组下标。
        /// </summary>
        /// <param name="index">逻辑索引</param>
        /// <returns>存储的数据</returns>
        public T Get(int index) => buffer[index % bufferSize];

        /// <summary>
        /// 清空缓冲区。
        /// 注意：这里是通过重新实例化数组来清空，这会触发一次垃圾回收（GC）。
        /// 在频繁调用的游戏循环中应谨慎使用，或者改为遍历数组赋默认值。
        /// </summary>
        public void Clear() => buffer = new T[bufferSize];
    }
}