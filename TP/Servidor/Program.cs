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
        try
        {
            TcpListener server = new TcpListener(IPAddress.Parse("127.0.0.1"), port);
            server.Start();
            Console.WriteLine("SERVIDOR pronto para receber dados...");

            while (true)
            {
                TcpClient client = server.AcceptTcpClient();
                Thread clientThread = new Thread(() => HandleClient(client));
                clientThread.Start();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Erro ao iniciar servidor: " + e.Message);
        }
    }

    static void HandleClient(TcpClient client)
    {
        try
        {
            using (client)
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

            Console.WriteLine("Conexão com agregador encerrada.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro no cliente: " + ex.Message);
        }
    }

    static void SaveData(string data)
    {
        lock (fileLock)
        {
            try
            {
                string dir = Path.GetDirectoryName(dataFile);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.AppendAllText(dataFile, $"[{DateTime.Now}] {data}{Environment.NewLine}");
                Console.WriteLine("Dados armazenados com sucesso.");
            }
            catch (Exception e)
            {
                Console.WriteLine("Erro ao salvar dados: " + e.Message);
            }
        }
    }
}
