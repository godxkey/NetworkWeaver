using System;
using System.Collections.Generic;
using JackFrame;
using JackBuffer;

namespace JackFrame.Network {

    public class TcpServer {

        TcpLowLevelServer server;
        int maxMessageSize;

        SortedDictionary<ushort, Action<int, ArraySegment<byte>>> dic;

        public event Action<int> OnConnectedHandle;
        public event Action<int> OnDisconnectedHandle;

        // 1. 构造
        public TcpServer(int maxMessageSize = 4096) {
            this.maxMessageSize = maxMessageSize;
            server = new TcpLowLevelServer(maxMessageSize);
            dic = new SortedDictionary<ushort, Action<int, ArraySegment<byte>>>();

            server.OnConnectedHandle += OnConnected;
            server.OnDisconnectedHandle += OnDisconnected;
            server.OnDataHandle += OnRecvData;
        }

        public void Tick() {
            server.Tick();
        }

        public void StartListen(int port) {
            server.StartListen(port);
        }

        public void RestartListen() {
            server.RestartListen();
        }

        public void StopListen() {
            server.StopListen();
        }

        public void Send<T>(byte serviceId, byte messageId, int connId, T msg) where T : IJackMessage<T> {
            byte[] data = msg.ToBytes();
            if (data.Length >= maxMessageSize - 2) {
                throw new Exception("Message is too long");
            }
            byte[] dst = new byte[data.Length + 2];
            int offset = 0;
            dst[offset] = serviceId;
            offset += 1;
            dst[offset] = messageId;
            offset += 1;
            Buffer.BlockCopy(data, 0, dst, offset, data.Length);
            server.Send(connId, dst);
        }

        public void On<T>(byte serviceId, byte messageId, Func<T> generateHandle, Action<int, T> handle) where T : IJackMessage<T> {

            if (generateHandle == null) {
                throw new Exception("未注册: " + nameof(generateHandle));
            }

            ushort key = (ushort)serviceId;
            key |= (ushort)(messageId << 8);
            if (!dic.ContainsKey(key)) {
                dic.Add(key, (connId, byteData) => {
                    T msg = generateHandle.Invoke();
                    int offset = 2;
                    msg.FromBytes(byteData.Array, ref offset);
                    handle.Invoke(connId, msg);
                });
            }

        }

        void OnConnected(int connId) {
            if (OnConnectedHandle != null) {
                OnConnectedHandle.Invoke(connId);
            }
        }

        void OnDisconnected(int connId) {
            if (OnDisconnectedHandle != null) {
                OnDisconnectedHandle.Invoke(connId);
            }
        }

        void OnRecvData(int connId, ArraySegment<byte> data) {
            var arr = data.Array;
            if (arr.Length < 2) {
                return;
            }

            byte serviceId = arr[0];
            byte messageId = arr[1];
            ushort key = (ushort)serviceId;
            key |= (ushort)(messageId << 8);
            dic.TryGetValue(key, out var handle);
            if (handle != null) {
                handle.Invoke(connId, data);
            } else {
                System.Console.WriteLine($"未注册 serviceId:{serviceId}, messageId:{messageId}");
            }

        }

    }

}