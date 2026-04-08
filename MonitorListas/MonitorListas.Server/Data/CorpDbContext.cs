using Microsoft.EntityFrameworkCore;
using MonitorListas.Server.Models;

namespace MonitorListas.Server.Data
{
    public class CorpDbContext : DbContext
    {
        public CorpDbContext(DbContextOptions<CorpDbContext> options) : base(options) { }

        public DbSet<UsuarioLegado> Usuarios { get; set; }
    }
}
