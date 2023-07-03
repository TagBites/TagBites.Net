using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TagBites.Net;

namespace Chat.ClientApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var client = new Client("127.0.0.1", 82);
            client.Received += (s, e) => Console.WriteLine(e.Message.ToString());
            await client.ConnectAsync();

            while (true)
                await client.SendAsync(Console.ReadLine());
        }
    }
}
