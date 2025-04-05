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
    private static int port = 8080;
    private static object dbLock = new object();

    static void Main()
    {
        using (var context = new DBContext())
        {
            context.Database.EnsureCreated();
            Console.WriteLine("Base de dados pronta.");
        }

        Console.WriteLine($"SERVIDOR em escuta na porta {port}...");

        // Inicia menu administrativo
        new Thread(() => MenuServidor()).Start();

        TcpListener listener = new TcpListener(IPAddress.Parse("127.0.0.1"), port);
        listener.Start();

        while (true)
        {
            TcpClient client = listener.AcceptTcpClient();
            Thread clientThread = new Thread(() => HandleClient(client));
            clientThread.Start();
        }
    }

    static void MenuServidor()
    {
        while (true)
        {
            Console.WriteLine("\n========= MENU SERVIDOR =========");
            Console.WriteLine("1 - Mostrar Agregadores");
            Console.WriteLine("2 - Mostrar WAVYs");
            Console.WriteLine("3 - Mostrar dados de um Agregador");
            Console.WriteLine("4 - Mostrar dados de uma WAVY");
            Console.WriteLine("5 - Exportar dados de um Agregador (CSV)");
            Console.WriteLine("6 - Exportar dados de uma WAVY (CSV)");
            Console.WriteLine("7 - Mostrar Estatísticas do Sistema");
            Console.WriteLine("8 - Sair do Menu");
            Console.Write("Escolha uma opção: ");
            var op = Console.ReadLine()?.Trim();

            switch (op)
            {
                case "1":
                    ListarAgregadores();
                    break;
                case "2":
                    ListarWavys();
                    break;
                case "3":
                    Console.Write("ID do Agregador: ");
                    MostrarAgregador(Console.ReadLine()?.Trim());
                    break;
                case "4":
                    Console.Write("ID da WAVY: ");
                    MostrarWavy(Console.ReadLine()?.Trim());
                    break;
                case "5":
                    Console.Write("ID do Agregador: ");
                    ExportarAgregadorCsv(Console.ReadLine()?.Trim());
                    break;
                case "6":
                    Console.Write("ID da WAVY: ");
                    ExportarWavyCsv(Console.ReadLine()?.Trim());
                    break;
                case "7":
                    MostrarEstatisticas();
                    break;
                case "8":
                    Console.WriteLine("A sair do menu. Servidor continua a receber dados...");
                    return;
                default:
                    Console.WriteLine("Opção inválida.");
                    break;
            }
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
        if (!header.Contains("WavyID") || !header.Contains("Sensor"))
            throw new Exception("Cabeçalho CSV inválido");

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

    static void MostrarAgregador(string id)
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

            Console.WriteLine($"Agregador: {agg.Name}, WAVYs: {agg.Wavys.Count}");
            foreach (var w in agg.Wavys)
            {
                Console.WriteLine($" - Wavy: {w.Name}, Estado: {w.Estado}, Sensores: {w.Sensores.Count}");
            }
        }
    }

    static void MostrarWavy(string id)
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

            Console.WriteLine($"WAVY: {wavy.Name}, Estado: {wavy.Estado}");
            foreach (var sensor in wavy.Sensores)
            {
                Console.WriteLine($" - Sensor: {sensor.Nome}, {sensor.DataHora:yyyy-MM-dd HH:mm:ss}");
                foreach (var kvp in sensor.Valores)
                {
                    Console.WriteLine($"   {kvp.Key}: {kvp.Value}");
                }
            }
        }
    }

    static void ListarAgregadores()
    {
        lock (dbLock)
        {
            using var context = new DBContext();
            var aggs = context.Agregadores.ToList();
            Console.WriteLine("Agregadores existentes:");
            foreach (var a in aggs)
                Console.WriteLine(" - " + a.Name);
        }
    }

    static void ListarWavys()
    {
        lock (dbLock)
        {
            using var context = new DBContext();
            var wavys = context.Wavys.ToList();
            Console.WriteLine("WAVYs existentes:");
            foreach (var w in wavys)
                Console.WriteLine(" - " + w.Name);
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
}
