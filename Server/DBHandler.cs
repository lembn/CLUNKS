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

        internal static void LoadExp(string expPath, string dbPath)
        {
            if (!File.Exists($@"{dbPath}\data.db"))
                CreateDB(dbPath);

            //TODO: Change connection strings
            using (SqliteConnection connection = new SqliteConnection(connectionString))
            using (SqliteCommand command = new SqliteCommand())
            {
                connection.Open();
                command.Connection = connection;
                XDocument exp = XDocument.Load(expPath);
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
            string PK = "PRIMARY KEY";
            string create =
            $@"
                {table} elevations ({name}, canCallSubserver {iBool}, canCallRoom {iBool}, canCallUser {iBool},
                                    canCallGroup {iBool}, canMsgSubserver {iBool}, canMsgRoom {iBool}, canMsgUser {iBool},
                                    canMsgGroup {iBool}, canCreateRoom {iBool}, canCreateGroup {iBool});
                {table} subservers ({name});
                {table} rooms ({name}, password TEXT NOT NULL);
                {table} subserver_rooms (subserverID INTEGER REFERENCES subservers(id), roomID INTEGER REFERENCES rooms(id), {PK} (subserverID, roomID));
                {table} room_rooms (parentRoom INTEGER REFERENCES rooms(id), childRoom INTEGER REFERENCES rooms(id), {PK} (parentRoom, childRoom));
                {table} users (user{name}, password TEXT NOT NULL, elevation INTEGER REFERENCES elevations(id));
                {table} users_subservers (userID INTEGER REFERENCES users(id), subserverID INTEGER REFERENCES subservers(id), {PK} (userID, subserverID));
                {table} users_rooms (userID INTEGER REFERENCES users(id), roomID INTEGER REFERENCES rooms(id), {PK} (userID, roomID));
                {table} groups ({name}, password TEXT NOT NULL, owner INTEGER references users(id));
                {table} room_groups (roomID INTEGER REFERENCES rooms(id), groupID INTEGER REFERENCES groups(id), {PK} (roomID, groupID));
                {table} group_groups (parentGroup INTEGER REFERENCES groups(id), childGroup INTEGER REFERENCES groups(id), {PK} (parentGroup, childGroup));
                {table} users_groups (userID INTEGER REFERENCES users(id), groupID INTEGER REFERENCES groups(id), {PK} (userID, groupID));
                {table} notifications (sender INTEGER REFERENCES users(id), receiver INTEGER REFERENCES users(id), time INTEGER, msg TEXT, type TEXT, {PK} (sender, receiver, time));
            ";

            File.Create($@"{path}\data.db");
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
    }
}
