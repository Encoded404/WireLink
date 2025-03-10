namespace WireLink
{
    internal struct TypedByte
    {
        public int dataType;
        public byte[] message;
        public TypedByte(int dataType, byte[] message)
        {
            this.dataType = dataType;
            this.message = message;
        }
    }
    internal struct NetworkData
    {
        public int dataType;
        public long messageID;
        public TypedByte[] message;
        public NetworkData(long messageID, TypedByte[] message)
        {
            this.messageID = messageID;
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
