﻿using System.Threading.Tasks;
using Octgn.Communication.Packets;
using System;

namespace Octgn.Communication.Test
{
    public class SlowConnection : IConnection
    {
        public ISerializer Serializer {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        public string ConnectionId => _connection.ConnectionId;

        public bool IsClosed { get => _connection.IsClosed; set => _connection.IsClosed = value; }

        private readonly IConnection _connection;

        public event ConnectionClosed ConnectionClosed {
            add => _connection.ConnectionClosed += value;
            remove => _connection.ConnectionClosed -= value;
        }
        public event RequestPacketReceived RequestReceived {
            add => _connection.RequestReceived += value;
            remove => _connection.RequestReceived -= value;
        }

        public SlowConnection(IConnection connection)
        {
            _connection = connection;
        }

        public async Task Connect()
        {
            await _connection.Connect();
        }

        public async Task<ResponsePacket> Request(RequestPacket packet)
        {
            await Task.Delay(2000);
            return await _connection.Request(packet);
        }

        public async Task Response(ResponsePacket packet)
        {
            await Task.Delay(2000);
            await _connection.Response(packet);
        }

        public IConnection Clone()
        {
            return new SlowConnection( _connection.Clone());
        }

        public bool Equals(IConnection other)
        {
            if (other == null) return false;
            if (!(other is SlowConnection)) return false;
            return (other as SlowConnection)._connection.Equals(_connection);
        }
    }
}
