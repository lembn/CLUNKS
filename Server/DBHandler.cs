using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Server
{
    //TODO: Summarise
    internal static class DBHandler
    {
        internal static string connectionString;

        internal static void LoadExp(string dataPath)
        {
            if (!File.Exists($@"{dataPath}\data.db"))
                CreateDB($@"{dataPath}\data.db");

            //TODO: Change connection strings
            using (SqliteConnection connection = new SqliteConnection(connectionString))
            using (SqliteCommand command = new SqliteCommand())
            {
                connection.Open();
                command.Connection = connection;                
                XDocument exp = XDocument.Load(Directory.GetFiles(dataPath, "*.exp")[0]);
                foreach (XElement elevation in exp.Descendants("elevation"))
                {
                    IEnumerable<bool> privileges = from num in Convert.ToString(Convert.ToInt32(elevation.Attribute("privilege")), 2).PadLeft(8, '0').ToCharArray() select num == '1';
                    command.CommandText =
                    $@"
                        INSERT INTO elevations VALUES({elevation.Attribute("name")}, {privileges.ToString().Substring(1, privileges.Count() - 2)});
                    ";
                    //TODO: test statement
                    command.ExecuteNonQuery();
                }

                foreach (XElement subserver in exp.Descendants("subserver"))
                {
                    command.CommandText = $"INSERT INTO subservers VALUES ({subserver.Attribute("name")});";
                    command.ExecuteNonQuery();
                    command.CommandText = "SELECT last_insert_rowid();";
                    int subserverID = (int)command.ExecuteScalar();

                    foreach (XElement user in subserver.Elements("user"))
                        AddUser(command, user, subserverID);

                    foreach (XElement room in subserver.Elements("room"))
                        ProcessRoom(command, room, false);
                }
            }
        }

        internal static void CreateDB(string path)
        {
            string table = "CREATE TABLE IF NOT EXISTS";
            string name = "name TEXT UNIQUE NOT NULL";
            string iBool = "INTEGER NOT NULL";
            string IPK = $"id INTEGER PRIMARY KEY";
            string create =
            $@"
                {table} elevations ({IPK}, {name}, canCallSubserver {iBool}, canCallRoom {iBool}, canCallUser {iBool},
                                    canCallGroup {iBool}, canMsgSubserver {iBool}, canMsgRoom {iBool}, canMsgUser {iBool},
                                    canMsgGroup {iBool}, canCreateRoom {iBool}, canCreateGroup {iBool});
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
            using (SqliteConnection connection = new SqliteConnection(connectionString))
            using (SqliteCommand command = new SqliteCommand(create, connection))
            {
                connection.Open();
                command.ExecuteNonQuery();
            }
        }

        internal static void AddUser(SqliteCommand command, XElement user, int parentID)
        {
            command.CommandText = $"SELECT rowid FROM elevations WHERE name='{user.Attribute("elevation")}'";
            command.CommandText = $"INSERT INTO users VALUES ({user.Attribute("$username")}, {user.Attribute("$password")}, {command.ExecuteScalar()});";
            command.ExecuteNonQuery();
            command.CommandText = "SELECT last_insert_rowid();";
            int userID = (int)command.ExecuteScalar();
            command.CommandText = $"INSERT INTO user_subservers VALUES ({userID}, {parentID});";
            command.ExecuteNonQuery();
        }

        internal static void ProcessRoom(SqliteCommand command, XElement room, bool parentIsRoom = true)
        {
            command.CommandText = $"INSERT INTO rooms VALUES ({room.Attribute("name")}, {room.Attribute("password")});";
            command.ExecuteNonQuery();
            command.CommandText = "SELECT last_insert_rowid();";
            int roomID = (int)command.ExecuteScalar();

            string parent = parentIsRoom ? "rooms" : "subservers";
            command.CommandText = $"SELECT rowid FROM {parent} WHERE name='{room.Parent.Attribute("name")}'";
            command.CommandText = $"INSERT INTO user_{parent} VALUES ({command.ExecuteScalar()}, {roomID});";
            command.ExecuteNonQuery();

            foreach (XElement user in room.Elements("user"))
                AddUser(command, user, roomID);

            foreach (XElement child in room.Elements("room"))
                ProcessRoom(command, child);
        }

        internal static bool CheckUser(string parentName, string username)
        {
            string stmt =
            $@"
                SELECT COUNT(*) FROM {{0}}s, users, users_{{0}}s
                WHERE users.username='{username}'
                AND {{0}}s.name='{parentName}'
                AND {{0}}s.id=users_{{0}}s.{{0}}ID
                AND users.id=users_{{0}}s.userID;
            ";
            using (SqliteConnection connection = new SqliteConnection("Data Source =data.db;"))
            using (SqliteCommand command = new SqliteCommand("SELECT name FROM sqlite_schema WHERE type='table';", connection))
            {
                connection.Open();
                SqliteDataReader tableReader = command.ExecuteReader();
                List<string> tables = new List<string>();
                while (tableReader.Read())
                    tables.Add(tableReader.GetString(0));
                tableReader.Close();
                foreach (string table in tables)
                {
                    command.CommandText = $"PRAGMA table_info({table});";
                    List<string> feilds = new List<string>();
                    tableReader = command.ExecuteReader();
                    while (tableReader.Read())
                        feilds.Add(tableReader.GetString(1));
                    tableReader.Close();
                    if (!feilds.Contains("name"))
                        continue;
                    command.CommandText = $"SELECT COUNT(*) FROM {table} WHERE name='{parentName}';";
                    if (Convert.ToInt64(command.ExecuteScalar()) > 0)
                    {
                        command.CommandText = String.Format(stmt, table.Substring(0, table.Length - 1));
                        return Convert.ToInt64(command.ExecuteScalar()) > 0;
                    }
                }
            }
            return false;
        }

        //TODO: Write Login
        internal static bool Login(string username, string password)
        {
            throw new NotImplementedException();
        }
    }
}
