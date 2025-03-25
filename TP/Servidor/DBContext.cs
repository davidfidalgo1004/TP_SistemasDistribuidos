using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Servidor
{
    class DBContext : DbContext
    {
        public DbSet<Agregador> Agregadores { get; set; }
        public DbSet<Wavy> Wavys { get; set; }
        public DbSet<Sensor> Sensores { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Configura a string de conexão para o banco de dados "server"
            optionsBuilder.UseSqlServer("Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=DBServer;Integrated Security=True");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configura o relacionamento: Um Agregador possui muitos Wavys.
            modelBuilder.Entity<Agregador>()
                .HasMany(a => a.Wavys)
                .WithOne() // sem propriedade de navegação reversa em Wavy
                .HasForeignKey("AgregadorId"); // chave estrangeira a ser criada na tabela de Wavys

            // Configura o relacionamento: Um Wavy possui muitos Sensores.
            modelBuilder.Entity<Wavy>()
                .HasMany(w => w.Sensores)
                .WithOne() // sem propriedade de navegação reversa em Sensor
                .HasForeignKey("WavyId"); // chave estrangeira a ser criada na tabela de Sensores
        }
    }
}
