using System.Net;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using SimpleTypeSerializer;

namespace WireLink
{
    internal class PacketHandler
    {
        UdpSocket socket;
        // this bool indicates whether the client is connected to a server
        // this should be set to false when its a server
        bool isConnectedToServer = false;

        private Queue<NetworkData>[,] queues;

        public PacketHandler()
        {
            socket = new UdpSocket(this);

            // Initialize the 2D array (2 speeds × 3 importance levels)
            queues = new Queue<NetworkData>[2, 3];
            
            // Fill the array with the appropriate queues
            queues[0, 0] = new Queue<NetworkData>();;
            queues[0, 1] = new Queue<NetworkData>();;
            queues[0, 2] = new Queue<NetworkData>();;
            queues[1, 0] = new Queue<NetworkData>();;
            queues[1, 1] = new Queue<NetworkData>();;
            queues[1, 2] = new Queue<NetworkData>();;
        }
        public void AddKnownClient(EndPoint clientID)
        {

        }
        public void AttemptGracefullNetclientShutdown(EndPoint clientID)
        {
            // Implementation for graceful network client shutdown
        }
        public void TerminateClient(EndPoint clientID)
        {

        }
        public async Task Send(NetworkData data, PacketImportance importance, packetSpeedImportance packetSpeed)
        {
            if (packetSpeed == packetSpeedImportance.instant)
            {
                switch (importance)
                {
                    case PacketImportance.Low:
                        await sendLowImportance(data);
                        break;
                    case PacketImportance.Medium:
                        await sendMediumImportance(data);
                        break;
                    case PacketImportance.High:
                        await sendHighImportance(data);
                        break;
                }
            }
            else
            {
                // Convert enum to int for array indexing (assuming Low=0, Medium=1, High=2)
                int importanceIndex = (int)importance;
                int speedIndex = (int)packetSpeed;
                
                // Access the right queue directly
                queues[speedIndex, importanceIndex].Enqueue(data);
            }
        }

#if NETSTANDARD2_1
        private async Task sendLowImportance(NetworkData data, EndPoint? clientID = null)
#else
        private async Task sendLowImportance(NetworkData data, SocketAddress? clientID = null)
#endif
        {
            // Implementation for sending low importance packets
            byte[] messageData = data.message.getData();
            // +1 for the header byte
            // +1 for the importance byte
            // +1 for the data type byte
            byte[] serializedData = new byte[messageData.Length + 3];

            serializedData[0] = (byte)byteCodes.simpleMessageHeader;
            serializedData[1] = (byte)PacketImportance.Low;
            serializedData[2] = (byte)data.message.dataType;
            Array.Copy(messageData, 0, serializedData, 3, messageData.Length);

            if (isConnectedToServer)
            {
                await socket.Send(serializedData);
            }
            else if (clientID != null)
            {
                await socket.Send(serializedData, clientID);
            }
        }

#if NETSTANDARD2_1
        private async Task sendMediumImportance(NetworkData data, EndPoint? clientID = null)
#else
        private async Task sendMediumImportance(NetworkData data, SocketAddress? clientID = null)
#endif
        {
            // Implementation for sending medium importance packets
            // Implementation for sending low importance packets
            byte[] messageData = data.message.getData();
            // +1 for the header byte
            // +1 for the importance byte
            // +1 for the data type byte
            byte[] serializedData = new byte[messageData.Length + 3];

            serializedData[0] = (byte)byteCodes.simpleMessageHeader;
            serializedData[1] = (byte)PacketImportance.Medium;
            serializedData[2] = (byte)data.message.dataType;

            Array.Copy(messageData, 0, serializedData, 3, messageData.Length);

            if (isConnectedToServer)
            {
                await socket.Send(serializedData);
            }
            else if (clientID != null)
            {
                await socket.Send(serializedData, clientID);
            }
        }

#if NETSTANDARD2_1
        private async Task sendHighImportance(NetworkData data, EndPoint? clientID = null)
#else
        private async Task sendHighImportance(NetworkData data, SocketAddress? clientID = null)
#endif
        {
            // Implementation for sending high importance packets
        }
    }
}