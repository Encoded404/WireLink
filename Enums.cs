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
}
