using System.Net;
using System.Net.Sockets;

namespace WireLink
{
    internal class UdpSocket
    {
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
        public void AddListener(Task function)
        {

        }
    }
}