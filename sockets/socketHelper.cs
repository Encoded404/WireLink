namespace WireLink
{
    internal static class SocketHelper
    {
        public static int ExtraMessageLength = PrepareMessage([]).Length + 1; // add 1 to be safe

        public static byte[] PrepareMessage(byte[] data)
        {
            return data;
        }
    }
}