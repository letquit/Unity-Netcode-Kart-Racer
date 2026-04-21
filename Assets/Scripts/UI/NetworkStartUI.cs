using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Kart {
    /// <summary>
    /// 网络游戏开始界面管理器，处理主机和客户端的启动功能
    /// </summary>
    public class NetworkStartUI : MonoBehaviour {
        [SerializeField] Button startHostButton;
        [SerializeField] Button startClientButton;
        
        /// <summary>
        /// 初始化组件，绑定按钮点击事件
        /// </summary>
        void Start() {
            startHostButton.onClick.AddListener(StartHost);
            startClientButton.onClick.AddListener(StartClient);
        }
        
        /// <summary>
        /// 启动主机模式，作为服务器和客户端运行
        /// </summary>
        void StartHost() {
            Debug.Log("Starting host");
            // 启动网络主机模式
            NetworkManager.Singleton.StartHost();
            // 隐藏当前UI界面
            Hide();
        }

        /// <summary>
        /// 启动客户端模式，连接到现有服务器
        /// </summary>
        void StartClient() {
            Debug.Log("Starting client");
            // 启动网络客户端模式
            NetworkManager.Singleton.StartClient();
            // 隐藏当前UI界面
            Hide();
        }

        /// <summary>
        /// 隐藏当前游戏对象
        /// </summary>
        void Hide() => gameObject.SetActive(false);
    }
}
