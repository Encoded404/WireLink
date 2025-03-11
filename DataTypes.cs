namespace WireLink
{
    internal struct TypedByteArray
    {
        public int dataType;
        public byte[] message;
        public TypedByteArray(int dataType, byte[] message)
        {
            this.dataType = dataType;
            this.message = message;
        }
    }
    internal struct TypeWrapper
    {
        public int dataType;
        public 
    }
    internal struct NetworkData
    {
        public int dataType;
        public long messageID;
        public TypedByteArray[] message;
        public NetworkData(long messageID, TypedByteArray[] message)
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
