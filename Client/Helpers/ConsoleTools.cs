using System;
using System.Threading;

namespace Common.Helpers
{
    public static class ConsoleTools
    {
        public static bool AskYesNo(string question)
        {
            ConsoleKey response;
            do
            {
                Console.Write($"{question}? [y/n] ");
                response = Console.ReadKey(false).Key;
                if (response != ConsoleKey.Enter)
                    Console.WriteLine();

            } while (response != ConsoleKey.Y && response != ConsoleKey.N);

            return response == ConsoleKey.Y;
        }

        public static string HideInput(string prompt)
        {
            Console.Write($"{prompt}>>> ");
            string input = "";
            ConsoleKeyInfo info;
            bool entered = false;
            do
            {
                while (!Console.KeyAvailable)
                    Thread.Sleep(10);
                info = Console.ReadKey(true);
                switch (info.Key)
                {
                    case ConsoleKey.Enter:
                        entered = true;
                        break;
                    case ConsoleKey.Backspace:
                        if (!string.IsNullOrEmpty(input))
                        {
                            input = input.Substring(0, input.Length - 1);
                            int pos = Console.CursorLeft;
                            Console.SetCursorPosition(pos - 1, Console.CursorTop);
                            Console.Write(" ");
                            Console.SetCursorPosition(pos - 1, Console.CursorTop);
                        }
                        break;
                    default:
                        Console.Write("*");
                        input += info.KeyChar;
                        break;
                }
            } while (!entered);
            Console.WriteLine();
            return input;
        }
    }
}
