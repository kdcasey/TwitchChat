namespace TwitchChat.Controls
{
    using System.Collections.ObjectModel;

    //  This can be expanded to include icons for chat groups. maybe if chatters are separated by badges
    public class ChatGroupViewModel
    {
        public string Name { get; set; }
        public ObservableCollection<ChatMemberViewModel> Members { get; set; }

        public ChatGroupViewModel()
        {
            Members = new ObservableCollection<ChatMemberViewModel>();
        }
    }
}
