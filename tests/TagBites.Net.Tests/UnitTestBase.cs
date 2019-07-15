using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace TagBites.Net.Tests
{
    public class UnitTestBase
    {
        private string ServerHost { get; }
        private int ServerPort { get; }
        private NetworkConfig Config { get; }

        public UnitTestBase()
        {
            Config = NetworkConfig.Default;
            ServerPort = 54300;
            ServerHost = "127.0.0.1";
        }


        protected Server CreateServer(NetworkConfig config = null)
        {
            return new Server(ServerHost, ServerPort, config ?? Config);
        }
        protected Client CreateClient(NetworkConfig config = null)
        {
            return new Client(ServerHost, ServerPort, config ?? Config);
        }
    }
}
