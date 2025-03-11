using System.Text;
using ConsoleLogger;
using System.IO.Hashing;
using SimpleTypeSerilizer;
using System.Collections;

namespace WireLink
{
    internal class DataConversionHelper
    {
        private static Dictionary <string, int> StringHashLookupTable = new Dictionary <string, int>();
        private static Dictionary <Type, int> TypeHashLookupTable = new Dictionary <string, int>();
        private static int ComputexxHash(string input)
        {
            // Convert string to byte array
            byte[] byteArray = Encoding.UTF8.GetBytes(input);
            
            // Compute xxHash32
            return BitConverter.ToInt32(XxHash32.Hash(byteArray));
        }
        public static int computeHash(Type input)
        {
            if(StringHashLookupTable.ContainsKey(input))
            {
                return StringHashLookupTable[input];
            }

            int returnValue = ComputexxHash(input.FullName);

            HashLookupTable.Add(input, returnValue);

            return returnValue;
        }
        public static int computeHash(string input)
        {
            if(StringHashLookupTable.ContainsKey(input))
            {
                return StringHashLookupTable[input];
            }

            int returnValue = ComputexxHash(input);

            HashLookupTable.Add(input, returnValue);

            return returnValue;
        }

        public static TypedByteArray[] SerializeData(object data)
        {
            object serializedData = TypeSerilizer.GetValues(data);

            List<object?> DataList = TypeSerilizer.GetValues(serializedData);

            List<TypedByteArray> byteList = new List<TypedByteArray>(); 
            foreach(object? obj in DataList)
            {
                if(obj != null)
                {
                    byteList.Add(ConvertSimpleToBytes(obj));
                }
            }
            return byteList.ToArray();
        }
        public static TypedByteArray ConvertSimpleToBytes(object value)
        {
            switch (value)
            {
                case bool b: return new TypedByteArray(computeHash(value.GetType()), BitConverter.GetBytes(b));
                case char c: return new TypedByteArray(computeHash(value.GetType()), BitConverter.GetBytes(c));
                case short s: return new TypedByteArray(computeHash(value.GetType()), BitConverter.GetBytes(s));
                case ushort us: return new TypedByteArray(computeHash(value.GetType()), BitConverter.GetBytes(us));
                case int i: return new TypedByteArray(computeHash(value.GetType()), BitConverter.GetBytes(i));
                case uint ui: return new TypedByteArray(computeHash(value.GetType()), BitConverter.GetBytes(ui));
                case long l: return new TypedByteArray(computeHash(value.GetType()), BitConverter.GetBytes(l));
                case ulong ul: return new TypedByteArray(computeHash(value.GetType()), BitConverter.GetBytes(ul));
                case float f: return new TypedByteArray(computeHash(value.GetType()), BitConverter.GetBytes(f));
                case double d: return new TypedByteArray(computeHash(value.GetType()), BitConverter.GetBytes(d));
                case Enum e: return new TypedByteArray(computeHash(value.GetType()), BitConverter.GetBytes(Convert.ToInt64(e))); // Enums are stored as their underlying type
                case string str: return new TypedByteArray(computeHash(value.GetType()), System.Text.Encoding.UTF8.GetBytes(str)); // Strings require encoding
                default: throw new ArgumentException("Unsupported type");
            }
        }
        // private static List<byte[]> getBytes(object data)
        // {
            
        // }
    }
}