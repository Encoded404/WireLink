namespace WireLink
{
    internal enum byteCodes : byte
    {
        terminateConnection,
        messageHeaderStart,
        messageHeaderEnd,
        verifyConnectionRequest,
        verifyConnection,
        verifyConnectionResponse,
        connectionVerified,
        recievedInvalidData,
        heartBeat
    }
}
