using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace WireLink
{
    internal class UdpSocket
    {
        public UdpSocket(PacketHandler packetHelper)
        {
            _socket = new Socket(SocketType.Dgram, ProtocolType.Udp);
            mainCancellationToken = mainCancellationTokenSource.Token;
            this.packetHelper = packetHelper;
        }

        public void ShutDown()
        {
            mainCancellationTokenSource.Cancel();

            mainCancellationTokenSource.Dispose();
            receiveMethodCancellationTokenSource.Dispose();
            receiveMethodLinkedCancellationTokenSource.Dispose();
        }

        Socket _socket;
        PacketHandler packetHelper;

        CancellationTokenSource mainCancellationTokenSource = new CancellationTokenSource();
        CancellationToken mainCancellationToken;

        static Dictionary<IPAddress, int> mtuCache = new Dictionary<IPAddress, int>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void checkSocketData(int dataLength, int pathMTU = 1200)
        {
            if (dataLength + SocketHelper.MessagePaddingLength > pathMTU - 24) // 8 bytes for the UDP header and 16 for other stuff like VPN or alike
            {
                throw new InvalidOperationException($"[Wirelink lib] {(int)SocketError.MessageSize} you cannot send more than {508 - SocketHelper.MessagePaddingLength} bytes in a single packet\n" +
            "contact the developer if this message is a common ocurrance");
            }
        }
        private int getPathMTU(IPAddress address)
        {
            if (mtuCache.TryGetValue(address, out int mtu))
            {
                return mtu;
            }
            else
            {
                return findPathMTU(address);
            }
        }

        bool isBound = false;
        public async Task Send(byte[] data)
        {
            if (!isBound) { throw new InvalidOperationException("[Wirelink lib] cannot call send without an address if the socket has not been bound"); }
            checkSocketData(data.Length);

            data = SocketHelper.PrepareMessage(data);

            await SendRaw(data);
        }

#if NETSTANDARD2_1
        public async Task Send(byte[] data, EndPoint address)
#else
        public async Task Send(byte[] data, SocketAddress address)
#endif
        {
            if (isBound) { throw new InvalidOperationException("[Wirelink lib] cannot call send with an address if socket is already bound"); }
            checkSocketData(data.Length);

            data = SocketHelper.PrepareMessage(data);

            await SendRaw(data, address);
        }

#if NETSTANDARD2_1
        public async Task Send(byte[] data, EndPoint[] addresses)
#else
        public async Task Send(byte[] data, SocketAddress[] addresses)
#endif
        {
            if (isBound) { throw new InvalidOperationException("[Wirelink lib] cannot call send with an address if socket is already bound"); }
            checkSocketData(data.Length);

            data = SocketHelper.PrepareMessage(data);

            Task[] tasks = addresses.Select(address => SendRaw(data, address)).ToArray();
            await Task.WhenAll(tasks);
        }

#if NETSTANDARD2_1
        private async Task SendRaw(ArraySegment<byte> data, EndPoint address)
        {
            SocketAsyncEventArgs args = new SocketAsyncEventArgs();
            args.SetBuffer(data.ToArray());
            args.RemoteEndPoint = address;
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            args.Completed += (s, e) => { tcs.SetResult(true); };

            if (!_socket.SendToAsync(args))
            {
                tcs.SetResult(true);
            }

            await tcs.Task;
        }
#else
        private async Task SendRaw(ArraySegment<byte> data, SocketAddress address)
        {
            await _socket.SendToAsync(data, SocketFlags.None, address.Serialize(), mainCancellationToken);
        }
#endif
        private async Task SendRaw(ReadOnlyMemory<byte> data)
        {
#if NETSTANDARD2_1
            SocketAsyncEventArgs args = new SocketAsyncEventArgs();
            args.SetBuffer(data.ToArray());
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            args.Completed += (s, e) => { tcs.SetResult(true); };

            if (!_socket.SendAsync(args))
            {
                tcs.SetResult(true);
            }

            await tcs.Task;
#else
            await _socket.SendAsync(data, SocketFlags.None, mainCancellationToken);
#endif
        }

        public void StartRecieving(int port)
        {
            isReceiving = true;
            receiveMethodLinkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(receiveMethodCancellationTokenSource.Token, mainCancellationTokenSource.Token);
            receiveMethodCancellationToken = receiveMethodLinkedCancellationTokenSource.Token;

            Task.Run(() => RecieveMethod(port));
        }
        public void StopRecieving()
        {
            isReceiving = false;
            receiveMethodCancellationTokenSource.Cancel();
        }
        public delegate void DataReceivedEventHandler(ReadOnlyMemory<byte> data);
        public event DataReceivedEventHandler? RecieveCallback;
        private bool isReceiving = false;
        private CancellationTokenSource receiveMethodCancellationTokenSource = new CancellationTokenSource();
        private CancellationTokenSource receiveMethodLinkedCancellationTokenSource = new CancellationTokenSource();
        private CancellationToken receiveMethodCancellationToken;
        private async Task RecieveMethod(int port)
        {
            while (isReceiving)
            {
                byte[] buffer = new byte[1024];
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, port);
#if NETSTANDARD2_1
                SocketAsyncEventArgs args = new SocketAsyncEventArgs();
                args.SetBuffer(buffer);
                args.RemoteEndPoint = endPoint;
                TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
                
                args.Completed += (s, e) => { tcs.SetResult(true); };
                
                if (!_socket.ReceiveFromAsync(args))
                {
                    tcs.SetResult(true);
                }
                
                await tcs.Task;
                
                if (receiveMethodCancellationToken.IsCancellationRequested)
                    break;
                    
                int recievedBytes = args.BytesTransferred;
                EndPoint remoteEndPoint = args.RemoteEndPoint;
                
                ReadOnlyMemory<byte> trimmedBuffer = new ReadOnlyMemory<byte>(buffer, 1, recievedBytes - 1);
                await HandleReceivedData((IPEndPoint)remoteEndPoint, (byteCodes)buffer[0], trimmedBuffer);
#else
                SocketReceiveFromResult recievedResult = await _socket.ReceiveFromAsync(buffer, SocketFlags.None, endPoint, receiveMethodCancellationToken);
                ReadOnlyMemory<byte> trimmedBuffer = new ReadOnlyMemory<byte>(buffer, 1, recievedResult.ReceivedBytes - 1);
                await HandleReceivedData((IPEndPoint)recievedResult.RemoteEndPoint, (byteCodes)buffer[0], trimmedBuffer);
#endif
            }
        }

        private async Task HandleReceivedData(IPEndPoint sender, byteCodes byteCode, ReadOnlyMemory<byte> data)
        {
            switch (byteCode)
            {
                case byteCodes.simpleMessageHeader:
                    await ExecuteCallbacks(sender, data);
                    break;
                case byteCodes.terminateConnection:
                    packetHelper.TerminateClient(sender);
                    break;
                case byteCodes.heartBeat:
                    break;
            }
        }

        private async Task ExecuteCallbacks(EndPoint sender, ReadOnlyMemory<byte> data)
        {
            Action<ReadOnlyMemory<byte>>[]? callbacks = RecieveCallback?.GetInvocationList().OfType<Action<ReadOnlyMemory<byte>>>().ToArray();

            if (callbacks != null)
            {
                Task[] tasks = callbacks.Select(callback => Task.Run(() => callback(data))).ToArray();
                await Task.WhenAll(tasks);
            }
        }

        private void anounceInvalidData(EndPoint sender, int messageID)
        {

        }

        /// <summary>
        /// connect to a remote socket
        /// </summary>
        /// <param name="address">the adress of the remote host</param>
        public void connect(IPAddress address)
        {

        }

        public static int findPathMTU(IPAddress adress, int timeout = 1000)
        {
            const int icmpEchoHeaderSize = 8;
            const int mtuMinSize = 60;
            const int mtuMaxSize = 65500;
            int mtuLowerBound = mtuMinSize, mtuUpperBound = mtuMaxSize;
            int? bestMtu = default(int?);
            using var ping = new System.Net.NetworkInformation.Ping();
            // the first argument is the max amount of hops allowed before dropping the package, the second is whether to use the DF (Don't Fragment) flag
            var options = new System.Net.NetworkInformation.PingOptions(240, true);
            for(int currentMtu; mtuLowerBound <= mtuUpperBound; )
            {
                currentMtu = (mtuLowerBound + mtuUpperBound) / 2;
                byte[] buffer = new byte[currentMtu];
                System.Net.NetworkInformation.PingReply reply = ping.Send(adress, timeout, buffer, options);
                if(reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                {
                    bestMtu = currentMtu + icmpEchoHeaderSize;
                    mtuLowerBound = currentMtu + 1;
                }
                else
                    mtuUpperBound = currentMtu - 1;
            }
            mtuCache[adress] = bestMtu ?? 1200; // default to 1200 if no MTU was found
            Logging.WriteLine($"[Wirelink lib] MTU for {adress} is {bestMtu ?? 1200} bytes");
            return bestMtu ?? 1200; // default to 1200 if no MTU was found
        }
    }
}