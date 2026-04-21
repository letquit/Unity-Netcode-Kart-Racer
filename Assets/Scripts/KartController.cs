using System;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using Utilities;

namespace Kart
{
    /// <summary>
    /// 表示车辆轴的信息，包括左右轮、是否为驱动轴和转向轴等属性
    /// </summary>
    [System.Serializable]
    public class AxleInfo
    {
        public WheelCollider leftWheel;
        public WheelCollider rightWheel;
        public bool motor;
        public bool steering;
        public WheelFrictionCurve originalForwardFriction;
        public WheelFrictionCurve originalSidewaysFriction;
    }

    /// <summary>
    /// 网络输入数据包结构，用于序列化客户端输入信息
    /// </summary>
    public struct InputPayload : INetworkSerializable
    {
        public int tick;
        public DateTime timestamp;
        public ulong networkObjectId;
        public Vector3 inputVector;
        public Vector3 position;

        /// <summary>
        /// 序列化网络数据的方法
        /// </summary>
        /// <typeparam name="T">缓冲区序列化器类型</typeparam>
        /// <param name="serializer">用于序列化的缓冲区序列化器</param>
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref tick);
            serializer.SerializeValue(ref timestamp);
            serializer.SerializeValue(ref networkObjectId);
            serializer.SerializeValue(ref inputVector);
            serializer.SerializeValue(ref position);
        }
    }

    /// <summary>
    /// 网络状态数据包结构，用于序列化车辆物理状态信息
    /// </summary>
    public struct StatePayload : INetworkSerializable
    {
        public int tick;
        public ulong networkObjectId;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 velocity;
        public Vector3 angularVelocity;

        /// <summary>
        /// 序列化网络数据的方法
        /// </summary>
        /// <typeparam name="T">缓冲区序列化器类型</typeparam>
        /// <param name="serializer">用于序列化的缓冲区序列化器</param>
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref tick);
            serializer.SerializeValue(ref networkObjectId);
            serializer.SerializeValue(ref position);
            serializer.SerializeValue(ref rotation);
            serializer.SerializeValue(ref velocity);
            serializer.SerializeValue(ref angularVelocity);
        }
    }

    /// <summary>
    /// 卡丁车控制器类，处理卡丁车的物理模拟、输入控制和网络同步功能
    /// </summary>
    public class KartController : NetworkBehaviour
    {
        [Header("Axle Information")] 
        [SerializeField] private AxleInfo[] axleInfos;

        [Header("Motor Attributes")]
        [SerializeField] private float maxMotorTorque = 3000f;
        [SerializeField] private float maxSpeed;

        [Header("Steering Attributes")] 
        [SerializeField] private float maxSteeringAngle = 30f;
        [SerializeField] private AnimationCurve turnCurve;
        [SerializeField] private float turnStrength = 1500f;

        [Header("Braking and Drifting")] 
        [SerializeField] private float driftSteerMultiplier = 1.5f; // Change in steering during a drift
        [SerializeField] private float brakeTorque = 10000f;

        [Header("Physics")]
        [SerializeField] private Transform centerOfMass;
        [SerializeField] private float downForce = 100f;
        [SerializeField] private float gravity = Physics.gravity.y;
        [SerializeField] private float lateralGScale = 10f; // Scaling factor for lateral G forces;

        [Header("Banking")] 
        [SerializeField] private float maxBankAngle = 5f;
        [SerializeField] private float bankSpeed = 2f;

        [Header("Refs")] 
        [SerializeField] private InputReader playerInput;
        [SerializeField] private Circuit circuit;
        [SerializeField] private AIDriverData driverData;
        [SerializeField] private CinemachineVirtualCamera playerCamera;
        [SerializeField] private AudioListener playerAudioListener;

        private IDrive input;
        private Rigidbody rb;
        private ClientNetworkTransform clientNetworkTransform;

        private Vector3 kartVelocity;
        private float brakeVelocity;
        private float driftVelocity;

        private RaycastHit hit;

        private const float thresholdSpeed = 10f;
        private const float centerOfMassOffset = -0.5f;
        private Vector3 originalCenterOfMass;

        public bool IsGrounded = true;
        public Vector3 Velocity => kartVelocity;
        public float MaxSpeed => maxSpeed;

        // Netcode general
        private NetworkTimer networkTimer;
        private const float k_serverTickRate = 60f; // 60 FPS
        private const int k_bufferSize = 1024;

        // Netcode client specific
        private CircularBuffer<StatePayload> clientStateBuffer;
        private CircularBuffer<InputPayload> clientInputBuffer;
        private StatePayload lastServerState;
        private StatePayload lastProcessedState;

        // Netcode server specific
        private CircularBuffer<StatePayload> serverStateBuffer;
        private Queue<InputPayload> serverInputQueue;

        [Header("Netcode")]
        [SerializeField] private float reconciliationCooldownTime = 1f;
        [SerializeField] private float reconciliationThreshold = 10f;
        [SerializeField] private float extrapolationLimit = 0.5f;   // 500 milliseconds
        [SerializeField] private float extrapolationMultiplier = 1.2f;
        [SerializeField] private GameObject serverCube;
        [SerializeField] private GameObject clientCube;

        private StatePayload extrapolationState;
        private CountdownTimer extrapolationTimer;

        private CountdownTimer reconciliationTimer;

        [Header("Netcode Debug")]
        [SerializeField] private TextMeshPro networkText;
        [SerializeField] private TextMeshPro playerText;
        [SerializeField] private TextMeshPro serverRpcText;
        [SerializeField] private TextMeshPro clientRpcText;

        /// <summary>
        /// 初始化卡丁车控制器，在Awake阶段设置输入、物理属性和网络相关组件
        /// </summary>
        private void Awake()
        {
            if (playerInput is IDrive driveInput)
            {
                input = driveInput;
            }

            rb = GetComponent<Rigidbody>();
            clientNetworkTransform = GetComponent<ClientNetworkTransform>();
            input.Enable();

            rb.centerOfMass = centerOfMass.localPosition;
            originalCenterOfMass = centerOfMass.localPosition;

            foreach (AxleInfo axleInfo in axleInfos)
            {
                axleInfo.originalForwardFriction = axleInfo.leftWheel.forwardFriction;
                axleInfo.originalSidewaysFriction = axleInfo.leftWheel.sidewaysFriction;
            }

            networkTimer = new NetworkTimer(k_serverTickRate);
            clientStateBuffer = new CircularBuffer<StatePayload>(k_bufferSize);
            clientInputBuffer = new CircularBuffer<InputPayload>(k_bufferSize);

            serverStateBuffer = new CircularBuffer<StatePayload>(k_bufferSize);
            serverInputQueue = new Queue<InputPayload>();
            
            reconciliationTimer = new CountdownTimer(reconciliationCooldownTime);
            extrapolationTimer = new CountdownTimer(extrapolationLimit);

            reconciliationTimer.OnTimerStart += () =>
            {
                extrapolationTimer.Stop();
            };
            
            extrapolationTimer.OnTimerStart += () =>
            {
                reconciliationTimer.Stop();
                clientNetworkTransform.authorityMode = AuthorityMode.Server;
                clientNetworkTransform.SyncPositionX = false;
                clientNetworkTransform.SyncPositionY = false;
                clientNetworkTransform.SyncPositionZ = false;
            };
            extrapolationTimer.OnTimerStop += () =>
            {
                extrapolationState = default;
                clientNetworkTransform.authorityMode = AuthorityMode.Client;
                clientNetworkTransform.SyncPositionX = true;
                clientNetworkTransform.SyncPositionY = true;
                clientNetworkTransform.SyncPositionZ = true;
            };
        }

        /// <summary>
        /// 设置卡丁车的输入接口
        /// </summary>
        /// <param name="input">驱动输入接口</param>
        public void SetInput(IDrive input)
        {
            this.input = input;
        }

        /// <summary>
        /// 当网络对象生成时调用，设置相机优先级和音频监听器
        /// </summary>
        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                playerAudioListener.enabled = false;
                playerCamera.Priority = 0;
                return;
            }

            playerCamera.Priority = 100;
            playerAudioListener.enabled = true;
            
            networkText.SetText($"Player {NetworkManager.LocalClientId} Host: {NetworkManager.IsHost} Server: {IsServer} Client: {IsClient}");
            if (!IsServer) serverRpcText.SetText("Not Server");
            if (!IsClient) clientRpcText.SetText("Not Client");
        }

        private void Update()
        {
            networkTimer.Update(Time.deltaTime);
            reconciliationTimer.Tick(Time.deltaTime);
            extrapolationTimer.Tick(Time.deltaTime);
            
            playerText.SetText($"Owner: {IsOwner} NetworkObjectId: {NetworkObjectId} Velocity: {kartVelocity.magnitude:F1}");
            
            if (Input.GetKeyDown(KeyCode.Q))
            {
                transform.position += transform.forward * 20f;
            }
            
            // Run on Update or FixedUpdate, or both - depends on the game, consider exposing an option to the editor
            Extrapolate();
        }

        private void FixedUpdate()
        {
            while (networkTimer.ShouldTick())
            {
                HandleClientTick();
                HandleServerTick();
            }
            Extrapolate();
        }

        /// <summary>
        /// 处理服务器端的网络逻辑，处理客户端输入并计算状态
        /// </summary>
        private void HandleServerTick()
        {
            if (!IsServer) return;
            
            var bufferIndex = -1;
            InputPayload inputPayload = default;
            while (serverInputQueue.Count > 0)
            {
                inputPayload = serverInputQueue.Dequeue();

                bufferIndex = inputPayload.tick % k_bufferSize;

                StatePayload statePayload = ProcessMovement(inputPayload);
                serverStateBuffer.Add(statePayload, bufferIndex);
            }

            if (bufferIndex == -1) return;
            SendToClientRpc(serverStateBuffer.Get(bufferIndex));
            HandleExtrapolation(serverStateBuffer.Get(bufferIndex), CalculateLatencyInMillis(inputPayload));
        }

        /// <summary>
        /// 根据外推状态更新物体位置
        /// </summary>
        private void Extrapolate()
        {
            if (IsServer && extrapolationTimer.IsRunning)
            {
                transform.position += extrapolationState.position.With(y: 0);
            }
        }
        
        /// <summary>
        /// 处理状态外推逻辑，基于延迟预测未来状态
        /// </summary>
        /// <param name="latest">最新的状态数据</param>
        /// <param name="latency">网络延迟时间</param>
        private void HandleExtrapolation(StatePayload latest, float latency)
        {
            if (ShouldExtrapolate(latency))
            {
                float axisLength = latency * latest.angularVelocity.magnitude * Mathf.Rad2Deg;
                Quaternion angularRotation = Quaternion.AngleAxis(axisLength, latest.angularVelocity);
                if (extrapolationState.position != default)
                {
                    latest = extrapolationState;
                }

                var posAdjustment = latest.velocity * (1 + latency * extrapolationMultiplier);
                extrapolationState.position = posAdjustment;
                extrapolationState.rotation = angularRotation * latest.rotation;
                extrapolationState.velocity = latest.velocity;
                extrapolationState.angularVelocity = latest.angularVelocity;
                extrapolationTimer.Start();
            }
            else
            {
                extrapolationTimer.Stop();
                // Reconcile if desired
            }
        }

        /// <summary>
        /// 判断是否应该进行外推计算
        /// </summary>
        /// <param name="latency">网络延迟时间</param>
        /// <returns>如果延迟在限制范围内且大于固定时间步长则返回true</returns>
        private bool ShouldExtrapolate(float latency) => latency < extrapolationLimit && latency > Time.fixedDeltaTime;

        /// <summary>
        /// 处理客户端的网络逻辑，收集输入并发送到服务器
        /// </summary>
        private void HandleClientTick()
        {
            if (!IsClient || !IsOwner) return;

            var currentTick = networkTimer.CurrentTick;
            var bufferIndex = currentTick % k_bufferSize;

            InputPayload inputPayload = new InputPayload()
            {
                tick = currentTick,
                timestamp = DateTime.Now,
                networkObjectId = NetworkObjectId,
                inputVector = input.Move,
                position = transform.position
            };

            clientInputBuffer.Add(inputPayload, bufferIndex);
            SendToServerRpc(inputPayload);

            StatePayload statePayload = ProcessMovement(inputPayload);
            clientStateBuffer.Add(statePayload, bufferIndex);

            HandleServerReconciliation();
        }

        /// <summary>
        /// 计算输入数据的网络延迟
        /// </summary>
        /// <param name="inputPayload">输入数据包</param>
        /// <returns>以秒为单位的延迟时间</returns>
        private static float CalculateLatencyInMillis(InputPayload inputPayload)
        {
            return (DateTime.Now - inputPayload.timestamp).Milliseconds / 1000f;
        }

        /// <summary>
        /// 接收服务器发送的状态数据
        /// </summary>
        /// <param name="statePayload">服务器状态数据包</param>
        [ClientRpc]
        private void SendToClientRpc(StatePayload statePayload)
        {
            clientRpcText.SetText($"Received state from server Tick {statePayload.tick} Server POS: {statePayload.position}");
            serverCube.transform.position = statePayload.position.With(y: 4);
            
            if (!IsOwner) return;
            lastServerState = statePayload;
        }

        /// <summary>
        /// 向服务器发送输入数据
        /// </summary>
        /// <param name="input">客户端输入数据包</param>
        [ServerRpc]
        private void SendToServerRpc(InputPayload input)
        {
            serverRpcText.SetText($"Received input from client Tick: {input.tick} Client POS: {input.position}");
            clientCube.transform.position = input.position.With(y: 4);
            
            serverInputQueue.Enqueue(input);
        }

        /// <summary>
        /// 判断是否需要进行服务器状态协调
        /// </summary>
        /// <returns>如果满足协调条件则返回true</returns>
        private bool ShouldReconcile()
        {
            bool isNewServerState = !lastServerState.Equals(default);
            bool isLastStateUndefinedOrDifferent = lastProcessedState.Equals(default)
                                                   || !lastProcessedState.Equals(lastServerState);

            return isNewServerState && isLastStateUndefinedOrDifferent && !reconciliationTimer.IsRunning && !extrapolationTimer.IsRunning;
        }

        /// <summary>
        /// 处理服务器状态协调逻辑，校正客户端与服务器之间的状态差异
        /// </summary>
        private void HandleServerReconciliation()
        {
            if (!ShouldReconcile()) return;

            float positionError;
            int bufferIndex;
            StatePayload rewindState = default;

            bufferIndex = lastServerState.tick % k_bufferSize;
            if (bufferIndex - 1 < 0) return; // Not enough information to reconcile

            rewindState =
                IsHost
                    ? serverStateBuffer.Get(bufferIndex - 1)
                    : lastServerState; // Host RPCs execute immediately, so we can use the last server state
            positionError = Vector3.Distance(rewindState.position, clientStateBuffer.Get(bufferIndex).position);

            if (positionError > reconciliationThreshold)
            {
                ReconcileState(rewindState);
                reconciliationTimer.Start();
            }

            lastProcessedState = lastServerState;
        }

        /// <summary>
        /// 执行状态协调，将物体状态调整为服务器状态
        /// </summary>
        /// <param name="rewindState">需要回滚到的状态</param>
        private void ReconcileState(StatePayload rewindState)
        {
            transform.position = rewindState.position;
            transform.rotation = rewindState.rotation;
            rb.linearVelocity = rewindState.velocity;
            rb.angularVelocity = rewindState.angularVelocity;

            if (!rewindState.Equals(lastServerState)) return;

            clientStateBuffer.Add(rewindState, rewindState.tick);

            // Replay all inputs from the rewind state to the current state
            int tickToReplay = lastServerState.tick;

            while (tickToReplay < networkTimer.CurrentTick)
            {
                int bufferIndex = tickToReplay % k_bufferSize;
                StatePayload statePayload = ProcessMovement(clientInputBuffer.Get(bufferIndex));
                clientStateBuffer.Add(statePayload, bufferIndex);
                tickToReplay++;
            }
        }

        /// <summary>
        /// 处理移动逻辑并返回当前状态数据
        /// </summary>
        /// <param name="input">输入数据包</param>
        /// <returns>当前状态数据包</returns>
        private StatePayload ProcessMovement(InputPayload input)
        {
            Move(input.inputVector);

            return new StatePayload()
            {
                tick = input.tick,
                networkObjectId = input.networkObjectId,
                position = transform.position,
                rotation = transform.rotation,
                velocity = rb.linearVelocity,
                angularVelocity = rb.angularVelocity
            };
        }

        /// <summary>
        /// 处理卡丁车的基本移动逻辑
        /// </summary>
        /// <param name="inputVector">输入向量</param>
        private void Move(Vector2 inputVector)
        {
            float verticalInput = AdjustInput(input.Move.y);
            float horizontalInput = AdjustInput(input.Move.x);

            float motor = maxMotorTorque * verticalInput;
            float steering = maxSteeringAngle * horizontalInput;

            UpdateAxles(motor, steering);
            UpdateBanking(horizontalInput);

            kartVelocity = transform.InverseTransformDirection(rb.linearVelocity);

            if (IsGrounded)
            {
                HandleGroundedMovement(verticalInput, horizontalInput);
            }
            else
            {
                HandleAirborneMovement(verticalInput, horizontalInput);
            }
        }

        /// <summary>
        /// 处理卡丁车在地面上的移动逻辑
        /// </summary>
        /// <param name="verticalInput">垂直输入值</param>
        /// <param name="horizontalInput">水平输入值</param>
        private void HandleGroundedMovement(float verticalInput, float horizontalInput)
        {
            if (!IsOwner) return;
            // if (rb.isKinematic) return;
            
            // Turn logic
            if (Mathf.Abs(verticalInput) > 0.1f || Mathf.Abs(kartVelocity.z) > 1)
            {
                float turnMultiplier = Mathf.Clamp01(turnCurve.Evaluate(kartVelocity.magnitude / maxSpeed));
                rb.AddTorque(Vector3.up *
                             (horizontalInput * Mathf.Sign(kartVelocity.z) * turnStrength * 100f * turnMultiplier));
            }

            // Acceleration Logic
            if (!input.IsBraking)
            {
                float targetSpeed = verticalInput * maxSpeed;
                Vector3 forwardWithoutY = transform.forward.With(y: 0).normalized;
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, forwardWithoutY * targetSpeed, networkTimer.MinTimeBetweenTicks);
            }

            // Downforce - always push the cart down, using lateral Gs to scale the force if the Kart is moving sideways fast
            float speedFactor = Mathf.Clamp01(rb.linearVelocity.magnitude / maxSpeed);
            float lateralG = Mathf.Abs(Vector3.Dot(rb.linearVelocity, transform.right));
            float downForceFactor = Mathf.Max(speedFactor, lateralG / lateralGScale);
            rb.AddForce(-transform.up * (downForce * rb.mass * downForceFactor));

            // Shift Center of Mass
            float speed = rb.linearVelocity.magnitude;
            Vector3 centerOfMassAdjustment = (speed > thresholdSpeed)
                ? new Vector3(0f, 0f,
                    Mathf.Abs(verticalInput) > 0.1f ? Mathf.Sign(verticalInput) * centerOfMassOffset : 0f)
                : Vector3.zero;
            rb.centerOfMass = originalCenterOfMass + centerOfMassAdjustment;
        }

        /// <summary>
        /// 处理卡丁车在空中的移动逻辑
        /// </summary>
        /// <param name="verticalInput">垂直输入值</param>
        /// <param name="horizontalInput">水平输入值</param>
        private void HandleAirborneMovement(float verticalInput, float horizontalInput)
        {
            if (!IsOwner) return;
            
            // Apply gravity to the Kart while its airborne
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, rb.linearVelocity + Vector3.down * gravity,
                Time.deltaTime * gravity);
        }

        /// <summary>
        /// 更新卡丁车的倾斜角度
        /// </summary>
        /// <param name="horizontalInput">水平输入值</param>
        private void UpdateBanking(float horizontalInput)
        {
            // Bank the Kart in the opposite direction of the turn
            float targetBankAngle = horizontalInput * -maxBankAngle;
            Vector3 currentEuler = transform.localEulerAngles;
            currentEuler.z = Mathf.LerpAngle(currentEuler.z, targetBankAngle, Time.deltaTime * bankSpeed);
            transform.localEulerAngles = currentEuler;
        }

        /// <summary>
        /// 更新所有轴的控制参数
        /// </summary>
        /// <param name="motor">电机扭矩值</param>
        /// <param name="steering">转向角度值</param>
        private void UpdateAxles(float motor, float steering)
        {
            foreach (AxleInfo axleInfo in axleInfos)
            {
                HandleSteering(axleInfo, steering);
                HandleMotor(axleInfo, motor);
                HandleBrakesAndDrift(axleInfo);
                UpdateWheelVisuals(axleInfo.leftWheel);
                UpdateWheelVisuals(axleInfo.rightWheel);
            }
        }

        /// <summary>
        /// 更新轮子的视觉表示
        /// </summary>
        /// <param name="collider">轮子碰撞体</param>
        private void UpdateWheelVisuals(WheelCollider collider)
        {
            if (collider.transform.childCount == 0) return;

            Transform visualWheel = collider.transform.GetChild(0);

            Vector3 position;
            Quaternion rotation;
            collider.GetWorldPose(out position, out rotation);

            visualWheel.transform.position = position;
            visualWheel.transform.rotation = rotation;
        }

        /// <summary>
        /// 处理轴的转向逻辑
        /// </summary>
        /// <param name="axleInfo">轴信息</param>
        /// <param name="steering">转向角度值</param>
        private void HandleSteering(AxleInfo axleInfo, float steering)
        {
            if (axleInfo.steering)
            {
                float steeringMultiplier = input.IsBraking ? driftSteerMultiplier : 1f;
                axleInfo.leftWheel.steerAngle = steering * steeringMultiplier;
                axleInfo.rightWheel.steerAngle = steering * steeringMultiplier;
            }
        }

        /// <summary>
        /// 处理轴的电机逻辑
        /// </summary>
        /// <param name="axleInfo">轴信息</param>
        /// <param name="motor">电机扭矩值</param>
        private void HandleMotor(AxleInfo axleInfo, float motor)
        {
            if (axleInfo.motor)
            {
                axleInfo.leftWheel.motorTorque = motor;
                axleInfo.rightWheel.motorTorque = motor;
            }
        }

        /// <summary>
        /// 处理刹车和漂移逻辑
        /// </summary>
        /// <param name="axleInfo">轴信息</param>
        private void HandleBrakesAndDrift(AxleInfo axleInfo)
        {
            if (!IsOwner) return; 
            
            if (axleInfo.motor)
            {
                if (input.IsBraking)
                {
                    rb.constraints = RigidbodyConstraints.FreezeRotationX;

                    float newZ = Mathf.SmoothDamp(rb.linearVelocity.z, 0, ref brakeVelocity, 1f);
                    rb.linearVelocity = rb.linearVelocity.With(z: newZ);

                    axleInfo.leftWheel.brakeTorque = brakeTorque;
                    axleInfo.rightWheel.brakeTorque = brakeTorque;
                    ApplyDriftFriction(axleInfo.leftWheel);
                    ApplyDriftFriction(axleInfo.rightWheel);
                }
                else
                {
                    rb.constraints = RigidbodyConstraints.None;

                    axleInfo.leftWheel.brakeTorque = 0;
                    axleInfo.rightWheel.brakeTorque = 0;
                    ResetDriftFriction(axleInfo.leftWheel);
                    ResetDriftFriction(axleInfo.rightWheel);
                }
            }
        }

        /// <summary>
        /// 重置轮子的漂移摩擦力
        /// </summary>
        /// <param name="wheel">轮子碰撞体</param>
        private void ResetDriftFriction(WheelCollider wheel)
        {
            AxleInfo axleInfo = axleInfos.FirstOrDefault(axle => axle.leftWheel == wheel || axle.rightWheel == wheel);
            if (axleInfo == null) return;

            wheel.forwardFriction = axleInfo.originalForwardFriction;
            wheel.sidewaysFriction = axleInfo.originalSidewaysFriction;
        }

        /// <summary>
        /// 应用漂移摩擦力到轮子
        /// </summary>
        /// <param name="wheel">轮子碰撞体</param>
        private void ApplyDriftFriction(WheelCollider wheel)
        {
            if (wheel.GetGroundHit(out var hit))
            {
                wheel.forwardFriction = UpdateFriction(wheel.forwardFriction);
                wheel.sidewaysFriction = UpdateFriction(wheel.sidewaysFriction);
                IsGrounded = true;
            }
        }

        /// <summary>
        /// 更新轮子的摩擦力曲线
        /// </summary>
        /// <param name="friction">原始摩擦力曲线</param>
        /// <returns>更新后的摩擦力曲线</returns>
        private WheelFrictionCurve UpdateFriction(WheelFrictionCurve friction)
        {
            friction.stiffness = input.IsBraking
                ? Mathf.SmoothDamp(friction.stiffness, .5f, ref driftVelocity, Time.deltaTime * 2f)
                : 1f;
            return friction;
        }

        /// <summary>
        /// 调整输入值，将较大值映射为1或-1
        /// </summary>
        /// <param name="input">原始输入值</param>
        /// <returns>调整后的输入值</returns>
        private float AdjustInput(float input)
        {
            return input switch
            {
                >= .7f => 1f,
                <= -.7f => -1f,
                _ => input
            };
        }
    }
}