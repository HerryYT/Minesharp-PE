using System;

namespace MCPEServer
{
    class Program
    {
        static void Main()
        {
            Console.WriteLine("MCPE Server software written in C#");

            Server server = new Server();
            server.ReceiveMessages(19132);


        }
    }
}
