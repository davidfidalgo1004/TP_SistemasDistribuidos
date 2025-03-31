using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using Agregador;

class AggregatorServer
{
    // Porta onde os wavys se conectam para enviar dados
    private static int wavysPort = 4000;
    // Dados de conexão com o servidor
    private static string serverIP = "127.0.0.1";
    private static int serverPort = 13000;
    private static string jsonFileName = "dados.json";

    // Lista para acumular os dados recebidos dos wavys
    private static List<string> dadosRecebidos = new List<string>();
    private static object dadosLock = new object();

    // Timer para atualizar o JSON a cada 30 minutos
    private static Timer jsonUpdateTimer;
    // Timer para enviar os dados agendados (ex.: às 22:30)
    private static Timer envioTimer;

    // Conexão persistente com o servidor
    private static TcpClient persistentServerClient;
    private static StreamReader serverReader;
    private static StreamWriter serverWriter;

    static void Main()
    {
        // Conecta imediatamente ao servidor ao iniciar
        ConnectToServer();

        // Agenda o envio do arquivo (por exemplo, às 22:30)
        AgendarEnvio();

        // Agenda a atualização do JSON a cada 30 minutos
        jsonUpdateTimer = new Timer(AtualizarJsonCallback, null, TimeSpan.Zero, TimeSpan.FromMinutes(30));

        // Inicia o listener para receber conexões dos wavys
        TcpListener listener = new TcpListener(IPAddress.Parse("127.0.0.1"), wavysPort);
        listener.Start();
        Console.WriteLine("AGREGADOR pronto para receber conexões de wavys...");

        while (true)
        {
            TcpClient client = listener.AcceptTcpClient();
            Thread clientThread = new Thread(() => HandleWavyClient(client));
            clientThread.Start();
        }
    }

    // Método para conectar-se ao servidor e aguardar o "OK 100"
    static void ConnectToServer()
    {
        try
        {
            persistentServerClient = new TcpClient(serverIP, serverPort);
            NetworkStream ns = persistentServerClient.GetStream();
            serverReader = new StreamReader(ns, Encoding.UTF8);
            serverWriter = new StreamWriter(ns, Encoding.UTF8) { AutoFlush = true };

            string greeting = serverReader.ReadLine();
            Console.WriteLine(greeting + "\nConexão ao servidor efetuado com sucesso!");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao conectar ao servidor: " + ex.Message);
        }
    }

    // Trata a conexão dos wavys (que enviam dados)
    static void HandleWavyClient(TcpClient client)
    {
        using (NetworkStream stream = client.GetStream())
        {
            byte[] buffer = new byte[1024];
            int bytesRead;
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Console.WriteLine($"Recebido de wavys: {message}");
                if (message.StartsWith("DADOS"))
                {
                    lock (dadosLock)
                    {
                        dadosRecebidos.Add(message);
                    }
                }
            }
        }
    }

    // Callback do timer que atualiza o JSON a cada 30 minutos
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

    // Agenda o envio do arquivo para um horário fixo (exemplo: 22:30)
    static void AgendarEnvio()
    {
        DateTime agora = DateTime.Now;
        DateTime proximoEnvio = new DateTime(agora.Year, agora.Month, agora.Day, 14,42, 0);
        if (agora > proximoEnvio)
            proximoEnvio = proximoEnvio.AddDays(1);
        TimeSpan tempoAteEnvio = proximoEnvio - agora;
        envioTimer = new Timer(EnviarDados, null, tempoAteEnvio, Timeout.InfiniteTimeSpan);
        Console.WriteLine($"Envio agendado para: {proximoEnvio}");
    }

    // Envia o arquivo JSON para o servidor usando a conexão persistente
    static void EnviarDados(object state)
    {
        try
        {
            // Atualiza o JSON antes de enviar
            lock (dadosLock)
            {
                AtualizarJson();
            }

            string jsonContent = File.ReadAllText(jsonFileName);
            // Envia o conteúdo JSON via conexão persistente
            serverWriter.WriteLine(jsonContent);
            Console.WriteLine($"Arquivo {jsonFileName} enviado para o servidor.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao enviar o arquivo: " + ex.Message);
        }

        // Após o envio, limpa os dados e atualiza o JSON
        lock (dadosLock)
        {
            dadosRecebidos.Clear();
            AtualizarJson();
        }

        // Reagenda o próximo envio
        AgendarEnvio();
    }

    // Opcional: método para encerrar a conexão de forma graciosa
    static void DisconnectFromServer()
    {
        try
        {
            if (serverWriter != null)
            {
                serverWriter.WriteLine("QUIT");
                string response = serverReader.ReadLine();
                Console.WriteLine("Resposta do servidor: " + response);
            }
            persistentServerClient.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao desconectar: " + ex.Message);
        }
    }
}
