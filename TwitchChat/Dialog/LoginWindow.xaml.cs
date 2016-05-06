namespace TwitchChat.Dialog
{
    using System.Windows;
    using System.Windows.Navigation;

    /// <summary>
    /// Interaction logic for Login.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        public string Token { get; set; }

        public LoginWindow()
        {
            InitializeComponent();
            wbMain.Navigating += OnNavigating;
            wbMain.Navigate(string.Format("https://api.twitch.tv/kraken/oauth2/authorize?response_type=token&client_id={0}&redirect_uri={1}&scope={2}", 
                App.CLIENTID, 
                "http://dummy", 
                "chat_login user_read"));
        }

        void OnNavigating(object sender, NavigatingCancelEventArgs e)
        {
            if (e.Uri.Fragment.StartsWith("#access_token"))
            {
                var fragments = e.Uri.Fragment.TrimStart('#').Split('&');
                foreach (var fragment in fragments)
                {
                    var values = fragment.Split(new char[] { '=' }, 2);
                    switch (values[0])
                    {
                        case "access_token":
                            Token = values[1];
                            break;
                    }
                }
                wbMain.Navigating -= OnNavigating;
                this.Close();
            }
        }
    }
}
