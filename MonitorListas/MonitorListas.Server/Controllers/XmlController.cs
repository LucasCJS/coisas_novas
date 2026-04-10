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
using System.Runtime.InteropServices;

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

        // --- MÉTODO AUXILIAR PARA RESOLVER O CAMINHO ---
        // Assim você não repete código e garante que todos os métodos olhem pro lugar certo
        private string ObterCaminhoPastaOrigem()
        {
            bool isDocker = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
            return isDocker ? "/app/xml_origem" : _settings.CaminhoPasta;
        }

        [HttpGet("recentes")]
        public IActionResult GetRecentes()
        {
            try
            {
                // Usa o método auxiliar aqui
                string caminho = ObterCaminhoPastaOrigem();

                var arquivosParaTela = new List<ArquivoXml>();
                var todosOsErros = _dbContext.Arquivos.Where(a => !a.IsValido).ToList();

                var directory = new DirectoryInfo(caminho);
                if (directory.Exists)
                {
                    var arquivosNaPasta = directory.EnumerateFiles("*.xml", new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive });

                    foreach (var f in arquivosNaPasta)
                    {
                        var erroNoBanco = todosOsErros.FirstOrDefault(e => e.Nome == f.Name);

                        if (erroNoBanco != null)
                        {
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

                Console.WriteLine($"[Monitor] Caminho: {caminho}");
                Console.WriteLine($"[Monitor] Existe: {Directory.Exists(caminho)}");
                Console.WriteLine($"[Monitor] Arquivos: {Directory.GetFiles(caminho).Length}");

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

                string caminhoOrigem = ObterCaminhoPastaOrigem();

                // 1. Tenta achar na pasta Origem (ignorando maiúsculas/minúsculas)
                string caminhoFisico = BuscarArquivoIgnorandoCase(caminhoOrigem, arquivo);

                // 2. Se não achou, tenta achar na Quarentena (ignorando maiúsculas/minúsculas)
                if (string.IsNullOrEmpty(caminhoFisico))
                {
                    string pastaQuarentena = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Quarentena_XML");
                    caminhoFisico = BuscarArquivoIgnorandoCase(pastaQuarentena, arquivo);
                }

                // 3. Se continuou vazio, realmente não existe
                if (string.IsNullOrEmpty(caminhoFisico))
                    return NotFound("Arquivo não encontrado em nenhum local.");

                var stream = new FileStream(caminhoFisico, FileMode.Open, FileAccess.Read, FileShare.Read);

                // Usamos o Path.GetFileName para devolver com o nome exato que está no disco
                return File(stream, "application/xml", Path.GetFileName(caminhoFisico));
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro no download: {ex.Message}");
            }
        }

        // --- NOVO MÉTODO AUXILIAR ---
        // Ele faz o File.Exists, mas funciona no Linux ignorando maiúsculas e minúsculas
        private string BuscarArquivoIgnorandoCase(string diretorio, string nomeArquivo)
        {
            var dirInfo = new DirectoryInfo(diretorio);
            if (!dirInfo.Exists) return null;

            // Busca o arquivo exato sem ligar para o tamanho da letra
            var file = dirInfo.GetFiles(nomeArquivo, new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive }).FirstOrDefault();

            return file?.FullName; // Retorna o caminho completo se achar, ou null se não achar
        }

        // 1. APAGA APENAS DA PASTA ORIGINAL
        [HttpDelete("apagar-original")]
        public IActionResult ApagarOriginal([FromQuery] string arquivo)
        {
            if (string.IsNullOrEmpty(arquivo)) return BadRequest("Nome do arquivo não informado.");

            try
            {
                // Usa o método auxiliar AQUI também!
                string pastaOriginal = ObterCaminhoPastaOrigem();
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

        // ==========================================
        // 4. DUPLICAR ARQUIVO (VIRADA DE DIA)
        // ==========================================
        [HttpPost("duplicar-para-hoje")]
        public IActionResult DuplicarParaHoje([FromQuery] string arquivo)
        {
            if (string.IsNullOrEmpty(arquivo)) return BadRequest("Nome inválido.");

            string caminhoOrigem = ObterCaminhoPastaOrigem();
            string caminhoFisico = BuscarArquivoIgnorandoCase(caminhoOrigem, arquivo);

            if (string.IsNullOrEmpty(caminhoFisico))
                return NotFound("Arquivo original não encontrado na pasta.");

            try
            {
                // 1. Extrair a data do nome do arquivo (procura por 8 números seguidos: yyyyMMdd)
                var match = System.Text.RegularExpressions.Regex.Match(arquivo, @"\d{8}");
                if (!match.Success)
                    return BadRequest("Não foi possível identificar uma data (yyyyMMdd) no nome do arquivo.");

                string dataAntigaStr = match.Value;
                if (!DateTime.TryParseExact(dataAntigaStr, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out DateTime dataArquivo))
                    return BadRequest("A data encontrada no nome do arquivo é inválida.");

                // 2. Regra: Máximo 1 dia atrás
                double diasDeDiferenca = (DateTime.Today - dataArquivo.Date).TotalDays;
                if (diasDeDiferenca > 1 || diasDeDiferenca < 0)
                {
                    return BadRequest($"Você só pode duplicar arquivos gerados hoje ou ontem. Data do arquivo: {dataArquivo:dd/MM/yyyy}");
                }

                DateTime hoje = DateTime.Today;
                string strHojeNome = hoje.ToString("yyyyMMdd");
                string strHojeTagTraco = hoje.ToString("yyyy-MM-dd");

                // 3. Montar o novo nome
                string novoNome = arquivo.Replace(dataAntigaStr, strHojeNome);
                string novoCaminhoFisico = Path.Combine(caminhoOrigem, novoNome);

                if (System.IO.File.Exists(novoCaminhoFisico))
                    return BadRequest($"O arquivo de hoje ({novoNome}) já existe na pasta!");

                // 4. Abrir e alterar o conteúdo do XML
                var xdoc = System.Xml.Linq.XDocument.Load(caminhoFisico);

                var dtAtualizacao = xdoc.Descendants("DT_ATUALIZACAO").FirstOrDefault();
                if (dtAtualizacao != null) dtAtualizacao.Value = strHojeTagTraco;

                var cdVersao = xdoc.Descendants("CD_VERSAO").FirstOrDefault();
                if (cdVersao != null) cdVersao.Value = strHojeNome;

                // 5. Salvar a cópia
                xdoc.Save(novoCaminhoFisico);

                return Ok(new { mensagem = "Arquivo duplicado com sucesso!", novoArquivo = novoNome });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao processar o XML: {ex.Message}");
            }
        }
    }
}