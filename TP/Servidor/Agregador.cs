using System.Collections.Generic;

namespace Servidor
{
    public class Agregador
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Password { get; set; }
        public List<Wavy> Wavys { get; set; } = new List<Wavy>();

 
    }
}
