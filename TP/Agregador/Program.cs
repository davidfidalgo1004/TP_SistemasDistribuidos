using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class Aggregator
{
    private static string pastaCsvs = "csvs";
    private static string tempCsv = "aggregador_temp.csv";
    private static string servidorIP = "127.0.1.1";
    private static int servidorPorta = 8080;
    private static int portaAgregador = 7000;
    private static int intervaloEnvioMin = 30;

    static void Main()
    {
        Console.Write("ID do Agregador (Apenas o número): ");
        string agregadorID = Console.ReadLine()?.Trim();
        if (!int.TryParse(agregadorID, out int idNumerico))
        {
            Console.WriteLine("ID inválido. Deve ser um número inteiro.");
            return;
        }

        portaAgregador = 7000 + idNumerico;
        Console.WriteLine($"Agregador associado à porta {portaAgregador}.");

        Directory.CreateDirectory(pastaCsvs);
        Console.WriteLine("Agregador em execução...");

        new Thread(ServidorEscuta).Start();

        Timer envioTimer = new Timer(_ => EnviarCsvsAoServidor(), null,
                                     TimeSpan.FromMinutes(1),
                                     TimeSpan.FromMinutes(intervaloEnvioMin));

        Timer limpezaTimer = new Timer(_ => LimparCsvs(), null,
                                       TimeSpan.FromDays(2),
                                       TimeSpan.FromDays(2));

        MenuAgregador();
    }

    static void ServidorEscuta()
    {
        TcpListener listener = new TcpListener(IPAddress.Any, portaAgregador);
        listener.Start();
        Console.WriteLine($"Agregador a escutar na porta {portaAgregador}...");

        while (true)
        {
            TcpClient client = listener.AcceptTcpClient();
            new Thread(() => ReceberCsvDoWavy(client)).Start();
        }
    }

    static void ReceberCsvDoWavy(TcpClient client)
    {
        List<string> linhasRecebidas = new();

        try
        {
            using NetworkStream stream = client.GetStream();
            using StreamReader reader = new StreamReader(stream);
            using StreamWriter writer = new StreamWriter(stream) { AutoFlush = true };

            string linha;
            while ((linha = reader.ReadLine()) != null)
            {
                if (linha.Trim().Equals("QUIT", StringComparison.OrdinalIgnoreCase))
                    break;

                linhasRecebidas.Add(linha);
            }

            if (linhasRecebidas.Count > 1)
            {
                string wavyID = linhasRecebidas[1].Split(',')[0];
                string path = Path.Combine(pastaCsvs, wavyID + ".csv");

                if (!File.Exists(path))
                    File.WriteAllLines(path, new[] { linhasRecebidas[0] });

                File.AppendAllLines(path, linhasRecebidas.Skip(1));
                writer.WriteLine("RECEBIDO 200");
                Console.WriteLine($"CSV de {wavyID} recebido e guardado em {path}.");
            }
            else
            {
                writer.WriteLine("ERRO 400");
                Console.WriteLine("ERRO: CSV inválido ou vazio.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao receber CSV: " + ex.Message);
        }
        finally
        {
            client.Close();
        }
    }

    static void EnviarCsvsAoServidor()
    {
        try
        {
            var ficheiros = Directory.GetFiles(pastaCsvs, "*.csv");
            List<string> linhasParaEnviar = new();
            string cabecalho = "";

            foreach (var file in ficheiros)
            {
                var linhas = File.ReadAllLines(file).ToList();
                if (linhas.Count == 0) continue;

                if (string.IsNullOrWhiteSpace(cabecalho))
                    cabecalho = linhas[0];

                foreach (var l in linhas.Skip(1))
                {
                    if (l.EndsWith("False", StringComparison.OrdinalIgnoreCase))
                        linhasParaEnviar.Add(l);
                }
            }

            if (linhasParaEnviar.Count == 0)
            {
                Console.WriteLine("Nenhum dado novo para enviar ao servidor.");
                return;
            }

            File.WriteAllLines(tempCsv, new[] { cabecalho }.Concat(linhasParaEnviar));

            using TcpClient client = new TcpClient(servidorIP, servidorPorta);
            using NetworkStream stream = client.GetStream();
            using StreamWriter writer = new StreamWriter(stream) { AutoFlush = true };
            using StreamReader reader = new StreamReader(stream);

            foreach (var linha in File.ReadAllLines(tempCsv))
                writer.WriteLine(linha);
            writer.WriteLine("QUIT");

            string resposta = reader.ReadLine();
            if (resposta != null && resposta.StartsWith("RECEBIDO"))
            {
                Console.WriteLine("Dados enviados com sucesso ao servidor. Marcando como enviados...");
                foreach (var file in ficheiros)
                {
                    var linesFile = File.ReadAllLines(file).ToList();
                    for (int i = 0; i < linesFile.Count; i++)
                    {
                        if (linesFile[i].EndsWith("False", StringComparison.OrdinalIgnoreCase)
                            && linhasParaEnviar.Contains(linesFile[i]))
                        {
                            linesFile[i] = linesFile[i].Replace("False", "True");
                        }
                    }
                    File.WriteAllLines(file, linesFile);
                }
            }
            else
            {
                Console.WriteLine("⚠️ Falha na confirmação do servidor ou sem resposta.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao enviar CSVs: " + ex.Message);
        }
    }

    static void LimparCsvs()
    {
        try
        {
            var ficheiros = Directory.GetFiles(pastaCsvs, "*.csv");
            foreach (var file in ficheiros)
            {
                var linhas = File.ReadAllLines(file).ToList();
                if (linhas.Count == 0) continue;

                var cabecalho = linhas[0];
                var restantes = linhas.Skip(1)
                                      .Where(l => !l.EndsWith("True", StringComparison.OrdinalIgnoreCase))
                                      .ToList();

                if (restantes.Count == 0)
                {
                    File.Delete(file);
                    Console.WriteLine($"Ficheiro '{Path.GetFileName(file)}' removido (tudo enviado).");
                }
                else
                {
                    File.WriteAllLines(file, new[] { cabecalho }.Concat(restantes));
                    Console.WriteLine($"Ficheiro '{Path.GetFileName(file)}' limpo (linhas enviadas removidas).");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao limpar CSVs: " + ex.Message);
        }
    }

    static void MenuAgregador()
    {
        while (true)
        {
            Console.WriteLine("\n========== MENU AGREGADOR ==========");
            Console.WriteLine("1 - Listar WAVYs registadas");
            Console.WriteLine("2 - Ver conteúdo dos CSVs");
            Console.WriteLine("3 - Limpar CSVs manualmente");
            Console.WriteLine("4 - Forçar envio de dados ao servidor");
            Console.WriteLine("5 - Sair");
            Console.Write("Escolha uma opção: ");

            switch (Console.ReadLine())
            {
                case "1":
                    ListarWavys();
                    break;
                case "2":
                    VerConteudoCsvs();
                    break;
                case "3":
                    LimparCsvs();
                    break;
                case "4":
                    EnviarCsvsAoServidor();
                    break;
                case "5":
                    Console.WriteLine("A encerrar Agregador...");
                    Environment.Exit(0);
                    break;
                default:
                    Console.WriteLine("Opção inválida.");
                    break;
            }
        }
    }

    static void VerConteudoCsvs()
    {
        var ficheiros = Directory.GetFiles(pastaCsvs, "*.csv");
        if (ficheiros.Length == 0)
        {
            Console.WriteLine("Nenhum ficheiro CSV encontrado.");
            return;
        }

        foreach (var file in ficheiros)
        {
            Console.WriteLine($"\n--- Conteúdo de '{Path.GetFileName(file)}' ---");
            foreach (var linha in File.ReadAllLines(file))
            {
                Console.WriteLine(linha);
            }
        }
    }

    static void ListarWavys()
    {
        var ficheiros = Directory.GetFiles(pastaCsvs, "*.csv");
        if (ficheiros.Length == 0)
        {
            Console.WriteLine("Nenhuma WAVY registada ainda.");
            return;
        }

        Console.WriteLine("WAVYs registadas:");
        foreach (var file in ficheiros)
        {
            string nome = Path.GetFileNameWithoutExtension(file);
            Console.WriteLine($"- {nome}");
        }
    }
}
