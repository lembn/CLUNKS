using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;

namespace TestEnv
{
    class Program
    {
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

        static void Main(string[] args)
        {
            string a = HideInput("write");
            Console.WriteLine(a);
            Console.Read();
        }
    }

}
