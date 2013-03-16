/*
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2007-2009 iMatix Corporation
    Copyright (c) 2011 VMware, Inc.
    Copyright (c) 2007-2011 Other contributors as noted in the AUTHORS file

    This file is part of 0MQ.

    0MQ is free software; you can redistribute it and/or modify it under
    the terms of the GNU Lesser General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.

    0MQ is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Diagnostics;

namespace NetMQ.zmq
{
	public class Req : Dealer
	{


		//  If true, request was already sent and reply wasn't received yet or
		//  was raceived partially.
		private bool m_receivingReply;

		//  If true, we are starting to send/recv a message. The first part
		//  of the message must be empty message part (backtrace stack bottom).
		private bool m_messageBegins;


		public Req(Ctx parent, int tid, int sid)
			: base(parent, tid, sid)
		{


			m_receivingReply = false;
			m_messageBegins = true;
			m_options.SocketType = ZmqSocketType.Req;
		}


		protected override void XSend(Msg msg, SendRecieveOptions flags)
		{
			//  If we've sent a request and we still haven't got the reply,
			//  we can't send another request.
			if (m_receivingReply)
			{
				throw NetMQException.Create("Cannot send another request", ErrorCode.EFSM);				
			}

			
			//  First part of the request is the request identity.
			if (m_messageBegins)
			{
				Msg bottom = new Msg();
				bottom.SetFlags(MsgFlags.More);
				base.XSend(bottom, 0);
				m_messageBegins = false;
			}

			bool more = msg.HasMore;

			base.XSend(msg, flags);
			
			//  If the request was fully sent, flip the FSM into reply-receiving state.
			if (!more)
			{
				m_receivingReply = true;
				m_messageBegins = true;
			}
		}

		override
			protected Msg XRecv(SendRecieveOptions flags)
		{
			Msg msg = null;
			//  If request wasn't send, we can't wait for reply.
			if (!m_receivingReply)
			{
				throw NetMQException.Create(ErrorCode.EFSM);
			}

			//  First part of the reply should be the original request ID.
			if (m_messageBegins)
			{
				msg = base.XRecv(flags);
				if (msg == null)
					return null;

				// TODO: This should also close the connection with the peer!
				if (!msg.HasMore || msg.Size != 0)
				{
					while (true)
					{
						msg = base.XRecv(flags);
						Debug.Assert(msg != null);
						if (!msg.HasMore)
							break;
					}
					throw AgainException.Create();
				}

				m_messageBegins = false;
			}

			msg = base.XRecv(flags);
			if (msg == null)
				return null;

			//  If the reply is fully received, flip the FSM into request-sending state.
			if (!msg.HasMore)
			{
				m_receivingReply = false;
				m_messageBegins = true;
			}

			return msg;
		}

		override
			protected bool XHasIn()
		{
			//  TODO: Duplicates should be removed here.

			if (!m_receivingReply)
				return false;

			return base.XHasIn();
		}

		override
			protected bool XHasOut()
		{
			if (m_receivingReply)
				return false;

			return base.XHasOut();
		}


		public class ReqSession : Dealer.DealerSession
		{


			enum State
			{
				Identity,
				Bottom,
				Body
			};

			State m_state;

			public ReqSession(IOThread ioThread, bool connect,
												SocketBase socket, Options options,
												Address addr)
				: base(ioThread, connect, socket, options, addr)
			{
				m_state = State.Identity;
			}

			override
				public void PushMsg(Msg msg)
			{
				switch (m_state)
				{
					case State.Bottom:
						if (msg.Flags == MsgFlags.More && msg.Size == 0)
						{
							m_state = State.Body;
							base.PushMsg(msg);
						}
						break;
					case State.Body:
						if (msg.Flags == MsgFlags.More)
							base.PushMsg(msg);
						if (msg.Flags == 0)
						{
							m_state = State.Bottom;
							base.PushMsg(msg);
						}
						break;
					case State.Identity:
						if (msg.Flags == 0)
						{
							m_state = State.Bottom;
							base.PushMsg(msg);
						}
						break;
					default:

						throw NetMQException.Create(ErrorCode.EFAULT);
				}
			}

			protected override void Reset()
			{
				base.Reset();
				m_state = State.Identity;
			}

		}

	}
}
