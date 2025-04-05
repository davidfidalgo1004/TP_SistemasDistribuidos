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

    private static List<string> sensoresAtivos = new();
    private static readonly string pastaDados = "dadosWAVY";
    private static string csvFilePath;

    private static readonly int intervaloRecolhaSegundos = 30;
    private static readonly int intervaloEnvioSegundos = 60;
    private static readonly double intervaloLimpezaDias = 2.0;

    private static bool recolhaAtiva = true;

    static void Main()
    {
        Console.Write("ID da WAVY: ");
        wavyID = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(wavyID))
        {
            Console.WriteLine("ID da WAVY não pode ser vazio.");
            return;
        }

        Directory.CreateDirectory(pastaDados);
        string configPath = Path.Combine(pastaDados, wavyID + "_config.txt");

        if (!File.Exists(configPath))
        {
            Console.WriteLine($"Ficheiro de configuração '{configPath}' não existe. Criar novo? (s/n)");
            var resp = Console.ReadLine()?.Trim().ToLower();
            if (resp == "s")
            {
                Console.Write("Indique o AgregadorID (ex: 1 => porta 7001): ");
                if (int.TryParse(Console.ReadLine(), out int idNum))
                {
                    agregadorPorta = 7000 + idNum;
                    File.WriteAllLines(configPath, new[] {
                        $"AgregadorID={idNum}",
                        $"AgregadorIP={agregadorIP}"
                    });
                }
                else
                {
                    Console.WriteLine("ID inválido.");
                    return;
                }
            }
            else return;
        }

        foreach (var line in File.ReadAllLines(configPath))
        {
            if (line.StartsWith("AgregadorID="))
                agregadorPorta = 7000 + int.Parse(line.Split('=')[1]);
            else if (line.StartsWith("AgregadorIP="))
                agregadorIP = line.Split('=')[1].Trim();
        }

        csvFilePath = Path.Combine(pastaDados, wavyID + ".csv");
        Console.WriteLine($"Ligação a agregador em {agregadorIP}:{agregadorPorta}");

        string sensorPath = Path.Combine(pastaDados, wavyID + ".txt");
        if (!File.Exists(sensorPath))
        {
            Console.WriteLine("Sensores ainda não definidos. Introduza separados por vírgula:");
            var input = Console.ReadLine();
            File.WriteAllText(sensorPath, input);
        }

        sensoresAtivos = File.ReadAllText(sensorPath)
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim()).ToList();

        if (!File.Exists(csvFilePath))
            File.WriteAllText(csvFilePath, "Nome,DataHora,Chave,Valor,DadosEnviados\n");

        new Thread(RecolherSensores).Start();
        new Thread(EnviarParaAgregador).Start();

        Timer limpezaTimer = new Timer(_ => LimparCsv(), null,
            TimeSpan.FromDays(intervaloLimpezaDias),
            TimeSpan.FromDays(intervaloLimpezaDias));

        MenuLoop();
    }

    static void MenuLoop()
    {
        while (true)
        {
            Console.WriteLine("\n========= MENU PRINCIPAL WAVY =========");
            Console.WriteLine("1 - Adicionar novo sensor");
            Console.WriteLine("2 - Remover sensor existente");
            Console.WriteLine("3 - Ver últimos dados recolhidos por sensor");
            Console.WriteLine("4 - Parar recolha de dados");
            Console.WriteLine("5 - Retomar recolha de dados");
            Console.WriteLine("6 - Reconfigurar ligação ao Agregador");
            Console.WriteLine("7 - Ver sensores ativos");
            Console.WriteLine("8 - Sair da aplicação");
            Console.Write("Escolha uma opção: ");

            switch (Console.ReadLine()?.Trim())
            {
                case "1":
                    AdicionarSensor();
                    break;
                case "2":
                    RemoverSensor();
                    break;
                case "3":
                    ListarUltimosValores();
                    break;
                case "4":
                    recolhaAtiva = false;
                    Console.WriteLine("Recolha de dados foi PAUSADA.");
                    break;
                case "5":
                    recolhaAtiva = true;
                    Console.WriteLine("Recolha de dados foi RETOMADA.");
                    break;
                case "6":
                    ReconfigurarAgregador();
                    break;
                case "7":
                    Console.WriteLine("Sensores ativos: " + string.Join(", ", sensoresAtivos));
                    break;
                case "8":
                    Console.WriteLine("Encerrando WAVY...");
                    Environment.Exit(0);
                    break;
                default:
                    Console.WriteLine("Opção inválida.");
                    break;
            }
        }
    }

    static void AdicionarSensor()
    {
        Console.Write("Nome do novo sensor: ");
        var novo = Console.ReadLine()?.Trim();
        if (!string.IsNullOrEmpty(novo) && !sensoresAtivos.Contains(novo))
        {
            sensoresAtivos.Add(novo);
            AtualizarSensores();
            Console.WriteLine($"Sensor '{novo}' adicionado.");
        }
        else
        {
            Console.WriteLine("Sensor inválido ou já existente.");
        }
    }

    static void RemoverSensor()
    {
        Console.Write("Nome do sensor a remover: ");
        var remover = Console.ReadLine()?.Trim();
        if (sensoresAtivos.Remove(remover))
        {
            AtualizarSensores();
            Console.WriteLine($"Sensor '{remover}' removido.");
        }
        else
        {
            Console.WriteLine("Sensor não encontrado.");
        }
    }

    static void AtualizarSensores()
    {
        string sensorPath = Path.Combine(pastaDados, wavyID + ".txt");
        File.WriteAllText(sensorPath, string.Join(",", sensoresAtivos));
    }

    static void ReconfigurarAgregador()
    {
        Console.Write("Novo IP do Agregador: ");
        var ip = Console.ReadLine()?.Trim();
        Console.Write("Novo ID do Agregador (ex: 1 => porta 7001): ");
        if (int.TryParse(Console.ReadLine(), out int id))
        {
            agregadorIP = ip;
            agregadorPorta = 7000 + id;

            string configPath = Path.Combine(pastaDados, wavyID + "_config.txt");
            File.WriteAllLines(configPath, new[]
            {
                $"AgregadorID={id}",
                $"AgregadorIP={agregadorIP}"
            });

            Console.WriteLine($"Configuração atualizada: {agregadorIP}:{agregadorPorta}");
        }
        else
        {
            Console.WriteLine("ID inválido.");
        }
    }

    static void RecolherSensores()
    {
        while (true)
        {
            if (recolhaAtiva)
            {
                foreach (var tipo in sensoresAtivos)
                {
                    var sensor = GerarSensor(tipo);
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

                writer.WriteLine(wavyID);
                writer.WriteLine("ENVIAR_DADOS");
                writer.WriteLine(cabecalho);
                foreach (var linha in naoEnviadas)
                    writer.WriteLine(linha);
                writer.WriteLine("QUIT");

                var resposta = reader.ReadLine();
                if (resposta != null && resposta.StartsWith("RECEBIDO"))
                {
                    for (int i = 1; i < linhas.Count; i++)
                    {
                        if (linhas[i].EndsWith("False") && naoEnviadas.Contains(linhas[i]))
                            linhas[i] = linhas[i].Replace("False", "True");
                    }
                    File.WriteAllLines(csvFilePath, linhas);
                    Console.WriteLine("Dados enviados ao Agregador.");
                }
                else
                {
                    Console.WriteLine("Falha ao receber confirmação do Agregador.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Erro] {ex.Message}");
            }
        }
    }

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
                Console.WriteLine("[LIMPEZA] Dados enviados removidos.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[LIMPEZA] Erro: " + ex.Message);
        }
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

        var dadosPorSensor = new Dictionary<string, List<(DateTime data, string chave, string valor, bool enviado)>>();
        foreach (var linha in linhas.Skip(1))
        {
            var partes = linha.Split(',');
            if (partes.Length < 5) continue;
            string nome = partes[0];
            if (!DateTime.TryParse(partes[1], out DateTime dt)) continue;
            string chave = partes[2];
            string valor = partes[3];
            bool enviado = partes[4].Equals("True", StringComparison.OrdinalIgnoreCase);

            if (!dadosPorSensor.ContainsKey(nome))
                dadosPorSensor[nome] = new();
            dadosPorSensor[nome].Add((dt, chave, valor, enviado));
        }

        foreach (var sensor in sensoresAtivos)
        {
            if (!dadosPorSensor.ContainsKey(sensor))
            {
                Console.WriteLine($"Sensor '{sensor}' sem dados recolhidos.");
                continue;
            }

            var entradas = dadosPorSensor[sensor].OrderByDescending(e => e.data).ToList();
            var ultimaData = entradas.First().data;
            bool enviadoUltimo = entradas.First().enviado;

            Console.WriteLine($"\n[Sensor: {sensor}] Último registo: {ultimaData:O} | Enviado: {enviadoUltimo}");
            foreach (var ent in entradas.Where(e => e.data == ultimaData))
                Console.WriteLine($"   > {ent.chave}: {ent.valor}");
        }
    }

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
            default:
                valores["valor"] = rand.NextDouble().ToString("F2");
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

