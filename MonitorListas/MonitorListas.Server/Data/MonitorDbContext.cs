using Microsoft.EntityFrameworkCore;
using MonitorListas.Server.Models;

namespace MonitorListas.Server.Data
{
    public class MonitorDbContext : DbContext
    {
        public MonitorDbContext(DbContextOptions<MonitorDbContext> options) : base(options)
        {
        }

        // Esta é a nossa tabela mágica que vai guardar o histórico
        public DbSet<ArquivoRegistro> Arquivos { get; set; }
    }
}