namespace TwitchChat.Controls
{
    using System;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Net;
    using System.Windows;
    using System.Windows.Input;

    public class ChannelViewModel : ViewModelBase
    {
        private TwitchIrcClient _irc;

        //  Store channel specific status
        private bool _mod;
        private bool _subscriber;

        //  The badges that are taken from https://api.twitch.tv/kraken/chat/{0}/badges
        private BadgesResult _badges;

        //  Name of the channel
        private string _channelName;
        public string ChannelName
        {
            get { return _channelName; }
            set
            {
                _channelName = value;
                NotifyPropertyChanged();
            }
        }

        //  New message to be sent
        private string _message;
        public string Message
        {
            get { return _message; }
            set
            {
                _message = value;
                NotifyPropertyChanged();
            }
        }

        //  Groups of chatters
        public ObservableCollection<ChatGroupViewModel> ChatGroups { get; set; } = new ObservableCollection<ChatGroupViewModel>();
        //  Messages from to the current channel
        public ObservableCollection<ChatMessageViewModel> Messages { get; set; } = new ObservableCollection<ChatMessageViewModel>();

        //  Commands available for each channel
        public ICommand SendCommand { get; private set; }
        public ICommand PartCommand { get; private set; }

        //  Event to notify other models channel has been parted
        public event EventHandler Parted;

        public ChannelViewModel(TwitchIrcClient irc, string channelName)
        {
            _irc = irc;

            _irc.MessageReceived += MessageReceived;
            _irc.UserParted += UserParted;
            _irc.UserJoined += UserJoined;
            _irc.NamesReceived += NamesReceived;
            _irc.UserStateReceived += UserStateReceived;

            _channelName = channelName;

            _irc.Join(_channelName);

            SendCommand = new DelegateCommand(Send);
            PartCommand = new DelegateCommand(Part);

            using (var wc = new WebClient())
            {
                _badges = Json.Helper.Parse<BadgesResult>(wc.DownloadData(string.Format("https://api.twitch.tv/kraken/chat/{0}/badges", channelName)));
            }
        }

        private void UserStateReceived(object sender, UserStateEventArgs e)
        {
            if (e.Channel == ChannelName)
            {
                _mod = e.Mod;
                _subscriber = e.Subscriber;
            }
        }

        private void NamesReceived(object sender, NamesReceivedEventArgs e)
        {
            if (e.Channel == ChannelName)
            {
                //  Add names to general viewer group
                var group = _getGroup("Viewers");

                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var name in e.Names)
                    {
                        if (!ChatGroups.SelectMany(x => x.Members).Any(x => x.Name.ToLower() == name.ToLower()))
                            group.Members.Add(new ChatMemberViewModel() { Name = name });
                    }
                });
            }
        }

        private void UserJoined(object sender, TwitchEventArgs e)
        {
            if (e.Channel == ChannelName)
            {
                //  Add names to general viewer group
                var group = _getGroup("Viewers");

                Application.Current.Dispatcher.Invoke(() =>
                {
                    group.Members.Add(new ChatMemberViewModel() { Name = e.User });
                });
            }
        }

        private void UserParted(object sender, TwitchEventArgs e)
        {
            if (e.Channel == ChannelName)
            {
                //  Search groups for user
                foreach (var group in ChatGroups)
                {
                    //  Find user among the group members
                    var user = group.Members.FirstOrDefault(x => x.Name.ToLower() == e.User.ToLower());
                    if (user != null)
                    {
                        //  Remove once user is gound and removed
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            group.Members.Remove(user);
                        });
                        break;
                    }
                }
            }
        }

        private void MessageReceived(object sender, MessageEventArgs e)
        {
            if (e.Channel == ChannelName)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Messages.Add(new ChatMessageViewModel(e, _badges));
                    if (Messages.Count > App.MAXMESSAGES)
                        Messages.RemoveAt(0);
                });
            }
        }

        void Send()
        {
            //  Populate a name value collection to fake a MessageEventArg
            NameValueCollection nvc = new NameValueCollection();
            nvc["color"] = _irc.Color;
            nvc["display-name"] = _irc.DisplayName;
            nvc["mod"] = _mod ? "1" : "0";
            nvc["subscriber"] = _subscriber ? "1" : "0";
            nvc["turbo"] = _irc.Turbo ? "1" : "0";
            nvc["user-type"] = _irc.UserType == UserType.Mod ? "mod" :
                _irc.UserType == UserType.GlobalMod ? "global_mod" :
                _irc.UserType == UserType.Admin ? "admin" :
                _irc.UserType == UserType.Staff ? "staff" :
                "";
            Messages.Add(new ChatMessageViewModel(new MessageEventArgs(nvc, Message) { User = _irc.User, Channel = ChannelName }, _badges));
            if (Messages.Count > App.MAXMESSAGES)
                Messages.RemoveAt(0);

            _irc.Message(_channelName, Message);
            Message = string.Empty;
        }

        void Part()
        {
            _irc.Part(_channelName);
            //  Raise the event.  MainWindowViewModel uses this to remove from list of channels
            if (Parted != null) Parted(this, EventArgs.Empty);
        }

        #region Helpers

        //  Helper to get and create groups
        ChatGroupViewModel _getGroup(string groupName)
        {
            //  Attempt to get group
            var group = ChatGroups.FirstOrDefault(x => x.Name == groupName);
            if (group == null)
            {
                //  Create group if it doesnt exist
                group = new ChatGroupViewModel() { Name = groupName };

                Application.Current.Dispatcher.Invoke(() =>
                {
                    ChatGroups.Add(group);
                });
            }
            return group;
        }

        #endregion
    }
}
