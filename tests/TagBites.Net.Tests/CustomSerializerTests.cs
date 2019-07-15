using System;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace TagBites.Net.Tests
{
    public class CustomSerializerTests : UnitTestBase
    {
        public static object[][] Configs { get; } =
        {
            new object[]{ new NetworkConfig(Encoding.UTF8) },
            new object[]{ new NetworkConfig(Encoding.Default) },
            new object[]{ new NetworkConfig(Encoding.UTF8, new NewtonsoftJsonSerializer()) },
            new object[]{ new NetworkConfig(Encoding.Default, new NewtonsoftJsonSerializer()) }
        };


        [Theory]
        [MemberData(nameof(Configs))]
        public async Task SendReceiveTest(NetworkConfig config)
        {
            // Helpers
            object testObject = null;
            var received = false;

            async Task Send(NetworkClient networkClient, object message)
            {
                received = false;
                testObject = message;
                await networkClient.SendAsync(message);
            }
            void Received(object message)
            {
                received = true;
                var other = message as SimpleClass;

                // ReSharper disable once AccessToModifiedClosure
                Assert.Equal(testObject, other);
                // ReSharper disable once AccessToModifiedClosure
                Assert.False(ReferenceEquals(testObject, other));
            }
            async Task WaitForReceive()
            {
                // ReSharper disable once AccessToModifiedClosure
                for (var i = 0; i < 5 && !received; i++)
                    await Task.Delay(10);

                // ReSharper disable once AccessToModifiedClosure
                Assert.True(received);
            }

            // Test
            using var server = CreateServer(config);
            server.Received += (s, e) => Received(e.Message);
            server.Listening = true;

            using var client = CreateClient(config);
            client.Received += (s, e) => Received(e.Message);
            await client.ConnectAsync();

            await Send(client, new SimpleClass() { A = 1, B = 2 });
            await WaitForReceive();

            await Send(server.GetClients()[0], new SimpleClass() { A = 2, B = 1 });
            await WaitForReceive();
        }

        [Theory]
        [MemberData(nameof(Configs))]
        public async Task RmiTest(NetworkConfig config)
        {
            // Helpers
            object testObject = null;
            var received = false;

            Task Send(INetworkHub networkHub, object message)
            {
                received = false;
                testObject = message;
                return networkHub.Send(message);
            }
            void Received(object message)
            {
                received = true;
                var other = message as SimpleClass;

                // ReSharper disable once AccessToModifiedClosure
                Assert.Equal(testObject, other);
                // ReSharper disable once AccessToModifiedClosure
                Assert.False(ReferenceEquals(testObject, other));
            }
            async Task WaitForReceive()
            {
                // ReSharper disable once AccessToModifiedClosure
                for (var i = 0; i < 5 && !received; i++)
                    await Task.Delay(10);

                // ReSharper disable once AccessToModifiedClosure
                Assert.True(received);
            }

            // Test
            var hub = new NetworkHub();
            hub.Received += (s, e) => Received(e.Message);

            using var server = CreateServer(config);
            server.Use<INetworkHub, NetworkHub>(x => hub);
            server.Listening = true;

            using var client = CreateClient(config);
            client.Use<INetworkHub, NetworkHub>(hub);
            await client.ConnectAsync();

            await Send(client.GetController<INetworkHub>(), new SimpleClass() { A = 1, B = 2 });
            await WaitForReceive();

            await Send(server.GetClients()[0].GetController<INetworkHub>(), new SimpleClass() { A = 1, B = 2 });
            await WaitForReceive();
        }

        [Serializable]
        private sealed class SimpleClass
        {
            public int A { get; set; }
            public int B { get; set; }


            private bool Equals(SimpleClass other)
            {
                return A == other.A && B == other.B;
            }
            public override bool Equals(object obj)
            {
                return ReferenceEquals(this, obj) || obj is SimpleClass other && Equals(other);
            }
            public override int GetHashCode()
            {
                unchecked
                {
                    return (A * 397) ^ B;
                }
            }
        }
        public interface INetworkHub
        {
            Task Send(object message);
        }
        private class NetworkHub : INetworkHub
        {
            public event EventHandler<MessageEventArgs> Received;


            public async Task Send(object message) => Received?.Invoke(this, new MessageEventArgs() { Message = message });
        }
        public class MessageEventArgs : EventArgs
        {
            public object Message { get; set; }
        }
    }
}
