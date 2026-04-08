using Microsoft.EntityFrameworkCore;
using MonitorListas.Server.Data;
using MonitorListas.Server.Models;

namespace MonitorListas.Server.Repositories
{
    public class TbListaRepository
    {
        private readonly AppDbContext _context;
        public TbListaRepository(AppDbContext context)
        {
            _context = context;
        }
        public async Task<List<TbLista>> ObterTodosAsync()
        {
            return await _context.TbLista.ToListAsync();
        }

        public async Task<bool> VerificarSeHouveMudancaRecenteAsync(DateTime ultimaVerificacao)
        {
            return await _context.TbLista
                .AnyAsync(x => x.DataProcessado > ultimaVerificacao);
        }
    }
}
