using System.Net;
using System.Net.Sockets;

namespace WireLink
{
    internal class UdpSocket
    {
        public UdpSocket()
        {
            _socket = new Socket(SocketType.Dgram, ProtocolType.Udp);
        }

        Socket _socket;

        bool isBound = false;
        public void Send(byte[] data)
        {
            if (!isBound) { throw new InvalidOperationException("cannot call send without an adress if the socket has not been binded to an adress"); }
        }
        public void Send(byte[] data, IPAddress address)
        {
            if(isBound) { throw new InvalidOperationException("cannot call send with an adress if socket is already bound"); }
        }

        public void StartRecieving(int port)
        {

        }
        public void StopRecieving()
        {

        }
        private async Task RecieveFunction()
        {
            ArraySegment<byte> buffer = new ArraySegment<byte>();
            int bufferSize = await _socket.ReceiveAsync(buffer);
        }
        public void addReciever()
        {

        }

        public void AddListener(Task function)
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