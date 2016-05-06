namespace TwitchChat
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;

    /// <summary>
    /// Class to store messages
    /// </summary>
    public class Message
    {
        /// <summary>
        /// Raw IRC Message
        /// </summary>
        public string Raw { get; set; }
        /// <summary>
        /// Tags as defined in http://ircv3.net/specs/core/message-tags-3.2.html
        /// </summary>
        public NameValueCollection Tags { get; set; }

        //  http://tools.ietf.org/html/rfc1459#section-2.3.1
        public string Prefix { get; set; }
        public string Command { get; set; }
        public string[] Parameters { get; set; }
    }

    /// <summary>
    /// A very simple irc client based on rfc and twitch requirements.
    /// Only commands used by twitch irc are implemented
    /// </summary>
    public class IrcClient : IDisposable
    {
        #region Events

        public event EventHandler<ReceivedEventArgs> Received;
        public event EventHandler<SentEventArgs> Sent;
        public event EventHandler<DisconnectEventArgs> Disconnected;

        public event EventHandler Connected;
        public event EventHandler<ReconnectingEventArgs> Reconnecting;

        public virtual void OnReceived(Message message)
        {
            //  https://tools.ietf.org/html/rfc2812#section-3.7.2
            if (message.Command.Equals("PING")) _send("PONG :" + message.Parameters[0]);

            if (Received != null) Received(this, new ReceivedEventArgs
            {
                Message = message
            });
        }

        public virtual void OnSent(string message)
        {
            if (Sent != null) Sent(this, new SentEventArgs
            {
                Message = message
            });
        }

        public virtual void OnConnect()
        {
            //  Reset Failed Attempts
            _failedAttempt = 1;
            if (Connected != null) Connected(this, new EventArgs());
        }

        public virtual void OnReconnecting()
        {
            if (Reconnecting != null) Reconnecting(this, new ReconnectingEventArgs()
            {
                ConnectionAttempts = _failedAttempt
            });
        }

        public virtual void OnDisconnect()
        {
            if (!_planned && _failedAttempt < (1 * 2 ^ MaxRetries))
            {
                //Wait before reconnecting
                Debug.WriteLine("Disconnected. Retrying in {0} seconds...", _failedAttempt);
                Thread.Sleep(1000 * _failedAttempt);
                _failedAttempt *= 2;

                //Reconnect
                State = IRCState.Reconnecting;
                OnReconnecting();
                _connect(_nick, _user, _pass);
            }
            else if (!_planned)
            {
                //  Stop trying to reconnect
                State = IRCState.Closed;
                _failedAttempt = 1;
                Debug.WriteLine("Too many failed attempts");
            }
            else
            {
                State = IRCState.Closed;
            }

            if (Disconnected != null) Disconnected(this, new DisconnectEventArgs
            {
                Planned = _planned
            });
        }

        #endregion

        #region Private Fields

        private Thread _thread;
        private StreamWriter _stream;

        //  Reconnection 
        private bool _planned = false;
        private int _failedAttempt = 1;

        //  Channel management
        private List<string> _channels = new List<string>();

        //  Registration information
        private string _nick;
        private string _user;
        private string _pass;

        #endregion

        #region Public Properties

        /// <summary>
        /// Destination server
        /// </summary>
        public string Server { get; set; }
        /// <summary>
        /// Destination Port
        /// </summary>
        public int Port { get; set; }
        /// <summary>
        /// See if thread is running
        /// </summary>
        public bool IsRunning
        {
            get
            {
                return _thread != null && (_thread.ThreadState & (System.Threading.ThreadState.Stopped | System.Threading.ThreadState.Unstarted)) == 0;
            }
        }
        /// <summary>
        /// The maximum amount of retries after disconnectin unexpectedly
        /// </summary>
        public int MaxRetries { get; set; }

        public enum IRCState
        {
            Closed,
            Connecting,
            Registering,
            Registered,
            Closing,
            Reconnecting,
            Error
        }
        public IRCState State { get; set; }

        #endregion

        public IrcClient() : this("127.0.0.1", 6667) { }

        public IrcClient(string server, int port)
        {
            Server = server;
            Port = port;
            State = IRCState.Closed;
            MaxRetries = 5;
        }

        public void Connect(string nick, string user, string pass)
        {
            State = IRCState.Connecting;
            _connect(nick, user, pass);
        }

        private void _connect(string nick, string user, string pass)
        {
            if (IsRunning) throw new Exception("Client is already running");

            _nick = nick; _user = user; _pass = pass;

            if (_thread != null) _thread.Join();
            _thread = new Thread(new ThreadStart(this._run));
            _planned = false;
            _thread.Start();
        }

        #region IRC Commands

        private void _checkRegistration()
        {
            if (State != IRCState.Registered) throw new Exception("Client must be registered to send IRC commands");
        }

        /// <summary>
        /// http://tools.ietf.org/html/rfc1459#section-4.2.1
        /// </summary>
        /// <param name="channel"></param>
        public virtual void Join(string channel)
        {
            _checkRegistration();

            if (!_channels.Contains(channel))
            {
                _channels.Add(channel);
                _send("JOIN " + channel);
            }
            else
            {
                Debug.WriteLine("Already listening to channel {0}", channel);
            }
        }

        /// <summary>
        /// http://tools.ietf.org/html/rfc1459#section-4.2.2
        /// </summary>
        /// <param name="channel"></param>
        public virtual void Part(string channel)
        {
            _checkRegistration();

            if (_channels.Contains(channel))
            {
                _channels.Remove(channel);
                _send("PART " + channel);
            }
            else
            {
                Debug.WriteLine("Not listening to channel {0}", channel);
            }
        }

        /// <summary>
        /// http://tools.ietf.org/html/rfc1459#section-4.4.1
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="message"></param>
        public virtual void Message(string channel, string message)
        {
            _checkRegistration();

            _send(string.Format("PRIVMSG {0} :{1}", channel, message));
        }

        /// <summary>
        /// https://tools.ietf.org/html/rfc2812#section-3.1.7
        /// </summary>
        public virtual void Quit()
        {
            _checkRegistration();

            State = IRCState.Closing;
            _send("QUIT");
            _planned = true;
            _thread.Abort();
        }

        #endregion

        protected void _send(string input)
        {
            try
            {
                if (_stream != null)
                {
                    _stream.WriteLine(input);
                    _stream.Flush();

                    //  Raise Sent Event
                    OnSent(input);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Unhandled exception during send: {0}", ex.ToString());
            }
        }

        /// <summary>
        /// State message is parsing in
        /// </summary>
        enum MessageState
        {
            Start,
            TagKey,
            TagValue,
            Prefix,
            Command,
            StartParameter,
            Parameter,
            Trailing,
            EndLine
        }

        /// <summary>
        /// http://tools.ietf.org/html/rfc1459#section-2.3.1
        /// </summary>
        /// <param name="sr"></param>
        /// <returns></returns>
        private Message _read(StreamReader sr)
        {
            //  Initialize buffers
            MessageState mode = MessageState.Start;
            NameValueCollection tags = new NameValueCollection();
            StringBuilder key = new StringBuilder();
            StringBuilder value = new StringBuilder();
            StringBuilder input = new StringBuilder();
            StringBuilder prefix = new StringBuilder();
            StringBuilder command = new StringBuilder();
            StringBuilder parameter = new StringBuilder();
            List<string> @params = new List<string>();
            do
            {
                char[] ca = new char[1];
                sr.Read(ca, 0, 1);
                char c = ca[0];
                input.Append(c);

                switch (mode)
                {
                    case MessageState.Start:
                        switch (c)
                        {
                            case '@':
                                mode = MessageState.TagKey;
                                break;
                            case ':':
                                mode = MessageState.Prefix;
                                break;
                            case ' ':
                                break;
                            case '\r':
                                mode = MessageState.EndLine;
                                break;
                            default:
                                mode = MessageState.Command;
                                if (char.IsLetterOrDigit(c))
                                    command.Append(c);
                                else
                                    throw new FormatException(string.Format("Unexpected character in command {0}.", (int)c));
                                break;
                        }
                        break;
                    case MessageState.TagKey:
                        switch (c)
                        {
                            case ' ':
                                mode = MessageState.Start;
                                tags.Add(key.ToString(), null);
                                key.Clear();
                                break;
                            case ';':
                                mode = MessageState.TagKey;
                                tags.Add(key.ToString(), null);
                                key.Clear();
                                break;
                            case '=':
                                mode = MessageState.TagValue;
                                break;
                            case '\r':
                                mode = MessageState.EndLine;
                                key.Clear();
                                break;
                            default:
                                if (char.IsLetterOrDigit(c) || c == '-' || c == '.' || c == '/')
                                    key.Append(c);
                                else
                                    throw new FormatException(string.Format("Unexpected character {0} found.", (int)c));
                                break;
                        }
                        break;
                    case MessageState.TagValue:
                        switch (c)
                        {
                            case ' ':
                                mode = MessageState.Start;
                                tags.Add(key.ToString(), value.ToString());
                                key.Clear();
                                value.Clear();
                                break;
                            case ';':
                                mode = MessageState.TagKey;
                                tags.Add(key.ToString(), value.ToString());
                                key.Clear();
                                value.Clear();
                                break;
                            case '\\':
                                if (sr.Peek() == -1) throw new EndOfStreamException("Unexpected end of stream during escape sequence.");
                                char x = (char)sr.Read();
                                switch (x)
                                {
                                    case ':':
                                        value.Append(';');
                                        break;
                                    case 's':
                                        value.Append(' ');
                                        break;
                                    case '\\':
                                        value.Append('\\');
                                        break;
                                    case 'r':
                                        value.Append('\r');
                                        break;
                                    case 'n':
                                        value.Append('\n');
                                        break;
                                    default:
                                        throw new FormatException(string.Format("Unexpected escape sequence {0}.", (int)c));
                                }
                                break;
                            case '\r':
                            case '\n':
                            case '\0':
                                throw new FormatException("Unexpected character in escaped value.");
                            default:
                                value.Append(c);
                                break;
                        }
                        break;
                    case MessageState.Prefix:
                        switch (c)
                        {
                            case ' ':
                                mode = MessageState.Start;
                                break;
                            case '\r':
                            case '\n':
                            case '\0':
                                throw new FormatException("Unexpected character in prefix.");
                            default:
                                prefix.Append(c);
                                break;
                        }
                        break;
                    case MessageState.Command:
                        switch (c)
                        {
                            case ' ':
                                mode = MessageState.StartParameter;
                                break;
                            default:
                                if (char.IsLetterOrDigit(c))
                                    command.Append(c);
                                else
                                    throw new FormatException(string.Format("Unexpected character in command {0}.", (int)c));
                                break;
                        }
                        break;
                    case MessageState.StartParameter:
                        switch (c)
                        {
                            case ' ':
                                break;
                            case ':':
                                mode = MessageState.Trailing;
                                break;
                            case '\r':
                                mode = MessageState.EndLine;
                                break;
                            case '\n':
                            case '\0':
                                throw new FormatException(string.Format("Unexpected character in parameter list {0}.", (int)c));
                            default:
                                mode = MessageState.Parameter;
                                parameter.Append(c);
                                break;
                        }
                        break;
                    case MessageState.Parameter:
                        switch (c)
                        {
                            case ' ':
                                mode = MessageState.StartParameter;
                                if (parameter.Length > 0)
                                    @params.Add(parameter.ToString());
                                parameter.Clear();
                                break;
                            case '\r':
                                mode = MessageState.EndLine;
                                break;
                            case '\n':
                            case '\0':
                                throw new FormatException("Unexpected character in parameter list.");
                            default:
                                parameter.Append(c);
                                break;
                        }
                        break;
                    case MessageState.Trailing:
                        switch (c)
                        {
                            case '\r':
                                mode = MessageState.EndLine;
                                break;
                            case '\n':
                            case '\0':
                                throw new FormatException("Unexpected character in trailing parameter.");
                            default:
                                parameter.Append(c);
                                break;
                        }
                        break;
                    case MessageState.EndLine:
                        if (c == '\n')
                        {
                            input.Remove(input.Length - 2, 2);
                            var lastParam = parameter.ToString();
                            if (!string.IsNullOrWhiteSpace(lastParam)) @params.Add(lastParam);
                            var message = new Message
                            {
                                Raw = input.ToString(),
                                Tags = tags,
                                Prefix = prefix.ToString(),
                                Command = command.ToString(),
                                Parameters = @params.ToArray()
                            };

                            OnReceived(message);

                            return message;
                        }
                        else
                            throw new FormatException(string.Format("Found {0} instead of LF character.", (int)c));
                }
            } while (sr.Peek() != -1);
            throw new EndOfStreamException("Stream ended before a full message could be constructed");
        }

        /// <summary>
        /// Main thread loop
        /// </summary>
        private void _run()
        {
            TcpClient irc = new TcpClient();

            try
            {
                //  Check parameters
                if (string.IsNullOrWhiteSpace(_nick)) throw new ArgumentNullException("Nick is required.");

                //  Connect to server
                irc.Connect(Server, Port);
                Debug.WriteLine("Connected to {0}:{1}", Server, Port);

                //  Get network stream
                NetworkStream stream = irc.GetStream();
                _stream = new StreamWriter(stream);
                _channels.Clear();

                //  Connection Registration
                //  https://tools.ietf.org/html/rfc2812#section-3.1
                State = IRCState.Registering;

                //  https://tools.ietf.org/html/rfc2812#section-3.1.1
                if (!string.IsNullOrWhiteSpace(_pass)) _send("PASS " + _pass);

                //  https://tools.ietf.org/html/rfc2812#section-3.1.2
                _send("NICK " + _nick);

                //  https://tools.ietf.org/html/rfc2812#section-3.1.3
                if (!string.IsNullOrWhiteSpace(_user)) _send("USER " + _user);

                using (var sr = new StreamReader(stream))
                {
                    //  https://tools.ietf.org/html/rfc2812#section-3.1
                    if (_read(sr).Command != "001") throw new Exception("Registration Failed. Welcome message expected");

                    //  Raise connect event
                    State = IRCState.Registered;
                    OnConnect();
                    Debug.WriteLine("Connected thread: {0}", Thread.CurrentThread.ManagedThreadId);

                    while (true) _read(sr);
                }
            }
            catch (Exception e)
            {
                State = IRCState.Error;
                Debug.WriteLine("Unhandled Exception: {0}", e.ToString());
            }
            finally
            {
                //  Close TCP Client
                _stream = null;
                if (irc?.Connected ?? false) irc.Close();

                //  Raise Disconnect on a new thread
                new Thread(new ThreadStart(OnDisconnect)).Start();
            }
        }

        public void Dispose()
        {
            //  Make sure thread is closed
            _planned = true;
            if (_thread != null) _thread.Abort();
        }
    }

    #region EventArgs

    public class ReceivedEventArgs : EventArgs
    {
        public Message Message { get; set; }
    }
    public class SentEventArgs : EventArgs
    {
        public string Message { get; set; }
    }
    public class DisconnectEventArgs : EventArgs
    {
        public bool Planned { get; set; }
    }
    public class ReconnectingEventArgs : EventArgs
    {
        public int ConnectionAttempts { get; set; }
    }

    #endregion
}
