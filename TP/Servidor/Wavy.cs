using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Servidor
{
    class Wavy
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public List<Sensor> Sensores { get; set; } = new List<Sensor>();

    }
}
