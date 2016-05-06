namespace TwitchChat.Dialog
{
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Windows;
    using System.Windows.Input;

    public class WhisperWindowViewModel : ViewModelBase
    {
        private TwitchIrcClient _irc;

        //  User viewmodel is for
        private string _userName;
        public string UserName
        {
            get { return _userName; }
            set
            {
                _userName = value;
                NotifyPropertyChanged();
            }
        }

        //  Message to be sent to user
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

        //  Collectrion of all messages
        public ObservableCollection<MessageViewModel> Messages { get; set; } = new ObservableCollection<MessageViewModel>();

        //  Command to send message
        public ICommand SendCommand { get; private set; }

        public WhisperWindowViewModel(TwitchIrcClient irc, string userName, MessageEventArgs e = null)
        {
            _irc = irc;
            _irc.WhisperReceived += WhisperReceived;
            _userName = userName;

            SendCommand = new DelegateCommand(Send);

            if (e != null)
                WhisperReceived(this, e);
        }

        private void WhisperReceived(object sender, MessageEventArgs e)
        {
            if (e.User == UserName)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Messages.Add(new MessageViewModel(e));
                    if (Messages.Count > App.MAXMESSAGES)
                        Messages.RemoveAt(0);
                });
            }
        }

        //  Send a whisper to chosen user
        void Send()
        {
            //  Create NameValueCollection to fake a MessageEventArgs
            NameValueCollection nvc = new NameValueCollection();
            nvc["color"] = _irc.Color;
            nvc["display-name"] = _irc.DisplayName;

            Messages.Add(new MessageViewModel(new MessageEventArgs(nvc, Message) { User = _irc.User }));
            if (Messages.Count > App.MAXMESSAGES)
                Messages.RemoveAt(0);

            _irc.Whisper(_userName, Message);
            Message = string.Empty;
        }
    }
}