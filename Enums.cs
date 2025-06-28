namespace WireLink
{
    internal enum byteCodes : byte
    {
        terminateConnection,
        simpleMessageHeader,
        verifyConnectionRequest,
        verifyConnection,
        verifyConnectionResponse,
        connectionVerified,
        recievedInvalidData,
        heartBeat
    }

    internal enum ServerType
    {
        Client,
        Server,
        RelayServer,
    }

    internal enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Disconnecting,
        Terminated,
    }

    public enum PacketImportance : byte
    {
        /// <summary>
        /// Low importance packets may be dropped, and are not guaranteed to be sent or received, and can have unrecognised bitflips.
        /// </summary>
        Low,
        /// <summary>
        /// Medium importance packets are ensured to be sent and received, not dropped, and likely contain the correct data.
        /// </summary>
        Medium,
        /// <summary>
        /// High importance packets are ensured to be sent and received in order, not dropped, and contain the correct data.
        /// </summary>
        High
    }
    public enum packetSpeedImportance
    {
#if NETSTANDARD2_1

        /// <summary>
        /// sends as a group of packets, at the next update.
        /// </summary>
        nextUpdate = 0,
        /// <summary>
        /// sends as a group of packets, at the next fixed update.
        /// </summary>
        nextFixedUpdate = 1,
#else
        /// <summary>
        /// sends as a group of packets, at a slow rate.
        /// </summary>
        slow = 0,
        /// <summary>
        /// sends as a group of packets, at a medium rate.
        /// </summary>
        fast = 1,
#endif
        /// <summary>
        /// sends as a single packet instantly.
        /// </summary>
        instant = 2
    }
}
