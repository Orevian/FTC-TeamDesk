using FTC.TeamDesk.Core.Common;
using FTC.TeamDesk.Core.Models;
using FTC.TeamDesk.Security;
using FTC.TeamDesk.Services;
using FTC.TeamDesk.Tests.Fakes;
using Xunit;

namespace FTC.TeamDesk.Tests;

public class VaultServiceTests
{
    private const string Master = "a-long-master-password";

    private sealed class FastCrypto : IVaultCrypto
    {
        private readonly VaultCrypto _inner = new();
        public int DefaultIterations => 100_000; // keep tests quick
        public byte[] GenerateSalt() => _inner.GenerateSalt();
        public byte[] DeriveKey(string p, byte[] s, int i) => _inner.DeriveKey(p, s, i);
        public byte[] Encrypt(byte[] k, byte[] p) => _inner.Encrypt(k, p);
        public byte[] Decrypt(byte[] k, byte[] b) => _inner.Decrypt(k, b);
    }

    private static (VaultService svc, FakeVaultRepository repo, RecordingLog log, FakeClock clock) Create()
    {
        var repo = new FakeVaultRepository();
        var log = new RecordingLog();
        var clock = new FakeClock();
        return (new VaultService(repo, new FastCrypto(), new FakeSettings(), log, clock), repo, log, clock);
    }

    [Fact]
    public async Task CreatedVaultUnlocksAndRestartStartsLocked()
    {
        var (svc, repo, _, clock) = Create();
        await svc.CreateAsync(Master);
        Assert.True(svc.IsUnlocked);

        // "Restart": a fresh service over the same storage must start locked.
        var restarted = new VaultService(repo, new FastCrypto(), new FakeSettings(), new RecordingLog(), clock);
        await restarted.InitializeAsync();
        Assert.True(restarted.IsInitialized);
        Assert.False(restarted.IsUnlocked);
        Assert.Equal(VaultUnlockOutcome.Success, (await restarted.UnlockAsync(Master)).Outcome);
        Assert.True(restarted.IsUnlocked);
    }

    [Fact]
    public async Task WrongPasswordDoesNotUnlock()
    {
        var (svc, _, _, _) = Create();
        await svc.CreateAsync(Master);
        svc.Lock();
        var result = await svc.UnlockAsync("definitely-wrong-password");
        Assert.Equal(VaultUnlockOutcome.WrongPassword, result.Outcome);
        Assert.False(svc.IsUnlocked);
    }

    [Fact]
    public async Task RepeatedFailuresLockOutEvenTheCorrectPassword()
    {
        var (svc, _, _, clock) = Create();
        await svc.CreateAsync(Master);
        svc.Lock();
        for (var i = 0; i < 3; i++) await svc.UnlockAsync("wrong-wrong-wrong");
        var blocked = await svc.UnlockAsync(Master);
        Assert.Equal(VaultUnlockOutcome.LockedOut, blocked.Outcome);
        Assert.True(blocked.RetryAfter > TimeSpan.Zero);

        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(VaultUnlockOutcome.Success, (await svc.UnlockAsync(Master)).Outcome);
    }

    [Fact]
    public async Task StoredDataContainsNoPlaintext()
    {
        var (svc, repo, _, _) = Create();
        await svc.CreateAsync(Master);
        await svc.SaveAsync(new VaultItemEdit { Title = "Instagram", Category = "Social", Username = "team_ftc", Password = "P@ssw0rd-XYZ-123" });
        var raw = System.Text.Encoding.UTF8.GetString(repo.Entries.Single().EncryptedPayload);
        Assert.DoesNotContain("P@ssw0rd-XYZ-123", raw);
        Assert.DoesNotContain("Instagram", raw);
        Assert.DoesNotContain("team_ftc", raw);
    }

    [Fact]
    public async Task ListNeverExposesPasswordsButRevealDoes()
    {
        var (svc, _, log, _) = Create();
        await svc.CreateAsync(Master);
        var id = await svc.SaveAsync(new VaultItemEdit { Title = "GitHub", Category = "Dev", Username = "ftc", Password = "hunter2-hunter2" });
        var item = Assert.Single(await svc.ListAsync());
        Assert.Equal("GitHub", item.Title);
        Assert.Equal("hunter2-hunter2", await svc.RevealPasswordAsync(id));
        Assert.DoesNotContain(log.Entries, e => e.Description.Contains("hunter2"));
        Assert.Contains(log.Entries, e => e.Type == ActivityActions.VaultPasswordRevealed);
    }

    [Fact]
    public async Task EditWithoutPasswordKeepsExistingPassword()
    {
        var (svc, _, _, _) = Create();
        await svc.CreateAsync(Master);
        var id = await svc.SaveAsync(new VaultItemEdit { Title = "Site", Category = "Web", Password = "original-password" });
        await svc.SaveAsync(new VaultItemEdit { Id = id, Title = "Site renamed", Category = "Web", Password = null });
        Assert.Equal("original-password", await svc.RevealPasswordAsync(id));
        Assert.Equal("Site renamed", (await svc.ListAsync()).Single().Title);
    }

    [Fact]
    public async Task LockedVaultRefusesEverything()
    {
        var (svc, _, _, _) = Create();
        await svc.CreateAsync(Master);
        var id = await svc.SaveAsync(new VaultItemEdit { Title = "A", Category = "B", Password = "pw-pw-pw-pw" });
        svc.Lock();
        await Assert.ThrowsAsync<DomainException>(() => svc.ListAsync());
        await Assert.ThrowsAsync<DomainException>(() => svc.RevealPasswordAsync(id));
        await Assert.ThrowsAsync<DomainException>(() => svc.SaveAsync(new VaultItemEdit { Title = "x", Category = "y", Password = "z" }));
    }

    [Fact]
    public async Task ChangingMasterPasswordKeepsEntriesReadable()
    {
        var (svc, repo, _, clock) = Create();
        await svc.CreateAsync(Master);
        var id = await svc.SaveAsync(new VaultItemEdit { Title = "A", Category = "B", Password = "entry-password" });
        await svc.ChangeMasterPasswordAsync(Master, "another-long-master-pw");
        svc.Lock();

        Assert.NotEqual(VaultUnlockOutcome.Success, (await svc.UnlockAsync(Master)).Outcome);
        clock.Advance(TimeSpan.FromHours(1));
        Assert.Equal(VaultUnlockOutcome.Success, (await svc.UnlockAsync("another-long-master-pw")).Outcome);
        Assert.Equal("entry-password", await svc.RevealPasswordAsync(id));
    }

    [Fact]
    public async Task RejectsShortMasterPassword()
    {
        var (svc, _, _, _) = Create();
        await Assert.ThrowsAsync<DomainException>(() => svc.CreateAsync("short"));
    }

    [Fact]
    public async Task ActivityLogNeverContainsMasterPassword()
    {
        var (svc, _, log, _) = Create();
        await svc.CreateAsync(Master);
        svc.Lock();
        await svc.UnlockAsync(Master);
        await svc.UnlockAsync("wrong-attempt-value");
        Assert.All(log.Entries, e => { Assert.DoesNotContain(Master, e.Description); Assert.DoesNotContain("wrong-attempt-value", e.Description); });
    }
}
