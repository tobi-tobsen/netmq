using System;
using System.Linq;
using NUnit.Framework;
using NetMQ.Devices;
using NetMQ.Sockets;

namespace NetMQ.Tests.Devices
{
    public abstract class ForwarderDeviceTestBase : DeviceTestBase<ForwarderDevice, SubscriberSocket>
    {

        protected const string Topic = "CanHazTopic";

        protected override void SetupTest()
        {
            CreateDevice = c =>
            {
                var device = new ForwarderDevice(c, Frontend, Backend);
                device.FrontendSetup.Subscribe(Topic);
                return device;
            };

            CreateClientSocket = c => c.CreatePublisherSocket();
        }

        protected override void WorkerSocketAfterConnect(SubscriberSocket socket)
        {
            socket.Subscribe(Topic);
        }

        protected override void DoWork(NetMQSocket socket)
        {
            var received = socket.ReceiveStringMessages().ToList();
            Console.WriteLine("Worker received: ");

            for (var i = 0; i < received.Count; i++)
            {
                var r = received[i];
                Console.WriteLine("{0}: {1}", i, r);
            }

            Console.WriteLine("------");
        }

        protected override void DoClient(int id, NetMQSocket socket)
        {
            const string value = "Hello World";
            var expected = value + " " + id;
            Console.WriteLine("Client: {0} Publishing: {1}", id, expected);
            socket.SendMore(Topic);
            socket.Send(expected);
        }

        protected override SubscriberSocket CreateWorkerSocket(NetMQContext context)
        {
            return context.CreateSubscriberSocket();
        }
    }

    [TestFixture]
    public class ForwarderSingleClientTest : ForwarderDeviceTestBase
    {
        [Test]
        public void Run()
        {

            // Allow the subscribers some time to connect
            StartClient(0, 100);

            SleepUntilWorkerReceives(1, TimeSpan.FromMilliseconds(1000));

            StopWorker();
            Device.Stop();

            Assert.AreEqual(1, WorkerReceiveCount);
        }
    }

    [TestFixture]
    public class ForwarderMultiClientTest : ForwarderDeviceTestBase
    {
        [Test]
        public void Run()
        {
            for (var i = 0; i < 10; i++)
            {
                // Allow the subscribers some time to connect
                StartClient(i, 100);
            }

            SleepUntilWorkerReceives(10, TimeSpan.FromMilliseconds(8000));

            StopWorker();
            Device.Stop();

            Assert.AreEqual(10, WorkerReceiveCount);
        }
    }

    [TestFixture]
    public class ForwardDevicePollerBug_Issue72
    {
        [Test]
        public void ShouldNotBlockAfterDisposeOfContext()
        {
            // https://github.com/zeromq/netmq/issues/72: 
            // "I just instantiated a ForwarderDevice subscribed some topics and then immediately disposed the context. [...] The NetMQ.zmq.Signaler.WaitEvent(int timeout) blocks the processing..."

            var ctx = NetMQContext.Create();

            ForwarderDevice device = new ForwarderDevice(ctx, "inproc://front.addr", "inproc://back.addr");
            device.FrontendSetup.Subscribe("test");
            device.Start();
            
            var timer = new System.Timers.Timer(1000);
            timer.Elapsed += (s, e) => { Assert.Fail("Process is blocking"); };
            timer.Start();
            // device.Stop(); // <-- if this is missing, the process hangs.
            ctx.Dispose();
            timer.Stop();
        }
    }
}