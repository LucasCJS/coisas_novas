using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MonitorListas.Server.Services;

public class MonitorHub : Hub
{
    private readonly MonitoradorPastaService _service;

    public MonitorHub(MonitoradorPastaService service)
    {
        _service = service;
    }

    public override async Task OnConnectedAsync()
    {
        Console.WriteLine($"SignalR conectado: {Context.ConnectionId}");

        await _service.ReenviarEstado(Context.ConnectionId);

        await base.OnConnectedAsync();
    }
}