using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using Servidor;  // Contém as classes Agregador, Wavy, Sensor e DBContext
using Microsoft.EntityFrameworkCore;

class DataServer
{
    private static int port = 13000;
    private static object dbLock = new object();

    static void Main()
    {
        // Inicializa o banco de dados (cria as tabelas se ainda não existirem)
        using (var context = new DBContext())
        {
            context.Database.EnsureCreated();
            Console.WriteLine("Banco de dados inicializado.");
        }

        TcpListener listener = new TcpListener(IPAddress.Parse("127.0.0.1"), port);
        listener.Start();
        Console.WriteLine("SERVIDOR EM ESCUTA...");

        while (true)
        {
            TcpClient client = listener.AcceptTcpClient();
            Thread clientThread = new Thread(() => HandleClient(client));
            clientThread.Start();
        }
    }

    static void HandleClient(TcpClient client)
    {
        try
        {
            using (NetworkStream stream = client.GetStream())
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
            {
                
                writer.WriteLine("OK 100");
                Console.WriteLine("Enviado: OK 100");

                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    // Se receber "QUIT", envia "BYE" e encerra a conexão
                    if (line.Trim().Equals("QUIT", StringComparison.OrdinalIgnoreCase))
                    {
                        writer.WriteLine("BYE");
                        Console.WriteLine("Recebido QUIT, enviando BYE e fechando conexão.");
                        break;
                    }
                    else
                    {
                        Console.WriteLine("Mensagem recebida:");
                        Console.WriteLine(line);

                        // Tenta desserializar o JSON recebido para a estrutura Agregador
                        try
                        {
                            Agregador agregador = JsonSerializer.Deserialize<Agregador>(line);
                            if (agregador != null)
                            {
                                SaveAgregador(agregador);
                            }
                            else
                            {
                                Console.WriteLine("Falha ao desserializar o JSON para a estrutura Agregador.");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Erro ao desserializar JSON: " + ex.Message);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro no tratamento do cliente: " + ex.Message);
        }
        finally
        {
            client.Close();
        }
    }

    // Método que salva ou atualiza o objeto Agregador na base de dados
    static void SaveAgregador(Agregador agregador)
    {
        lock (dbLock)
        {
            using (var context = new DBContext())
            {
                // Verifica se já existe um Agregador com o mesmo nome
                var existingAgregador = context.Agregadores
                    .Include(a => a.Wavys)
                        .ThenInclude(w => w.Sensores)
                    .FirstOrDefault(a => a.Name == agregador.Name);

                if (existingAgregador != null)
                {
                    // Neste exemplo, removemos os Wavys antigos e inserimos os novos
                    context.Wavys.RemoveRange(existingAgregador.Wavys);
                    existingAgregador.Wavys = agregador.Wavys;
                    context.SaveChanges();
                    Console.WriteLine("Agregador existente atualizado com novos dados.");
                }
                else
                {
                    // Insere um novo Agregador na base de dados
                    context.Agregadores.Add(agregador);
                    context.SaveChanges();
                    Console.WriteLine("Novo agregador salvo na base de dados.");
                }
            }
        }
    }
}
