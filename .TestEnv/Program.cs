using Common.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TestEnv
{
    public struct Pointer
    {
        #region Private Members

        private readonly int min;
        private readonly int max;
        private int value;

        #endregion

        public Pointer(int min, int max)
        {
            value = 0;
            this.min = min;
            this.max = max;
        }

        public void SetMax() => value = max;
        public void SetMin() => value = min;
        public bool IsMax() => value == max;
        public bool IsMin() => value == min;
        public void Reset() => value = 0;

        public static int operator -(int a, Pointer b) => a - b.value;
        public static Pointer operator -(Pointer a, int b)
        {
            a.value = Math.Max(a.min, a.value - b);
            return a;
        }
        public static Pointer operator +(Pointer a, int b)
        {
            a.value = Math.Min(a.max, a.value +b);
            return a;
        }
        public static Pointer operator ++(Pointer a)
        {
            a.value = Math.Min(a.max, a.value + 1);
            return a;
        }
        public static Pointer operator --(Pointer a)
        {
            a.value = Math.Max(a.min, a.value - 1);
            return a;
        }
        public static bool operator ==(Pointer a, int b) => a.value == b;
        public static bool operator !=(Pointer a, int b) => a.value != b;
        public static bool operator <=(Pointer a, int b) => a.value <= b;
        public static bool operator >=(Pointer a, int b) => a.value >= b;

        public static implicit operator int(Pointer a) => a.value;
    }

    /// <summary>
    /// A class to represent a line of a feed
    /// </summary>
    public class FeedItem
    {
        private readonly List<List<KeyValuePair<string, ConsoleColor>>> lines;

        #region Public Members

        public readonly int length;
        public Pointer offset;

        #endregion

        public FeedItem(string username, string message, string you, string entity = null, ConsoleColor @default = ConsoleColor.Gray)
        {
            List<string> text = new List<string>();
            List<ConsoleColor> colours = new List<ConsoleColor>();

            Action<string, ConsoleColor> Add = (textToAdd, colourToAdd) => { text.Add(textToAdd); colours.Add(colourToAdd); };

            bool isGlobal = entity != null;
            Add(username == you ? "YOU" : username, ConsoleColor.Blue);
            if (isGlobal)
            {
                Add("@", @default);
                Add(entity, ConsoleColor.DarkGreen);
            }
            Add(" - ", @default);
            Add(message, @default);
            int counter = 0;
            int index = 0;
            int width = Console.WindowWidth;
            lines = new List<List<KeyValuePair<string, ConsoleColor>>>();
            for (int i = 0; i < text.Count; i++)
            {
                if (lines.Count == 0)
                    lines.Add(new List<KeyValuePair<string, ConsoleColor>>());
                if (counter + text[i].Length > width)
                {
                    int overflow = counter + text[i].Length - width;
                    lines[index].Add(new KeyValuePair<string, ConsoleColor>(text[i].Substring(0, text[i].Length - overflow), colours[i]));
                    index++;
                    counter = 0;
                    lines.Add(new List<KeyValuePair<string, ConsoleColor>>());
                    text.Insert(i + 1, text[i].Substring(text[i].Length - overflow, overflow));
                    colours.Insert(i + 1, colours[i]);
                }
                else
                {
                    lines[index].Add(new KeyValuePair<string, ConsoleColor>(text[i], colours[i]));
                    counter += text[i].Length;
                }
            }
            length = lines.Count;
            offset = new Pointer(-(length - 1), length - 1);
        }

        public List<List<KeyValuePair<string, ConsoleColor>>> Get(int num)
        {
            if (offset >= 0)
                return lines.Skip(offset).Take(num).ToList();
            else
            {
                lines.Reverse();
                List<List<KeyValuePair<string, ConsoleColor>>> output = lines.Skip(0 - offset).Take(num).ToList();
                lines.Reverse();
                output.Reverse();
                return output;
            }
        }

        public bool IsSingle() => length == 1;
    }

    /// <summary>
    /// A Thread safe class to represent the incoming feed of a user
    /// </summary>
    public static class Feed
    {
        #region Private Members

        private static FeedItem[] feed;
        private static Pointer pointer; //points to the bottom visible line
        private static int size;
        private static bool canScroll;
        private static int capacity;
        private static int counter;
        private static int top;
        private static int bottom;
        private static int width;
        private static object lockHolder;
        private static string active = "[INCOMING FEED]";
        private static string inactive = "[FEED - (DEACTIVATED)]";
        private static bool alive;
        private static bool deactivating;
        private static CancellationTokenSource sleeper;

        #endregion

        /// <summary>
        /// A method to setup the Feed class
        /// </summary>
        /// <param name="size">The number of lines the feed should display</param>
        /// <param name="capacity">The number of lines the feed should store</param>
        /// <param name="you">The user's username</param>
        public static void Initialise(int size, int capacity, string you)
        {
            Feed.size = size;
            Feed.capacity = capacity;
            pointer = new Pointer(0, capacity - 1);
            feed = new FeedItem[capacity];
            lockHolder = new object();
            sleeper = new CancellationTokenSource();
        }

        /// <summary>
        /// A method to display the feed
        /// </summary>
        public static void Show()
        {
            sleeper.Cancel();
            if (alive && !deactivating)
                Deactivate();
            Console.WriteLine();
            top = Console.CursorTop;
            width = Console.WindowWidth;
            PrintHeader(active);
            Console.Write(new string('\n', size) + new string('=', Console.WindowWidth));
            Update((Console.CursorLeft, Console.CursorTop));            
            Console.ResetColor();
            bottom = Console.CursorTop;
            alive = true;
            sleeper.Dispose();
            sleeper = new CancellationTokenSource();
            ThreadHelper.GetECThread(sleeper.Token, () =>
            {
                //if cursor is at top of console and the offset > size of feed
                if ((Console.CursorTop > Console.WindowHeight - 1) && (Console.CursorTop - (Console.WindowHeight - 1) >= (bottom - top)))
                    Deactivate();
                else
                    sleeper.Token.WaitHandle.WaitOne(3000);
            }).Start();
        }

        /// <summary>
        /// A method to add a Feedline to the Feed
        /// </summary>
        /// <param name="line">The line to add</param>
        public static void Append(FeedItem line)
        {
            lock (lockHolder)
            {
                int lines = 0;
                for (int i = counter; i >= 0; i--)
                {
                    if (i == 0)
                    {
                        feed[0] = line;
                        if (counter > 0)
                            if (feed[1].length > 1)
                                feed[1].offset.SetMax();
                    }
                    else if (i == capacity)
                        continue;
                    else
                        feed[i] = feed[i - 1];
                    lines += feed[i].length;
                    if (lines > size)
                        canScroll = true;
                }
                if (counter != capacity)
                    counter++;
                pointer.SetMin();
                Update((Console.CursorLeft, Console.CursorTop));
            }
        }

        /// <summary>
        /// A method to scroll the feed
        /// </summary>
        /// <param name="scrollUp">An boolean to represent if the pointer should be incremented (to
        /// scroll up) or decremented (to scroll down)</param>
        public static void Scroll(bool scrollUp)
        {
            lock (lockHolder)
            {
                if (!canScroll)
                    return;       
                /// scrolling does opposite operations to pointer and offset because pointer is backwards and offset is not
                /// as pointer approaches 0, messages get newer but as offset approaches 0, lines get closer to the first 
                /// line (older)
                if (scrollUp)
                {
                    /// The pointer should only be moved if it currently points to a FeedItem that only holds
                    /// one line or a FeedItem that holds multiple lines, but has been scrolled up so far that only
                    /// one line is showing
                    if (feed[pointer].IsSingle() || (!feed[pointer].IsSingle() && feed[pointer].offset.IsMin()))
                    {
                        pointer++;
                        if (!feed[pointer].IsSingle())
                            feed[pointer].offset.Reset();
                    }
                    /// If feed[pointer] is a FeedItem that holds multiple lines and is displaying more than one of
                    /// them in the feed, to sucesfully scroll up, the offset for this FeedItem needs to be manipulated
                    /// such that when Update() is called, this FeedItem will display 1 less line than it is currently displaying
                    else
                    {
                        /// To achieve this, if the FeedItem's offset is negative then it is either displaying
                        /// `feed[pointer].length + feed[pointer].offset` lines or `size` lines if
                        /// `feed[pointer].length + feed[pointer].offset` > size so the offset can be decremented
                        /// to achieve the target offset. 
                        if (feed[pointer].offset < 0)
                        {
                            feed[pointer].offset.SetMin();
                            feed[pointer].offset -= Math.Max(size, (feed[pointer].length + feed[pointer].offset) - 2);
                        }
                        /// Otherwise the FeedItem's offset is positive so it must be displaying `size` lines
                        /// and filling the screen since if the pointer is on this FeedItem then it is being
                        /// treated as the 'bottom' item so must have enough lines on display to fill from the
                        /// bottom of the feed to the top.
                        else
                        {
                            feed[pointer].offset.SetMin();
                            feed[pointer].offset += size - 2;
                        }
                    }
                }
                else
                {

                }
                Update((Console.CursorLeft, Console.CursorTop));
            }
        }

        /// <summary>
        /// A method to set the feed to inactive
        /// </summary>
        public static void Deactivate()
        {
            deactivating = true;
            (int, int) original = (Console.CursorLeft, Console.CursorTop);
            Console.CursorTop = top;
            PrintHeader(inactive);
            Console.CursorTop = top;
            Console.SetCursorPosition(original.Item1, original.Item2);
            alive = false;
            deactivating = false;
        }

        /// <summary>
        /// A method to update the display of the feed
        /// </summary>
        /// <param name="original">The X-Y coordinates of the Console's cursor before Update was called</param>
        private static void Update((int, int) original)
        {
            if (counter < 1)
                return;
            List<List<KeyValuePair<string, ConsoleColor>>> lines = new List<List<KeyValuePair<string, ConsoleColor>>>();
            int i = pointer;
            List<int> empty = new List<int>();
            while (lines.Count < size && i < capacity)
            {
                if (feed[i] != null)
                    lines.InsertRange(0, feed[i].Get(size - lines.Count));
                else
                    empty.Add(i);
                i++;
            }
            Console.CursorTop = top + 1;
            for (i = 0; i < size; i++)
                Console.Write(new string(' ', Console.WindowWidth));
            Console.CursorTop = top + 1 + (size - lines.Count);
            i = pointer;
            foreach (List<KeyValuePair<string, ConsoleColor>> line in lines)
            {
                int before = Console.CursorTop;                
                if (!empty.Contains(i))
                    foreach (KeyValuePair<string, ConsoleColor> entry in line)
                    {
                        Console.ForegroundColor = entry.Value;
                        Console.Write(entry.Key);
                    }
                if (Console.CursorTop == before)
                    Console.SetCursorPosition(0, ++Console.CursorTop);
                i++;
            }
            Console.ResetColor();
            Console.SetCursorPosition(original.Item1, original.Item2);
        }

        /// <summary>
        /// A method to output the first line of the feed
        /// </summary>
        /// <param name="header">The title of the feed</param>
        private static void PrintHeader(string header)
        {
            header = Balance(header);
            string dashes = new string('-', (width - header.Length) / 2);
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(dashes + header + dashes);
        }

        /// <summary>
        /// A method to pad strings with the '-' character so they can be centered on screen
        /// </summary>
        /// <param name="input">The input string</param>
        /// <returns>The padded string</returns>
        private static string Balance(string input)
        {
            if ((Console.WindowWidth % 2 == 0) == (input.Length % 2 == 0))
                return input;
            else
                return $"{input}-";
        }
    }

    class Program
    {
        private static string username = "lem";

        static void Main(string[] args)
        {
            Feed.Initialise(3, 5, username);
            Feed.Show();
            //Feed.Append(new FeedItem(username, "msg1", username, "test"));
            Feed.Append(new FeedItem(username, new string('b', 680) + "end", username, "test"));
            Feed.Append(new FeedItem(username, new string('b', 120) + "end", username, "test"));
            //Feed.Append(new FeedItem(username, "msg2", username));
            //Feed.Append(new FeedItem(username, "msg3", username));
            //Feed.Append(new FeedItem(username, "msg4", username));
            //Feed.Append(new FeedItem(username, "msg5", username));
            //Feed.Append(new FeedItem(username, "msg6", username));
            //Feed.Append(new FeedItem(username, "msg7", username));
            Console.Write("test>>> ");
            Task.Run(() =>
            {
                while (true)
                {
                    var a = Console.Read();
                    Feed.Scroll(true);
                }
            });
        }
    }
}