NetMQ
=====

NetMQ is 100% native C# port of ZeroMQ.

NetMQ is lightweight messaging library which extends the
standard socket interfaces with features traditionally provided by
specialised messaging middleware products. NetMQ sockets provide an
abstraction of asynchronous message queues, multiple messaging patterns,
message filtering (subscriptions), seamless access to multiple transport
protocols and more.

NetMQ is still under development, although the current repository is pretty stable.


## Installation

You can find NetMQ in [nuget](https://nuget.org/packages/NetMQ/).

## Using

For using NetMQ make sure you read the [ZeroMQ Guide](http://zguide.zeromq.org/page:all). 
NetMQ documentation is still work in progress but you can found small example [here](https://gist.github.com/somdoron/5175967).

	using (NetMQContext ctx = NetMQContext.Create())
	{
		using (var server = ctx.CreateResponseSocket())
		{
			server.Bind("tcp://127.0.0.1:5556");
 
			using (var client = ctx.CreateRequestSocket())
			{
				client.Connect("tcp://127.0.0.1:5556");
 
				client.Send("Hello");
 
				string m1 = server.ReceiveString();
 
				Console.WriteLine("From Client: {0}", m1);
 
				server.Send("Hi Back");
 
				string m2 = client.ReceiveString();
 
				Console.WriteLine("From Server: {0}", m2);
 
				Console.ReadLine();
			}
		}
	}

## Contributing

We need help, so if you have good knowledge in C# and ZeroMQ just grab one of the issues and add a pull request.
We are using [C4 process](http://rfc.zeromq.org/spec:16), so make sure you read this before.

Some of the areas we need help with:
* Testing with libzmq (original zeromq library) in version 2.2 and 3.2.
* Porting tests from ZeroMQ to c#.
* Document the High Level API using C# style comments
* Test IPv6
* Compile on Mono and run on linux
* Make another High Level API which is the same as CLRZMQ

You can also help us with the following
* Joining our mailing list and be an active member
* Write tutorials in the github wiki
* Write about the probject in your blog

## Mailing list

you can join our mailing list at https://groups.google.com/d/forum/netmq-dev?hl=en. 

## Who owns NetMQ?

NetMQ is owned by all its authors and contributors. 
This is an open source project licensed under the LGPLv3. 
To contribute to NetMQ please read the [C4 process](http://rfc.zeromq.org/spec:16), it's what we use.
Their are open issues in the issues tab that still need to take care of, feel free to pick one up and submit a patch to the project.

