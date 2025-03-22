using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class AggregatorServer
{
    private static int port = 4000;
    private static string serverIP = "127.0.0.1"; // IP do SERVIDOR
    private static int serverPort = 13000;         //Porta do SERVIDOR

    static void Main()
    {
        TcpListener server = new TcpListener(IPAddress.Any, port);
        server.Start();
        Console.WriteLine("AGREGADOR pronto para receber conexões...");

        while (true)
        {
            TcpClient client = server.AcceptTcpClient();
            Thread clientThread = new Thread(() => HandleClient(client));
            clientThread.Start();
        }
    }

    static void HandleClient(TcpClient client)
    {
        using (NetworkStream stream = client.GetStream())
        {
            byte[] buffer = new byte[1024];
            int bytesRead;

            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Console.WriteLine($"Recebido: {message}");

                if (message.StartsWith("DADOS"))
                {
                    ForwardToServer(message);
                }
            }
        }
    }

    static void ForwardToServer(string data)
    {
        try
        {
            using (TcpClient serverClient = new TcpClient(serverIP, serverPort))
            using (NetworkStream stream = serverClient.GetStream())
            {
                byte[] messageBytes = Encoding.UTF8.GetBytes(data);
                stream.Write(messageBytes, 0, messageBytes.Length);
                Console.WriteLine($"Encaminhado para SERVIDOR: {data}");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Erro ao encaminhar: {e.Message}");
        }
    }
}
