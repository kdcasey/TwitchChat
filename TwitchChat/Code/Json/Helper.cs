namespace TwitchChat.Json
{
    using System.IO;
    using System.Runtime.Serialization.Json;

    public static class Helper
    {
        /// <summary>
        /// Parse json formatted data into an appropriate object T
        /// </summary>
        /// <typeparam name="T">The data contract to use when parsing the string</typeparam>
        /// <param name="input">string to parse</param>
        /// <returns>The json object</returns>
        public static T Parse<T>(byte[] input)
        {
            var ser = new DataContractJsonSerializer(typeof(T));
            using (var ms = new MemoryStream(input))
            {
                ms.Position = 0;
                return (T)ser.ReadObject(ms);
            }
        }
    }
}
