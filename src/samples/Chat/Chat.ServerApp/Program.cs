using TagBites.Net;

namespace Chat.ServerApp;

internal class Program
{
    private static void Main()
    {
        var server = new Server("127.0.0.1", 82);
        server.Received += async (s, e) => await server.SendToAllAsync($"{e.Client}: {e.Message}", e.Client);
        server.ClientConnected += async (s, e) => await server.SendToAllAsync($"{e.Client} connected", e.Client);
        server.ClientDisconnected += async (s, e) => await server.SendToAllAsync($"{e.Client} disconnected", e.Client);
        server.Listening = true;

        Console.ReadLine();
    }
}