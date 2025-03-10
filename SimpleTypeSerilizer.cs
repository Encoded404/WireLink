using System.Collections;
using System.Reflection;

namespace SimpleTypeSerilizer
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class NetworkVariableAttribute : Attribute { }

    public static class TypeSerilizer
    {
        /// <summary>
        /// Recursively retrieves serializable members from an object.
        /// </summary>
        public static List<object?> GetValues(object obj)
        {
            if (obj == null)
                throw new NullReferenceException("cannot serialize a value of type 'null'");

            Type type = obj.GetType();

            // If the type is a "simple" type, eg int, long, short, byte, return its value directly.
            // Console.WriteLine("type is: {0}", type);
            if (ReturnableTypes(type))
            {
                // Console.WriteLine("returning: {0}", obj);
                return new List<object?> { obj };
            }

            // If the object is a collection, eg list, array, hashset, etc, process each element one by one.
            if (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string))
            {
                var list = new List<object?>();
                foreach (var item in (IEnumerable)obj)
                {
                    list.Add(GetValues(item));
                }
                return list;
            }

            // Otherwise, assume it's a complex type, eg a type which contains other types and is not a collection (array, list, etc) 
            List<object?> result = new List<object?>();

            // Process all properties that have the custom attribute.
            PropertyInfo[] properties = (PropertyInfo[])type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                .Where(property => property.IsDefined(typeof(NetworkVariableAttribute), inherit: true));

            foreach (PropertyInfo property in properties)
            {
                if(property != null)
                {
                    object? value = property.GetValue(obj);
                    if(value != null) { result.Add(GetValues(value)); } // Recursive call.
                }
            }

            // Process fields with the custom attribute.
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                            .Where(field => field.IsDefined(typeof(NetworkVariableAttribute), inherit: true));

            foreach (FieldInfo field in fields)
            {
                object? value = field.GetValue(obj);
                if(value != null) { result.Add(GetValues(value)); } // Recursive call.
            }

            return result;
        }

        /// <summary>
        /// Determines if a type is "simple" (i.e. a primitive type, enum, or a common immutable type).
        /// </summary>
        private static bool ReturnableTypes(Type type)
        {
            return type.IsPrimitive
                || type.IsEnum
                || type.Equals(typeof(string));
        }
    }
}