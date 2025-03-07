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

        public static byte[][] SerializeData(object data)
        {
            object serializedData = TypeSerilizer.GetValues(data);

            return getBytes(serializedData);
        }
        private static byte[][] getBytes(object data)
        {
            List<List<byte>> result = new List<List<byte>>();
            Type type = data.GetType();
            if(typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string))
            {
                foreach(object obj in (IEnumerable)data)
                {
                    result.Add(getBytes(obj));
                }
            }
            else
            {
                
            }
        }
    }
}