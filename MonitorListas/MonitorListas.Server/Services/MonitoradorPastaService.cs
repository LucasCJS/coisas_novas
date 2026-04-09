using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MonitorListas.Server.Data;
using MonitorListas.Server.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace MonitorListas.Server.Services
{
    public class MonitoradorPastaService : BackgroundService
    {
        private readonly MonitorSettings _settings;
        private readonly IHubContext<MonitorHub> _hubContext;
        private readonly IServiceScopeFactory _scopeFactory;

        private HashSet<string> _arquivosEnviados = new();
        private List<ArquivoXml> _cacheAtual = new();

        public MonitoradorPastaService(
            IOptions<MonitorSettings> options,
            IHubContext<MonitorHub> hubContext,
            IServiceScopeFactory scopeFactory)
        {
            _settings = options.Value;
            _hubContext = hubContext;
            _scopeFactory = scopeFactory;
        }

        public async Task ReenviarEstado(string connectionId)
        {
            foreach (var arquivo in _cacheAtual)
            {
                await _hubContext.Clients.Client(connectionId)
                    .SendAsync("ReceberNovoArquivo", arquivo);
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            bool isDocker = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
            string caminho = isDocker ? "/app/xml_origem" : _settings.CaminhoPasta;

            TimeSpan intervalo = TimeSpan.FromSeconds(_settings.IntervaloVerificacaoSegundos);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<MonitorDbContext>();

                    RealizarFaxina(dbContext);

                    var directory = new DirectoryInfo(caminho);

                    if (directory.Exists)
                    {
                        var arquivosAtuais = directory
                            .EnumerateFiles("*.xml", new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive })
                            .OrderByDescending(f => f.LastWriteTime)
                            .Take(50)
                            .ToList();

                        var nomesAtuais = arquivosAtuais
                            .Select(a => a.Name)
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);

                        bool mudou = !_arquivosEnviados.SetEquals(nomesAtuais);

                        if (mudou)
                        {
                            Console.WriteLine($"[Monitor] Arquivos detectados: {nomesAtuais.Count}");

                            // LIMPE O CACHE E A TELA PRIMEIRO
                            _arquivosEnviados.Clear();
                            _cacheAtual.Clear();
                            await _hubContext.Clients.All.SendAsync("LimparLista", stoppingToken);

                            foreach (var arquivo in arquivosAtuais)
                            {
                                if (!IsFileReady(arquivo.FullName))
                                {
                                    // O arquivo ainda está sendo gravado na rede. Pula, mas NÃO adiciona nos "enviados".
                                    // No próximo ciclo de 5 segundos, o "mudou" vai dar true de novo e ele tenta novamente.
                                    continue;
                                }

                                var validacao = ValidadorXml.Validar(arquivo.FullName);

                                if (!validacao.IsValido)
                                {
                                    var jaExiste = await dbContext.Arquivos.AnyAsync(a => a.Nome == arquivo.Name, stoppingToken);

                                    if (!jaExiste)
                                    {
                                        var registro = new ArquivoRegistro
                                        {
                                            Nome = arquivo.Name,
                                            DataGeracao = arquivo.LastWriteTime,
                                            DataProcessamento = DateTime.Now,
                                            IsValido = false,
                                            ErrosFormatados = string.Join(";", validacao.Erros)
                                        };

                                        dbContext.Arquivos.Add(registro);
                                        await dbContext.SaveChangesAsync(stoppingToken);
                                    }
                                }

                                var novoXml = new ArquivoXml
                                {
                                    Nome = arquivo.Name,
                                    DataGeracao = arquivo.LastWriteTime,
                                    IsValido = validacao.IsValido,
                                    Erros = validacao.Erros
                                };

                                await _hubContext.Clients.All.SendAsync("ReceberNovoArquivo", novoXml, stoppingToken);

                                // ADICIONE AQUI: Só adiciona no cache e na lista se realmente leu com sucesso!
                                _cacheAtual.Add(novoXml);
                                _arquivosEnviados.Add(arquivo.Name);

                                Console.WriteLine($"[SignalR] {arquivo.Name} | Status: {validacao.IsValido}");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[IO Warning] Path not found: {caminho}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MonitoradorPastaService Error] {ex.Message}");
                }

                await Task.Delay(intervalo, stoppingToken);
            }
        }

        private bool IsFileReady(string filename)
        {
            try
            {
                using var stream = new FileStream(
                    filename,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite);

                return stream.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private void RealizarFaxina(MonitorDbContext dbContext)
        {
            try
            {
                var dataLimite = DateTime.Today.AddDays(-_settings.DiasRetencao);

                var registrosVencidos = dbContext.Arquivos
                    .Where(a => a.DataGeracao < dataLimite)
                    .ToList();

                if (registrosVencidos.Any())
                {
                    dbContext.Arquivos.RemoveRange(registrosVencidos);
                    dbContext.SaveChanges();

                    Console.WriteLine($"[Cleanup Task] {registrosVencidos.Count} stale records purged.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Cleanup Task Error] {ex.Message}");
            }
        }
    }
}