using Common.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;

namespace Client.Feed
{
    public static class Feed
    {
        #region Private Members

        private static List<List<KeyValuePair<string, ConsoleColor>>> lines;
        private static int pointer; //points to the bottom visible line
        private static int size;
        private static int top;
        private static int bottom;
        private static int width;
        private static string LIVEFEED = "[INCOMING FEED - (LIVE)]";
        private static string OLDFEED = "[INCOMING FEED - (▼)]";
        private static string DEADFEED = "[FEED - (DEACTIVATED)]";
        private static bool updated = true;
        private static bool deactivating;
        private static CancellationTokenSource sleeper;
        private static int saveCount;
        private static object saveLock;
        private static string tempLoc;

        #endregion

        #region Public Members

        public static string YOU;
        public static State isAlive;
        
        #endregion

        /// <summary>
        /// A method to setup the Feed class
        /// </summary>
        /// <param name="size">The number of lines the feed should display</param>
        public static void Initialise(int size)
        {
            Feed.size = size;
            isAlive = new State(false);
            lines = new List<List<KeyValuePair<string, ConsoleColor>>>();
            sleeper = new CancellationTokenSource();
            saveLock = new object();
            string commonPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            tempLoc = Path.Combine($@"{commonPath}\CLUNKS\temp");
            Directory.CreateDirectory(tempLoc);
        }

        /// <summary>
        /// A method to display the feed
        /// </summary>
        public static void Show()
        {
            ///if Show has been called and alive is already true then 
            ///it means the user is trying to get a new feed while one
            ///is already present so the old one should be decactivated
            ///if the deacitivation process hasn't already started
            if (isAlive)
                Deactivate(false);
            Console.WriteLine();
            top = Console.CursorTop;
            width = Console.WindowWidth;
            PrintHeader(LIVEFEED);
            Console.CursorTop++;
            Console.Write(new string('\n', size) + new string('=', Console.WindowWidth));
            bottom = Console.CursorTop - 1;
            if (File.Exists(Path.Combine(tempLoc, "msgs.xml")))
            {
                XDocument saveData = XDocument.Load(Path.Combine(tempLoc, "msgs.xml"));
                lines = new List<List<KeyValuePair<string, ConsoleColor>>>();
                foreach (XElement line in saveData.Descendants("line"))
                {
                    lines.Add(new List<KeyValuePair<string, ConsoleColor>>());
                    foreach (XElement entry in line.Descendants("entry"))
                        lines[lines.Count - 1].Add(new KeyValuePair<string, ConsoleColor>(entry.Attribute("text").Value, (ConsoleColor)Convert.ToInt32(entry.Attribute("colour").Value)));
                }
            }
            pointer = 0;
            Update((Console.CursorLeft, Console.CursorTop));
            isAlive = true;
            ThreadHelper.GetECThread(sleeper.Token, () =>
            {
                if ((Console.CursorTop - bottom > Console.WindowHeight) && isAlive)
                    Deactivate(true);
                else
                    sleeper.Token.WaitHandle.WaitOne(10); //3000
            }).Start();
        }

        /// <summary>
        /// A method to add new messages to the feed
        /// </summary>
        /// <param name="username">The username of the sender of the message</param>
        /// <param name="message">The message content</param>
        /// <param name="entity">The entity that the message was sent to (for global chat)</param>
        /// <param name="default">The default console colour</param>
        public static void Add(string username, string message, string entity, ConsoleColor @default = ConsoleColor.Gray)
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
            if (pointer > 0 && updated)
            {
                PrintHeader(OLDFEED);
                updated = false;
                pointer++;
            }
            int linesAdded = 1;
            int counter = 0;
            List<List<KeyValuePair<string, ConsoleColor>>> insertList;
            lock (isAlive)
            {
                if (isAlive)
                    insertList = lines;
                else
                    insertList = new List<List<KeyValuePair<string, ConsoleColor>>>();
                insertList.Insert(0, new List<KeyValuePair<string, ConsoleColor>>());
                lock (insertList)
                {
                    for (int i = 0; i < text.Count; i++)
                    {
                        if (counter + text[i].Length > width)
                        {
                            int overflow = counter + text[i].Length - width;
                            insertList[0].Add(new KeyValuePair<string, ConsoleColor>(text[i].Substring(0, text[i].Length - overflow), colours[i]));
                            insertList.Insert(0, new List<KeyValuePair<string, ConsoleColor>>());
                            pointer++;
                            linesAdded++;
                            counter = 0;
                            text.Insert(i + 1, text[i].Substring(text[i].Length - overflow, overflow));
                            colours.Insert(i + 1, colours[i]);
                        }
                        else
                        {
                            insertList[0].Add(new KeyValuePair<string, ConsoleColor>(text[i], colours[i]));
                            counter += text[i].Length;
                        }
                    }
                }
                lock (saveLock)
                    if (!isAlive)
                        Save(insertList);
                lock (lines)
                    if (isAlive && updated)
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
            lock (lines)
            {
                if (lines.Count < size)
                    return;
                if (scrollUp)
                {
                    if (pointer + size == lines.Count)
                        return;
                    else
                    {
                        pointer++;
                        if (pointer > 0 && updated)
                        {
                            PrintHeader(OLDFEED);
                            updated = false;
                        }
                    }
                }
                else
                {
                    if (pointer == 0)
                        return;
                    else
                    {
                        pointer--;
                        if (pointer == 0 && !updated)
                        {
                            PrintHeader(LIVEFEED);
                            updated = true;
                        }
                    }
                }
                Update((Console.CursorLeft, Console.CursorTop));
            }
        }

        /// <summary>
        /// Cleanup the resources held by the Feed class
        /// </summary>
        public static void Cleanup()
        {
            foreach (string filename in Directory.GetFiles(tempLoc))
                File.Delete(filename);
            Directory.Delete(tempLoc);
        }

        /// <summary>
        /// A method to set the feed to inactive
        /// </summary>
        public static void Deactivate(bool save)
        {
            lock (isAlive)
            {
                if (!isAlive)
                    return;
                if (deactivating)
                    return;
                PrintHeader(DEADFEED);
                isAlive = false;
            }
            lock (saveLock)
                if (save)
                    saveCount = Save(lines, saveCount);
            deactivating = false;
        }

        /// <summary>
        /// A method to save lines to the temporary file
        /// </summary>
        /// <param name="linesToSave">A list of lines to save</param>
        /// <param name="offset">The number of elements of 'linesToSave' to skip</param>
        /// <returns>The number of saved lines</returns>
        private static int Save(List<List<KeyValuePair<string, ConsoleColor>>> linesToSave, int offset = 0)
        {
            XDocument saveData;
            XElement root;
            FileStream stream = null;
            string path = Path.Combine(tempLoc, "msgs.xml");
            if (File.Exists(path))
            {
                saveData = XDocument.Load(path);
                root = saveData.Root;
            }
            else
            {
                stream = File.Create(path);
                saveData = new XDocument();
                root = new XElement("root");
                saveData.Add(root);
            }
            foreach (List<KeyValuePair<string, ConsoleColor>> lineData in linesToSave.Skip(offset))
            {
                XElement current = new XElement("line");
                foreach (KeyValuePair<string, ConsoleColor> data in lineData)
                    current.Add(new XElement("entry", new XAttribute("text", data.Key), new XAttribute("colour", (int)data.Value)));
                root.Add(current);
            }
            if (stream == null)
                saveData.Save(path);
            else
            {
                saveData.Save(stream);
                stream.Close();
                stream.Dispose();
            }
            int savedLines = lines.Count;
            linesToSave.Clear();
            return savedLines;
        }

        /// <summary>
        /// A method to update the display of the feed
        /// </summary>
        /// <param name="original">The X-Y coordinates of the Console's cursor before Update was called</param>
        private static void Update((int, int) original)
        {
            int counter = 0;
            lock (lines)
            {
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
}
