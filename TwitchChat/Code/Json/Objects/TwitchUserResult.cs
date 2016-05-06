namespace TwitchChat
{
    using System.Runtime.Serialization;

    /// <summary>
    /// Result from https://api.twitch.tv/kraken/user
    /// </summary>
    [DataContract]
    public class TwitchUserResult
    {
        [DataMember]
        public string name;
    }
}
