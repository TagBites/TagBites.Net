using System;
using System.Threading.Tasks;
using TagBites.Net;

namespace ChatWithControllers.ClientApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var client = new Client("127.0.0.1", 10500);
            client.Use<IChatClient, ChatClient>();
            client.ConnectAsync().Wait();

            while (true)
            {
                var message = Console.ReadLine();
                client.GetController<IChatServer>().Send(message);
            }
        }
    }

    public class ChatClient : IChatClient
    {
        public void MessageReceive(string userName, string message)
        {
            Console.WriteLine($"{userName ?? "Server"}: {message}");
        }
        public Task MessageReceiveAsync(string userName, string message)
        {
            MessageReceive(userName, message);
            return Task.CompletedTask;
        }
    }
}
