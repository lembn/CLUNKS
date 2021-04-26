using Common.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
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
    /// alert its owner when an item in the queue is renoved. Items are added to the back
    /// of the queue and removed from the back, creating a reverse LIFO structure
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
    /// A Thread safe class to represent the incoming message feed of a user
    /// </summary>
    public static class Feed
    {
        #region Private Members

        private static CircularQueue<int> buffer;
        private static List<List<KeyValuePair<string, ConsoleColor>>> lines;
        private static int pointer; //points to the bottom visible line
        private static int size;
        private static int top;
        private static int bottom;
        private static int width;
        private static string LIVEFEED = "[INCOMING FEED - (LIVE)]";
        private static string OLDFEED = "[INCOMING FEED - (▼)]";
        private static string DEADFEED = "[FEED - (DEACTIVATED)]";
        private static bool notification;
        private static bool alive;
        private static bool deactivating;
        private static CancellationTokenSource sleeper;

        #endregion

        public static string YOU;

        /// <summary>
        /// A method to setup the Feed class
        /// </summary>
        /// <param name="size">The number of lines the feed should display</param>
        /// <param name="capacity">The number of messages the feed should store</param>
        public static void Initialise(int size, int capacity)
        {
            Feed.size = size;
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
            ///if Show has been called and alive is already true then 
            ///it means the user is trying to get a new feed while one
            ///is already present so the old one should be decactivated
            ///if the deacitivation process hasn't already started
            if (alive && !deactivating)
                Deactivate();
            Console.WriteLine();
            top = Console.CursorTop;
            width = Console.WindowWidth;
            PrintHeader(LIVEFEED);
            Console.CursorTop++;
            Console.Write(new string('\n', size) + new string('=', Console.WindowWidth));
            bottom = Console.CursorTop - 1;
            Update((Console.CursorLeft, Console.CursorTop));            
            alive = true;
            sleeper.Dispose();
            sleeper = new CancellationTokenSource();
            ThreadHelper.GetECThread(sleeper.Token, () =>
            {
                ///deactivate if...
                /// - cursor is at the bottom of the console
                /// - console been scrolled far enough for the feed to be off screen
                /// - feed is still alive
                if ((Console.CursorTop > Console.WindowHeight - 1) && (Console.CursorTop >= size + 2 + (Console.WindowHeight - 1)) && alive)
                    Deactivate();
                else
                    sleeper.Token.WaitHandle.WaitOne(3000);
            }).Start();
        }

        /// <summary>
        /// A method to add new messages to the feed
        /// </summary>
        /// <param name="username">The username of the sender of the message</param>
        /// <param name="message">The message content</param>
        /// <param name="entity">The entity that the message was sent to (for global chat)</param>
        /// <param name="default">The default console colour</param>
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
            if (pointer > 0)
            {   
                PrintHeader(OLDFEED);
                notification = true;
                pointer++;
            }
            int linesAdded = 1;
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
                        linesAdded++;
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
            lock (buffer)
                buffer.Enqueue(linesAdded);
            if (alive)
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
                    else
                    {
                        pointer--;
                        if (pointer == 0 && notification)
                        {
                            PrintHeader(LIVEFEED);
                            notification = false;
                        }
                    }
                }
            Update((Console.CursorLeft, Console.CursorTop));
            }
        }

        /// <summary>
        /// A method to set the feed to inactive
        /// </summary>
        private static void Deactivate()
        {
            deactivating = true;
            PrintHeader(DEADFEED);
            alive = false;
            pointer = 0;
            deactivating = false;
        }

        /// <summary>
        /// A method invoked by an event to act as an event handler used to remove a range
        /// of elements from lines.
        /// </summary>
        /// <param name="sender">The object who triggered the event</param>
        /// <param name="e">The event args</param>
        private static void RemoveLines(object sender, CQRemoveEventArgs<int> e)
        {
            lock (lines)
                lines.RemoveRange(lines.Count - (e.item + 1), e.item);
        }

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
            Console.CursorLeft = 0;
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
            int orignalTop = Console.CursorTop;
            int orignalLeft = Console.CursorLeft;
            Console.SetCursorPosition(0, top);
            /// If window width and header length are both odd or both even
            /// then they are fine, if one is even and the other is even then
            /// the header needs to be padded to match the window width
            if ((Console.WindowWidth % 2 == 0) != (header.Length % 2 == 0))
                header = $"{header}-";
            string dashes = new string('-', (width - header.Length) / 2);
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(dashes + header + dashes);
            Console.SetCursorPosition(orignalLeft, orignalTop);
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
            //Feed.Add(username, new string('b', 680) + "end", "test");
            //Feed.Add(username, "msg1", "test");
            //Feed.Add(username, new string('b', 120) + "end", "test");
            //Feed.Add(username, "msg2");
            //Feed.Add(username, "msg3");
            //Feed.Add(username, "msg4");
            //Feed.Scroll(true);
            //Feed.Add(username, "msg5");
            //Feed.Add(username, "msg6");
            //Feed.Add(username, "msg7");
            Task.Run(() =>
            {
                int counter = 0;
                while (true)
                {
                    //if (counter == 7)
                    //    Feed.Show();
                    Feed.Add(username, $"msg{++counter}", "test");
                    Thread.Sleep(1500);
                }
            });
            Task.Run(() =>
            {
                while (true)
                    Console.Read();
            });
        }
    }
}