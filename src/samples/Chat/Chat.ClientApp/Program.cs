using TagBites.Net;

namespace Chat.ClientApp;

internal class Program
{
    private static async Task Main()
    {
        var client = new Client("127.0.0.1", 82);
        client.Received += (s, e) => Console.WriteLine(e.Message.ToString());
        await client.ConnectAsync();

        while (true)
            await client.SendAsync(Console.ReadLine());

        // ReSharper disable once FunctionNeverReturns
    }
}