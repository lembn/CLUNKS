using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Text;

namespace Common.Helpers
{
    /// <summary>
    /// A helper class for converting objects from one form to another, while attempting
    /// to preserve as much information as possible
    /// </summary>
    public static class ObjectConverter
    {
        #region Private Members

        private static bool madeSerializer = false; //A boolean to check if the seriliazer has be initialised
        private static JsonSerializer serializer; //A serializer used for serializing objects into JSON strings

        #endregion

        #region Methods

        /// <summary>
        /// A method for converting enum values to strings holding the bytes which represent value
        /// </summary>
        /// <param name="obj">The enum value to convert</param>
        /// <returns>A string holding the bytes which represent value</returns>
        public static string EnumToByteString(object obj)
        {
            int i = (int)obj;
            byte[] bytes = BitConverter.GetBytes(i);
            return Encoding.UTF8.GetString(bytes);
        }
        
        /// <summary>
        /// Serializes an object into a JObject
        /// </summary>
        /// <param name="obj">The object to serialize</param>
        /// <returns>The JObject representation of 'obj'</returns>
        public static JObject GetJObject(object obj)
        {
            var sb = new StringBuilder();
            var sw = new StringWriter(sb);
            if (!madeSerializer) serializer = GetJsonSerializer();
            serializer.Serialize(sw, obj);
            return JObject.Parse(sb.ToString());
        }

        /// <summary>
        /// Creates a custom JsonSerializer
        /// </summary>
        /// <returns>The JsonSerializer</returns>
        public static JsonSerializer GetJsonSerializer()
        {
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.None,
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            };
            settings.PreserveReferencesHandling = PreserveReferencesHandling.None;
            return JsonSerializer.Create(settings);
        }

        #endregion
    }
}