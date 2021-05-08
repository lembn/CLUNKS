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
        public static string[] entityTables = { "subservers", "rooms", "groups" };
        private static string[] _entityTables = { "subserver", "room", "group" };

        /// <summary>
        /// A method to check if a user exists on the database
        /// </summary>
        /// <param name="username">The username of the user</param>
        /// <returns>True if the user exists, false otherwise</returns>
        public static bool UserExists(string username)
        {
            using (Cursor cursor = new Cursor(connectionString))
                return cursor.Execute<int>("SELECT COUNT(*) FROM users WHERE name=$name", username) > 0;
        }

        /// <summary>
        /// A method to check if a user exists in a parent entity (subserver/room/group)
        /// </summary>
        /// <param name="entity">The name of the parent entity</param>
        /// <param name="username">The name of the user</param>
        /// <param name="checkPresent">Should be set to true if users need to be present for the method to return true, false otherwise</param>
        /// <returns>True if the users exists and checkPresent is false or if the user exists and is not present and checkPresent is true, false otherwise</returns>
        public static bool UserInEntity(string entity, string username)
        {
            string checkStmt =
            $@"
                SELECT userID, {{0}}ID
                FROM users_{{0}}s
                INNER JOIN users ON users.id=users_{{0}}s.userID
                INNER JOIN {{0}}s ON {{0}}s.id=users_{{0}}s.{{0}}ID
                WHERE users.name=$username
                AND {{0}}s.name=$parentName;
            ";
            string table = GetTable(entity);
            if (table == null)
                return false;
            object[] results;
            using (Cursor cursor = new Cursor(connectionString))
                results = cursor.Execute<object[]>(String.Format(checkStmt, table.Substring(0, table.Length - 1)), username, entity);
            if (results != null && results.Length > 0)
                return true;
            return false;
        }

        /// <summary>
        /// A method to check if a user is currently active
        /// </summary>
        /// <param name="username">The name of the user to check</param>
        /// <returns>True if the user is logged in on the database, false otherwise</returns>
        public static bool UserLoggedIn(string username)
        {
            using (Cursor cursor = new Cursor(connectionString))
                return cursor.Execute<int>("SELECT loggedIn FROM users WHERE name=$username", username) == 1;
        }

        /// <summary>
        /// A method to get the (hashed) password of an entity if it exists
        /// </summary>
        /// <param name="entityName">The name of the entity</param>
        /// <returns>The hashed password of the entity if it exists, empty string otherwise</returns>
        public static string GetEntityPassword(string entityName)
        {
            using (Cursor cursor = new Cursor(connectionString))
            {
                string table = GetTable(entityName);
                if (table != null)
                    return String.Empty;
                if ((from info in cursor.Execute<object[][]>($"PRAGMA table_info({table})") select (string)info[1]).Contains("password"))
                    return cursor.Execute<string>($"SELECT password FROM {table} WHERE name=$entityName;", entityName);
            }                                        
            return String.Empty;
        }

        /// <summary>
        /// A method to verify the username and password of a user
        /// </summary>
        /// <param name="username">The user's username</param>
        /// <param name="password">The user's (plaintext) password</param>
        /// <returns>True if the combination is correct, false otherwise</returns>
        public static bool LoginUser(string username, string password)
        {
            bool Update(Cursor cursor, bool state)
            {
                if (state)
                    cursor.Execute("UPDATE users SET loggedIn=1 WHERE name=$username;", username);
                return state;
            }

            using (Cursor cursor = new Cursor(connectionString))
            {
                string hash = cursor.Execute<string>("SELECT password FROM users WHERE name=$username;", username);
                if (String.IsNullOrEmpty(hash.Trim()))
                    if (String.IsNullOrEmpty(password.Trim()))
                        return Update(cursor, true);
                    else
                        return false;
                try
                {
                    return Update(cursor, BCrypt.Net.BCrypt.Verify(password, hash));
                }
                catch (BCrypt.Net.SaltParseException)
                {
                    return false;
                }                
            }
        }
        
        /// <summary>
        /// A method to obtain an entity's trace as a string
        /// </summary>
        /// <param name="trace">The current trace of the entity</param>
        /// <param name="cursor">The cursor to use for the operation</param>
        /// <returns>The trace of the entity</returns>
        public static string Trace(string trace, Cursor cursor = null)
        {
            if (cursor == null)
                cursor = new Cursor(connectionString);
            string bottom = trace.Split(" - ")[0];
            if (cursor.Execute<int>("SELECT COUNT(*) FROM subservers WHERE name=$entityName;", bottom) > 0)
            {
                cursor.Dispose();
                return trace;
            }
            int index = 1;
            if (cursor.Execute<int>("SELECT COUNT(*) FROM groups WHERE name=$entityName;", bottom) > 0)
                index = 2;
            int id = cursor.Execute<int>($"SELECT id FROM {entityTables[index]} WHERE name=$entityName;", bottom);
            int parentID;
            int offset = 0;
            parentID = cursor.Execute<int>($"SELECT parent from {_entityTables[index]}_{entityTables[index]} WHERE child='{id}';");
            if (parentID == 0)
            {
                parentID = cursor.Execute<int>($"SELECT {_entityTables[index - 1]}ID from {_entityTables[index - 1]}_{entityTables[index]} WHERE {_entityTables[index]}ID='{id}';");
                offset++;
            }
            return Trace($"{cursor.Execute<string>($"SELECT name from {entityTables[index - offset]} WHERE id='{parentID}';")} - {trace}", cursor);
        }

        /// <summary>
        /// A method to load EXP configurations into the database
        /// </summary>
        public static void LoadExp()
        {
            string table = "CREATE TABLE IF NOT EXISTS";
            string name = "name TEXT UNIQUE NOT NULL";
            string iBool = "INTEGER NOT NULL";
            string IPK = "id INTEGER PRIMARY KEY";
            File.WriteAllBytes(Regex.Match(connectionString, "(?<=(Data Source=)).*(?=;)").Value, new byte[0]);
            using (Cursor cursor = new Cursor(connectionString))
            {
                cursor.Execute(
                $@"
                    {table} elevations ({IPK}, {name}, canCallSubservers {iBool}, canCallRooms {iBool}, canCallGroups {iBool}, canCallUsers {iBool}, canMsgSubservers {iBool},
                                        canMsgRooms {iBool}, canMsgGroups {iBool}, canMsgUsers {iBool}, canCreateGroups {iBool});
                    {table} subservers ({IPK}, {name});
                    {table} rooms ({IPK}, {name}, password TEXT NOT NULL);
                    {table} subserver_rooms ({IPK}, subserverID INTEGER REFERENCES subservers(id), roomID INTEGER REFERENCES rooms(id), UNIQUE (subserverID, roomID));
                    {table} room_rooms ({IPK}, parent INTEGER REFERENCES rooms(id), child INTEGER REFERENCES rooms(id), UNIQUE (parent, child));
                    {table} users ({IPK}, {name}, password TEXT NOT NULL, elevation INTEGER REFERENCES elevations(id), loggedIn {iBool});
                    {table} users_subservers ({IPK}, userID INTEGER REFERENCES users(id), subserverID INTEGER REFERENCES subservers(id), present {iBool}, UNIQUE (userID, subserverID));
                    {table} users_rooms ({IPK}, userID INTEGER REFERENCES users(id), roomID INTEGER REFERENCES rooms(id), present {iBool}, UNIQUE (userID, roomID));
                    {table} groups ({IPK}, {name}, password TEXT NOT NULL, owner INTEGER references users(id));
                    {table} room_groups ({IPK}, roomID INTEGER REFERENCES rooms(id), groupID INTEGER REFERENCES groups(id), UNIQUE (roomID, groupID));
                    {table} group_groups ({IPK}, parent INTEGER REFERENCES groups(id), child INTEGER REFERENCES groups(id), UNIQUE (parent, child));
                    {table} users_groups ({IPK}, userID INTEGER REFERENCES users(id), groupID INTEGER REFERENCES groups(id), present {iBool}, UNIQUE (userID, groupID));
                    {table} notifications ({IPK}, sender INTEGER REFERENCES users(id), receiver INTEGER REFERENCES users(id), time TEXT, msg TEXT, isMsg {iBool}, isGlobal {iBool}, UNIQUE (sender, receiver, time));
                ");

                XDocument exp = XDocument.Load(Directory.GetFiles(Regex.Match(connectionString, "(?<=(Data Source=)).*(?=(\\\\.*db;))").Value, "*.exp")[0]);
                foreach (XElement elevation in exp.Descendants("elevation"))
                {
                    int[] paramList = (from num in Convert.ToString(Convert.ToInt32(elevation.Attribute("privilege").Value), 2).PadLeft(9, '0') select (int)Char.GetNumericValue(num)).ToArray();
                    cursor.Execute(
                    @" INSERT INTO elevations 
                        (name, canCallSubservers, canCallRooms, canCallGroups, canCallUsers, canMsgSubservers, canMsgRooms, canMsgGroups, canMsgUsers, canCreateGroups) 
                        VALUES($name, $param0, $param1, $param2, $param3, $param4, $param5, $param6, $param7, $param8);
                    ", elevation.Attribute("name").Value, paramList[0], paramList[1], paramList[2], paramList[3], paramList[4], paramList[5], paramList[6], paramList[7], paramList[8]);
                }

                IEnumerable<XElement> globalUsers = exp.Descendants("globalUsers").Descendants("user");

                foreach (XElement subserver in exp.Descendants("subserver"))
                {
                    cursor.Execute("INSERT INTO subservers (name) VALUES ($name);", subserver.Attribute("name").Value);
                    int subserverID = cursor.Execute<int>("SELECT last_insert_rowid();");

                    List<int> processed = new List<int>();
                    foreach (XElement user in subserver.Descendants("user").Concat(globalUsers))
                    {
                        if (!processed.Contains(user.ToString().GetHashCode()))
                        {
                            int elevationID = cursor.Execute<int>("SELECT id FROM elevations WHERE name=$name", user.Attribute("elevation").Value);
                            cursor.Execute("INSERT INTO users (name, password, elevation, loggedIn) VALUES ($name, $password, $elevationID, 0);", user.Attribute("username").Value, user.Attribute("password").Value, elevationID);
                            int userID = cursor.Execute<int>("SELECT last_insert_rowid();");
                            cursor.Execute("INSERT INTO users_subservers (userID, subserverID, present) VALUES ($userID, $parentID, 0);", userID, subserverID);
                            processed.Add(user.ToString().GetHashCode());
                        }
                    }

                    processed.Clear();                    
                    foreach (XElement room in subserver.Elements("room"))
                    {
                        if (!processed.Contains(room.ToString().GetHashCode()))
                        {
                            ProcessRoom(cursor, room, subserverID, globalUsers, false);
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
        private static void ProcessRoom(Cursor cursor, XElement room, int parentID, IEnumerable<XElement> globalUsers, bool parentIsRoom = true)
        {
            cursor.Execute("INSERT INTO rooms (name, password) VALUES ($name, $password)", room.Attribute("name").Value, room.Attribute("password").Value);
            int roomID = cursor.Execute<int>("SELECT last_insert_rowid();");
            cursor.Execute($"INSERT INTO {(parentIsRoom ? "room" : "subserver")}_rooms ({(parentIsRoom ? "parent, child" : "subserverID, roomID")}) VALUES ($parent, $roomID);", parentID, roomID);

            List<int> processed = new List<int>();
            foreach (XElement user in room.Descendants("user").Concat(globalUsers))
            {
                if (!processed.Contains(user.ToString().GetHashCode()))
                {
                    int userID = cursor.Execute<int>("SELECT id FROM users WHERE name=$name;", user.Attribute("username").Value);
                    cursor.Execute("INSERT INTO users_rooms (userID, roomID, present) VALUES ($userID, $parentID, 0);", userID, roomID);
                    processed.Add(user.ToString().GetHashCode());
                }
            }

            processed.Clear();
            foreach (XElement child in room.Elements("room"))
            {
                if (!processed.Contains(room.ToString().GetHashCode()))
                {
                    ProcessRoom(cursor, child, roomID, globalUsers);
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
                string table = GetTable(parentName);
                cursor.Execute(
                $@"
                    UPDATE users_{table}
                    SET present={(isPresent ? 1 : 0)}
                    WHERE EXISTS( SELECT *
			                        FROM users
			                        INNER JOIN {table} ON {table}.name=$entityname AND {table}.id=users_{table}.{table.Substring(0, table.Length - 1)}ID
			                        WHERE users.name=$username AND users.id=users_{table}.userID);
                ", parentName, username);
            }
        }

        /// <summary>
        /// A method to get the database id of a user
        /// </summary>
        /// <param name="username">The user's username</param>
        /// <returns>The user's id</returns>
        public static int GetUserID(string username)
        {
            using (Cursor cursor = new Cursor(connectionString))
                return cursor.Execute<int>("SELECT id FROM users WHERE name=$name", username);
        }

        /// <summary>
        /// A method to unset the presence of a user throughout the database
        /// </summary>
        /// <param name="userID">The ID of the user</param>
        /// <returns>The username of the user</returns>
        public static string Logout(int userID)
        {
            string username;
            using (Cursor cursor = new Cursor(connectionString))
            {
                cursor.Execute($"UPDATE users SET loggedIn=0 WHERE id='{userID}';");
                cursor.Execute($"UPDATE users_subservers SET present=0 userID='{userID}';");
                cursor.Execute($"UPDATE users_rooms SET present=0 userID='{userID}';");
                cursor.Execute($"UPDATE users_groups SET present=0 userID='{userID}';");
                int[] groups = Cursor.GetIntArray(cursor.Execute<object[]>($"SELECT id FROM groups WHERE owner='{userID}';"));
                if (cursor.CanRecover)
                    groups = new int[] { cursor.Recover<int>() };
                foreach (object groupID in groups)
                {
                    int newOwner = cursor.Execute<int>($"SELECT userID FROM users_groups WHERE groupID='{groupID}' LIMIT 1;");
                    cursor.Execute($"UPDATE groups SET owner={newOwner} WHERE id={groupID};");
                    if (newOwner == 0)
                    {
                        cursor.Execute($"DELETE FROM groups WHERE groupID={groupID}");
                        cursor.Execute($"DELETE FROM room_groups WHERE groupID={groupID}");
                        cursor.Execute($"DELETE FROM group_groups WHERE groupID={groupID}");
                    }
                }
                username = cursor.Execute<string>($"SELECT name FROM users WHERE id='{userID}';");
            }
            return username;
        }

        /// <summary>
        /// A method to create a group in the database
        /// </summary>
        /// <param name="name">The name of the group</param>
        /// <param name="password">The password to apply to the group</param>
        /// <param name="userID">The database ID of the user creating the group</param>
        /// <returns>True if the group was successfully created, false otherwise</returns>
        public static bool MakeGroup(string name, string password, string parentName, int userID)
        {
            using (Cursor cursor = new Cursor(connectionString))
            {
                if (cursor.Execute<int>($"SELECT COUNT(*) FROM groups WHERE name=$name;", name) > 0)
                    return false;
                int elevationID = cursor.Execute<int>($"SELECT elevation FROM users WHERE id='{userID}';");
                if (cursor.Execute<int>($"SELECT canCreateGroup FROM elevations WHERE id='{elevationID}';") == 0)
                    return false;
                cursor.Execute($"INSERT INTO groups (name, password, owner) VALUES ($name, $password, {userID});", name, password);
                int groupID = cursor.Execute<int>($"SELECT last_insert_rowid();");
                int parentID;
                int index = 1;
                parentID = cursor.Execute<int>($"SELECT id FROM rooms WHERE name=$parentName;", parentName);
                if (parentID == 0)
                {
                    parentID = cursor.Execute<int>($"SELECT id FROM groups WHERE name=$parentName;", parentName);
                    index = 2;
                }
                cursor.Execute($"INSERT INTO {(index == 1 ? "room_groups (roomID, groupID)" : "group_groups (parent, child)")} VALUES ({parentID}, {groupID});");
                int[] users = Cursor.GetIntArray(cursor.Execute<object[]>($"SELECT userID FROM users_{entityTables[index]} WHERE {_entityTables[index]}ID='{parentID}';"));
                if (cursor.CanRecover)
                    users = new int[] { cursor.Recover<int>() };
                foreach (object user in users)
                    cursor.Execute($"INSERT INTO users_groups (userID, groupID, present) VALUES ({user}, {groupID}, 0);");
                return true;
            }
        }

        /// <summary>
        /// A method to clear all group data from the database
        /// </summary>
        public static void DestroyGroups()
        {
            using (Cursor cursor = new Cursor(connectionString))
            {
                cursor.Execute("DELETE FROM groups");
                cursor.Execute("DELETE FROM users_groups");
            }
        }

        /// <summary>
        /// A method to get an array of ids of users currently logged into an entity. Only users who 
        /// have the given entity at the top of their trace are returned
        /// </summary>
        /// <param name="entityName">The name of the entity</param>
        /// <param name="inactive">A boolean representing if logged in or logged out users should be checked</param>
        /// <param name="ignore">A userID to ignore</param>
        /// <returns>The desired user list and an array</returns>
        public static int[] GetActiveUsers(string entityName, bool inactive = false)
        {
            object[] users = null;
            using (Cursor cursor = new Cursor(connectionString))
            {
                string table = GetTable(entityName);
                users = cursor.Execute<object[]>(@$"SELECT userID FROM users_{table}
                                                 INNER JOIN {table} ON {table}.name='{entityName}'
                                                 INNER JOIN users ON users.id=users_{table}.userID
                                                 WHERE loggedIn={(inactive ? 0 : 1)};");
                if (cursor.CanRecover)
                    users = new object[] { cursor.Recover<int>() };
            }                           
            return Cursor.GetIntArray(users);
        }

        /// <summary>
        /// A method to create a notification on the database
        /// </summary>
        /// <param name="sender">The username of the sender of the notification</param>
        /// <param name="recipientID">The ID of the recipient user</param>
        /// <param name="message">The notification message</param>
        /// <param name="isMsg">A bool to represent if the notification is for a missed message or not</param>
        /// <param name="isGlobal">A bool to represent if the notification is for a global event or not</param>
        public static void CreateNotification(string sender, int recipientID, string message, bool isMsg, bool isGlobal = false)
        {
            using (Cursor cursor = new Cursor(connectionString))
                cursor.Execute($@"INSERT INTO notifications (sender, receiver, time, msg, isMsg, isGlobal)
                                  VALUES ({GetUserID(sender)}, {recipientID}, '{DateTime.UtcNow}', '{message}', {(isMsg ? 1 : 0)}, {(isGlobal ? 1 : 0)});");
        }

        /// <summary>
        /// A method to check if a user has the privelleges to message a certain type of entity
        /// </summary>
        /// <param name="userID">The ID of the user to check</param>
        /// <param name="type">The type of entity to check against</param>
        /// <returns>True if the user can message, false otherwise</returns>
        public static bool CanMessage(int userID, string type)
        {
            using (Cursor cursor = new Cursor(connectionString))
            {
                int elevationID = cursor.Execute<int>($"SELECT elevation FROM users WHERE id='{userID}';");
                return cursor.Execute<int>($"SELECT canMsg{type} FROM elevations WHERE id='{elevationID}';") == 1;
            }
        }

        /// <summary>
        /// A method to find the table that an entity exists in
        /// </summary>
        /// <param name="entityName">The name of the entity</param>
        /// <returns>The name of the table</returns>
        public static string GetTable(string entityName, bool capitalize = false)
        {
            using (Cursor cursor = new Cursor(connectionString))
                foreach (string table in entityTables)
                    if (cursor.Execute<int>($"SELECT COUNT(*) FROM {table} WHERE name=$parentName;", entityName) > 0)
                    {
                        if (capitalize)
                            return Char.ToUpper(table[0]) + table.Substring(1);
                        else
                            return table;
                    }
                        
            return null;
        }
    }
}
