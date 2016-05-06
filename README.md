# TwitchChat

## Purpose

This project is just to share how to make a twitch client using C# and WPF.
There are no extra libraries to install.  
Code is heavily commented with links to the RPF and Twitch IRC github.
I use a separate version in my personal projects, but decided to share my starting point.

## Setup

To use you need to create a Twitch Application by logging into Twitch and going to this link https://www.twitch.tv/kraken/oauth2/clients/new

Set the name to anything and the redirect URI to "http://dummy".
Once you have the client id, set it up on the const string located in App.xaml.cs file.

## Notes

"Code/IrcClient.cs" and "Code/TwitchIrcClient.cs" are un altered from my personal bot projects.  Only difference is they are compiled into separate libraries instead of beind placed directly into the application.  The WPF application itself is to demonstrate the library's usage.  In my personal projects I also use Json.NET, but for the purpose of keeping this open source version external-library-free, I've used the Microsoft Json Serializer located in "Code/Json/*".