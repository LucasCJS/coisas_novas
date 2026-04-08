using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.SignalR;
using MonitorListas.Server.Data;
using MonitorListas.Server.Repositories;
using MonitorListas.Server.Hubs;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MonitorListas.Server.Services
{
    public class MonitorExecutaveisListaService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<MonitorHub> _hubContext;
        private DateTime _ultimaExecucaoService = DateTime.Now;

        public MonitorExecutaveisListaService(
            IServiceScopeFactory scopeFactory,
            IHubContext<MonitorHub> hubContext)
        {
            _scopeFactory = scopeFactory;
            _hubContext = hubContext;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _ = Task.Run(async () =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();

                        var requisicaoRepo = scope.ServiceProvider.GetRequiredService<RequisicaoRepository>();
                        var tbListaRepo = scope.ServiceProvider.GetRequiredService<TbListaRepository>();

                        bool mudouRequisicao = await requisicaoRepo.VerificarSeHouveMudancaRecenteAsync(_ultimaExecucaoService);
                        bool mudouTbLista = await tbListaRepo.VerificarSeHouveMudancaRecenteAsync(_ultimaExecucaoService);

                        if (mudouRequisicao || mudouTbLista)
                        {
                            _ultimaExecucaoService = DateTime.Now;

                            await _hubContext.Clients.All.SendAsync("AtualizarTelaExecutaveis", stoppingToken);
                            Console.WriteLine("[SignalR Hub] Dispatched 'AtualizarTelaExecutaveis' event.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[MonitorExecutaveisListaService Error] {ex.Message}");
                    }

                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }, stoppingToken);
        }
    }
}