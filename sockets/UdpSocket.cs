using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace WireLink
{
    internal class UdpSocket
    {
        public UdpSocket(PacketHelper packetHelper)
        {
            _socket = new Socket(SocketType.Dgram, ProtocolType.Udp);
            mainCancellationToken = mainCancellationTokenSource.Token;
            this.packetHelper = packetHelper;
        }

        public async void ShutDown()
        {
            await mainCancellationTokenSource.CancelAsync();

            mainCancellationTokenSource.Dispose();
            receiveMethodCancellationTokenSource.Dispose();
            receiveMethodLinkedCancellationTokenSource.Dispose();
        }

        Socket _socket;
        PacketHelper packetHelper;

        CancellationTokenSource mainCancellationTokenSource = new CancellationTokenSource();
        CancellationToken mainCancellationToken;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void checkSocketMessage(int dataLength)
        {
            if(dataLength + SocketHelper.MessagePaddingLength > 508) { throw new SocketException((int)SocketError.MessageSize, "you cannot send more than 508 bytes in a single packet"); }
        }

        bool isBound = false;
        public async Task Send(byte[] data)
        {
            if (!isBound) { throw new InvalidOperationException("cannot call send without an address if the socket has not been bound"); }
            checkSocketMessage(data.Length);

            data = SocketHelper.PrepareMessage(data);

            await SendRaw(data);
        }
        public async Task Send(byte[] data, IPEndPoint address)
        {
            if(isBound) { throw new InvalidOperationException("cannot call send with an address if socket is already bound"); }
            checkSocketMessage(data.Length);

            data = SocketHelper.PrepareMessage(data);

            await SendRaw(data, address);
        }
        public async Task Send(byte[] data, IPEndPoint[] addresses)
        {
            if(isBound) { throw new InvalidOperationException("cannot call send with an address if socket is already bound"); }
            checkSocketMessage(data.Length);

            data = SocketHelper.PrepareMessage(data);

            Task[] tasks = addresses.Select(address => SendRaw(data, address)).ToArray();
            await Task.WhenAll(tasks);
        }

        private async Task SendRaw(ReadOnlyMemory<byte> data, IPEndPoint address)
        {
            await _socket.SendToAsync(data, SocketFlags.None, address, mainCancellationToken);
        }
        private async Task SendRaw(ReadOnlyMemory<byte> data)
        {
            await _socket.SendAsync(data, SocketFlags.None, mainCancellationToken);
        }

        public void StartRecieving(int port)
        {
            isReceiving = true;
            receiveMethodLinkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(receiveMethodCancellationTokenSource.Token, mainCancellationTokenSource.Token);
            receiveMethodCancellationToken = receiveMethodLinkedCancellationTokenSource.Token;

            Task.Run(() => RecieveMethod(port));
        }
        public async Task StopRecieving()
        {
            isReceiving = false;
            await receiveMethodCancellationTokenSource.CancelAsync();
        }
        public delegate void DataReceivedEventHandler(ReadOnlyMemory<byte> data);
        public event DataReceivedEventHandler? RecieveCallback;
        private bool isReceiving = false;
        private CancellationTokenSource receiveMethodCancellationTokenSource = new CancellationTokenSource();
        private CancellationTokenSource receiveMethodLinkedCancellationTokenSource = new CancellationTokenSource();
        private CancellationToken receiveMethodCancellationToken;
        private async Task RecieveMethod(int port)
        {
            while(isReceiving)
            {
                byte[] buffer = new byte[1024];
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, port);
                SocketReceiveFromResult recievedResult = await _socket.ReceiveFromAsync(buffer, SocketFlags.None, endPoint, receiveMethodCancellationToken);

                ReadOnlyMemory<byte> trimmedBuffer = new ReadOnlyMemory<byte>(buffer, 1, recievedResult.ReceivedBytes - 1);
                
                HandleReceivedData((IPEndPoint)recievedResult.RemoteEndPoint, (byteCodes)buffer[0], trimmedBuffer);
            }
        }

        private void HandleReceivedData(IPEndPoint sender, byteCodes byteCode, ReadOnlyMemory<byte> data)
        {
            switch(byteCode)
            {
                case byteCodes.simpleMessageHeader:
                    ExecuteCallbacks(sender, data);
                    break;
                case byteCodes.terminateConnection:
                    packetHelper.TerminateClient(sender);
                    break;
                case byteCodes.heartBeat:
                    break;
            }
        }

        private void ExecuteCallbacks(EndPoint sender, ReadOnlyMemory<byte> data)
        {
            Action<ReadOnlyMemory<byte>>[]? callbacks = RecieveCallback?.GetInvocationList().OfType<Action<ReadOnlyMemory<byte>>>().ToArray();

            if (callbacks != null)
            {
                callbacks.Select(callback => Task.Run(() => callback(data)));
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
    }
}