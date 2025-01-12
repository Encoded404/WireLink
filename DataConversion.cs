using System.Text;
using ConsoleLogger;
using System.IO.Hashing;

namespace WireLink
{
    internal class DataConversionHelper
    {
        private static Dictionary <string, int> lookupTable = new Dictionary <string, int>();
        public static int computeHash(string input)
        {
            if(lookupTable.ContainsKey(input))
            {
                return lookupTable[input];
            }

            // Convert string to byte array
            byte[] byteArray = Encoding.UTF8.GetBytes(input);
            
            // Compute xxHash32
            int returnValue = BitConverter.ToInt32(XxHash32.Hash(byteArray));
            
            lookupTable.Add(input, returnValue);

            return returnValue;
        }

        public static int computeHash(Type input)
        {
            return computeHash(input.Name);
        }
    }
}