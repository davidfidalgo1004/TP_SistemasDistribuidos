using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using Agregador;

class AgregadorServer
{
    private static int port = 7000;
    private static string serverIP = "127.0.0.1";
    private static int serverPort = 6000;

    private static List<Wavy> wavys = new List<Wavy>();
    private static object dadosLock = new object();
    private static string diretorioCsv = "csvs";

    private static TcpClient persistentServerClient;
    private static StreamReader serverReader;
    private static StreamWriter serverWriter;

    private static Timer envioTimer;
    private static Timer limpezaAutomaticaTimer;

    static void Main()
    {
        ConnectToServer();
        AgendarEnvio();

        // Inicia limpeza automática a cada 2 dias
        limpezaAutomaticaTimer = new Timer(_ => CsvExporter.LimparCsv(diretorioCsv, "."), null, TimeSpan.FromDays(2), TimeSpan.FromDays(2));

        TcpListener listener = new TcpListener(IPAddress.Parse("127.0.0.1"), port);
        listener.Start();
        Console.WriteLine("AGREGADOR pronto para receber conexoes de WAVYs...");

        while (true)
        {
            Console.WriteLine("Comando (Clean . | Clean nome.csv | Export | Enviar):");
            string input = Console.ReadLine();
            if (input.StartsWith("Clean"))
            {
                var partes = input.Split(' ');
                string alvo = partes.Length > 1 ? partes[1] : ".";
                CsvExporter.LimparCsv(diretorioCsv, alvo);
            }
            else if (input.Equals("Export", StringComparison.OrdinalIgnoreCase))
            {
                lock (dadosLock)
                {
                    CsvExporter.ExportarDadosWavys(wavys, diretorioCsv);
                }
            }
            else if (input.Equals("Enviar", StringComparison.OrdinalIgnoreCase))
            {
                lock (dadosLock)
                {
                    CsvExporter.ExportarDadosWavys(wavys, diretorioCsv);
                    if (EnviarCsvsAoServidor())
                    {
                        CsvExporter.MarcarComoEnviado(wavys);
                        CsvExporter.ExportarDadosWavys(wavys, diretorioCsv);
                    }
                }
            }
            else
            {
                TcpClient client = listener.AcceptTcpClient();
                Thread clientThread = new Thread(() => HandleWavyClient(client));
                clientThread.Start();
            }
        }
    }

    static void ConnectToServer()
    {
        try
        {
            persistentServerClient = new TcpClient(serverIP, serverPort);
            NetworkStream ns = persistentServerClient.GetStream();
            serverReader = new StreamReader(ns, Encoding.UTF8);
            serverWriter = new StreamWriter(ns, Encoding.UTF8) { AutoFlush = true };

            string greeting = serverReader.ReadLine();
            Console.WriteLine("Conectado ao servidor: " + greeting);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao conectar ao servidor: " + ex.Message);
        }
    }

    static void HandleWavyClient(TcpClient client)
    {
        using (NetworkStream stream = client.GetStream())
        using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
        {
            string linha;
            while ((linha = reader.ReadLine()) != null)
            {
                Console.WriteLine("Recebido: " + linha);

                if (linha.StartsWith("DADOS "))
                {
                    string json = linha.Substring(6);
                    try
                    {
                        Sensor sensor = JsonSerializer.Deserialize<Sensor>(json);
                        if (sensor != null)
                        {
                            lock (dadosLock)
                            {
                                // Aqui falta lógica para associar a WAVY correta
                                // Exemplo rápido:
                                Wavy wavy = wavys.Find(w => w.Name == "WAVY1");
                                if (wavy == null)
                                {
                                    wavy = new Wavy { Name = "WAVY1" }; // ou outro ID real
                                    wavys.Add(wavy);
                                }
                                wavy.Sensores.Add(sensor);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Erro ao processar sensor: " + ex.Message);
                    }
                }
            }
        }
    }

    static void AgendarEnvio()
    {
        DateTime agora = DateTime.Now;
        DateTime proximoEnvio = new DateTime(agora.Year, agora.Month, agora.Day, 22, 30, 0);
        if (agora > proximoEnvio)
            proximoEnvio = proximoEnvio.AddDays(1);

        TimeSpan delay = proximoEnvio - agora;
        envioTimer = new Timer(EnviarDadosAgendados, null, delay, Timeout.InfiniteTimeSpan);
        Console.WriteLine("Envio agendado para: " + proximoEnvio);
    }

    static void EnviarDadosAgendados(object state)
    {
        lock (dadosLock)
        {
            CsvExporter.ExportarDadosWavys(wavys, diretorioCsv);
            if (EnviarCsvsAoServidor())
            {
                CsvExporter.MarcarComoEnviado(wavys);
                CsvExporter.ExportarDadosWavys(wavys, diretorioCsv);
            }
        }
        AgendarEnvio();
    }

    static bool EnviarCsvsAoServidor()
    {
        try
        {
            foreach (var file in Directory.GetFiles(diretorioCsv, "*.csv"))
            {
                string conteudo = File.ReadAllText(file);
                serverWriter.WriteLine(conteudo);
            }
            serverWriter.WriteLine("QUIT");

            string resposta = serverReader.ReadLine();
            return resposta.StartsWith("RECEBIDO 200");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao enviar CSVs: " + ex.Message);
            return false;
        }
    }
}
