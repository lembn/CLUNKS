using Common.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace TestEnv
{
    /// <summary>
    /// A class to hold the Event Arguments for the event trigger by an event being removed
    /// from the Circular Queue
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class CQRemoveEventArgs<T> : EventArgs
    {
        public T item;

        public CQRemoveEventArgs(T item) => this.item = item;
    }

    /// <summary>
    /// An implementation of a circuar queue that can be used to hold FeedItems and
    /// alert its owner when an item in the queue is renoved
    /// </summary>
    public class CircularQueue<T>
    {
        #region Public Members

        public delegate void RemoveHandler(object sender, CQRemoveEventArgs<T> e);
        public event RemoveHandler Remove;

        #endregion

        #region Private Members

        private int size;
        private T[] buffer;
        private int front;
        private T empty;

        #endregion

        public CircularQueue(T empty, int capacity)
        {
            front = -1;
            size = 0;
            this.empty = empty;
            buffer = Enumerable.Repeat(empty, capacity).ToArray();;
        }

        /// <summary>
        /// A method to add a new FeedItem into the queue
        /// </summary>
        /// <param name="item">THe FeedItem to add</param>
        public void Enqueue(T item)
        {
            if (Comparer<T>.Default.Compare(item, empty) == 0)
                return;
            if (size == 0)
                front = 0;
            else
                front = (front + 1) % buffer.Length;
            if (Comparer<T>.Default.Compare(buffer[front], empty) != 0)
                Remove?.Invoke(this, new CQRemoveEventArgs<T>(buffer[front]));
            buffer[front] = item;
            size++;
        }
    }

    /// <summary>
    /// A Thread safe class to represent the incoming feed of a user
    /// </summary>
    public static class Feed
    {
        #region Private Members

        private static CircularQueue<int> buffer;
        private static List<List<KeyValuePair<string, ConsoleColor>>> lines;
        private static int pointer; //points to the bottom visible line
        private static int size;
        private static int capacity;
        private static int top;
        private static int bottom;
        private static int width;
        private static string active = "[INCOMING FEED]";
        private static string inactive = "[FEED - (DEACTIVATED)]";
        private static bool alive;
        private static bool deactivating;
        private static CancellationTokenSource sleeper;

        #endregion

        public static string YOU;

        /// <summary>
        /// A method to setup the Feed class
        /// </summary>
        /// <param name="size">The number of lines the feed should display</param>
        /// <param name="capacity">The number of lines the feed should store</param>
        /// <param name="you">The user's username</param>
        public static void Initialise(int size, int capacity)
        {
            Feed.size = size;
            Feed.capacity = capacity;
            buffer = new CircularQueue<int>(-1, capacity);
            buffer.Remove += RemoveLines;
            lines = new List<List<KeyValuePair<string, ConsoleColor>>>();
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
            bottom = Console.CursorTop - 1;
            Update((Console.CursorLeft, Console.CursorTop));            
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

        public static void Add(string username, string message, string entity = null, ConsoleColor @default = ConsoleColor.Gray)
        {
            List<string> text = new List<string>();
            List<ConsoleColor> colours = new List<ConsoleColor>();

            Action<string, ConsoleColor> Add = (textToAdd, colourToAdd) => { text.Add(textToAdd); colours.Add(colourToAdd); };

            bool isGlobal = entity != null;
            Add(username == YOU ? "YOU" : username, ConsoleColor.Blue);
            if (isGlobal)
            {
                Add("@", @default);
                Add(entity, ConsoleColor.DarkGreen);
            }
            Add(" - ", @default);
            Add(message, @default);
            lock (buffer)
                buffer.Enqueue(lines.Count);
            pointer = 0;
            int counter = 0;
            lines.Insert(0, new List<KeyValuePair<string, ConsoleColor>>());
            lock (lines)
            {
                for (int i = 0; i < text.Count; i++)
                {
                    if (counter + text[i].Length > width)
                    {
                        int overflow = counter + text[i].Length - width;
                        lines[0].Add(new KeyValuePair<string, ConsoleColor>(text[i].Substring(0, text[i].Length - overflow), colours[i]));
                        lines.Insert(0, new List<KeyValuePair<string, ConsoleColor>>());
                        pointer++;
                        counter = 0;
                        text.Insert(i + 1, text[i].Substring(text[i].Length - overflow, overflow));
                        colours.Insert(i + 1, colours[i]);
                    }
                    else
                    {
                        lines[0].Add(new KeyValuePair<string, ConsoleColor>(text[i], colours[i]));
                        counter += text[i].Length;
                    }
                }
            }
            Update((Console.CursorLeft, Console.CursorTop));
        }

        /// <summary>
        /// A method to scroll the feed
        /// </summary>
        /// <param name="scrollUp">An boolean to represent if the pointer should be incremented (to
        /// scroll up) or decremented (to scroll down)</param>
        public static void Scroll(bool scrollUp)
        {
            lock (lines)
            {
                if (lines.Count < size)
                    return;
                if (scrollUp)
                {
                    if (pointer + size == lines.Count)
                        return;
                    else pointer++;
                }
                else
                {
                    if (pointer == 0)
                        return;
                    else pointer--;
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
        /// A method invoked by an event to act as an event handler used to remove a range
        /// of elements from lines.
        /// </summary>
        /// <param name="sender">The object who triggered the event</param>
        /// <param name="e">The event args</param>
        private static void RemoveLines(object sender, CQRemoveEventArgs<int> e) => lines.RemoveRange(e.item, lines.Count - 1);

        /// <summary>
        /// A method to update the display of the feed
        /// </summary>
        /// <param name="original">The X-Y coordinates of the Console's cursor before Update was called</param>
        private static void Update((int, int) original)
        {
            int counter = 0;
            if (size > lines.Count - pointer)
                Console.CursorTop = bottom - (lines.Count - pointer);
            else
                Console.CursorTop = top + 1;
            foreach (List<KeyValuePair<string, ConsoleColor>> line in lines.Skip(pointer).Take(size).ToArray().Reverse())
            {
                if (counter == size)
                    break;
                Console.Write(new string(' ', Console.WindowWidth));
                Console.CursorTop--;
                foreach (KeyValuePair<string, ConsoleColor> entry in line)
                {
                    Console.ForegroundColor = entry.Value;
                    Console.Write(entry.Key);
                }
                Console.SetCursorPosition(0, Console.CursorLeft == 0 ? Console.CursorTop : Console.CursorTop + 1);
                counter++;
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
            Feed.YOU = username;
            Feed.Initialise(3, 5);
            Feed.Show();
            Feed.Add(username, new string('b', 680) + "end", "test");
            Feed.Add(username, "msg1", "test");
            //Feed.Add(username, new string('b', 120) + "end", "test");
            //Feed.Add(username, "msg2");
            //Feed.Add(username, "msg3");
            //Feed.Add(username, "msg4");
            //Feed.Add(username, "msg5");
            //Feed.Add(username, "msg6");
            //Feed.Add(username, "msg7");
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