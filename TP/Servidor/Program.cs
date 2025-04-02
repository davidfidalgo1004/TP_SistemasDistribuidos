using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Servidor;
using Microsoft.EntityFrameworkCore;

class DataServer
{
    private static int port = 13000;
    private static object dbLock = new object();

    static void Main()
    {
        using (var context = new DBContext())
        {
            context.Database.EnsureCreated();
            Console.WriteLine("Base de dados pronta.");
        }

        // Thread para comandos na consola
        new Thread(() => ConsoleCommandLoop()).Start();

        TcpListener listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        Console.WriteLine($"SERVIDOR em escuta na porta {port}...");

        while (true)
        {
            TcpClient client = listener.AcceptTcpClient();
            Thread clientThread = new Thread(() => HandleClient(client));
            clientThread.Start();
        }
    }

    static void ConsoleCommandLoop()
    {
        while (true)
        {
            Console.Write("Comando> ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input)) continue;

            var parts = input.Split(' ', 3);
            string cmd = parts[0].ToLower();

            if (cmd == "show" && parts.Length == 3)
            {
                if (parts[1] == "aggregator")
                    MostrarAgregador(parts[2]);
                else if (parts[1] == "wavy")
                    MostrarWavy(parts[2]);
            }
            else if (cmd == "list")
            {
                if (parts.Length > 1 && parts[1] == "aggregators")
                    ListarAgregadores();
                else if (parts.Length > 1 && parts[1] == "wavys")
                    ListarWavys();
            }
            else if (cmd == "export" && parts.Length == 3)
            {
                if (parts[1] == "aggregator")
                    ExportarAgregadorCsv(parts[2]);
                else if (parts[1] == "wavy")
                    ExportarWavyCsv(parts[2]);
            }
            else if (cmd == "stats")
            {
                MostrarEstatisticas();
            }
            else
            {
                Console.WriteLine("Comando inválido. Exemplos:");
                Console.WriteLine(" - show aggregator AG1");
                Console.WriteLine(" - show wavy WAVY1");
                Console.WriteLine(" - list aggregators");
                Console.WriteLine(" - list wavys");
                Console.WriteLine(" - export aggregator AG1");
                Console.WriteLine(" - export wavy WAVY1");
                Console.WriteLine(" - stats");
            }
        }
    }

    static void MostrarEstatisticas()
    {
        lock (dbLock)
        {
            using var context = new DBContext();
            int totalAgregadores = context.Agregadores.Count();
            int totalWavys = context.Wavys.Count();
            int totalSensores = context.Wavys.SelectMany(w => w.Sensores).Count();
            int enviados = context.Wavys.SelectMany(w => w.Sensores).Count(s => s.DadosEnviados);
            int pendentes = totalSensores - enviados;

            Console.WriteLine("Resumo do Sistema:");
            Console.WriteLine($" - Nº Agregadores: {totalAgregadores}");
            Console.WriteLine($" - Nº WAVYs: {totalWavys}");
            Console.WriteLine($" - Nº Sensores: {totalSensores}");
            Console.WriteLine($" - Sensores enviados: {enviados}");
            Console.WriteLine($" - Sensores pendentes: {pendentes}");
        }
    }

    static void ExportarAgregadorCsv(string id)
    {
        lock (dbLock)
        {
            using var context = new DBContext();
            var agg = context.Agregadores
                .Include(a => a.Wavys)
                    .ThenInclude(w => w.Sensores)
                .FirstOrDefault(a => a.Name == id);

            if (agg == null)
            {
                Console.WriteLine("Agregador não encontrado.");
                return;
            }

            string path = $"agregador_{id}.csv";
            using var writer = new StreamWriter(path);
            writer.WriteLine("WavyID,Sensor,DataHora,Chave,Valor,DadosEnviados");

            foreach (var wavy in agg.Wavys)
            {
                foreach (var sensor in wavy.Sensores)
                {
                    foreach (var kvp in sensor.Valores)
                    {
                        writer.WriteLine($"{wavy.Name},{sensor.Nome},{sensor.DataHora:yyyy-MM-dd HH:mm:ss},{kvp.Key},{kvp.Value},{sensor.DadosEnviados}");
                    }
                }
            }

            Console.WriteLine($"Exportado para {path}");
        }
    }

    static void ExportarWavyCsv(string id)
    {
        lock (dbLock)
        {
            using var context = new DBContext();
            var wavy = context.Wavys
                .Include(w => w.Sensores)
                .FirstOrDefault(w => w.Name == id);

            if (wavy == null)
            {
                Console.WriteLine("WAVY não encontrada.");
                return;
            }

            string path = $"wavy_{id}.csv";
            using var writer = new StreamWriter(path);
            writer.WriteLine("WavyID,Sensor,DataHora,Chave,Valor,DadosEnviados");

            foreach (var sensor in wavy.Sensores)
            {
                foreach (var kvp in sensor.Valores)
                {
                    writer.WriteLine($"{wavy.Name},{sensor.Nome},{sensor.DataHora:yyyy-MM-dd HH:mm:ss},{kvp.Key},{kvp.Value},{sensor.DadosEnviados}");
                }
            }

            Console.WriteLine($"Exportado para {path}");
        }
    }

    static void HandleClient(TcpClient client)
    {
        using (NetworkStream stream = client.GetStream())
        using (StreamReader reader = new StreamReader(stream))
        using (StreamWriter writer = new StreamWriter(stream) { AutoFlush = true })
        {
            writer.WriteLine("OK 100");
            Console.WriteLine("Agregador conectado. Enviado OK 100.");

            var linhasRecebidas = new List<string>();
            string linha;
            while ((linha = reader.ReadLine()) != null)
            {
                if (linha.Trim().Equals("QUIT", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Recebido QUIT. A processar CSV...");
                    break;
                }
                linhasRecebidas.Add(linha);
            }

            try
            {
                ProcessarCsv(linhasRecebidas);
                writer.WriteLine("RECEBIDO 200");
                Console.WriteLine("CSV processado com sucesso e enviado RECEBIDO 200.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro ao processar CSV: " + ex.Message);
            }
        }

        client.Close();
    }

    static void ProcessarCsv(List<string> linhas)
    {
        if (linhas.Count == 0) return;

        var header = linhas[0];
        var colunas = header.Split(',');

        if (!header.Contains("WavyID") || !header.Contains("Sensor"))
            throw new Exception("Cabecalho CSV invalido");

        var agregador = new Agregador { Name = "Agregador1" };

        var wavysDict = new Dictionary<string, Wavy>();

        for (int i = 1; i < linhas.Count; i++)
        {
            var campos = linhas[i].Split(',');
            if (campos.Length < 6) continue;

            string wavyID = campos[0];
            string sensorNome = campos[1];
            DateTime data = DateTime.Parse(campos[2]);
            string chave = campos[3];
            string valor = campos[4];
            bool.TryParse(campos[5], out bool enviado);

            if (!wavysDict.ContainsKey(wavyID))
            {
                wavysDict[wavyID] = new Wavy
                {
                    Name = wavyID,
                    Estado = "operacao"
                };
            }

            var sensor = wavysDict[wavyID].Sensores.FirstOrDefault(s => s.Nome == sensorNome && s.DataHora == data);
            if (sensor == null)
            {
                sensor = new Sensor
                {
                    Nome = sensorNome,
                    DataHora = data,
                    DadosEnviados = enviado
                };
                wavysDict[wavyID].Sensores.Add(sensor);
            }

            sensor.Valores[chave] = valor;
        }

        agregador.Wavys = wavysDict.Values.ToList();

        lock (dbLock)
        {
            using (var context = new DBContext())
            {
                var existente = context.Agregadores.Include(a => a.Wavys).ThenInclude(w => w.Sensores)
                    .FirstOrDefault(a => a.Name == agregador.Name);

                if (existente != null)
                {
                    context.Wavys.RemoveRange(existente.Wavys);
                    existente.Wavys = agregador.Wavys;
                }
                else
                {
                    context.Agregadores.Add(agregador);
                }

                context.SaveChanges();
            }
        }
    }
}
