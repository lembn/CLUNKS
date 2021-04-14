//using Microsoft.Data.Sqlite;
//using System;
//using System.Collections.Generic;
//using System.Collections.Immutable;
//using System.IO;
//using System.Linq;
//using System.Text.RegularExpressions;
//using System.Threading;
//using System.Xml.Linq;

//namespace TestEnv
//{
//    class Program
//    {
//        public static string HideInput(string prompt)
//        {
//            Console.Write($"{prompt}>>> ");
//            string input = "";
//            ConsoleKeyInfo info;
//            bool entered = false;
//            do 
//            {
//                while (!Console.KeyAvailable)
//                    Thread.Sleep(10);
//                info = Console.ReadKey(true);
//                switch (info.Key)
//                {
//                    case ConsoleKey.Enter:
//                        entered = true;
//                        break;
//                    case ConsoleKey.Backspace:
//                        if (!string.IsNullOrEmpty(input))
//                        {
//                            input = input.Substring(0, input.Length - 1);
//                            int pos = Console.CursorLeft;
//                            Console.SetCursorPosition(pos - 1, Console.CursorTop);
//                            Console.Write(" ");
//                            Console.SetCursorPosition(pos - 1, Console.CursorTop);
//                        }
//                        break;
//                    default:
//                        Console.Write("*");
//                        input += info.KeyChar;
//                        break;
//                }            
//            } while (!entered);
//            Console.WriteLine();
//            return input;
//        }

//        static void Main(string[] args)
//        {
//            string a = HideInput("write");
//            Console.WriteLine(a);
//            Console.Read();
//        }
//    }

//}

using System;
using System.Threading.Tasks;

namespace ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
            Counter c = new Counter(3);
            c.ThresholdReached += c_ThresholdReached;

            Console.WriteLine("press 'a' key to increase total");
            while (Console.ReadKey(true).KeyChar == 'a')
            {
                Console.WriteLine("adding one");
                c.Add(1);
            }
        }

        static void c_ThresholdReached(object sender, ThresholdReachedEventArgs e)
        {
            Task.Run(() => 
            {
                int a = 0;
                for (Int64 i = 0; i < Int64.MaxValue; i++)
                    a++;
                Console.WriteLine("The threshold of {0} was reached at {1}.", e.Threshold, e.TimeReached);
            });            
        }
    }

    class Counter
    {
        private int threshold;
        private int total;

        public Counter(int passedThreshold)
        {
            threshold = passedThreshold;
        }

        public void Add(int x)
        {
            total += x;
            if (total >= threshold)
            {
                ThresholdReachedEventArgs args = new ThresholdReachedEventArgs();
                args.Threshold = threshold;
                args.TimeReached = DateTime.Now;
                OnThresholdReached(args);
            }
        }

        protected virtual void OnThresholdReached(ThresholdReachedEventArgs e)
        {
            EventHandler<ThresholdReachedEventArgs> handler = ThresholdReached;
            //if (handler != null)
            //{
            //    Console.WriteLine("in");
            //    handler(this, e);
            //    Console.WriteLine("out");
            //}

            Console.WriteLine("in");
            handler?.Invoke(this, e);
            Console.WriteLine("out");
        }

        public event EventHandler<ThresholdReachedEventArgs> ThresholdReached;
    }

    public class ThresholdReachedEventArgs : EventArgs
    {
        public int Threshold { get; set; }
        public DateTime TimeReached { get; set; }
    }
}
