using System.Text;
using ConsoleLogger;
using System.IO.Hashing;
using SimpleTypeSerilizer;
using System.Collections;

namespace WireLink
{
    internal class DataConversionHelper
    {
        private static Dictionary <string, int> HashLookupTable = new Dictionary <string, int>();
        public static int computeHash(string input)
        {
            if(HashLookupTable.ContainsKey(input))
            {
                return HashLookupTable[input];
            }

            // Convert string to byte array
            byte[] byteArray = Encoding.UTF8.GetBytes(input);
            
            // Compute xxHash32
            int returnValue = BitConverter.ToInt32(XxHash32.Hash(byteArray));
            
            HashLookupTable.Add(input, returnValue);

            return returnValue;
        }

        public static int computeHash(Type input)
        {
            return computeHash(input.Name);
        }

        public static TypedByte[] SerializeData(object data)
        {
            object serializedData = TypeSerilizer.GetValues(data);

            List<object?> DataList = TypeSerilizer.GetValues(serializedData);

            List<TypedByte> byteList = new List<TypedByte>(); 
            foreach(object? obj in DataList)
            {
                if(obj != null)
                {
                    byteList.Add(ConvertSimpleToBytes(obj));
                }
            }
            return byteList.ToArray();
        }
        public static TypedByte ConvertSimpleToBytes(object value)
        {
            switch (value)
            {
                case bool b: return new TypedByte(computeHash(value.GetType()), BitConverter.GetBytes(b));
                case char c: return new TypedByte(computeHash(value.GetType()), BitConverter.GetBytes(c));
                case short s: return new TypedByte(computeHash(value.GetType()), BitConverter.GetBytes(s));
                case ushort us: return new TypedByte(computeHash(value.GetType()), BitConverter.GetBytes(us));
                case int i: return new TypedByte(computeHash(value.GetType()), BitConverter.GetBytes(i));
                case uint ui: return new TypedByte(computeHash(value.GetType()), BitConverter.GetBytes(ui));
                case long l: return new TypedByte(computeHash(value.GetType()), BitConverter.GetBytes(l));
                case ulong ul: return new TypedByte(computeHash(value.GetType()), BitConverter.GetBytes(ul));
                case float f: return new TypedByte(computeHash(value.GetType()), BitConverter.GetBytes(f));
                case double d: return new TypedByte(computeHash(value.GetType()), BitConverter.GetBytes(d));
                case Enum e: return new TypedByte(computeHash(value.GetType()), BitConverter.GetBytes(Convert.ToInt64(e))); // Enums are stored as their underlying type
                case string str: return new TypedByte(computeHash(value.GetType()), System.Text.Encoding.UTF8.GetBytes(str)); // Strings require encoding
                default: throw new ArgumentException("Unsupported type");
            }
        }
        // private static List<byte[]> getBytes(object data)
        // {
            
        // }
    }
}