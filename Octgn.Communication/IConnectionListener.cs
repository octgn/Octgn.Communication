using System;

namespace Octgn.Communication
{
    public interface IConnectionListener
    {
        bool IsEnabled { get; set; }
        event ConnectionCreated ConnectionCreated;
    }

    public delegate void ConnectionCreated(object sender, ConnectionCreatedEventArgs args);

    public class ConnectionCreatedEventArgs : EventArgs
    {
        public IConnection Connection { get; set; }
    }
}
