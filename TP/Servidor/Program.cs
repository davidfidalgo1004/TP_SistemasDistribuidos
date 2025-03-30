using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;

class DataServer
{
    private static int port = 13000;
    private static string dataFile = "dados_oceanos.txt"; // Arquivo para armazenar os dados recebidos
    private static object fileLock = new object();

    static void Main()
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

    static void HandleClient(TcpClient client)
    {
        try
        {
            using (NetworkStream stream = client.GetStream())
            {
                // Usamos um MemoryStream para acumular os bytes lidos
                using (MemoryStream ms = new MemoryStream())
                {
                    byte[] buffer = new byte[1024];
                    int bytesRead;
                    // Lê os dados enquanto houver bytes sendo enviados
                    while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        ms.Write(buffer, 0, bytesRead);
                    }
                    // Converte os bytes lidos para string (conteúdo do JSON)
                    string jsonData = Encoding.UTF8.GetString(ms.ToArray());
                    Console.WriteLine($"Arquivo JSON recebido: {jsonData}");

                    // Caso queira desserializar os dados para uma lista, por exemplo:
                    // var dados = JsonSerializer.Deserialize<List<string>>(jsonData);

                    // Armazena os dados recebidos em um arquivo, se necessário:
                    lock (fileLock)
                    {
                        File.AppendAllText(dataFile, jsonData + Environment.NewLine);
                        Console.WriteLine("Dados armazenados com sucesso.");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Erro ao receber dados: " + e.Message);
        }
    }
}
