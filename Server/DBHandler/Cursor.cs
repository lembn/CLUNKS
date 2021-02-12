using Microsoft.Data.Sqlite;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Server.DBHandler
{
    //TODO: Summarise
    internal class Cursor : IDisposable
    {
        #region Private Memebers

        private SqliteConnection connection;
        private SqliteCommand command;
        private SqliteDataReader reader;
        private bool disposed;

        #endregion

        public Cursor(string connectionString)
        {
            connection = new SqliteConnection(connectionString);
            connection.Open();
            command = new SqliteCommand("PRAGMA journal_mode='wal';", connection);
            command.ExecuteNonQuery();
        }

        public object Execute(string statement, params object[] parameters)
        {
            string[] toReplace = (from match in Regex.Matches(statement, @"(\$)(\w)*") select match.Value).ToArray();
            command.CommandText = Regex.Replace(statement, @"\t|\n|\r|", "").Trim();
            command.Parameters.Clear();
            for (int i = 0; i < toReplace.Length; i++)
                command.Parameters.AddWithValue(toReplace[i], parameters[i]);
            if (command.CommandText.Split()[0].ToLower() == "select" || (command.CommandText.Split()[0].ToLower() == "pragma" && !command.CommandText.Contains('=')))
            {
                List<object[]> results = new List<object[]>();
                reader = command.ExecuteReader();
                while (reader.Read())
                {
                    object[] newValues = new object[reader.FieldCount];
                    reader.GetValues(newValues);
                    results.Add(newValues);
                }
                reader.Close();
                if (results.Count == 1)
                {
                    if (results[0].Length == 1)
                        return results[0][0];
                    else
                        return results[0];
                }
                else
                {
                    foreach (object[] result in results)
                        if (result.Length > 1)
                            return results.ToArray();
                    return (from result in results select result[0]).ToArray();
                }
            }
            else
            {
                command.ExecuteNonQueryAsync();
                return null;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    command.Dispose();
                    connection.Dispose();
                    reader?.DisposeAsync();
                }
                disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
