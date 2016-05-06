namespace TwitchChat
{
    using System.Runtime.Serialization;

    /// <summary>
    /// The result from a get request to http://tmi.twitch.tv/servers?channel={channel_name}
    /// </summary>
    [DataContract]
    public class TwitchServerResult
    {
        [DataMember]
        public string cluster;
        [DataMember]
        public string[] servers;
        [DataMember]
        public string[] websockets_servers;
    }
}
