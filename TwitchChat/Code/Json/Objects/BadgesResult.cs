namespace TwitchChat
{
    using System.Runtime.Serialization;

    /// <summary>
    /// Result from https://api.twitch.tv/kraken/chat/{channel}/badges
    /// </summary>
    [DataContract]
    public class BadgesResult
    {
        [DataContract]
        public class BadgeFormat
        {
            [DataMember]
            public string alpha;
            [DataMember]
            public string image;
            [DataMember]
            public string svg;
        }

        [DataMember]
        public BadgeFormat admin;
        [DataMember]
        public BadgeFormat broadcaster;
        [DataMember]
        public BadgeFormat global_mod;
        [DataMember]
        public BadgeFormat mod;
        [DataMember]
        public BadgeFormat staff;
        [DataMember]
        public BadgeFormat subscriber;
        [DataMember]
        public BadgeFormat turbo;
    }
}
