using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TwitchChat
{
    public class TwitchApiClient : WebClient
    {
        public const string MIMETYPE = "application/vnd.twitchtv.v3+json"; //Use version 3
        public const string BASEURI = "https://api.twitch.tv/kraken";

        public TwitchApiClient()
        {
            Headers.Clear();
            Headers.Add("Accept", MIMETYPE);
            Headers.Add("Client-ID", App.CLIENTID);
        }

        public TwitchApiClient(string oauth) : this()
        {
            Headers.Add("Authorization", "OAuth " + oauth);
        }
    }
}
