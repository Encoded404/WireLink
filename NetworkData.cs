using SimpleTypeSerializer;

namespace WireLink
{
    internal struct NetworkData
    {
        public long messageID;
        public dataContainer message;
        public NetworkData(long messageID, dataContainer message)
        {
            this.messageID = messageID;
            this.message = message;
        }
    }
}