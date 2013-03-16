/*
    Copyright (c) 2010-2011 250bpm s.r.o.
    Copyright (c) 2010-2011 Other contributors as noted in the AUTHORS file

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
using System.Collections.Generic;
using System.Diagnostics;

//  Base class for objects forming a part of ownership hierarchy.
//  It handles initialisation and destruction of such objects.
namespace NetMQ.zmq
{
	abstract public class Own : ZObject
	{

		protected Options m_options;

		//  True if termination was already initiated. If so, we can destroy
		//  the object if there are no more child objects or pending term acks.
		private bool m_terminating;

		//  Sequence number of the last command sent to this object.
		private readonly AtomicLong m_sentSeqnum;

		//  Sequence number of the last command processed by this object.
		private long m_processedSeqnum;

		//  Socket owning this object. It's responsible for shutting down
		//  this object.
		private Own m_owner;

		//  List of all objects owned by this socket. We are responsible
		//  for deallocating them before we quit.
		//typedef std::set <own_t*> owned_t;
		private HashSet<Own> owned;

		//  Number of events we have to get before we can destroy the object.
		private int m_termAcks;


		//  Note that the owner is unspecified in the constructor.
		//  It'll be supplied later on when the object is plugged in.

		//  The object is not living within an I/O thread. It has it's own
		//  thread outside of 0MQ infrastructure.
		public Own(Ctx parent, int tid)
			: base(parent, tid)
		{
			m_terminating = false;
			m_sentSeqnum = new AtomicLong(0);
			m_processedSeqnum = 0;
			m_owner = null;
			m_termAcks = 0;

			m_options = new Options();
			owned = new HashSet<Own>();
		}

		//  The object is living within I/O thread.
		public Own(IOThread ioThread, Options options)
			: base(ioThread)
		{
			m_options = options;
			m_terminating = false;
			m_sentSeqnum = new AtomicLong(0);
			m_processedSeqnum = 0;
			m_owner = null;
			m_termAcks = 0;

			owned = new HashSet<Own>();
		}

		abstract public void Destroy();

		//  A place to hook in when phyicallal destruction of the object
		//  is to be delayed.
		protected virtual void ProcessDestroy()
		{
			Destroy();
		}


		private void SetOwner(Own owner)
		{
			Debug.Assert(m_owner == null);
			m_owner = owner;
		}

		//  When another owned object wants to send command to this object
		//  it calls this function to let it know it should not shut down
		//  before the command is delivered.
		public void IncSeqnum()
		{
			//  This function may be called from a different thread!
			m_sentSeqnum.IncrementAndGet();
		}

		protected override void ProcessSeqnum()
		{
			//  Catch up with counter of processed commands.
			m_processedSeqnum++;

			//  We may have catched up and still have pending terms acks.
			CheckTermAcks();
		}

		//  Launch the supplied object and become its owner.
		protected void LaunchChild(Own object_)
		{
			//  Specify the owner of the object.
			object_.SetOwner(this);

			//  Plug the object into the I/O thread.
			SendPlug(object_);

			//  Take ownership of the object.
			SendOwn(this, object_);
		}






		//  Terminate owned object
		protected void TermChild(Own object_)
		{
			ProcessTermReq(object_);
		}


		protected override void ProcessTermReq(Own object_)
		{
			//  When shutting down we can ignore termination requests from owned
			//  objects. The termination request was already sent to the object.
			if (m_terminating)
				return;

			//  If I/O object is well and alive let's ask it to terminate.

			//  If not found, we assume that termination request was already sent to
			//  the object so we can safely ignore the request.
			if (!owned.Contains(object_))
				return;

			owned.Remove(object_);
			RegisterTermAcks(1);

			//  Note that this object is the root of the (partial shutdown) thus, its
			//  value of linger is used, rather than the value stored by the children.
			SendTerm(object_, m_options.Linger);
		}


		protected override void ProcessOwn(Own object_)
		{
			//  If the object is already being shut down, new owned objects are
			//  immediately asked to terminate. Note that linger is set to zero.
			if (m_terminating)
			{
				RegisterTermAcks(1);
				SendTerm(object_, 0);
				return;
			}

			//  Store the reference to the owned object.
			owned.Add(object_);
		}

		//  Ask owner object to terminate this object. It may take a while
		//  while actual termination is started. This function should not be
		//  called more than once.
		protected void Terminate()
		{
			//  If termination is already underway, there's no point
			//  in starting it anew.
			if (m_terminating)
				return;

			//  As for the root of the ownership tree, there's noone to terminate it,
			//  so it has to terminate itself.
			if (m_owner == null)
			{
				ProcessTerm(m_options.Linger);
				return;
			}

			//  If I am an owned object, I'll ask my owner to terminate me.
			SendTermReq(m_owner, this);
		}

		//  Returns true if the object is in process of termination.
		protected bool IsTerminating
		{
			get { return m_terminating; }
		}

		//  Term handler is protocted rather than private so that it can
		//  be intercepted by the derived class. This is useful to add custom
		//  steps to the beginning of the termination process.
		override
			protected void ProcessTerm(int linger)
		{
			//  Double termination should never happen.
			Debug.Assert(!m_terminating);

			//  Send termination request to all owned objects.
			foreach (Own it in owned)
			{
				SendTerm(it, linger);
			}
			
			RegisterTermAcks(owned.Count);
			owned.Clear();

			//  Start termination process and check whether by chance we cannot
			//  terminate immediately.
			m_terminating = true;
			CheckTermAcks();
		}

		//  Use following two functions to wait for arbitrary events before
		//  terminating. Just add number of events to wait for using
		//  register_tem_acks functions. When event occurs, call
		//  remove_term_ack. When number of pending acks reaches zero
		//  object will be deallocated.
		public void RegisterTermAcks(int count)
		{
			m_termAcks += count;
		}

		public void UnregisterTermAck()
		{
			Debug.Assert(m_termAcks > 0);
			m_termAcks--;
			
			//  This may be a last ack we are waiting for before termination...
			CheckTermAcks();
		}



		override
			protected void ProcessTermAck()
		{

			UnregisterTermAck();

		}


		private void CheckTermAcks()
		{
		
			if (m_terminating && m_processedSeqnum == m_sentSeqnum.Get() &&
					m_termAcks == 0)
			{

				//  Sanity check. There should be no active children at this point.
				Debug.Assert(owned.Count == 0);

				//  The root object has nobody to confirm the termination to.
				//  Other nodes will confirm the termination to the owner.
				if (m_owner != null)
					SendTermAck(m_owner);

				//  Deallocate the resources.
				ProcessDestroy();
			}
		}



	}
}
