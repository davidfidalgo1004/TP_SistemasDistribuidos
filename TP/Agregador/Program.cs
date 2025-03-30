using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;

class AggregatorServer
{
    private static int port = 4000;
    private static string serverIP = "127.0.0.1"; // IP do servidor
    private static int serverPort = 13000;         // Porta do servidor
    private static string jsonFileName = "dados.json";

    // Lista para acumular os dados dos wavys
    private static List<string> dadosRecebidos = new List<string>();
    private static object dadosLock = new object();

    // Timer para atualizar o JSON a cada 30 minutos
    private static Timer jsonUpdateTimer;

    static void Main()
    {
        // Agendar o envio do arquivo para as 22:30
        AgendarEnvio();

        // Agendar a atualização do JSON a cada 30 minutos
        jsonUpdateTimer = new Timer(AtualizarJsonCallback, null, TimeSpan.Zero, TimeSpan.FromMinutes(30));

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
                    lock (dadosLock)
                    {
                        // Acumula os dados recebidos
                        dadosRecebidos.Add(message);
                    }
                }
            }
        }
    }

    // Callback do timer que chama a atualização do JSON a cada 30 minutos
    static void AtualizarJsonCallback(object state)
    {
        lock (dadosLock)
        {
            AtualizarJson();
        }
    }

    // Serializa a lista de dados para o arquivo JSON
    static void AtualizarJson()
    {
        string json = JsonSerializer.Serialize(dadosRecebidos, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(jsonFileName, json);
        Console.WriteLine("Arquivo JSON atualizado.");
    }

    // Agendamento do envio do arquivo para as 22:30
    static void AgendarEnvio()
    {
        DateTime agora = DateTime.Now;
        DateTime proximoEnvio = new DateTime(agora.Year, agora.Month, agora.Day, 22, 30, 0);
        if (agora > proximoEnvio)
            proximoEnvio = proximoEnvio.AddDays(1);

        TimeSpan tempoAteEnvio = proximoEnvio - agora;
        Timer envioTimer = new Timer(EnviarDados, null, tempoAteEnvio, Timeout.InfiniteTimeSpan);
        Console.WriteLine($"Envio agendado para: {proximoEnvio}");
    }

    // Envia o arquivo JSON para o servidor e limpa a lista de dados
    static void EnviarDados(object state)
    {
        try
        {
            using (TcpClient serverClient = new TcpClient(serverIP, serverPort))
            using (NetworkStream stream = serverClient.GetStream())
            {
                byte[] fileBytes = File.ReadAllBytes(jsonFileName);
                stream.Write(fileBytes, 0, fileBytes.Length);
                Console.WriteLine($"Arquivo {jsonFileName} enviado para o servidor.");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Erro ao enviar o arquivo: {e.Message}");
        }

        // Após o envio, limpa os dados acumulados e atualiza o JSON
        lock (dadosLock)
        {
            dadosRecebidos.Clear();
            AtualizarJson();
        }

        // Reagenda o envio para o próximo dia
        AgendarEnvio();
    }
}
