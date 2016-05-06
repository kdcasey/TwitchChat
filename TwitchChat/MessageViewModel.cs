namespace TwitchChat
{
    //  Our viewmodel to show messages
    public class MessageViewModel
    {
        public string User { get; set; }
        public string Message { get; set; }
        public string Color { get; set; }

        public MessageViewModel(MessageEventArgs message)
        {
            User = string.IsNullOrWhiteSpace(message.DisplayName) ? message.User : message.DisplayName;
            Message = message.Message;
            Color = string.IsNullOrWhiteSpace(message.Color) ? "#000000" : message.Color;
        }
    }
}
