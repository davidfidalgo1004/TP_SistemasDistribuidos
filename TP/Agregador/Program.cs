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

    // Porta base do agregador
    private static int portaAgregador = 7000;

    // Tempo para envio periódico para o servidor
    private static int intervaloEnvioMin = 30;

    static void Main()
    {
        Console.Write("ID do Agregador (Apenas o numero): ");
        string agregadorID = Console.ReadLine()?.Trim();
        if (!int.TryParse(agregadorID, out int idNumerico))
        {
            Console.WriteLine("ID inválido. Deve ser um número inteiro.");
            return;
        }

        // Calcula porta com base no ID
        portaAgregador = 7000 + idNumerico;
        Console.WriteLine($"Agregador associado à porta {portaAgregador}.");

        // Cria a pasta onde ficarão os CSV
        Directory.CreateDirectory(pastaCsvs);
        Console.WriteLine("Agregador em execução...");

        // Thread para receber dados das WAVYs
        new Thread(ServidorEscuta).Start();

        // Timer para envio periódico (ex.: a cada 30min)
        Timer envioTimer = new Timer(_ => EnviarCsvsAoServidor(), null,
                                     TimeSpan.FromMinutes(1), // Tempo até ao 1º envio
                                     TimeSpan.FromMinutes(intervaloEnvioMin));

        // Timer para limpeza automática a cada 2 dias
        Timer limpezaTimer = new Timer(_ => LimparCsvs(), null,
                                       TimeSpan.FromDays(2),   // Tempo até primeira limpeza
                                       TimeSpan.FromDays(2));  // Repetição a cada 2 dias

        // Mantém a aplicação viva
        while (true)
        {
            Thread.Sleep(1000);
        }
    }

    // Escuta na portaAgregador e recebe CSV das WAVYs
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

    // Método que recebe linhas CSV (com cabeçalho + N linhas) e guarda num ficheiro wavyID.csv
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
                // QUIT indica fim do CSV
                if (linha.Trim().Equals("QUIT", StringComparison.OrdinalIgnoreCase))
                    break;

                linhasRecebidas.Add(linha);
            }

            if (linhasRecebidas.Count > 1)
            {
                // Ex.: 2ª linha => "WAVYID, 2025-xx-xx, etc."
                string wavyID = linhasRecebidas[1].Split(',')[0];
                string path = Path.Combine(pastaCsvs, wavyID + ".csv");

                // Se o ficheiro não existir, escreve primeiro o cabeçalho
                if (!File.Exists(path))
                    File.WriteAllLines(path, new[] { linhasRecebidas[0] });

                // Acrescenta as linhas de dados
                File.AppendAllLines(path, linhasRecebidas.Skip(1));

                // Responde ao Wavy com sucesso
                writer.WriteLine("RECEBIDO 200");
                Console.WriteLine($"CSV de {wavyID} recebido e guardado em {path}.");
            }
            else
            {
                // CSV inválido
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

    // Agrupa todos os ficheiros .csv com linhas que terminem em "False" e envia ao servidor
    static void EnviarCsvsAoServidor()
    {
        try
        {
            var ficheiros = Directory.GetFiles(pastaCsvs, "*.csv");
            List<string> linhasParaEnviar = new();
            string cabecalho = "";

            // Percorre cada CSV do aggregator
            foreach (var file in ficheiros)
            {
                var linhas = File.ReadAllLines(file).ToList();
                if (linhas.Count == 0) continue;

                // Usa o cabeçalho do primeiro CSV encontrado
                if (string.IsNullOrWhiteSpace(cabecalho))
                    cabecalho = linhas[0];

                // Linhas que terminam em "False"
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

            // Cria um ficheiro temporário com cabeçalho + linhas a enviar
            File.WriteAllLines(tempCsv, new[] { cabecalho }.Concat(linhasParaEnviar));

            // Envia esse temp ao servidor
            using TcpClient client = new TcpClient(servidorIP, servidorPorta);
            using NetworkStream stream = client.GetStream();
            using StreamWriter writer = new StreamWriter(stream) { AutoFlush = true };
            using StreamReader reader = new StreamReader(stream);

            // Manda as linhas
            foreach (var linha in File.ReadAllLines(tempCsv))
            {
                writer.WriteLine(linha);
            }
            // Sinal de fim
            writer.WriteLine("QUIT");

            // Lê resposta
            string resposta = reader.ReadLine();
            if (resposta != null && resposta.StartsWith("RECEBIDO"))
            {
                Console.WriteLine("Dados enviados com sucesso ao servidor. Marcando como enviados...");

                // Marca as linhas nos ficheiros originais
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

    // Limpa cada CSV, removendo linhas enviadas (True). Apaga o ficheiro se ficar vazio
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
}
