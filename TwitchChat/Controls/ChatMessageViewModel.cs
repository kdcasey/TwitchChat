namespace TwitchChat.Controls
{
    using System.Collections.ObjectModel;

    //  Our viewmodel to display messages with badges
    public class ChatMessageViewModel : MessageViewModel
    {
        public ObservableCollection<string> Badges { get; set; }

        public ChatMessageViewModel(MessageEventArgs message, BadgesResult badges) : base(message)
        {
            Badges = new ObservableCollection<string>();

            if (message.UserType == UserType.Admin)
                Badges.Add(badges.admin.image);
            if (message.UserType == UserType.GlobalMod)
                Badges.Add(badges.global_mod.image);
            if (message.UserType == UserType.Staff)
                Badges.Add(badges.staff.image);
            if (message.Subscriber)
                Badges.Add(badges.subscriber.image);
            if (message.Mod || message.UserType == UserType.Mod)
                Badges.Add(badges.mod.image);
            if (message.Turbo)
                Badges.Add(badges.turbo.image);
            if (message.Channel == message.User)
                Badges.Add(badges.broadcaster.image);
        }
    }
}
