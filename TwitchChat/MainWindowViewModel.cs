namespace TwitchChat
{
    using System;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Windows;
    using System.Windows.Input;

    public class MainWindowViewModel : ViewModelBase
    {
        //  Main IRC client
        private TwitchIrcClient _irc;

        //  New Channel name input
        private string _newChannelName;
        public string NewChannelName
        {
            get
            {
                return _newChannelName;
            }
            set
            {
                _newChannelName = value;
                NotifyPropertyChanged();
            }
        }

        // New Whisper name input
        private string _newWhisperUserName;
        public string NewWhisperUserName
        {
            get { return _newWhisperUserName; }
            set
            {
                _newWhisperUserName = value;
                NotifyPropertyChanged();
            }
        }

        public bool IsLoggedIn
        {
            get { return _irc?.State == IrcClient.IRCState.Registered; }
        }

        public ICommand LoginCommand { get; private set; }
        public ICommand LogoutCommand { get; private set; }

        public ICommand JoinCommand { get; private set; }
        public ICommand WhisperCommand { get; private set; }

        private Controls.ChannelViewModel _currentChannel;
        public Controls.ChannelViewModel CurrentChannel
        {
            get { return _currentChannel; }
            set
            {
                _currentChannel = value;
                NotifyPropertyChanged();
            }
        }

        //  Stores all joined channels
        public ObservableCollection<Controls.ChannelViewModel> Channels { get; set; }
        //  Stores all active whisper sessions
        public ObservableCollection<Dialog.WhisperWindowViewModel> Whispers { get; set; }

        public MainWindowViewModel()
        {
            _irc = new TwitchIrcClient();

            _irc.WhisperReceived += OnWhisperReceived;
            _irc.Connected += OnConnected;
            _irc.Disconnected += OnDisconnected;

            //  Setup delegate commands
            LoginCommand = new RelayCommand(Login, () => !IsLoggedIn);
            LogoutCommand = new RelayCommand(Logout, () => IsLoggedIn);

            JoinCommand = new RelayCommand(Join, () => IsLoggedIn);
            WhisperCommand = new RelayCommand(Whisper, () => IsLoggedIn);

            //  Setup observable collections
            Channels = new ObservableCollection<Controls.ChannelViewModel>();
            Whispers = new ObservableCollection<Dialog.WhisperWindowViewModel>();
        }

        private void OnDisconnected(object sender, DisconnectEventArgs e)
        {
            NotifyPropertyChanged("IsLoggedIn");
        }

        private void OnConnected(object sender, EventArgs e)
        {
            NotifyPropertyChanged("IsLoggedIn");
        }

        //  Join a new channel
        void Join()
        {
            if (!Channels.Any(x => x.ChannelName == NewChannelName))
            {
                var vm = new Controls.ChannelViewModel(_irc, NewChannelName);
                vm.Parted += OnParted;
                Channels.Add(vm);
                CurrentChannel = vm;
            }
            NewChannelName = string.Empty;
        }

        //  Channel was parted
        private void OnParted(object sender, EventArgs e)
        {
            var vm = sender as Controls.ChannelViewModel;
            Channels.Remove(vm);
        }

        //  Open a whisper once a whisper is received
        private void OnWhisperReceived(object sender, MessageEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (!Whispers.Any(x => x.UserName.ToLower() == e.User.ToLower()))
                    Whispers.Add(new Dialog.WhisperWindowViewModel(_irc, e.User, e));
            });
        }

        //  Start a new private message
        void Whisper()
        {
            if (!Whispers.Any(x => x.UserName.ToLower() == NewWhisperUserName.ToLower()))
            {
                using (var wc = new WebClient())
                {
                    try
                    {
                        var result = Json.Helper.Parse<TwitchUserResult>(wc.DownloadData("https://api.twitch.tv/kraken/users/" + NewWhisperUserName.ToLower()));
                        if (result.name.ToLower() == _irc.User.ToLower())
                            MessageBox.Show("Unable to message self");
                        else
                            Whispers.Add(new Dialog.WhisperWindowViewModel(_irc, NewWhisperUserName));
                    }
                    catch
                    {
                        MessageBox.Show("User not found");
                    }
                }
            }
            NewWhisperUserName = string.Empty;
        }

        //  Login to twitch
        void Login()
        {
            var login = new Dialog.LoginWindow();
            login.ShowDialog();
            var token = login.Token;

            try
            {
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("Accept", "application/vnd.twitchtv.v3+json");
                    wc.Headers.Add("Authorization", "OAuth " + token);
                    var result = Json.Helper.Parse<TwitchUserResult>(wc.DownloadData("https://api.twitch.tv/kraken/user"));

                    _irc.Login(result.name, "oauth:" + token);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Unhandled exception while logging in: {0}", args: ex);
            }
        }

        //  Logout from twitch
        public void Logout()
        {
            if (_irc.State == IrcClient.IRCState.Registered) _irc.Quit();
        }
    }
}