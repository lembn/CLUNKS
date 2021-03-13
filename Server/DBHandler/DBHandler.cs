using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Server.DBHandler
{
    //TODO: Summarise
    internal static class DBHandler
    {
        public static string connectionString; //The connection string to use when connecting to the database

        /// <summary>
        /// A method to check if a user exists in a parent entity (subserver/room/group)
        /// </summary>
        /// <param name="parentName">The name of the parent entity</param>
        /// <param name="username">The name of the user</param>
        /// <returns>True if the users exists, false otherwise</returns>
        public static bool CheckUser(string parentName, string username)
        {
            string stmt =
            $@"
                SELECT COUNT(*)
                FROM users_{{0}}s
                INNER JOIN users ON users.id=users_{{0}}s.userID
                INNER JOIN {{0}}s ON {{0}}s.id=users_{{0}}s.{{0}}ID
                WHERE users.name='{username}'
                AND {{0}}s.name='{parentName}';
            ";
            using (Cursor cursor = new Cursor(connectionString))
                foreach (string table in GetTables(cursor))
                    if (Convert.ToInt32(cursor.Execute($"SELECT count(*) FROM {table} WHERE name=$parentName;", parentName)) > 0)
                        return Convert.ToInt32(cursor.Execute(String.Format(stmt, table.Substring(0, table.Length - 1)))) > 0;
            return false;
        }

        /// <summary>
        /// A method to verify the username and password of a user
        /// </summary>
        /// <param name="username">The user's username</param>
        /// <param name="password">The user's (plaintext) password</param>
        /// <returns>True if the combination is correct, false otherwise</returns>
        public static bool Login(string username, string password)
        {
            using (Cursor cursor = new Cursor(connectionString))
            {
                string hash = (string)cursor.Execute("SELECT password FROM users WHERE name=$username;", username);
                if (hash.Trim() == "" && password.Trim() == "")
                    return true;
                try
                {
                    return BCrypt.Net.BCrypt.Verify(password, hash);
                }
                catch (BCrypt.Net.SaltParseException)
                {
                    return false;
                }                
            }
        }

        /// <summary>
        /// A method to load EXP configurations into the database
        /// </summary>
        public static void LoadExp()
        {
            string table = "CREATE TABLE IF NOT EXISTS";
            string name = "name TEXT UNIQUE NOT NULL";
            string iBool = "INTEGER NOT NULL";
            string IPK = $"id INTEGER PRIMARY KEY";
            File.WriteAllBytes(Regex.Match(connectionString, "(?<=(Data Source=)).*(?=;)").Value, new byte[0]);
            using (Cursor cursor = new Cursor(connectionString))
            {
                cursor.Execute(
                $@"
                    {table} elevations ({IPK}, {name}, canCallSubserver {iBool}, canCallRoom {iBool}, canCallGroup {iBool}, canCallUser {iBool}, canMsgSubserver {iBool},
                                        canMsgRoom {iBool}, canMsgGroup {iBool}, canMsgUser {iBool}, canCreateRoom {iBool}, canCreateGroup {iBool});
                    {table} subservers ({IPK}, {name});
                    {table} rooms ({IPK}, {name}, password TEXT NOT NULL);
                    {table} subserver_rooms ({IPK}, subserverID INTEGER REFERENCES subservers(id), roomID INTEGER REFERENCES rooms(id), UNIQUE (subserverID, roomID));
                    {table} room_rooms ({IPK}, parentRoom INTEGER REFERENCES rooms(id), childRoom INTEGER REFERENCES rooms(id), UNIQUE (parentRoom, childRoom));
                    {table} users ({IPK}, {name}, password TEXT NOT NULL, elevation INTEGER REFERENCES elevations(id));
                    {table} users_subservers ({IPK}, userID INTEGER REFERENCES users(id), subserverID INTEGER REFERENCES subservers(id), present {iBool}, UNIQUE (userID, subserverID));
                    {table} users_rooms ({IPK}, userID INTEGER REFERENCES users(id), roomID INTEGER REFERENCES rooms(id), present {iBool}, UNIQUE (userID, roomID));
                    {table} groups ({IPK}, {name}, password TEXT NOT NULL, owner INTEGER references users(id));
                    {table} room_groups ({IPK}, roomID INTEGER REFERENCES rooms(id), groupID INTEGER REFERENCES groups(id), UNIQUE (roomID, groupID));
                    {table} group_groups ({IPK}, parentGroup INTEGER REFERENCES groups(id), childGroup INTEGER REFERENCES groups(id), UNIQUE (parentGroup, childGroup));
                    {table} users_groups ({IPK}, userID INTEGER REFERENCES users(id), groupID INTEGER REFERENCES groups(id), present {iBool}, UNIQUE (userID, groupID));
                    {table} notifications ({IPK}, sender INTEGER REFERENCES users(id), receiver INTEGER REFERENCES users(id), time INTEGER, msg TEXT, type TEXT, UNIQUE (sender, receiver, time));
                ");

                XDocument exp = XDocument.Load(Directory.GetFiles(Regex.Match(connectionString, "(?<=(Data Source=)).*(?=(\\\\.*db;))").Value, "*.exp")[0]);
                foreach (XElement elevation in exp.Descendants("elevation"))
                {
                    int[] paramList = (from num in Convert.ToString(Convert.ToInt32(elevation.Attribute("privilege").Value), 2).PadLeft(10, '0') select (int)Char.GetNumericValue(num)).ToArray();
                    cursor.Execute(
                    $@" INSERT INTO elevations 
                        (name, canCallSubserver, canCallRoom, canCallGroup, canCallUser, canMsgSubserver, canMsgRoom, canMsgGroup, canMsgUser, canCreateRoom, canCreateGroup) 
                        VALUES($name, $param0, $param1, $param2, $param3, $param4, $param5, $param6, $param7, $param8, $param9);
                    ", elevation.Attribute("name").Value, paramList[0], paramList[1], paramList[2], paramList[3], paramList[4], paramList[5], paramList[5], paramList[6], paramList[7], paramList[8], paramList[9]);
                }

                foreach (XElement subserver in exp.Descendants("subserver"))
                {
                    cursor.Execute("INSERT INTO subservers (name) VALUES ($name);", subserver.Attribute("name").Value);
                    int subserverID = Convert.ToInt32(cursor.Execute("SELECT last_insert_rowid();"));

                    List<int> processed = new List<int>();
                    foreach (XElement user in subserver.Elements("user"))
                    {
                        if (!processed.Contains(user.ToString().GetHashCode()))
                        {
                            int elevationID = Convert.ToInt32(cursor.Execute("SELECT id FROM elevations WHERE name=$name", user.Attribute("elevation").Value));
                            cursor.Execute("INSERT INTO users (name, password, elevation) VALUES ($name, $password, $elevationID);", user.Attribute("username").Value, user.Attribute("password").Value, elevationID);
                            int userID = Convert.ToInt32(cursor.Execute("SELECT last_insert_rowid();"));
                            cursor.Execute($"INSERT INTO users_subservers (userID, subserverID, present) VALUES ($userID, $parentID, 0);", userID, subserverID);
                            processed.Add(user.ToString().GetHashCode());
                        }
                    }

                    processed.Clear();
                    var a = subserver.Descendants("room").ToArray();
                    foreach (XElement room in subserver.Descendants("room"))
                    {
                        if (!processed.Contains(room.ToString().GetHashCode()))
                        {
                            ProcessRoom(cursor, room, subserverID, false);
                            processed.Add(room.ToString().GetHashCode());
                        }
                    }
                }
            }
        }

        /// <summary>
        /// A method to recursively add a room into the database from and EXP
        /// </summary>
        /// <param name="cursor">The Cursor to use</param>
        /// <param name="room">The EXP representaion of the room</param>
        /// <param name="parentID">The database id of the parent of the room</param>
        /// <param name="parentIsRoom">A boolean to represent if the parent of the room is another room</param>
        private static void ProcessRoom(Cursor cursor, XElement room, int parentID, bool parentIsRoom = true)
        {
            cursor.Execute("INSERT INTO rooms (name, password) VALUES ($name, $password)", room.Attribute("name").Value, room.Attribute("password").Value);
            int roomID = Convert.ToInt32(cursor.Execute("SELECT last_insert_rowid();"));
            cursor.Execute($"INSERT INTO {(parentIsRoom ? "room" : "subserver")}_rooms ({(parentIsRoom ? "parentRoom, childRoom" : "subserverID, roomID")}) VALUES ($parent, $roomID);", parentID, roomID);

            List<int> processed = new List<int>();
            foreach (XElement user in room.Elements("user"))
            {
                if (!processed.Contains(user.ToString().GetHashCode()))
                {
                    int userID = Convert.ToInt32(cursor.Execute("SELECT id FROM users WHERE name=$name;", user.Attribute("username").Value));
                    cursor.Execute($"INSERT INTO users_rooms (userID, roomID, present) VALUES ($userID, $parentID, 0);", userID, roomID);
                    processed.Add(user.ToString().GetHashCode());
                }
            }

            processed.Clear();
            foreach (XElement child in room.Descendants("room"))
            {
                if (!processed.Contains(room.ToString().GetHashCode()))
                {
                    ProcessRoom(cursor, child, roomID);
                    processed.Add(room.ToString().GetHashCode());
                }
            }
        }

        /// <summary>
        /// A method to set a user as present in a parent entity
        /// </summary>
        /// <param name="parentName">The name of the parent entity</param>
        /// <param name="username">The username of the user</param>
        public static void SetPresent(string parentName, string username, bool isPresent = true)
        {
            using (Cursor cursor = new Cursor(connectionString))
            {
                foreach (string table in GetTables(cursor))
                    if (Convert.ToInt32(cursor.Execute($"SELECT count(*) FROM {table} WHERE name=$parentName;", parentName)) > 0)
                        cursor.Execute(
                        $@"
                            UPDATE users_{table}
                            SET present={(isPresent ? 1 : 0)}
                            WHERE EXISTS(
                                SELECT *
                                FROM users_{table}
                                INNER JOIN users ON users.name=$name AND users_{table}.userID=users.id
                                INNER JOIN {table} ON users_{table}.subserverID={table.Substring(0, table.Length - 1)}.id);
                        ", username);
            }
        }

        /// <summary>
        /// A method to get an array containg the names of all the tables in the database
        /// </summary>
        /// <param name="cursor">The Cursor to use</param>
        /// <returns>Array containg the names of all the tables in the database</returns>
        private static string[] GetTables(Cursor cursor) =>
            (from table in (object[])cursor.Execute("SELECT sql FROM sqlite_schema WHERE type='table';")
             where table.ToString().Contains("name")
             select Regex.Match(table.ToString(), @"(?<=CREATE TABLE\s)\w*").Value)
             .ToArray();
    }
}
