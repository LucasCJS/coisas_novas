using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MonitorListas.Server.Data;
using MonitorListas.Server.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MonitorListas.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class XmlController : ControllerBase
    {
        private readonly MonitorSettings _settings;
        private readonly MonitorDbContext _dbContext;
        private readonly AppDbContext _appDbContext;

        public XmlController(IOptions<MonitorSettings> options, MonitorDbContext dbContext)
        {
            _settings = options.Value;
            _dbContext = dbContext;
        }

        [HttpGet("recentes")]
        public IActionResult GetRecentes()
        {
            try
            {
                var arquivosParaTela = new List<ArquivoXml>();

                // Traz todos os erros do banco de uma vez (Rápido e sem travar arquivos)
                var todosOsErros = _dbContext.Arquivos.Where(a => !a.IsValido).ToList();

                // ==========================================
                // 1. O ESPELHO DA PASTA (Sem ler o conteúdo do XML!)
                // ==========================================
                var directory = new DirectoryInfo(_settings.CaminhoPasta);
                if (directory.Exists)
                {
                    var arquivosNaPasta = directory.EnumerateFiles("*.xml");

                    foreach (var f in arquivosNaPasta)
                    {
                        // Cruza o nome do arquivo físico com o que temos no Banco de Dados
                        var erroNoBanco = todosOsErros.FirstOrDefault(e => e.Nome == f.Name);

                        if (erroNoBanco != null)
                        {
                            // Se está no banco, é um arquivo Inválido que ainda não foi apagado da pasta
                            arquivosParaTela.Add(new ArquivoXml
                            {
                                Nome = f.Name,
                                DataGeracao = f.LastWriteTime,
                                IsValido = false,
                                Erros = string.IsNullOrEmpty(erroNoBanco.ErrosFormatados)
                                        ? new List<string>()
                                        : erroNoBanco.ErrosFormatados.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList()
                            });
                        }
                        else
                        {
                            // Se NÃO está no banco de erros, assumimos que é VÁLIDO
                            arquivosParaTela.Add(new ArquivoXml
                            {
                                Nome = f.Name,
                                DataGeracao = f.LastWriteTime,
                                IsValido = true,
                                Erros = new List<string>()
                            });
                        }
                    }
                }

                // ==========================================
                // 2. O COFRE (Erros antigos da Quarentena)
                // ==========================================
                foreach (var erro in todosOsErros)
                {
                    // Adiciona os erros que já foram apagados da pasta física, mas continuam no histórico
                    if (!arquivosParaTela.Any(x => x.Nome == erro.Nome))
                    {
                        arquivosParaTela.Add(new ArquivoXml
                        {
                            Nome = erro.Nome,
                            DataGeracao = erro.DataGeracao,
                            IsValido = false,
                            Erros = string.IsNullOrEmpty(erro.ErrosFormatados)
                                    ? new List<string>()
                                    : erro.ErrosFormatados.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList()
                        });
                    }
                }

                return Ok(arquivosParaTela.OrderByDescending(f => f.DataGeracao));
            }
            catch (Exception ex)
            {
                return BadRequest($"Erro ao ler os dados: {ex.Message}");
            }
        }

        // ==========================================
        // 3. O DOWNLOAD BLINDADO
        // ==========================================
        [HttpGet("download")]
        public IActionResult DownloadArquivo([FromQuery] string arquivo)
        {
            try
            {
                if (string.IsNullOrEmpty(arquivo) || arquivo.Contains("..") || arquivo.Contains("/") || arquivo.Contains("\\"))
                    return BadRequest("Nome inválido.");

                string caminhoFisico = Path.Combine(_settings.CaminhoPasta, arquivo);

                if (!System.IO.File.Exists(caminhoFisico))
                {
                    caminhoFisico = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Quarentena_XML", arquivo);
                }

                if (!System.IO.File.Exists(caminhoFisico))
                    return NotFound("Arquivo não encontrado em nenhum local.");

                var stream = new FileStream(caminhoFisico, FileMode.Open, FileAccess.Read, FileShare.Read);
                return File(stream, "application/xml", arquivo);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro no download: {ex.Message}");
            }
        }

        // 1. APAGA APENAS DA PASTA ORIGINAL
        [HttpDelete("apagar-original")]
        public IActionResult ApagarOriginal([FromQuery] string arquivo)
        {
            if (string.IsNullOrEmpty(arquivo)) return BadRequest("Nome do arquivo não informado.");

            try
            {
                // Se você tiver o _settings.CaminhoPasta configurado no Controller, use ele.
                // Caso contrário, substitua pela sua string fixa de caminho temporariamente.
                string pastaOriginal = _settings.CaminhoPasta;
                string caminhoOriginal = Path.Combine(pastaOriginal, arquivo);

                if (System.IO.File.Exists(caminhoOriginal))
                {
                    System.IO.File.Delete(caminhoOriginal);
                }

                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao apagar arquivo original: {ex.Message}");
            }
        }

        // 2. APAGA DO SQLITE E DA QUARENTENA (HARD DELETE)
        [HttpDelete("apagar-quarentena")]
        public async Task<IActionResult> ApagarQuarentena([FromQuery] string arquivo)
        {
            if (string.IsNullOrEmpty(arquivo)) return BadRequest("Nome do arquivo não informado.");

            try
            {
                // Apaga do SQLite
                var registro = await _dbContext.Arquivos.FirstOrDefaultAsync(a => a.Nome == arquivo);
                if (registro != null)
                {
                    _dbContext.Arquivos.Remove(registro);
                    await _dbContext.SaveChangesAsync();
                }

                // Apaga da Quarentena Física
                string pastaQuarentena = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Quarentena_XML");
                string caminhoQuarentena = Path.Combine(pastaQuarentena, arquivo);

                if (System.IO.File.Exists(caminhoQuarentena))
                {
                    System.IO.File.Delete(caminhoQuarentena);
                }

                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao limpar sistema: {ex.Message}");
            }
        }
    }
}