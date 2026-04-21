using Unity.Netcode.Components; // 引入 Netcode 的组件库，为了使用 NetworkTransform
using UnityEngine;

namespace Kart
{
    // 自定义权限模式枚举，比原生的更直观
    public enum AuthorityMode
    {
        Server, // 服务器权威：服务器决定物体的位置，客户端只能看
        Client  // 客户端权威：客户端（Owner）决定物体的位置，通常用于玩家车辆
    }

    // 禁止在同一物体上添加多个此组件，防止冲突
    [DisallowMultipleComponent]
    public class ClientNetworkTransform : MonoBehaviour
    {
        // 在 Inspector 面板显示的权限模式选择
        public AuthorityMode authorityMode = AuthorityMode.Client;

        // 是否同步 X/Y/Z 轴位置的开关
        public bool SyncPositionX = true;
        public bool SyncPositionY = true;
        public bool SyncPositionZ = true;

        // 引用底层的 NetworkTransform 组件
        [SerializeField] private NetworkTransform target;

        // 当组件被首次添加或重置时调用
        private void Reset()
        {
            // 自动寻找同物体上的 NetworkTransform 组件
            if (!target) target = GetComponent<NetworkTransform>();
            Apply(); // 应用设置
        }

        // 当 Inspector 面板中的数值被修改时调用
        private void OnValidate()
        {
            // 确保 target 不为空
            if (!target) target = GetComponent<NetworkTransform>();
            Apply(); // 实时应用设置，方便在编辑器中查看效果
        }

        // 游戏运行时唤醒
        private void Awake() => Apply();

        /// <summary>
        /// 将本脚本的配置应用到目标的 NetworkTransform 组件上
        /// </summary>
        public void Apply()
        {
            if (!target) return;

            // 映射权限模式
            // 如果选择 Server，则设为 Server 模式
            // 如果选择 Client，则设为 Owner 模式（Owner 在 Netcode 中代表拥有该物体的客户端）
            target.AuthorityMode = authorityMode == AuthorityMode.Server
                ? NetworkTransform.AuthorityModes.Server
                : NetworkTransform.AuthorityModes.Owner;

            // 同步轴向设置
            target.SyncPositionX = SyncPositionX;
            target.SyncPositionY = SyncPositionY;
            target.SyncPositionZ = SyncPositionZ;
        }
    }
}