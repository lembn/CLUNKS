using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Server.DBHandler
{
    //TODO: Summarise
    internal static class DBHandler
    {
        public static string connectionString;

        public static void CreateDB(string path)
        {
            string table = "CREATE TABLE IF NOT EXISTS";
            string name = "name TEXT UNIQUE NOT NULL";
            string iBool = "INTEGER NOT NULL";
            string IPK = $"id INTEGER PRIMARY KEY";
            string create =
            $@"
                {table} elevations ({IPK}, {name}, canCallSubserver {iBool}, canCallRoom {iBool}, canCallGroup {iBool},
                                    canCallUser {iBool}, canMsgSubserver {iBool}, canMsgRoom {iBool}, canMsgGroup {iBool},
                                    canMsgUser {iBool}, canCreateRoom {iBool}, canCreateGroup {iBool});
                {table} subservers ({IPK}, {name});
                {table} rooms ({IPK}, {name}, password TEXT NOT NULL);
                {table} subserver_rooms ({IPK}, subserverID INTEGER REFERENCES subservers(id), roomID INTEGER REFERENCES rooms(id), UNIQUE (subserverID, roomID));
                {table} room_rooms ({IPK}, parentRoom INTEGER REFERENCES rooms(id), childRoom INTEGER REFERENCES rooms(id), UNIQUE (parentRoom, childRoom));
                {table} users ({IPK}, user{name}, password TEXT NOT NULL, elevation INTEGER REFERENCES elevations(id));
                {table} users_subservers ({IPK}, userID INTEGER REFERENCES users(id), subserverID INTEGER REFERENCES subservers(id), present {iBool}, UNIQUE (userID, subserverID));
                {table} users_rooms ({IPK}, userID INTEGER REFERENCES users(id), roomID INTEGER REFERENCES rooms(id), present {iBool}, UNIQUE (userID, roomID));
                {table} groups ({IPK}, {name}, password TEXT NOT NULL, owner INTEGER references users(id));
                {table} room_groups ({IPK}, roomID INTEGER REFERENCES rooms(id), groupID INTEGER REFERENCES groups(id), UNIQUE (roomID, groupID));
                {table} group_groups ({IPK}, parentGroup INTEGER REFERENCES groups(id), childGroup INTEGER REFERENCES groups(id), UNIQUE (parentGroup, childGroup));
                {table} users_groups ({IPK}, userID INTEGER REFERENCES users(id), groupID INTEGER REFERENCES groups(id), present {iBool}, UNIQUE (userID, groupID));
                {table} notifications ({IPK}, sender INTEGER REFERENCES users(id), receiver INTEGER REFERENCES users(id), time INTEGER, msg TEXT, type TEXT, UNIQUE (sender, receiver, time));
            ";

            File.WriteAllBytes(path, new byte[0]);
            using (Cursor cursor = new Cursor(connectionString))
                cursor.Execute(create);
        }

        public static bool CheckUser(string parentName, string username)
        {
            string stmt =
            $@"
                SELECT COUNT(*) FROM {{0}}s, users, users_{{0}}s
                WHERE users.username='{username}'
                AND {{0}}s.name='{parentName}'
                AND {{0}}s.id=users_{{0}}s.{{0}}ID
                AND users.id=users_{{0}}s.userID;
            ";
            using (Cursor cursor = new Cursor(connectionString))
            {
                string[] tables = (from table in (object[])cursor.Execute("SELECT sql FROM sqlite_schema WHERE type='table';")
                                   where table.ToString().Contains("name")
                                   select Regex.Match(table.ToString(), @"(?<=CREATE TABLE\s)\w*").Value)
                                   .ToArray();
                foreach (string table in tables)
                    if (Convert.ToInt32(cursor.Execute($"SELECT count(*) FROM {table} WHERE name=$parentName;", parentName)) > 0)
                        return Convert.ToInt32(cursor.Execute(String.Format(stmt, table.Substring(0, table.Length - 1)))) > 0;
            }
            return false;
        }

        public static bool Login(string username, string password)
        {
            using (Cursor cursor = new Cursor(connectionString))
            {
                string hash = (string)cursor.Execute("SELECT password FROM users WHERE username=$username;", username);
                if (hash.Trim() == "" && password.Trim() == "")
                    return true;
                return BCrypt.Net.BCrypt.Verify(password, hash);
            }
        }

        public static void LoadExp(string dataPath)
        {
            if (!File.Exists($@"{dataPath}\data.db"))
                CreateDB($@"{dataPath}\data.db");

            using (Cursor cursor = new Cursor(connectionString))
            {
                XDocument exp = XDocument.Load(Directory.GetFiles(dataPath, "*.exp")[0]);
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
                    cursor.Execute("INSERT INTO subservers (name) VALUES ($name);", subserver.Attribute("name").ToString());
                    int subserverID = Convert.ToInt32(cursor.Execute("SELECT last_insert_rowid();"));

                    foreach (XElement user in subserver.Elements("user"))
                        AddUser(cursor, user, subserverID, false);

                    foreach (XElement room in subserver.Elements("room"))
                        ProcessRoom(cursor, room, subserverID, false);
                }
            }
        }

        private static void AddUser(Cursor cursor, XElement user, int parentID, bool parentIsRoom = true)
        {
            int elevationID = Convert.ToInt32(cursor.Execute("SELECT id FROM elevations WHERE name=$name", user.Attribute("elevation").Value));
            cursor.Execute("INSERT INTO users (username, password, elevation) VALUES ($name, $password, $elevationID);", user.Attribute("username").Value, user.Attribute("password").Value, elevationID);
            int userID = Convert.ToInt32(cursor.Execute("SELECT last_insert_rowid();"));
            string parent = parentIsRoom ? "room" : "subserver";
            cursor.Execute($"INSERT INTO users_{parent} (userID, {parent}ID) VALUES ($userID, $parentID);", userID, parentID);
        }

        private static void ProcessRoom(Cursor cursor, XElement room, int parentID, bool parentIsRoom = true)
        {
            cursor.Execute("INSERT INTO rooms (name, password) VALUES ($name, $password)", room.Attribute("name").ToString(), room.Attribute("password").ToString());
            int roomID = Convert.ToInt32(cursor.Execute("SELECT last_insert_rowid();"));
            cursor.Execute($"INSERT INTO {(parentIsRoom ? "room" : "subserver")}_rooms ({(parentIsRoom ? "parentRoom, childRoom" : "subserverID, roomID")}) VALUES ($parent, $roomID);", parentID, roomID);

            foreach (XElement user in room.Elements("user"))
                AddUser(cursor, user, roomID);

            foreach (XElement child in room.Elements("room"))
                ProcessRoom(cursor, child, roomID);
        }
    }
}
