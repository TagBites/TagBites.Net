using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TagBites.Net;

namespace Chat.ServerApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var server = new Server("127.0.0.1", 82);
            server.Received += async (s, e) => await server.SendToAllAsync($"{e.Client}: {e.Message}", e.Client);
            server.ClientConnected += async (s, e) => await server.SendToAllAsync($"{e.Client} connected", e.Client);
            server.ClientDisconnected += async (s, e) => await server.SendToAllAsync($"{e.Client} disconnected", e.Client);
            server.Listening = true;

            Console.ReadLine();
        }
    }
}
