using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using Utilities; // 假设包含 CountdownTimer 工具类
using Random = UnityEngine.Random;

namespace Kart
{
    /// <summary>
    /// 加密类型枚举。
    /// DTLS: 用于 UDP 传输（PC, Mobile, Console）。
    /// WSS: 用于 WebSocket 传输（WebGL）。
    /// </summary>
    [Serializable]
    public enum EncryptionType
    {
        DTLS, 
        WSS,
    }

    /// <summary>
    /// 多人游戏管理器。
    /// 负责处理网络初始化、大厅创建、加入以及 Relay 连接。
    /// </summary>
    public class Multiplayer : MonoBehaviour
    {
        [SerializeField] private string lobbyName = "Lobby";
        [SerializeField] private int maxPlayers = 4;
        [SerializeField] private EncryptionType encryption = EncryptionType.DTLS;
        
        // 单例实例
        public static Multiplayer Instance { get; private set; }
        
        // 当前玩家信息
        public string PlayerId { get; private set; }
        public string PlayerName { get; private set; }

        private Lobby currentLobby;
        // 根据加密类型返回对应的字符串标识
        private string connectionType => encryption == EncryptionType.DTLS ? k_dtlsEncryption : k_wssEncryption;

        // 大厅心跳间隔（秒），防止大厅超时被销毁
        private const float k_lobbyHeartbeatInterval = 20f;
        // 大厅数据轮询间隔（秒），用于检测更新
        private const float k_lobbyPollInterval = 65f;
        
        // 存储 Relay 加入码的键名
        private const string k_keyJoinCode = "RelayJoinCode";
        
        private const string k_dtlsEncryption = "dtls";
        private const string k_wssEncryption = "wss";

        // 倒计时定时器，用于心跳和轮询
        private CountdownTimer heartbeatTimer = new CountdownTimer(k_lobbyHeartbeatInterval);
        private CountdownTimer pollForUpdatesTimer = new CountdownTimer(k_lobbyPollInterval);

        private async void Start()
        {
            Instance = this;
            DontDestroyOnLoad(this); // 确保场景切换时不销毁

            await Authenticate(); // 初始化并认证服务

            // 配置心跳定时器事件
            heartbeatTimer.OnTimerStart += async () =>
            {
                await HandleHeartbeatAsync();
                heartbeatTimer.Start(); // 重启定时器
            };
    
            // 配置轮询定时器事件
            pollForUpdatesTimer.OnTimerStop += async () =>
            {
                await HandlePollForUpdatesAsync();
                pollForUpdatesTimer.Start();
            };
        }

        /// <summary>
        /// 随机生成玩家名并认证。
        /// </summary>
        private async Task Authenticate()
        {
            await Authenticate("Player" + Random.Range(0, 1000));
        }

        /// <summary>
        /// 初始化 Unity Services 并进行匿名认证。
        /// </summary>
        private async Task Authenticate(string playerName)
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                InitializationOptions options = new InitializationOptions();
                options.SetProfile(playerName); // 设置用户配置文件

                await UnityServices.InitializeAsync(options);
            }

            AuthenticationService.Instance.SignedIn += () =>
            {
                Debug.Log("Signed in as " + AuthenticationService.Instance.PlayerId);
            };

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                PlayerId = AuthenticationService.Instance.PlayerId;
                PlayerName = playerName;
            }
        }

        /// <summary>
        /// 创建大厅并启动主机。
        /// </summary>
        public async Task CreateLobby()
        {
            try
            {
                // 1. 分配 Relay 资源
                Allocation allocation = await AllocateRelay();
                // 2. 获取 Relay 加入码
                string relayJoinCode = await GetRelayJoinCode(allocation);

                // 3. 创建大厅
                CreateLobbyOptions options = new CreateLobbyOptions { IsPrivate = false };
                currentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);
                Debug.Log("Created lobby: " + currentLobby.Name + " with code " + currentLobby.LobbyCode);

                // 4. 启动心跳和轮询定时器
                heartbeatTimer.Start();
                pollForUpdatesTimer.Start();

                // 5. 将 Relay 加入码写入大厅数据（仅成员可见）
                await LobbyService.Instance.UpdateLobbyAsync(currentLobby.Id, new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
                    {
                        {k_keyJoinCode, new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode)}
                    }
                });
        
                // 6. 配置网络传输并启动主机
                NetworkManager.Singleton.GetComponent<UnityTransport>()
                    .SetRelayServerData(allocation.ToRelayServerData(connectionType));
        
                NetworkManager.Singleton.StartHost();
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError("Failed to create lobby: " + e.Message);
            }
        }

        /// <summary>
        /// 快速加入大厅并启动客户端。
        /// </summary>
        public async Task QuickJoinLobby()
        {
            try
            {
                // 1. 快速加入一个空闲大厅
                currentLobby = await LobbyService.Instance.QuickJoinLobbyAsync();
                pollForUpdatesTimer.Start();
        
                // 2. 从大厅数据中获取 Relay 加入码
                string relayJoinCode = currentLobby.Data[k_keyJoinCode].Value;
                // 3. 使用加入码连接到 Relay 服务器
                JoinAllocation joinAllocation = await JoinRelay(relayJoinCode);
        
                // 4. 配置网络传输并启动客户端
                NetworkManager.Singleton.GetComponent<UnityTransport>()
                    .SetRelayServerData(joinAllocation.ToRelayServerData(connectionType));
        
                NetworkManager.Singleton.StartClient();
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError("Failed to quick join lobby: " + e.Message);
            }
        }

        // --- 辅助方法 ---

        private async Task<Allocation> AllocateRelay()
        {
            try
            {
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
                return allocation;
            }
            catch (RelayServiceException e)
            {
                Debug.LogError("Failed to allocate relay: " + e.Message);
                return default;
            }
        }

        private async Task<string> GetRelayJoinCode(Allocation allocation)
        {
            try
            {
                string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                return relayJoinCode;
            }
            catch (RelayServiceException e)
            {
                Debug.LogError("Failed to get relay join code: " + e.Message);
                return default;
            }
        }

        private async Task<JoinAllocation> JoinRelay(string relayJoinCode)
        {
            try
            {
                JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);
                return joinAllocation;
            }
            catch (RelayServiceException e)
            {
                Debug.LogError("Failed to join relay: " + e.Message);
                return default;
            }
        }

        /// <summary>
        /// 发送心跳包，保持大厅活跃。
        /// </summary>
        private async Task HandleHeartbeatAsync()
        {
            try
            {
                await LobbyService.Instance.SendHeartbeatPingAsync(currentLobby.Id);
                Debug.Log("Sent heartbeat ping to lobby: " + currentLobby.Name);
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError("Failed to heartbeat lobby: " + e.Message);
            }
        }
        
        /// <summary>
        /// 轮询大厅更新，获取最新数据。
        /// </summary>
        private async Task HandlePollForUpdatesAsync()
        {
            try
            {
                Lobby lobby = await LobbyService.Instance.GetLobbyAsync(currentLobby.Id);
                Debug.Log("Polled for updates on lobby: " + lobby.Name);
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError("Failed to poll for updates lobby: " + e.Message);
            }
        }
    }
}