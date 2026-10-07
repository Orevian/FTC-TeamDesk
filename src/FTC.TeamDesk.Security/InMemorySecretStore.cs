using System.Collections.Concurrent;
using FTC.TeamDesk.Core.Abstractions.Services;

namespace FTC.TeamDesk.Security;

/// <summary>Non-persistent secret store used by tests and non-Windows tooling. Never used in the shipped app.</summary>
public sealed class InMemorySecretStore : ISecretStore
{
    private readonly ConcurrentDictionary<string, string> _items = new();
    public Task SetAsync(string name, string secret, CancellationToken ct = default) { _items[name] = secret; return Task.CompletedTask; }
    public Task<string?> GetAsync(string name, CancellationToken ct = default) => Task.FromResult(_items.TryGetValue(name, out var v) ? v : null);
    public Task DeleteAsync(string name, CancellationToken ct = default) { _items.TryRemove(name, out _); return Task.CompletedTask; }
    public Task<bool> ExistsAsync(string name, CancellationToken ct = default) => Task.FromResult(_items.ContainsKey(name));
}
