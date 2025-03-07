namespace WireLink
{
    internal struct NetworkData
    {
        public long messageID;
        public int dataType;
        public byte[] message;

        public NetworkData(long messageId, int dataType, byte[] message)
        {
            this.messageID = messageId;
            this.dataType = dataType;
            this.message = message;
        }
    }
    internal class ImmutableFlag
    {
        private bool _value = false;

        public bool Value => _value;
        
        public void Set()
        {
            Set(true);
        }
        public void Set(bool value)
        {
            _value = value ? true : _value;
        }

        public static implicit operator bool(ImmutableFlag flag)
        {
            return flag._value;
        }
    }
}
