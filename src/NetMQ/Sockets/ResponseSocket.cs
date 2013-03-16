﻿using System;
using NetMQ.zmq;

namespace NetMQ.Sockets
{
	/// <summary>
	/// Response socket
	/// </summary>
	class ResponseSocket : NetMQSocket
	{
		public ResponseSocket(SocketBase socketHandle)
			: base(socketHandle)
		{
		}

		public override void Subscribe(byte[] topic)
		{
			throw new NotSupportedException("Subscribe is not supported");
		}

		public override void Subscribe(string topic)
		{
			throw new NotSupportedException("Subscribe is not supported");
		}

		public override void Unsubscribe(byte[] topic)
		{
			throw new NotSupportedException("Unsubscribe is not supported");
		}

		public override void Unsubscribe(string topic)
		{
			throw new NotSupportedException("Unsubscribe is not supported");
		}
	}
}
