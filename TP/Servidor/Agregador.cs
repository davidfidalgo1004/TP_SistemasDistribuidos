using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Servidor
{
    class Agregador
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public List<Wavy> Wavys { get; set; } = new List<Wavy>();
    }
}
