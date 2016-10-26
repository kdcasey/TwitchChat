namespace TwitchChat
{
    using System;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Net;

    public class TwitchIrcClient : IrcClient
    {
        public string User { get; set; }

        //  The user properties are only updated when joining a channel, but they are global properties
        public string DisplayName { get; set; }
        public string Color { get; set; }
        public UserType UserType { get; set; }
        public bool Turbo { get; set; }

        public TwitchIrcClient()
        {
            //  Get available servers and initialize IrcClient with the first one
            using (var wc = new TwitchApiClient())
            {
                var result = Json.Helper.Parse<TwitchServerResult>(wc.DownloadData("http://tmi.twitch.tv/servers?channel=twitch"));
                var server = result.servers.First().Split(':');

                Server = server[0];
                Port = int.Parse(server[1]);
            }
        }

        /// <summary>
        /// Login to twitch irc client
        /// </summary>
        /// <param name="user">Username of account to login with</param>
        /// <param name="pass">Oath token obtained with chat_login scope</param>
        public void Login(string user, string pass)
        {
            //  Store the user name when logging in
            User = user;
            Connect(user, null, pass);
        }

        #region Twitch IRC

        //  Whisper commands are sent to jtv channel
        public void Whisper(string user, string message)
        {
            Message("jtv", string.Format("/w {0} {1}", user, message));
        }

        //  Channels are prefixed with hashtags
        public override void Join(string channel)
        {
            base.Join(string.Format("#{0}", channel));
        }

        public override void Part(string channel)
        {
            base.Part(string.Format("#{0}", channel));
        }

        public override void Message(string channel, string message)
        {
            base.Message(string.Format("#{0}", channel), message);
        }

        #endregion

        #region Events

        public event EventHandler<NamesReceivedEventArgs> NamesReceived;
        public event EventHandler<TwitchEventArgs> UserJoined;
        public event EventHandler<TwitchEventArgs> UserParted;
        public event EventHandler<UserModStatusEventArgs> UserModStatusUpdated;
        public event EventHandler<NoticeEventArgs> NoticeReceived;
        public event EventHandler<HostEventArgs> UserHosted;
        public event EventHandler<TwitchEventArgs> ChatCleared;
        public event EventHandler<MessageEventArgs> MessageReceived;
        public event EventHandler<MessageEventArgs> WhisperReceived;
        public event EventHandler<UserStateEventArgs> UserStateReceived;
        public event EventHandler<RoomStateEventArgs> RoomStateChanged;

        #endregion

        public override void OnConnect()
        {
            //  Request capabilities
            //  https://github.com/justintv/Twitch-API/blob/master/IRC.md#membership
            _send("CAP REQ :twitch.tv/membership");
            //  https://github.com/justintv/Twitch-API/blob/master/IRC.md#commands
            _send("CAP REQ :twitch.tv/commands");
            //  https://github.com/justintv/Twitch-API/blob/master/IRC.md#tags
            _send("CAP REQ :twitch.tv/tags");

            base.OnConnect();
        }

        public override void OnReceived(Message message)
        {
            switch (message.Command)
            {
                //  https://github.com/justintv/Twitch-API/blob/master/IRC.md#names
                case "353":
                    if (NamesReceived != null)
                    {
                        NamesReceived(this, new NamesReceivedEventArgs
                        {
                            Channel = message.Parameters[2].TrimStart('#'),
                            Names = message.Parameters[3].Split(' ')
                        });
                    }
                    break;
                //  https://github.com/justintv/Twitch-API/blob/master/IRC.md#join
                case "JOIN":
                    if (UserJoined != null)
                    {
                        UserJoined(this, new TwitchEventArgs
                        {
                            User = message.Prefix.Split('!')[0],
                            Channel = message.Parameters[0].TrimStart('#')
                        });
                    }
                    break;
                //  https://github.com/justintv/Twitch-API/blob/master/IRC.md#part
                case "PART":
                    if (UserParted != null)
                    {
                        UserParted(this, new TwitchEventArgs
                        {
                            User = message.Prefix.Split('!')[0],
                            Channel = message.Parameters[0].TrimStart('#')
                        });
                    }
                    break;
                //  https://github.com/justintv/Twitch-API/blob/master/IRC.md#mode
                case "MODE":
                    if (UserModStatusUpdated != null)
                    {
                        UserModStatusUpdated(this, new UserModStatusEventArgs
                        {
                            User = message.Parameters[2],
                            Channel = message.Parameters[0].TrimStart('#'),
                            On = (message.Parameters[1] == "+o")
                        });
                    }
                    break;
                //  https://github.com/justintv/Twitch-API/blob/master/IRC.md#notice
                case "NOTICE":
                    if (NoticeReceived != null)
                    {
                        NoticeReceived(this, new NoticeEventArgs(message.Tags, message.Parameters[1])
                        {
                            Channel = message.Parameters[0].TrimStart('#')
                        });
                    }
                    break;
                //  https://github.com/justintv/Twitch-API/blob/master/IRC.md#hosttarget
                case "HOSTTARGET":
                    if (UserHosted != null)
                    {
                        var target = message.Parameters[1].Split(' ');
                        UserHosted(this, new HostEventArgs
                        {
                            User = target[0],
                            Channel = message.Parameters[0].TrimStart('#'),
                            Number = int.Parse(target[1])
                        });
                    }
                    break;
                //  https://github.com/justintv/Twitch-API/blob/master/IRC.md#clearchat
                case "CLEARCHAT":
                    if (ChatCleared != null)
                    {
                        ChatCleared(this, new TwitchEventArgs
                        {
                            User = message.Parameters[1],
                            Channel = message.Parameters[0].TrimStart('#')
                        });
                    }
                    break;
                //  https://github.com/justintv/Twitch-API/blob/master/IRC.md#userstate
                case "USERSTATE":
                    if (UserStateReceived != null)
                    {
                        var args = new UserStateEventArgs(message.Tags)
                        {
                            Channel = message.Parameters[0].TrimStart('#')
                        };
                        DisplayName = args.DisplayName;
                        Color = args.Color;
                        UserType = args.UserType;
                        Turbo = args.Turbo;

                        UserStateReceived(this, args);
                    }
                    break;
                //  https://github.com/justintv/Twitch-API/blob/master/IRC.md#globaluserstate
                case "GLOBALUSERSTATE":
                    if (UserStateReceived != null)
                    {
                        UserStateReceived(this, new UserStateEventArgs(message.Tags));
                    }
                    break;
                //  https://github.com/justintv/Twitch-API/blob/master/IRC.md#roomstate-1
                case "ROOMSTATE":
                    if (RoomStateChanged != null)
                    {
                        RoomStateChanged(null, new RoomStateEventArgs(message.Tags)
                        {
                            Channel = message.Parameters[0].TrimStart('#')
                        });
                    }
                    break;
                //  https://github.com/justintv/Twitch-API/blob/master/IRC.md#privmsg
                case "PRIVMSG":
                    if (MessageReceived != null)
                    {
                        MessageReceived(null, new MessageEventArgs(message.Tags, message.Parameters[1])
                        {
                            User = message.Prefix.Split('!')[0],
                            Channel = message.Parameters[0].TrimStart('#')
                        });
                    }
                    break;
                case "WHISPER":
                    if (WhisperReceived != null)
                    {
                        WhisperReceived(null, new MessageEventArgs(message.Tags, message.Parameters[1])
                        {
                            User = message.Prefix.Split('!')[0]
                        });
                    }
                    break;
            }

            base.OnReceived(message);
        }
    }

    #region Enums

    /// <summary>
    /// User types as described by https://github.com/justintv/Twitch-API/blob/master/IRC.md#privmsg
    /// </summary>
    public enum UserType
    {
        Default, Mod, GlobalMod, Admin, Staff
    }

    /// <summary>
    /// Notice types https://github.com/justintv/Twitch-API/blob/master/IRC.md#notice
    /// </summary>
    public enum NoticeType
    {
        Error,
        SubOnlyMode,
        SlowMode,
        R9KMode,
        HostMode,
    }

    #endregion

    #region EventArgs

    public class TwitchEventArgs : EventArgs
    {
        public string User { get; set; }
        public string Channel { get; set; }
    }

    public class NamesReceivedEventArgs : TwitchEventArgs
    {
        public string[] Names { get; set; }
    }

    public class UserModStatusEventArgs : TwitchEventArgs
    {
        public bool On { get; set; }
    }

    public class NoticeEventArgs : TwitchEventArgs
    {
        public NoticeEventArgs(NameValueCollection tags, string message)
        {
            On = false;
            //  Read the msg-id tag and convert it to an enum based on https://github.com/justintv/Twitch-API/blob/master/IRC.md#notice
            switch (tags["msg-id"])
            {
                case "subs_on":
                    NoticeType = NoticeType.SubOnlyMode;
                    On = true;
                    break;
                case "subs_off":
                    NoticeType = NoticeType.SubOnlyMode;
                    break;
                case "slow_on":
                    NoticeType = NoticeType.SlowMode;
                    On = true;
                    break;
                case "slow_off":
                    NoticeType = NoticeType.SlowMode;
                    break;
                case "r9k_on":
                    NoticeType = NoticeType.R9KMode;
                    On = true;
                    break;
                case "r9k_off":
                    NoticeType = NoticeType.R9KMode;
                    break;
                case "host_on":
                    NoticeType = NoticeType.HostMode;
                    On = true;
                    break;
                case "host_off":
                    NoticeType = NoticeType.HostMode;
                    break;
                default:
                    NoticeType = NoticeType.Error;
                    break;
            }
            Message = message;
        }

        public NoticeType NoticeType { get; set; }
        public bool On { get; set; }
        public string Message { get; set; }
    }

    public class HostEventArgs : TwitchEventArgs
    {
        public int Number { get; set; }
    }

    public class UserStateEventArgs : TwitchEventArgs
    {
        public UserStateEventArgs(NameValueCollection init)
        {
            //  https://github.com/justintv/Twitch-API/blob/master/IRC.md#userstate-1
            Color = init["color"];
            DisplayName = init["display-name"];
            Mod = "1" == init["mod"];
            Subscriber = "1" == init["subscriber"];
            Turbo = "1" == init["turbo"];
            UserId = init["user-id"];
            UserType = init["user-type"] == "mod" ? UserType.Mod :
                init["user-type"] == "global_mod" ? UserType.GlobalMod :
                init["user-type"] == "admin" ? UserType.Admin :
                init["user-type"] == "staff" ? UserType.Staff :
                UserType.Default;
        }

        public string Color { get; set; }
        public string DisplayName { get; set; }
        public bool Mod { get; set; }
        public bool Subscriber { get; set; }
        public bool Turbo { get; set; }
        public string UserId { get; set; }
        public UserType UserType { get; set; }
    }

    public class RoomStateEventArgs : TwitchEventArgs
    {
        public RoomStateEventArgs(NameValueCollection init)
        {
            //  https://github.com/justintv/Twitch-API/blob/master/IRC.md#roomstate-1
            Language = init["broadcaster-lang"];
            R9K = init["r9k"] == null ? (bool?)null : (init["r9k"] == "1");
            Slow = init["slow"] == null ? (int?)null : (int.Parse(init["slow"]));
            SubOnly = init["subs-only"] == null ? (bool?)null : (init["subs-only"] == "1");
        }

        public string Language { get; set; }
        public bool? R9K { get; set; }
        public int? Slow { get; set; }
        public bool? SubOnly { get; set; }
    }

    public class MessageEventArgs : UserStateEventArgs
    {
        public MessageEventArgs(NameValueCollection init, string message) : base(init)
        {
            Message = message;
        }

        public string Message { get; set; }
    }

    #endregion
}
