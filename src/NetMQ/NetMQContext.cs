﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NetMQ.Sockets;
using NetMQ.zmq;
using NetMQ.Monitoring;

namespace NetMQ
{
	/// <summary>
	/// Context class of the NetMQ, you should have only one context in your application
	/// </summary>
	public class NetMQContext : IDisposable
	{
		readonly Ctx m_ctx;
		private int m_isClosed = 0;


		private NetMQContext(Ctx ctx)
		{
			m_ctx = ctx;
		}

		/// <summary>
		/// Create a new context
		/// </summary>
		/// <returns>The new context</returns>
		public static NetMQContext Create()
		{
			return new NetMQContext(ZMQ.CtxNew());
		}

		/// <summary>
		/// Number of IO Threads in the context, default is 1, 1 is good for most cases
		/// </summary>
		public int ThreadPoolSize
		{
			get { return ZMQ.CtxGet(m_ctx, ContextOption.IOThreads); }
			set { ZMQ.CtxSet(m_ctx, ContextOption.IOThreads, value); }
		}

		/// <summary>
		/// Maximum number of sockets
		/// </summary>
		public int MaxSockets
		{
			get { return ZMQ.CtxGet(m_ctx, zmq.ContextOption.MaxSockets); }
			set { ZMQ.CtxSet(m_ctx, ContextOption.MaxSockets, value); }
		}

		public NetMQSocket CreateSocket(ZmqSocketType socketType)
		{
			var socketHandle = ZMQ.Socket(m_ctx, socketType);

			switch (socketType)
			{				
				case ZmqSocketType.Pair:
					return new PairSocket(socketHandle);
					break;
				case ZmqSocketType.Pub:
					return new PublisherSocket(socketHandle);
					break;
				case ZmqSocketType.Sub:
					return new SubscriberSocket(socketHandle);
					break;
				case ZmqSocketType.Req:
					return new RequestSocket(socketHandle);
					break;
				case ZmqSocketType.Rep:
					return new ResponseSocket(socketHandle);
					break;
				case ZmqSocketType.Dealer:
					return new DealerSocket(socketHandle);
					break;
				case ZmqSocketType.Router:
					return new RouterSocket(socketHandle);
					break;
				case ZmqSocketType.Pull:
					return new PullSocket(socketHandle);
					break;
				case ZmqSocketType.Push:
					return new PushSocket(socketHandle);
					break;
				case ZmqSocketType.Xpub:
					return new XPublisherSocket(socketHandle);
					break;
				case ZmqSocketType.Xsub:
					return new XSubscriberSocket(socketHandle);
					break;
				default:
					throw new ArgumentOutOfRangeException("socketType");
			}
		}

		/// <summary>
		/// Create request socket
		/// </summary>
		/// <returns></returns>
		public NetMQSocket CreateRequestSocket()
		{
			var socketHandle = ZMQ.Socket(m_ctx, ZmqSocketType.Req);

			return new RequestSocket(socketHandle);
		}

		/// <summary>
		/// Create response socket
		/// </summary>
		/// <returns></returns>
		public NetMQSocket CreateResponseSocket()
		{
			var socketHandle = ZMQ.Socket(m_ctx, ZmqSocketType.Rep);

			return new ResponseSocket(socketHandle);
		}

		/// <summary>
		/// Create dealer socket
		/// </summary>
		/// <returns></returns>
		public NetMQSocket CreateDealerSocket()
		{
			var socketHandle = ZMQ.Socket(m_ctx, ZmqSocketType.Dealer);

			return new DealerSocket(socketHandle);
		}

		/// <summary>
		/// Create router socket
		/// </summary>
		/// <returns></returns>
		public NetMQSocket CreateRouterSocket()
		{
			var socketHandle = ZMQ.Socket(m_ctx, ZmqSocketType.Router);

			return new RouterSocket(socketHandle);
		}

		/// <summary>
		/// Create xpublisher socket
		/// </summary>
		/// <returns></returns>
		public NetMQSocket CreateXPublisherSocket()
		{
			var socketHandle = ZMQ.Socket(m_ctx, ZmqSocketType.Xpub);

			return new XPublisherSocket(socketHandle);
		}

		/// <summary>
		/// Create pair socket
		/// </summary>
		/// <returns></returns>
		public NetMQSocket CreatePairSocket()
		{
			var socketHandle = ZMQ.Socket(m_ctx, ZmqSocketType.Pair);

			return new PairSocket(socketHandle);
		}

		/// <summary>
		/// Create push socket
		/// </summary>
		/// <returns></returns>
		public NetMQSocket CreatePushSocket()
		{
			var socketHandle = ZMQ.Socket(m_ctx, ZmqSocketType.Push);

			return new PushSocket(socketHandle);
		}

		/// <summary>
		/// Create publisher socket
		/// </summary>
		/// <returns></returns>
		public NetMQSocket CreatePublisherSocket()
		{
			var socketHandle = ZMQ.Socket(m_ctx, ZmqSocketType.Pub);

			return new PublisherSocket(socketHandle);
		}

		/// <summary>
		/// Create pull socket
		/// </summary>
		/// <returns></returns>
		public NetMQSocket CreatePullSocket()
		{
			var socketHandle = ZMQ.Socket(m_ctx, ZmqSocketType.Pull);

			return new PullSocket(socketHandle);
		}

		/// <summary>
		/// Create subscriber socket
		/// </summary>
		/// <returns></returns>
		public NetMQSocket CreateSubscriberSocket()
		{
			var socketHandle = ZMQ.Socket(m_ctx, ZmqSocketType.Sub);

			return new SubscriberSocket(socketHandle);
		}

		/// <summary>
		/// Create xsub socket
		/// </summary>
		/// <returns></returns>
		public NetMQSocket CreateXSubscriberSocket()
		{
			var socketHandle = ZMQ.Socket(m_ctx, ZmqSocketType.Xsub);

			return new XSubscriberSocket(socketHandle);
		}

		public NetMQMonitor CreateMonitorSocket(string endpoint)
		{
			if (endpoint == null)
			{
				throw new ArgumentNullException("endpoint");
			}

			if (endpoint == string.Empty)
			{
				throw new ArgumentException("Unable to monitor to an empty endpoint.", "endpoint");
			}

			return new NetMQMonitor(CreatePairSocket(), endpoint);
		}

		/// <summary>
		/// Close the context
		/// </summary>
		public void Terminate()
		{
			if (Interlocked.CompareExchange(ref m_isClosed, 1, 0) == 0)
			{
				ZMQ.Term(m_ctx);
			}
		}

		public void Dispose()
		{
			Terminate();
		}
	}
}
