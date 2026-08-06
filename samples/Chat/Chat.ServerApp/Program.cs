using TagBites.Net;

namespace Chat.ServerApp;

internal class Program
{
    private static void Main()
    {
        var server = new Server("127.0.0.1", 8200);
        server.ClientConnected += async (s, e) =>
        {
            Console.WriteLine($"{e.Client} connected, {server.GetClients().Length} client(s) online.");
            await server.SendToAllAsync($"{e.Client} connected", e.Client);
        };
        server.ClientDisconnected += async (s, e) =>
        {
            Console.WriteLine($"{e.Client} disconnected, {server.GetClients().Length} client(s) online.");
            await server.SendToAllAsync($"{e.Client} disconnected", e.Client);
        };
        server.Received += async (s, e) =>
        {
            Console.WriteLine($"{e.Client}: {e.Message}");
            await server.SendToAllAsync($"{e.Client}: {e.Message}", e.Client);
        };
        server.Listening = true;

        Console.WriteLine($"Server is listening on {server.LocalEndpoint}, waiting for clients.");
        Console.ReadLine();
    }
}
