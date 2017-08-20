using Octgn.Communication;
using System;

namespace Octgn.Communication.Chat
{

    public class HostedGameInfo
    {
        public HostedGameInfo() {

        }

        public HostedGameInfo(Guid id, Guid gameguid, Version gameversion, int port, string name, User huser,
                          DateTime startTime, string gameName, string gameIconUrl, string userIconUrl, bool hasPassword, string ipAddress, string source, string status, bool spectator) {
            Id = id;
            GameGuid = gameguid;
            GameVersion = gameversion;
            Port = port;
            Name = name;
            Username = huser.NodeId;
            GameStatus = status;
            TimeStarted = startTime;
            HasPassword = hasPassword;
            GameName = gameName;
            IpAddress = ipAddress;
            Source = source;
            Spectator = spectator;
            GameIconUrl = gameIconUrl ?? string.Empty;
            UserIconUrl = userIconUrl ?? string.Empty;
        }

        public Guid Id { get; set; }

        public Guid GameGuid { get; set; }
        public Version GameVersion { get; set; }
        public int Port { get; set; }
        public String Name { get; set; }

        public string GameName { get; set; }

        public String GameIconUrl { get; set; }

        public string Username { get; set; }

        public String UserIconUrl { get; set; }

        public bool HasPassword { get; set; }
        public string GameStatus { get; set; }
        public DateTimeOffset TimeStarted { get; set; }

        public string IpAddress { get; set; }
        public string Source { get; set; }
        public bool Spectator { get; set; }

        public string ErrorMessage { get; set; }
        public override string ToString() {
            return $"HostedGameInfo(Id: {Id}, Host: {IpAddress}:{Port}, Source: {Source}, Status: {GameStatus}, Started: {TimeStarted}, User: {Username}, : '{Name}({GameGuid}) - {GameName}v{GameVersion}, Spec: {Spectator}, User Icon: {UserIconUrl}, Icon: {GameIconUrl}, Password: {HasPassword} ')";
        }
    }
}
