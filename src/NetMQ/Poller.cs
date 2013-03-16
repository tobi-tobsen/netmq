﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using NetMQ.zmq;

namespace NetMQ
{
	public class Poller
	{
		class PollerPollItem : PollItem
		{
			private NetMQSocket m_socket;

			public PollerPollItem(NetMQSocket socket, PollEvents events)
				: base(socket.SocketHandle, events)
			{
				m_socket = socket;
			}

			public NetMQSocket NetMQSocket
			{
				get { return m_socket; }
			}
		}

		private readonly IList<NetMQSocket> m_sockets = new List<NetMQSocket>();

		private PollItem[] m_pollset;
		private NetMQSocket[] m_pollact;
		private int m_pollSize;

		readonly List<NetMQTimer> m_timers = new List<NetMQTimer>();
		readonly List<NetMQTimer> m_zombies = new List<NetMQTimer>();

		readonly CancellationTokenSource m_cancellationTokenSource;
		readonly ManualResetEvent m_isStoppedEvent = new ManualResetEvent(false);
		private bool m_isStarted;

		private bool m_isDirty = true;

		public Poller()
		{
			PollTimeout = 1000;

			m_cancellationTokenSource = new CancellationTokenSource();
		}

		/// <summary>
		/// Poll timeout in milliseconds
		/// </summary>
		public int PollTimeout { get; set; }

		public bool IsStarted { get { return m_isStarted; } }

		public void AddSocket(NetMQSocket socket)
		{
			if (m_sockets.Contains(socket))
			{
				throw new ArgumentException("Socket already added to poller");
			}

			m_sockets.Add(socket);

			socket.EventsChanged += OnSocketEventsChanged;

			m_isDirty = true;
		}

		public void RemoveSocket(NetMQSocket socket)
		{
			socket.EventsChanged -= OnSocketEventsChanged;

			m_sockets.Remove(socket);
			m_isDirty = true;
		}

		private void OnSocketEventsChanged(object sender, NetMQSocketEventArgs e)
		{
			// when the sockets SendReady or ReceiveReady changed we marked the poller as dirty in order to reset the poll events
			m_isDirty = true;
		}

		public void AddTimer(NetMQTimer timer)
		{
			m_timers.Add(timer);
		}

		public void RemoveTimer(NetMQTimer timer)
		{
			timer.When = -1;
			m_zombies.Add(timer);
		}

		private void RebuildPollset()
		{
			m_pollset = null;
			m_pollact = null;

			m_pollSize = m_sockets.Count;
			m_pollset = new PollItem[m_pollSize];
			m_pollact = new NetMQSocket[m_pollSize];

			uint itemNbr = 0;
			foreach (var socket in m_sockets)
			{
				m_pollset[itemNbr] = new PollItem(socket.SocketHandle, socket.GetPollEvents());
				m_pollact[itemNbr] = socket;
				itemNbr++;
			}
			m_isDirty = false;
		}


		int TicklessTimer()
		{
			//  Calculate tickless timer
			Int64 tickless = Clock.NowMs() + PollTimeout;

			foreach (NetMQTimer timer in m_timers)
			{
				//  Find earliest timer
				if (timer.When == -1 && timer.Enable)
				{
					timer.When = timer.Interval + Clock.NowMs();
				}

				if (tickless > timer.When)
				{
					tickless = timer.When;
				}
			}

			int timeout = (int)(tickless - Clock.NowMs());
			if (timeout < 0)
			{
				timeout = 0;
			}

			return timeout;
		}

		public void Start()
		{
			Thread.CurrentThread.Name = "NetMQPollerThread";

			m_isStoppedEvent.Reset();
			m_isStarted = true;
			try
			{
				// the sockets may have been created in another thread, to make sure we can fully use them we do full memory barried
				// at the begining of the loop
				Thread.MemoryBarrier();

				//  Recalculate all timers now
				foreach (NetMQTimer netMQTimer in m_timers)
				{
					if (netMQTimer.Enable)
					{
						netMQTimer.When = Clock.NowMs() + netMQTimer.Interval;
					}
				}

				while (!m_cancellationTokenSource.IsCancellationRequested)
				{
					if (m_isDirty)
					{
						RebuildPollset();
					}

					ZMQ.Poll(m_pollset, m_pollSize, TicklessTimer());

					// that way we make sure we can continue the loop if new timers are added.
					// timers cannot be removed
					int timersCount = m_timers.Count;
					for (int i = 0; i < timersCount; i++)
					{
						var timer = m_timers[i];

						if (Clock.NowMs() >= timer.When && timer.When != -1)
						{
							timer.InvokeElapsed(this);

							if (timer.Enable)
							{
								timer.When = timer.Interval + Clock.NowMs();
							}
						}
					}

					for (int itemNbr = 0; itemNbr < m_pollSize; itemNbr++)
					{
						NetMQSocket socket = m_pollact[itemNbr];
						PollItem item = m_pollset[itemNbr];

						if (item.ResultEvent.HasFlag(PollEvents.PollError) && !socket.IgnoreErrors)
						{
							socket.Errors++;

							if (socket.Errors > 1)
							{
								RemoveSocket(socket);
								item.ResultEvent = PollEvents.None;
							}
						}
						else
						{
							socket.Errors = 0;
						}

						if (item.ResultEvent != PollEvents.None)
						{
							socket.InvokeEvents(this, item.ResultEvent);
						}
					}

					if (m_zombies.Any())
					{
						//  Now handle any timer zombies
						//  This is going to be slow if we have many zombies
						foreach (NetMQTimer netMQTimer in m_zombies)
						{
							m_timers.Remove(netMQTimer);
						}

						m_zombies.Clear();
					}
				}
			}
			finally
			{
				m_isStoppedEvent.Set();
			}
		}

		/// <summary>
		/// Stop the poller job, it may take a while until the poller is fully stopped
		/// </summary>
		/// <param name="waitForCloseToComplete">if true the method will block until the poller is fully stopped</param>
		public void Stop(bool waitForCloseToComplete)
		{
			m_cancellationTokenSource.Cancel();

			if (waitForCloseToComplete)
			{
				m_isStoppedEvent.WaitOne();
			}

			m_isStarted = false;
		}

		public void Stop()
		{
			Stop(true);
		}
	}
}
