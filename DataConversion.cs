using System.Text;
using ConsoleLogger;
using System.IO.Hashing;

namespace WireLink
{
    internal class DataConversionHelper
    {
        public DataConversionHelper()
        {
            InitBasicDataConversionFunctions();
        }

        object dataConversionTypesLock = new object();
        private Dictionary<int, (Func<object, byte[]> toByte, Func<byte[], object> fromByte)> dataConversionFunctions = new Dictionary<int, (Func<object, byte[]> toByte, Func<byte[], object> fromByte)>();
        private void InitBasicDataConversionFunctions()
        {
            dataConversionFunctions.Clear();

            AddTypeConversion<int>(BitConverter.GetBytes, (byte[] value) => { return BitConverter.ToInt32(value); });
            AddTypeConversion<long>(BitConverter.GetBytes, (byte[] value) => { return BitConverter.ToInt64(value); });
            AddTypeConversion<float>(BitConverter.GetBytes, (byte[] value) => { return BitConverter.ToSingle(value); });
            AddTypeConversion<double>(BitConverter.GetBytes, (byte[] value) => { return BitConverter.ToDouble(value); });
            AddTypeConversion<string>(Encoding.UTF8.GetBytes, Encoding.UTF8.GetString);
            AddTypeConversion<char[]>(Encoding.UTF8.GetBytes, Encoding.UTF8.GetChars);
        }

        private bool IsTypeConversionValid<T>(Func<object, byte[]> toByte, Func<byte[], object> fromByte)
        {
            // // Check if the delegate is a Func<T, byte> and validate the first argument's type
            // if (funcs.toByte.Method.GetParameters().Length == 1 &&
            //     funcs.fromByte.Method.GetParameters().Length == 1
            //     )
            // {
            //     Type toByteParameterType = funcs.toByte.Method.GetParameters()[0].ParameterType;
            //     Type fromByteParameterType = funcs.fromByte.Method.GetParameters()[0].ParameterType;
                
            //     // Check if the first parameter type matches the provided type
            //     if (toByteParameterType == type && funcs.toByte.Method.ReturnType == typeof(byte[]) &&
            //         fromByteParameterType == typeof(byte[]) && funcs.fromByte.Method.ReturnType == type
            //         )
            //     {
            //         return true;
            //     }
            // }
            // return false;

            T? value = default;

            if(value == null)
            {
                return true;
            }

            byte[] temp = toByte(value);
            T afterConversion = (T)fromByte(temp);

            if(EqualityComparer<T>.Default.Equals(afterConversion, value))
            {
                return true;
            }

            return false;
        }

        private int computeHash(string input)
        {
            // Convert string to byte array
            byte[] byteArray = Encoding.UTF8.GetBytes(input);
            
            // Compute xxHash32
            return BitConverter.ToInt32(XxHash32.Hash(byteArray));
        }

        private int computeHash(Type input)
        {
            return computeHash(input.Name);
        }

        // adds a function to dataConversionFunctions with type checking
        public bool AddTypeConversion<T>(Func<T, byte[]> toByteFunc, Func<byte[], T> fromByteFunc) where T : notnull
        {
            if (toByteFunc == null || fromByteFunc == null)
            {
                Logger.WriteLine("One of the conversion functions is null.", true);
                return false;
            }

            Type type = typeof(T);

            Func<object, byte[]> toByteFuncConverted = (object obj) => toByteFunc((T)obj); // Convert from T to byte[]
            Func<byte[], object> fromByteFuncConverted = (byte[] bytes) => fromByteFunc(bytes); // Convert from byte[] to T
            bool isValid = IsTypeConversionValid<T>(toByteFuncConverted, fromByteFuncConverted);

            if(isValid)
            {
                lock (dataConversionTypesLock)
                {
                    dataConversionFunctions[computeHash(type)] = (toByteFuncConverted, fromByteFuncConverted);
                }
                return true;
            }
            Logger.WriteLine($"function for type {type.Name} is not valid.", true);
            return false;
        }
        public bool RemoveTypeConversion(Type type)
        {
            lock (dataConversionTypesLock)
            {
                return dataConversionFunctions.Remove(computeHash(type));
            }
        }

        // execute a function from dataConversionFunctions
        public byte[] ConvertToByte<T>(T input) where T : notnull
        {
            Type type = typeof(T);

            bool doesFuncExist = false;
            (Func<object, byte[]> toByte, Func<byte[], object> fromByte) funcs;
            lock (dataConversionTypesLock)
            {
                doesFuncExist = dataConversionFunctions.TryGetValue(computeHash(type), out funcs);
            }
            if (doesFuncExist)
            {
                if (funcs.toByte.Method.GetParameters()[0].ParameterType == type)
                {
                    return funcs.toByte(input);
                }
                else
                {
                    throw new InvalidOperationException($"Stored function for type {type.Name} is of the wrong delegate type.");
                }
            }

            throw new KeyNotFoundException($"No function registered for type {type.Name}.");
        }
        public T ConvertToValue<T>(byte[] input) where T : notnull
        {
            Type type = typeof(T);

            bool doesFuncExist = false;
            (Func<object, byte[]> toByte, Func<byte[], object> fromByte) funcs;
            lock (dataConversionTypesLock)
            {
                doesFuncExist = dataConversionFunctions.TryGetValue(computeHash(type), out funcs);
            }
            if (doesFuncExist)
            {
                if (funcs.fromByte.Method.ReturnType == type)
                {
                    return (T)funcs.fromByte(input);
                }
                else
                {
                    throw new InvalidOperationException($"Stored function for type {type.Name} is of the wrong delegate type.");
                }
            }

            throw new KeyNotFoundException($"No function registered for type {type.Name}.");
        }
        // check if a function exist and is valid in dataConversionFunctions
        public bool DoesTypeConversionExist(Type type)
        {
            bool doesFuncExist = false;
            (Func<object, byte[]> toByte, Func<byte[], object> fromByte) funcs;
            lock (dataConversionTypesLock)
            {
                doesFuncExist = dataConversionFunctions.TryGetValue(computeHash(type), out funcs);
            }
            if (doesFuncExist)
            {
                // Check if the delegate is a Func<T, byte> and validate the first argument's type
                if (funcs.toByte.Method.GetParameters().Length == 1 &&
                    funcs.fromByte.Method.GetParameters().Length == 1
                    )
                {
                    Type toByteParameterType = funcs.toByte.Method.GetParameters()[0].ParameterType;
                    Type fromByteParameterType = funcs.fromByte.Method.GetParameters()[0].ParameterType;
                    
                    // Check if the first parameter type matches the provided type
                    if (toByteParameterType == type && funcs.toByte.Method.ReturnType == typeof(byte) &&
                        fromByteParameterType == typeof(byte) && funcs.fromByte.Method.ReturnType == type
                        )
                    {
                        return true;
                    }
                    else
                    {
                        Logger.WriteLine($"Stored function for type {type.Name} is of the wrong delegate type or return type.", true);
                        return false;
                    }
                }
            }
            return false;
        }
    }
}