using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MonitorListas.Server.Hubs;
using MonitorListas.Server.Models;
using MonitorListas.Server.Data;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace MonitorListas.Server.Services
{
    public class MonitoradorPastaService : BackgroundService
    {
        private readonly MonitorSettings _settings;
        private readonly IHubContext<MonitorHub> _hubContext;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly HashSet<string> _arquivosConhecidos = new HashSet<string>();

        public MonitoradorPastaService(
            IOptions<MonitorSettings> options,
            IHubContext<MonitorHub> hubContext,
            IServiceScopeFactory scopeFactory)
        {
            _settings = options.Value;
            _hubContext = hubContext;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            string caminho = _settings.CaminhoPasta;
            TimeSpan intervalo = TimeSpan.FromSeconds(_settings.IntervaloVerificacaoSegundos);

            _ = Task.Run(async () =>
            {
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
                            var arquivosAtuais = directory.EnumerateFiles("*.xml", new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive })
                            .OrderByDescending(f => f.LastWriteTime)
                            .Take(50); ;

                            foreach (var arquivo in arquivosAtuais)
                            {
                                if (!_arquivosConhecidos.Contains(arquivo.Name))
                                {
                                    if (!IsFileReady(arquivo.FullName))
                                    {
                                        continue;
                                    }

                                    var validacao = ValidadorXml.Validar(arquivo.FullName);

                                    if (!validacao.IsValido)
                                    {
                                        var registro = new ArquivoRegistro
                                        {
                                            Nome = arquivo.Name,
                                            DataGeracao = arquivo.LastWriteTime,
                                            DataProcessamento = DateTime.Now,
                                            IsValido = false,
                                            ErrosFormatados = string.Join(";", validacao.Erros)
                                        };

                                        if (!dbContext.Arquivos.Any(a => a.Nome == arquivo.Name))
                                        {
                                            dbContext.Arquivos.Add(registro);
                                            await dbContext.SaveChangesAsync(stoppingToken);
                                        }

                                        string pastaQuarentena = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Quarentena_XML");
                                        if (!Directory.Exists(pastaQuarentena)) Directory.CreateDirectory(pastaQuarentena);

                                        string caminhoDestino = Path.Combine(pastaQuarentena, arquivo.Name);
                                        File.Copy(arquivo.FullName, caminhoDestino, true);
                                    }

                                    var novoXml = new ArquivoXml
                                    {
                                        Nome = arquivo.Name,
                                        DataGeracao = arquivo.LastWriteTime,
                                        IsValido = validacao.IsValido,
                                        Erros = validacao.Erros
                                    };

                                    await _hubContext.Clients.All.SendAsync("ReceberNovoArquivo", novoXml, stoppingToken);
                                    Console.WriteLine($"[SignalR] {arquivo.Name} | Status: {validacao.IsValido}");

                                    _arquivosConhecidos.Add(arquivo.Name);
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
            }, stoppingToken);
        }

        private bool IsFileReady(string filename)
        {
            try
            {
                using (FileStream inputStream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    return inputStream.Length > 0;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void RealizarFaxina(MonitorDbContext dbContext)
        {
            try
            {
                var dataLimite = DateTime.Today.AddDays(-_settings.DiasRetencao);
                var registrosVencidos = dbContext.Arquivos.Where(a => a.DataGeracao < dataLimite).ToList();

                if (registrosVencidos.Any())
                {
                    dbContext.Arquivos.RemoveRange(registrosVencidos);
                    dbContext.SaveChanges();

                    string pastaQuarentena = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Quarentena_XML");
                    if (Directory.Exists(pastaQuarentena))
                    {
                        foreach (var reg in registrosVencidos)
                        {
                            string caminhoFisico = Path.Combine(pastaQuarentena, reg.Nome);
                            if (File.Exists(caminhoFisico))
                            {
                                File.Delete(caminhoFisico);
                            }
                        }
                    }
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