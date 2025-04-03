using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Servidor
{
    public class Sensor
    {
        public int Id { get; set; }

        public string? Nome { get; set; }
        public DateTime DataHora { get; set; }

        // Novo: múltiplos valores para um sensor (ex: latitude, longitude)
        [NotMapped]
        public Dictionary<string, string> Valores { get; set; } = new Dictionary<string, string>();

        // Novo: indica se os dados deste sensor já foram enviados
        public bool DadosEnviados { get; set; } = false;
    }
}