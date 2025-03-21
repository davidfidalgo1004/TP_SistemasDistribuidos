using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class DataServer
{
    private static int port = 6000;
    private static string dataFile = "dados_oceanicos.txt";
    private static object fileLock = new object(); // Mutex para controlo do acesso ao arquivo

    static void Main()
    {
        TcpListener server = new TcpListener(IPAddress.Any, port);
        server.Start();
        Console.WriteLine("SERVIDOR pronto para receber dados...");

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
                    SaveData(message);
                }
            }
        }
    }

    static void SaveData(string data)
    {
        lock (fileLock)
        {
            File.AppendAllText(dataFile, data + Environment.NewLine);
            Console.WriteLine("Dados armazenados com sucesso.");
        }
    }
}
