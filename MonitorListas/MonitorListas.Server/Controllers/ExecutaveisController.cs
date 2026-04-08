using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MonitorListas.Server.Data;
using MonitorListas.Server.Models;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MonitorListas.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExecutaveisController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ExecutaveisController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetStatusExecucoes()
        {
            try
            {
                var listaUnificada = new List<ListaRegistro>();

                // Recupera registros da tabela TB_REQUISICOES com limite de 100 registros
                var requisicoes = await _context.Requisicoes
                .Where(r => r.Tipo == "LISTA") // <--- ADICIONE ESTA LINHA AQUI
                .OrderByDescending(r => r.DtRequisicaoInicio)
                .ToListAsync();

                foreach (var req in requisicoes)
                {
                    string statusFinal = req.Status ?? "PENDENTE";

                    // Concatenação de erro caso o status final indique falha
                    if (statusFinal.Equals("ERRO", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(req.DeErro))
                    {
                        statusFinal = $"{statusFinal}: {req.DeErro}";
                    }

                    listaUnificada.Add(new ListaRegistro
                    {
                        CodigoLista = req.GuidRequisicao,
                        NomeLista = req.NomeParte ?? "Lista Sem Nome",
                        StatusLista = statusFinal,
                        InicioExecucao = req.DtRequisicaoInicio,
                        FimExecucao = req.DtRequisicao
                    });
                }

                // Recupera registros da tabela TB_LISTA (TopShelf)
                var listasTopShelf = await _context.TbLista.ToListAsync();

                foreach (var lista in listasTopShelf)
                {
                    listaUnificada.Add(new ListaRegistro
                    {
                        CodigoLista = lista.TipoLista,
                        NomeLista = lista.TipoLista,
                        StatusLista = lista.DescUltimoLog ?? "Sem Log",
                        InicioExecucao = lista.UltimaExecucao ?? DateTime.MinValue,
                        FimExecucao = lista.DataProcessado ?? DateTime.MinValue
                    });
                }

                // Ordena a lista consolidada de forma decrescente pela data de início
                var resultadoFinal = listaUnificada
                    .OrderByDescending(x => x.InicioExecucao)
                    .ToList();

                return Ok(resultadoFinal);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao consolidar os status: {ex.Message}");
            }
        }
    }
}