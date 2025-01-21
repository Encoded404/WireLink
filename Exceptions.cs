namespace WireLink
{
    class unknownThreadException : Exception
    {
        public unknownThreadException() { }
        public unknownThreadException(string message)
        : base(message) { }
    }
}