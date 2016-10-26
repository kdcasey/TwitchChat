namespace TwitchChat
{
    using System.Windows;
    using Dialog;
    using System.Configuration;
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        //  Read settings from app.config
        public static string CLIENTID
        {
            get
            {
                return ConfigurationManager.AppSettings["twitch-api:client-id"];
            }
        }
        public static int MAXMESSAGES
        {
            get
            {
                return int.Parse(ConfigurationManager.AppSettings["twitch-api:maxmessages"]);
            }
        }

        private MainWindowViewModel _vm;

        protected override void OnStartup(StartupEventArgs e)
        {
            var mainWindow = new MainWindow();
            _vm = new MainWindowViewModel();
            _vm.Whispers.CollectionChanged += Whispers_CollectionChanged;

            mainWindow.DataContext = _vm;
            mainWindow.Closing += (object sender, System.ComponentModel.CancelEventArgs ee) =>
            {
                _vm.Logout();
            };
            mainWindow.Show();
        }

        private void Whispers_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            foreach(WhisperWindowViewModel vm in e.NewItems)
            {
                var whisperWindow = new WhisperWindow();
                whisperWindow.DataContext = vm;
                whisperWindow.Closing += WhisperWindow_Closing;
                whisperWindow.Show();
            }
        }

        private void WhisperWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //  Remove a whipser once the window is closed
            _vm.Whispers.Remove(sender as WhisperWindowViewModel);
        }
    }
}
