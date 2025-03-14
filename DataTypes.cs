namespace WireLink
{
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
