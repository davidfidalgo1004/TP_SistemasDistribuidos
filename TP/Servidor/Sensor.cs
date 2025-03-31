using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Servidor
{
    class Sensor
    {
        public int Id { get; set; }

        public string? Nome { get; set; }
        public DateTime DataHora { get; set; }
        public string? JsonData { get; set; }
    }
}
