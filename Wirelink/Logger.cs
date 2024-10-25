namespace ConsoleLogger
{
    public class Logger
    {
        static List<char> inputChars= new List<char>();
        static Stream inputStream = Console.OpenStandardInput();
        public static void WriteLine(object? value)
        {
            Write(value, true);
        }
        public static void Write(object? value)
        {
            Write(value, false);
        }
        static void Write(object? value, bool useNewLine)
        {

            int originalX = Console.GetCursorPosition().Left;
            int originalY = Console.GetCursorPosition().Top;

            ClearCurrentConsoleLine();

            //Console.Write("\n");
            Console.SetCursorPosition(0, originalY - 1);
            Console.Write(value);
            if(useNewLine) { Console.Write('\n'); }

            if(inputChars.Count > 0) { Console.Write(inputChars.ToArray()); }
            {

            }

            Console.SetCursorPosition(originalX, originalY + 1);
        }
        static void WriteRaw(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if(value[i] == '\n')
                {
                    Logger.Write(@"\\n");
                }
                else
                {
                    Logger.Write(value[i]);
                }
            }
        }
        public static void ClearCurrentConsoleLine()
        {
            int currentLineCursor = Console.CursorTop;
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write(new string(' ', Console.WindowWidth)); 
            Console.SetCursorPosition(0, currentLineCursor);
        }

        public static string ReadLine()
        {
            string returnValue = "";
            while (true)
            {
                ConsoleKeyInfo input = Console.ReadKey(true);
                if(input.Key == ConsoleKey.Enter) { break; }
                char inputChar = input.KeyChar;
                inputChars.Add(inputChar);
                Console.Write(inputChar);
            }
            returnValue = new string(inputChars.ToArray()); // set return value
            inputChars = new List<char>(); //empty input list
            WriteLine(returnValue); //write command to console as history
            ClearCurrentConsoleLine(); // clear line to be ready for next write or read
            //WriteLine("returning: "+returnValue);
            return returnValue;
        }
    }
}