using System;
using System.Collections.Generic;

namespace Servidor
{
    public class Wavy
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public List<Sensor> Sensores { get; set; } = new List<Sensor>();

        // Novo: Estado da WAVY (associada, operacao, manutencao, desativada)
        public string? Estado { get; set; }
    }
}
