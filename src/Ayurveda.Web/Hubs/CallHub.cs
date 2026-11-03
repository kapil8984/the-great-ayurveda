using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;

namespace Ayurveda.Web.Hubs;

public class CallHub : Hub
{
    private static readonly ConcurrentDictionary<string, HashSet<string>> _roomParticipants = new();
    private static readonly ConcurrentDictionary<string, (string RoomId, string Role, string UserId)> _connectionMetadata = new();
    private readonly IMemoryCache _cache;
    private readonly ILogger<CallHub> _logger;

    public CallHub(IMemoryCache cache, ILogger<CallHub> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task JoinRoom(string roomId, string role, string userId)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            await Clients.Caller.SendAsync("Error", "Invalid room ID.");
            return;
        }

        roomId = roomId.Trim();
        var connectionId = Context.ConnectionId;

        var participants = _roomParticipants.GetOrAdd(roomId, _ => new HashSet<string>());

        lock (participants)
        {
            if (participants.Count >= 2 && !participants.Contains(connectionId))
            {
                _logger.LogWarning("Room {RoomId} is full (2 participants already present). Rejecting connection {ConnId}", roomId, connectionId);
                Clients.Caller.SendAsync("RoomFull", roomId);
                return;
            }

            participants.Add(connectionId);
        }

        _connectionMetadata[connectionId] = (roomId, role ?? "guest", userId ?? "anon");
        await Groups.AddToGroupAsync(connectionId, roomId);

        bool peerAlreadyPresent = false;
        lock (participants)
        {
            peerAlreadyPresent = participants.Count > 1;
        }

        _logger.LogInformation("Connection {ConnId} ({Role}) joined room {RoomId}. Peer already present: {PeerPresent}", 
            connectionId, role, roomId, peerAlreadyPresent);

        // Tell caller they joined and whether a peer is waiting
        await Clients.Caller.SendAsync("JoinedRoom", roomId, peerAlreadyPresent);

        // Tell others in room that a peer joined
        await Clients.OthersInGroup(roomId).SendAsync("PeerJoined", connectionId, role, userId);
    }

    public async Task SendSignal(string roomId, string signalType, string payload)
    {
        if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(signalType))
            return;

        // Rate limiting per connection (max 100 signals/min)
        var rateLimitKey = $"call_rl_{Context.ConnectionId}";
        var count = _cache.GetOrCreate(rateLimitKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
            return 0;
        });

        if (count > 120)
        {
            _logger.LogWarning("Rate limit exceeded on SendSignal for connection {ConnId}", Context.ConnectionId);
            return;
        }
        _cache.Set(rateLimitKey, count + 1, TimeSpan.FromMinutes(1));

        await Clients.OthersInGroup(roomId).SendAsync("ReceiveSignal", Context.ConnectionId, signalType, payload);
    }

    public async Task LeaveRoom(string roomId)
    {
        var connectionId = Context.ConnectionId;
        if (_roomParticipants.TryGetValue(roomId, out var participants))
        {
            lock (participants)
            {
                participants.Remove(connectionId);
                if (participants.Count == 0)
                {
                    _roomParticipants.TryRemove(roomId, out _);
                }
            }
        }

        _connectionMetadata.TryRemove(connectionId, out _);
        await Groups.RemoveFromGroupAsync(connectionId, roomId);
        await Clients.OthersInGroup(roomId).SendAsync("PeerLeft", connectionId);

        _logger.LogInformation("Connection {ConnId} left room {RoomId}", connectionId, roomId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        if (_connectionMetadata.TryRemove(connectionId, out var meta))
        {
            if (_roomParticipants.TryGetValue(meta.RoomId, out var participants))
            {
                lock (participants)
                {
                    participants.Remove(connectionId);
                    if (participants.Count == 0)
                    {
                        _roomParticipants.TryRemove(meta.RoomId, out _);
                    }
                }
            }

            await Clients.OthersInGroup(meta.RoomId).SendAsync("PeerLeft", connectionId);
            _logger.LogInformation("Connection {ConnId} disconnected from room {RoomId}", connectionId, meta.RoomId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
