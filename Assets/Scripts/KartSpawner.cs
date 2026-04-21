using System;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using Utilities;
using Random = UnityEngine.Random;

namespace Kart
{
    /// <summary>
    /// 赛车生成器。
    /// 负责在赛道上生成玩家赛车和 AI 对手，并处理发车位的随机化。
    /// </summary>
    public class KartSpawner : MonoBehaviour
    {
        [SerializeField] private Circuit circuit; // 赛道信息（包含出生点）
        [SerializeField] private AIDriverData aiDriverData; // AI 驾驶配置
        [SerializeField] private GameObject[] aiKartPrefabs; // AI 赛车预制体池

        [SerializeField] private GameObject playerKartPrefab; // 玩家赛车预制体
        [SerializeField] private CinemachineVirtualCamera playerCamera; // 玩家跟随相机

        [Header("Grid Randomization")]
        [SerializeField] private bool randomizeGrid = true; // 是否打乱发车位顺序
        [SerializeField] private bool randomizePlayerPosition = true; // 是否随机玩家起跑位置
        [SerializeField] private int seed = -1; // 随机种子（>=0 时使用固定种子，用于调试）

        private void Start()
        {
            // 1. 准备发车位列表
            var slots = new List<Transform>(circuit.spawnPoints);

            // 2. 处理随机种子
            if (seed >= 0) Random.InitState(seed);

            // 3. 打乱发车位顺序（如果是随机发车）
            if (randomizeGrid)
                Shuffle(slots);

            // 4. 确定玩家位置
            int playerSlotIndex = 0;
            if (randomizePlayerPosition)
                playerSlotIndex = Random.Range(0, slots.Count);

            // 5. 生成玩家赛车
            Transform playerSpawn = slots[playerSlotIndex];
            var playerKart = Instantiate(playerKartPrefab, playerSpawn.position, playerSpawn.rotation);

            // 6. 设置摄像机跟随
            if (playerCamera != null)
            {
                playerCamera.Follow = playerKart.transform;
                playerCamera.LookAt = playerKart.transform;
            }

            // 7. 移除玩家占用的发车位，剩下的留给 AI
            slots.RemoveAt(playerSlotIndex);

            // 8. 生成 AI 赛车
            for (int i = 0; i < slots.Count; i++)
            {
                // 随机选择一个 AI 赛车预制体
                var aiPrefab = aiKartPrefabs[Random.Range(0, aiKartPrefabs.Length)];

                // 使用构建者模式创建并配置 AI 赛车
                new AIKartBuilder(aiPrefab)
                    .WithDriverData(aiDriverData)
                    .WithCircuit(circuit)
                    .WithSpawnPoint(slots[i])
                    .Build();
            }
        }

        /// <summary>
        /// 费雪 - 耶茨洗牌算法。
        /// 用于随机打乱列表顺序。
        /// </summary>
        private static void Shuffle(List<Transform> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                // 使用元组语法交换元素
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>
        /// AI 赛车构建者。
        /// 使用构建者模式封装复杂的 AI 赛车实例化和配置过程。
        /// </summary>
        private class AIKartBuilder
        {
            private readonly GameObject prefab; // 预制体
            private AIDriverData data; // 依赖：AI 数据
            private Circuit circuit; // 依赖：赛道
            private Transform spawnPoint; // 依赖：出生点

            public AIKartBuilder(GameObject prefab)
            {
                this.prefab = prefab;
            }

            // 链式调用：设置 AI 数据
            public AIKartBuilder WithDriverData(AIDriverData data)
            {
                this.data = data;
                return this;
            }

            // 链式调用：设置赛道
            public AIKartBuilder WithCircuit(Circuit circuit)
            {
                this.circuit = circuit;
                return this;
            }

            // 链式调用：设置出生点
            public AIKartBuilder WithSpawnPoint(Transform spawnPoint)
            {
                this.spawnPoint = spawnPoint;
                return this;
            }

            /// <summary>
            /// 执行构建。
            /// 实例化赛车 -> 获取/添加组件 -> 注入依赖 -> 绑定控制器
            /// </summary>
            public GameObject Build()
            {
                // 实例化
                var instance = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
                
                // 获取或添加 AI 输入组件
                var aiInput = instance.GetOrAdd<AIInput>();
                
                // 注入依赖
                aiInput.AddCircuit(circuit);
                aiInput.AddDriverData(data);

                // 将 AI 输入绑定到赛车控制器
                instance.GetComponent<KartController>().SetInput(aiInput);

                return instance;
            }
        }
    }
}