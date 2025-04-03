using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;

class Wavy
{
    private static string wavyID;
    private static string agregadorIP = "127.0.1.1";
    private static int agregadorPorta = 7000;

    // A lista de sensores ativos vai ser lida de "WAVYID.txt"
    private static List<string> sensoresAtivos = new();

    // Pasta está guardado os dados e config dos WAVYS
    private static readonly string pastaDados = "dadosWAVY";

    // Caminho para o CSV local
    private static string csvFilePath;

    // Intervalos
    private static readonly int intervaloRecolhaSegundos = 30;     // Recolhe sensores a cada 30s
    private static readonly int intervaloEnvioSegundos = 60;      // Envia dados a cada 10min
    private static readonly double intervaloLimpezaDias = 2.0;     // Limpa CSV a cada 2 dias

    private static bool recolhaAtiva = true; // se quiseres parar recolha sem sair

    static void Main()
    {
        Console.Write("ID da WAVY: ");
        wavyID = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(wavyID))
        {
            Console.WriteLine("ID da WAVY não pode ser vazio. A terminar.");
            return;
        }

        Directory.CreateDirectory(pastaDados);

        // Caminho config =>  Wavy1_config.txt
        string configPath = Path.Combine(pastaDados, wavyID + "_config.txt");
        if (!File.Exists(configPath))
        {
            Console.WriteLine($"Ficheiro de configuração '{configPath}' não existe. Deseja criar? (s/n)");
            var resp = Console.ReadLine()?.Trim().ToLower();
            if (resp == "s")
            {
                Console.Write("Indique o AgregadorID (ex: 1 => porta 7001): ");
                if (int.TryParse(Console.ReadLine(), out int idNum))
                {
                    int porta = 7000 + idNum;

                    File.WriteAllLines(configPath, new[]
                    {
                        $"AgregadorID={idNum}",
                        $"AgregadorIP={agregadorIP}"
                    });
                    Console.WriteLine("Ficheiro de config criado com sucesso.");
                }
                else
                {
                    Console.WriteLine("ID inválido. A terminar.");
                    return;
                }
            }
            else
            {
                Console.WriteLine("A terminar sem criar config...");
                return;
            }
        }

        // Ler config
        var configLines = File.ReadAllLines(configPath);
        foreach (var line in configLines)
        {
            if (line.StartsWith("AgregadorID="))
            {
                if (int.TryParse(line.Split('=')[1], out int idNum))
                {
                    agregadorPorta = 7000 + idNum;
                }
            }
            else if (line.StartsWith("AgregadorIP="))
            {
                agregadorIP = line.Split('=')[1].Trim();
            }
        }

        // CSV local
        csvFilePath = Path.Combine(pastaDados, wavyID + ".csv");

        Console.WriteLine($"Ligação será feita ao Agregador {agregadorIP}:{agregadorPorta}");

        // Ficheiro com lista de sensores => Wavy1.txt
        string sensorPath = Path.Combine(pastaDados, wavyID + ".txt");
        if (!File.Exists(sensorPath))
        {
            Console.WriteLine("Sensores ainda não definidos. Introduza separados por vírgulas:");
            var input = Console.ReadLine();
            File.WriteAllText(sensorPath, input);
        }
        else
        {
            Console.WriteLine($"Sensores ativos: {File.ReadAllText(sensorPath)}");
        }

        sensoresAtivos = File.ReadAllText(sensorPath)
                             .Split(',', StringSplitOptions.RemoveEmptyEntries)
                             .Select(s => s.Trim())
                             .ToList();

        // Se CSV não existir => cria cabeçalho
        if (!File.Exists(csvFilePath))
        {
            File.WriteAllText(csvFilePath, "Nome,DataHora,Chave,Valor,DadosEnviados\n");
        }

        // Thread Recolha
        new Thread(RecolherSensores).Start();

        // Thread Envio
        new Thread(EnviarParaAgregador).Start();

        // Timer limpeza
        Timer limpezaTimer = new Timer(_ => LimparCsv(), null,
                                       TimeSpan.FromDays(intervaloLimpezaDias),
                                       TimeSpan.FromDays(intervaloLimpezaDias));

        // Thread comandos => add sensor X, remove sensor X, list, start/stop recolha
        new Thread(ComandoLoop).Start();

        // Mantém a app viva
        while (true)
        {
            Thread.Sleep(1000);
        }
    }

    // Lê comandos da consola
    static void ComandoLoop()
    {
        while (true)
        {
            Console.Write("Comando> ");
            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input)) continue;

            var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;
            string cmd = parts[0].ToLower();

            if (cmd == "add" && parts.Length == 2)
            {
                var sensorNovo = parts[1].Trim();
                if (!sensoresAtivos.Contains(sensorNovo))
                {
                    sensoresAtivos.Add(sensorNovo);
                    AtualizarSensores();
                    Console.WriteLine($"Sensor '{sensorNovo}' adicionado.");
                }
            }
            else if (cmd == "remove" && parts.Length == 2)
            {
                var sensorRem = parts[1].Trim();
                if (sensoresAtivos.Remove(sensorRem))
                {
                    AtualizarSensores();
                    Console.WriteLine($"Sensor '{sensorRem}' removido.");
                }
            }
            else if (cmd == "list")
            {
                ListarUltimosValores();
            }
            else if (cmd == "start")
            {
                recolhaAtiva = true;
                Console.WriteLine("Recolha reativada.");
            }
            else if (cmd == "stop")
            {
                recolhaAtiva = false;
                Console.WriteLine("Recolha parada temporariamente.");
            }
            else
            {
                Console.WriteLine("Comandos disponíveis:");
                Console.WriteLine(" add <sensor>");
                Console.WriteLine(" remove <sensor>");
                Console.WriteLine(" list");
                Console.WriteLine(" start");
                Console.WriteLine(" stop");
            }
        }
    }

    static void AtualizarSensores()
    {
        // Ex: "GPS,Temperatura"
        string sensorPath = Path.Combine(pastaDados, wavyID + ".txt");
        File.WriteAllText(sensorPath, string.Join(",", sensoresAtivos));
    }

    static void ListarUltimosValores()
    {
        if (!File.Exists(csvFilePath))
        {
            Console.WriteLine("Nenhum CSV encontrado ainda.");
            return;
        }
        var linhas = File.ReadAllLines(csvFilePath);
        if (linhas.Length < 2)
        {
            Console.WriteLine("Nenhum dado recolhido ainda.");
            return;
        }

        // Agrupar por sensor
        // Nome,DataHora,Chave,Valor,DadosEnviados
        var dadosPorSensor = new Dictionary<string, List<(DateTime data, string chave, string valor, bool enviado)>>();
        foreach (var linha in linhas.Skip(1))
        {
            var partes = linha.Split(',');
            if (partes.Length < 5) continue;
            string nome = partes[0];
            if (!DateTime.TryParse(partes[1], out DateTime dt)) continue;
            string chave = partes[2];
            string valor = partes[3];
            bool env = partes[4].Equals("True", StringComparison.OrdinalIgnoreCase);

            if (!dadosPorSensor.ContainsKey(nome))
                dadosPorSensor[nome] = new List<(DateTime, string, string, bool)>();
            dadosPorSensor[nome].Add((dt, chave, valor, env));
        }

        // Para cada sensor ativo => mostra últimos
        foreach (var sensor in sensoresAtivos)
        {
            if (!dadosPorSensor.ContainsKey(sensor))
            {
                Console.WriteLine($"Sensor '{sensor}' sem dados recolhidos ainda.");
                continue;
            }

            // Ordena por data desc => pega no mais recente
            var entradas = dadosPorSensor[sensor].OrderByDescending(e => e.data).ToList();
            DateTime ultimaData = entradas.First().data;
            bool ultimoEnviado = entradas.First().enviado;

            Console.WriteLine($"[Sensor: {sensor}] Ultimo registo => Data: {ultimaData:O} | Enviado: {ultimoEnviado}");
            // Mostra as chaves do mais recente
            var ultimoData = entradas.First().data;

            // Filtra as entradas com a data mais recente, para mostrar todas as chaves
            var ultimasIguais = entradas.Where(e => e.data == ultimoData).ToList();
            foreach (var ent in ultimasIguais)
            {
                Console.WriteLine($"   > {ent.chave}: {ent.valor}");
            }
        }
    }

    // Recolhe dados a cada 30s (se recolhaAtiva == true)
    static void RecolherSensores()
    {
        while (true)
        {
            if (recolhaAtiva)
            {
                foreach (var tipo in sensoresAtivos)
                {
                    var sensor = GerarSensor(tipo);
                    // Cada chave/valor => 1 linha
                    foreach (var kv in sensor.Valores)
                    {
                        string linha = $"{sensor.Nome},{sensor.DataHora:O},{kv.Key},{kv.Value},False";
                        File.AppendAllText(csvFilePath, linha + "\n");
                        Console.WriteLine($"[Recolha] {linha}");
                    }
                }
            }
            Thread.Sleep(intervaloRecolhaSegundos * 1000);
        }
    }

    // Envia dados "False" ao Agregador cada 10min
    static void EnviarParaAgregador()
    {
        while (true)
        {
            Thread.Sleep(intervaloEnvioSegundos * 1000);

            if (!File.Exists(csvFilePath)) continue;

            var linhas = File.ReadAllLines(csvFilePath).ToList();
            if (linhas.Count < 2) continue;

            var cabecalho = linhas[0];
            var naoEnviadas = linhas.Skip(1).Where(l => l.EndsWith("False")).ToList();
            if (!naoEnviadas.Any()) continue;

            try
            {
                using TcpClient client = new TcpClient(agregadorIP, agregadorPorta);
                using NetworkStream stream = client.GetStream();
                using StreamWriter writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
                using StreamReader reader = new StreamReader(stream, Encoding.UTF8);

                // Envia cabeçalho e linhas
                writer.WriteLine(cabecalho);
                foreach (var linha in naoEnviadas)
                {
                    writer.WriteLine(linha);
                }
                writer.WriteLine("QUIT");

                string resposta = reader.ReadLine();
                if (resposta != null && resposta.StartsWith("RECEBIDO"))
                {
                    Console.WriteLine("Confirmação do Agregador recebida.");
                    // Marca = True
                    for (int i = 1; i < linhas.Count; i++)
                    {
                        if (linhas[i].EndsWith("False") && naoEnviadas.Contains(linhas[i]))
                        {
                            linhas[i] = linhas[i].Replace("False", "True");
                        }
                    }
                    File.WriteAllLines(csvFilePath, linhas);
                }
                else
                {
                    Console.WriteLine("[!] Sem confirmação do Agregador ou resposta inválida.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Erro] " + ex.Message);
            }
        }
    }

    // Limpa CSV a cada 2 dias
    static void LimparCsv()
    {
        try
        {
            if (!File.Exists(csvFilePath)) return;

            var linhas = File.ReadAllLines(csvFilePath).ToList();
            if (linhas.Count < 2) return;

            var cabecalho = linhas[0];
            var restantes = linhas.Skip(1).Where(l => !l.EndsWith("True")).ToList();

            if (restantes.Count == 0)
            {
                File.Delete(csvFilePath);
                Console.WriteLine("[LIMPEZA] CSV apagado (tudo enviado).");
            }
            else
            {
                File.WriteAllLines(csvFilePath, new[] { cabecalho }.Concat(restantes));
                Console.WriteLine("[LIMPEZA] Dados enviados removidos do CSV.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[LIMPEZA] Erro ao limpar CSV: " + ex.Message);
        }
    }

    // Gera sensor fictício
    static dynamic GerarSensor(string tipo)
    {
        var rand = new Random();
        var valores = new Dictionary<string, string>();

        switch (tipo.Trim())
        {
            case "GPS":
                valores["latitude"] = (40 + rand.NextDouble()).ToString("F6");
                valores["longitude"] = (-8 + rand.NextDouble()).ToString("F6");
                break;
            case "Temperatura":
                valores["valor"] = (15 + rand.NextDouble() * 10).ToString("F2");
                break;
            case "Salinidade":
                valores["valor"] = (30 + rand.NextDouble() * 5).ToString("F2");
                break;
            case "pH":
                valores["valor"] = (6 + rand.NextDouble() * 2).ToString("F2");
                break;
            case "Oxigenio":
                valores["valor"] = (5 + rand.NextDouble() * 3).ToString("F2");
                break;
            case "Condutividade":
                valores["valor"] = (40 + rand.NextDouble() * 15).ToString("F2");
                break;
            case "Turbidez":
                valores["valor"] = (rand.NextDouble() * 100).ToString("F2");
                break;
            case "Pressao":
                valores["valor"] = (1000 + rand.NextDouble() * 50).ToString("F2");
                break;
            case "VelocidadeCorrente":
                valores["valor"] = (rand.NextDouble() * 3).ToString("F2");
                break;
            case "NivelMar":
                valores["valor"] = (rand.NextDouble() * 5).ToString("F2");
                break;
        }

        return new
        {
            Nome = tipo,
            DataHora = DateTime.Now,
            Valores = valores,
            DadosEnviados = false
        };
    }
}
