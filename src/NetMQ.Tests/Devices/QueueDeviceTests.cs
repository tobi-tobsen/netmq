using System;
using System.Threading;
using NUnit.Framework;
using NetMQ.Devices;

namespace NetMQ.Tests.Devices
{

	public abstract class QueueDeviceTestBase : DeviceTestBase<QueueDevice, RequestSocket, ResponseSocket>
	{
		protected override void SetupTest() {
			CreateDevice = c => new QueueDevice(c, Frontend, Backend);
			CreateClientSocket = c => c.CreateRequestSocket();
			CreateWorkerSocket = c => c.CreateResponseSocket();
		}

		protected override void DoWork(ResponseSocket socket) {
			var received = socket.ReceiveAllString();

			for (var i = 0; i < received.Count; i++) {
				if (i == received.Count - 1) {
					socket.Send(received[i]);
				} else {
					socket.SendMore(received[i]);
				}
			}
		}

		protected override void DoClient(int id, RequestSocket socket) {
			const string value = "Hello World";
			var expected = value + " " + id;
			Console.WriteLine("Client: {0} sending: {1}", id, expected);
			socket.Send(expected);

			Thread.Sleep(Random.Next(1, 50));

			var response = socket.ReceiveAllString();
			Assert.AreEqual(1, response.Count);
			Assert.AreEqual(expected, response[0]);
			Console.WriteLine("Client: {0} received: {1}", id, response[0]);
		}
	}

	[TestFixture]
	public class QueueSingleClientTest : QueueDeviceTestBase
	{
		[Test]
		public void Run() {
			StartClient(0);

			SleepUntilWorkerReceives(1, TimeSpan.FromMilliseconds(100));

			StopWorker();
			Device.Stop();

			Assert.AreEqual(1, WorkerReceiveCount);
		}		
	}

	[TestFixture]
	public class QueueMultiClientTest : QueueDeviceTestBase
	{
		[Test]
		public void Run() {
			for (var i = 0; i < 10; i++) {
				StartClient(i);
			}

			SleepUntilWorkerReceives(10, TimeSpan.FromMilliseconds(1000));

			StopWorker();
			Device.Stop();

			Assert.AreEqual(10, WorkerReceiveCount);
		}
	}
}