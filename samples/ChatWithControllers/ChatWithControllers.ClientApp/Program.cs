using TagBites.Net;

namespace ChatWithControllers.ClientApp;

internal class Program
{
    private static void Main()
    {
        var client = new Client("127.0.0.1", 10500);
        client.Use<IChatClient, ChatClient>();
        client.ConnectAsync().Wait();

        while (true)
        {
            var message = Console.ReadLine();
            client.GetController<IChatServer>().Send(message);
        }

        // ReSharper disable once FunctionNeverReturns
    }
}

public class ChatClient : IChatClient
{
    public void MessageReceive(string userName, string message)
    {
        Console.WriteLine($"{userName ?? "Server"}: {message}");
    }
}