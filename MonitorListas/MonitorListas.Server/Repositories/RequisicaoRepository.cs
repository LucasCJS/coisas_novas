using Microsoft.EntityFrameworkCore;
using MonitorListas.Server.Data;
using MonitorListas.Server.Models;

namespace MonitorListas.Server.Repositories
{
    public class RequisicaoRepository
    {
        private readonly AppDbContext _context;
        public RequisicaoRepository(AppDbContext context)
        {
            _context = context;
        }
        public async Task<List<Requisicao>> ObterTodosPorTipoAsync(string tipo)
        {
            return await _context.Requisicoes
                .Where(x => x.Tipo == tipo)
                .ToListAsync();
        }

        public async Task<bool> VerificarSeHouveMudancaRecenteAsync(DateTime ultimaVerificacao)
        {
            return await _context.Requisicoes
                .AnyAsync(x => x.DtStatus > ultimaVerificacao);
        }
    }
}
