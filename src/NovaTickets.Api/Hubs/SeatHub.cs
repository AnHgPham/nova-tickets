using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace NovaTickets.Api.Hubs;

[Authorize]
public sealed class SeatHub : Hub
{
    public Task WatchPerformance(string performanceId) => Groups.AddToGroupAsync(Context.ConnectionId, $"performance:{performanceId}");
    public Task LeavePerformance(string performanceId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"performance:{performanceId}");
}
