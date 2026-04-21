using Eflatun.SceneReference; // 引入 SceneReference 库，用于类型安全的场景引用
using Unity.Netcode;          // 引入 Unity Netcode，用于多人游戏网络管理
using UnityEngine.SceneManagement; // 引入场景管理命名空间

namespace Kart
{
    /// <summary>
    /// 场景加载器。
    /// 封装网络场景加载逻辑，确保客户端与主机场景同步。
    /// </summary>
    public static class Loader
    {
        /// <summary>
        /// 加载网络场景。
        /// 由网络管理器处理加载，自动同步所有客户端。
        /// </summary>
        /// <param name="scene">要加载的场景引用（使用 Eflatun.SceneReference）</param>
        public static void LoadNetwork(SceneReference scene)
        {
            // 使用 NetworkManager 的 SceneManager 加载场景
            // 参数1：场景名称
            // 参数2：加载模式（Single 表示替换当前场景，Additive 表示叠加）
            NetworkManager.Singleton.SceneManager.LoadScene(scene.Name, LoadSceneMode.Single);
        }
    }
}