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

        private SqliteConnection connection;
        private SqliteCommand command;
        private SqliteDataReader reader;
        private object lastResult;
        private bool disposed;

        #endregion

        public bool CanRecover { get; private set; }

        public Cursor(string connectionString)
        {
            connection = new SqliteConnection(connectionString);
            connection.Open();
            lastResult = new object();
            command = new SqliteCommand("PRAGMA journal_mode='wal';", connection);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// A method to execute an SQL statement and return the results if neccessary
        /// </summary>
        /// <typeparam name="T">The type of the result</typeparam>
        /// <param name="statement">The statement to execute</param>
        /// <param name="parameters">The parameters to substitute into the statement</param>
        /// <returns>The result of the query or null if there was no result</returns>
        public T Execute<T>(string statement, params object[] parameters)
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
                        return AutoCast<T>(results[0][0]);
                    else
                        return AutoCast<T>(results[0]);
                }
                else
                {
                    foreach (object[] result in results)
                        if (result.Length > 1)
                            return AutoCast<T>(results.ToArray());
                    return AutoCast<T>((from result in results select result[0]).ToArray());
                }
            }
            else
            {
                command.ExecuteNonQueryAsync();
                return default(T);
            }
        }

        /// <summary>
        /// A method to exeucte an SQL statement
        /// </summary>
        /// <param name="statement">The statement to execute</param>
        /// <param name="parameters">The parameters to substitute into the statement</param>
        public void Execute(string statement, params object[] parameters) => Execute<object>(statement, parameters);
        
        /// <summary>
        /// A method to recover a previous result after an unsuccesful autocast
        /// </summary>
        /// <typeparam name="T">The type of the result being recovered</typeparam>
        /// <returns>The recovered result</returns>
        public T Recover<T>() => AutoCast<T>(lastResult);

        /// <summary>
        /// A method to cast an object array to an integer array
        /// </summary>
        /// <param name="input">The input object array</param>
        /// <returns>The resultant interger array</returns>
        public static int[] GetIntArray(object[] input) => Array.ConvertAll(input, new Converter<object, int>(item => Convert.ToInt32(item)));

        /// <summary>
        /// A method to cast an object into a given type
        /// </summary>
        /// <typeparam name="T">The target type</typeparam>
        /// <param name="data">The object to cast</param>
        /// <returns>The casted object</returns>
        private T AutoCast<T>(object data)
        {
            if (data is T)
                return (T)data;
            try
            {
                Type targetType = typeof(T);
                T result = (T)Convert.ChangeType(data, targetType);
                CanRecover = false;
                return result;
            }
            catch (InvalidCastException)
            {
                lastResult = data;
                CanRecover = true;
                return default(T);
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
                lastResult = null;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
