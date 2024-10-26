namespace ConsoleLogger
{
    public class Logger
    {
        static List<char> inputChars= new List<char>();
        static Stream inputStream = Console.OpenStandardInput();

        static List<string> commands = new List<string>();
        static int redoCommandIndex = 0;
        public static void WriteLine(object? value)
        {
            Write(value, true);
        }
        public static void Write(object? value)
        {
            Write(value, false);
        }
        static bool isWriting = false;
        static void Write(object? value, bool useNewLine)
        {
            while(isWriting) {}
            isWriting = true;

            int originalX = Console.GetCursorPosition().Left;
            int originalY = Console.GetCursorPosition().Top;

            ClearCurrentConsoleLine();

            Console.WriteLine("");
            Console.SetCursorPosition(0, originalY - 1);
            Console.Write(value);
            if(useNewLine) { Console.Write('\n'); }

            redrawInput();

            Console.SetCursorPosition(originalX, Math.Clamp(originalY + 1, 0, Console.BufferHeight - 1));

            isWriting = false;
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
        static void redrawInput()
        {
            redoCommandIndex = Math.Clamp(redoCommandIndex, 0, commands.Count);
            //int cursor = Console.CursorLeft;
            ClearCurrentConsoleLine();
            if(inputChars.Count > 0 || redoCommandIndex > 0)
            {
                Console.Write(
                    redoCommandIndex > 0 ?
                        commands[redoCommandIndex - 1] :
                        new string(inputChars.ToArray()) 
                );
            }
        }
        public static string ReadLine()
        {
            string returnValue = "";
            while (true)
            {
                ConsoleKeyInfo input = Console.ReadKey(true);
                int cursorIndex = Console.CursorLeft;
                switch (input.Key)
                {
                    case ConsoleKey.Enter:
                        break;
                    case ConsoleKey.Backspace:
                        if(cursorIndex <= 0) {  continue; }

                        if(redoCommandIndex > 0) {inputChars = commands[redoCommandIndex - 1].ToCharArray().ToList(); redoCommandIndex = 0; }

                        //WriteLine("delete pressed");
                        if(inputChars.Count > 0) { inputChars.RemoveAt(Math.Clamp(cursorIndex - 1, 0, inputChars.Count-1)); }
                        
                        ClearCurrentConsoleLine();
                        //Console.Write("content is: ");
                        foreach(char ch in inputChars)
                        {
                            Console.Write(ch);
                        }

                        Console.SetCursorPosition(Math.Clamp(cursorIndex - 1, 0, Console.BufferWidth), Console.CursorTop);

                        continue;

                    case ConsoleKey.LeftArrow:
                        if(Console.CursorLeft > 0) {Console.CursorLeft -= 1; }
                        continue;
                    case ConsoleKey.RightArrow:
                        if(Console.CursorLeft < inputChars.Count) { Console.CursorLeft += 1; }
                        continue;
                    case ConsoleKey.UpArrow:
                        redoCommandIndex++;
                        redrawInput();
                        continue;
                    case ConsoleKey.DownArrow:
                        redoCommandIndex--;
                        redrawInput();
                        continue;
                        
                    default:

                        if(redoCommandIndex > 0) {inputChars = commands[redoCommandIndex - 1].ToCharArray().ToList(); redoCommandIndex = 0; }

                        char inputChar = input.KeyChar;
                        if(Console.CursorLeft >= inputChars.Count)
                        {
                            inputChars.Add(inputChar);
                            Console.Write(inputChar);
                        }
                        else
                        {
                            //Console.WriteLine("test");
                            inputChars.Insert( Math.Clamp(Console.CursorLeft, 0, inputChars.Count), inputChar);
                            Console.Write(inputChar);
                            cursorIndex = Console.CursorLeft;
                            for(int i = cursorIndex; i < inputChars.Count; i++)
                            {
                                Console.Write(inputChars[i]);
                            }
                            Console.SetCursorPosition(cursorIndex, Console.CursorTop);
                        }
                        continue;
                }
                if (input.Key == ConsoleKey.Enter) {break;}
                
            }
            if(redoCommandIndex > 0)
            {
                returnValue = new string(commands[redoCommandIndex - 1]);
                redoCommandIndex = 0;
            }
            else
            {
                returnValue = new string(inputChars.ToArray()); // set return value
            }
            inputChars = new List<char>(); //empty input list
            WriteLine(returnValue); //write command to console as history
            ClearCurrentConsoleLine(); // clear line to be ready for next write or read
            //WriteLine("returning: "+returnValue);
            commands.Reverse();
            commands.Add(returnValue);
            commands.Reverse();
            return returnValue;
        }
    }
}