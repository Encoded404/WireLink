using System.Net;

namespace WireLink
{
    internal class ClientPacketConnector
    {
        PacketHandler handler = new PacketHandler();
        public async Task ConnectToServer(EndPoint ServerAddress)
        {

        }
        public async Task Disconnect()
        {

        }
        public async Task SendPacket(NetworkData data)
        {
            handler.Send(data);
        }
    }
}