using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace NetMQ.Tests
{
    class NetMQTestRunner
    {
        [STAThread]
        static void Main(string[] args)
        {
            NUnit.ConsoleRunner.Runner.Main(new string[] { "/fixture:NetMQ.Tests.Devices.ForwardDevicePollerBug_Issue72", Assembly.GetExecutingAssembly().Location });
        }
    }
}
