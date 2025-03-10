using System.Net;

namespace WireLink
{
    internal class PacketHandler
    {
        public PacketHandler()
        {
            socket = new UdpSocket(this);
        }
        UdpSocket socket;
        public void AddKnownClient(EndPoint clientID)
        {
            
        }
        public void AttemptGracefullClientShutdown(EndPoint clientID)
        {

        }
        public void TerminateClient(EndPoint clientID)
        {

        }
        public async Task Send(NetworkData data)
        {
            List<byte> bytes = new List<byte>();
            bytes.Add((byte)byteCodes.simpleMessageHeader);
            bytes.Add()
            await socket.Send();
        }
    }
}