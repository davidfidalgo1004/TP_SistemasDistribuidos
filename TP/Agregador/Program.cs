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
    // Vamos construir a pasta do Agregador com base no ID
    private static string aggregatorFolder;
    private static string pastaCsvs;        // = aggregatorFolder + /csvs

    // Ficheiro temporário que usamos ao enviar ao servidor
    private static string tempCsv = "temp.csv";

    // Dados do servidor final
    private static string servidorIP = "127.0.1.1";
    private static int servidorPorta = 8080;

    // Porta base do Agregador
    private static int portaAgregador = 7000;

    // Intervalo para envio periódico (em minutos)
    private static int intervaloEnvioMin = 30;

    static void Main()
    {
        Console.Write("ID do Agregador (apenas número): ");
        string agregadorID = Console.ReadLine()?.Trim();
        if (!int.TryParse(agregadorID, out int idNumerico))
        {
            Console.WriteLine("ID inválido. Deve ser um número inteiro.");
            return;
        }

        // Constrói a porta = 7000 + ID
        portaAgregador = 7000 + idNumerico;

        // Cria a pasta principal do agregador, ex. "Agregador1"
        aggregatorFolder = "Agregador" + idNumerico;

        // A subpasta csvs ficará em "Agregador1/csvs"
        pastaCsvs = Path.Combine(aggregatorFolder, "csvs");

        // Criar a pasta do agregador e a subpasta csvs
        Directory.CreateDirectory(pastaCsvs);

        Console.WriteLine($"Agregador '{aggregatorFolder}' associado à porta {portaAgregador}.");
        Console.WriteLine("Agregador em execução...");

        // 1) Thread para receber dados das WAVYs
        new Thread(ServidorEscuta).Start();

        // 2) Timer para envio periódico ao servidor
        Timer envioTimer = new Timer(_ => EnviarCsvsAoServidor(),
                                     null,
                                     TimeSpan.FromMinutes(1),         // Espera 1 min para primeiro envio
                                     TimeSpan.FromMinutes(intervaloEnvioMin));

        // 3) Timer para limpeza automática a cada 2 dias
        Timer limpezaTimer = new Timer(_ => LimparCsvs(),
                                       null,
                                       TimeSpan.FromDays(2),  // Espera 2 dias para primeira limpeza
                                       TimeSpan.FromDays(2)); // Repetição a cada 2 dias

        // Mantém a app viva
        while (true)
        {
            Thread.Sleep(1000);
        }
    }

    // Escuta na porta do Agregador e recebe CSV das WAVYs
    static void ServidorEscuta()
    {
     
        TcpListener listener = new TcpListener(IPAddress.Parse("127.0.1.1"), portaAgregador);
        listener.Start();
        Console.WriteLine($"Agregador a escutar na porta {portaAgregador}...");

        while (true)
        {
            TcpClient client = listener.AcceptTcpClient();
            new Thread(() => ReceberCsvDoWavy(client)).Start();
        }
    }

    // Recebe linhas CSV (cabeçalho + N linhas) e guarda em aggregatorFolder/csvs/wavyID.csv
    static void ReceberCsvDoWavy(TcpClient client)
    {
        var linhasRecebidas = new List<string>();

        try
        {
            using NetworkStream stream = client.GetStream();
            using StreamReader reader = new StreamReader(stream);
            using StreamWriter writer = new StreamWriter(stream) { AutoFlush = true };

            string linha;
            while ((linha = reader.ReadLine()) != null)
            {
                // QUIT => fim do CSV
                if (linha.Trim().Equals("QUIT", StringComparison.OrdinalIgnoreCase))
                    break;

                linhasRecebidas.Add(linha);
            }

            // Se recebeu ao menos cabeçalho+1
            if (linhasRecebidas.Count > 1)
            {
                // Ex.: 2ª linha => "WAVYID, 2025-xx-xx, etc."
                // Pegamos o wavyID como a primeira coluna
                string wavyID = linhasRecebidas[1].Split(',')[0];

                // Gera o caminho final
                string path = Path.Combine(pastaCsvs, wavyID + ".csv");

                // Se o ficheiro não existir, escreve primeiro o cabeçalho
                if (!File.Exists(path))
                    File.WriteAllLines(path, new[] { linhasRecebidas[0] });

                // Acrescenta as linhas de dados
                File.AppendAllLines(path, linhasRecebidas.Skip(1));

                // Responde com sucesso
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

    // Junta todos os ficheiros .csv (com "False") e envia ao servidor => "127.0.1.1:8080"
    static void EnviarCsvsAoServidor()
    {
        try
        {
            // Procura os CSV no aggregatorFolder/csvs/
            var ficheiros = Directory.GetFiles(pastaCsvs, "*.csv");
            var linhasParaEnviar = new List<string>();
            string cabecalho = "";

            foreach (var file in ficheiros)
            {
                var linhas = File.ReadAllLines(file).ToList();
                if (linhas.Count == 0) continue;

                // Usa o cabeçalho do primeiro CSV válido
                if (string.IsNullOrWhiteSpace(cabecalho))
                    cabecalho = linhas[0];

                // Pega as linhas que terminam em "False"
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

            // Cria um ficheiro temporário com cabeçalho + linhas
            File.WriteAllLines(tempCsv, new[] { cabecalho }.Concat(linhasParaEnviar));

            // Envia o ficheiro temp ao servidor
            using TcpClient client = new TcpClient(servidorIP, servidorPorta);
            using NetworkStream stream = client.GetStream();
            using StreamWriter writer = new StreamWriter(stream) { AutoFlush = true };
            using StreamReader reader = new StreamReader(stream);

            // Manda as linhas
            foreach (var linha in File.ReadAllLines(tempCsv))
            {
                writer.WriteLine(linha);
            }
            writer.WriteLine("QUIT");

            // Lê a resposta
            string resposta = reader.ReadLine();
            if (resposta != null && resposta.StartsWith("RECEBIDO"))
            {
                Console.WriteLine("Dados enviados com sucesso ao servidor. Marcando como enviados...");

                // Marca as linhas 'False' => 'True' em cada ficheiro
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

    // Limpa os ficheiros CSV, removendo as linhas 'True', e apaga o ficheiro se ficar vazio
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
