namespace TwitchChat
{
    using System.Windows;
    using Dialog;

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        //  Create a client id for twitch application that redirects to http://dummy
        public const string CLIENTID = "";
        public const int MAXMESSAGES = 150;

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
