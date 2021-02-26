using Microsoft.Data.Sqlite;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Server.DBHandler
{
    /// <summary>
    /// A database cursor
    /// </summary>
    internal class Cursor : IDisposable
    {
        #region Private Memebers

        private SqliteConnection connection; //The connection to the database
        private SqliteCommand command; //The current SQL command
        private SqliteDataReader reader; //A reader to read results from the database
        private bool disposed;

        #endregion

        public Cursor(string connectionString)
        {
            connection = new SqliteConnection(connectionString);
            connection.Open();
            command = new SqliteCommand("PRAGMA journal_mode='wal';", connection);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// A method to execute SQL statements on the database and return the results if needed
        /// </summary>
        /// <param name="statement">The SQL statement to execute</param>
        /// <param name="parameters">Paramaters for parameterised queries (prepared statements)</param>
        /// <returns>Null if the query returned no items, a single object if only one item was returned,
        /// a single-dimensional array if only one column was queried or a multi-dimensional array otherwise.</returns>
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
