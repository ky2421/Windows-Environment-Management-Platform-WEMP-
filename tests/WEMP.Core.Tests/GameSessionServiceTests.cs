using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WEMP.GameMode.Detection;
using WEMP.GameMode.Services;
using WEMP.Infrastructure.Data;
using WEMP.Infrastructure.Data.Entities;

namespace WEMP.Core.Tests;

/// <summary>游戏会话服务测试：FakeDetector 与 FakeSwitcher 隔离系统副作用。</summary>
public class GameSessionServiceTests
{
    private sealed class FakeDetector(bool result) : IGameDetector
    {
        public bool IsGameProcess(string processName) => result;

        public bool IsGameProcessById(int processId) => result;
    }

    private sealed class FakeSwitcher : IGameStateSwitcher
    {
        public int EnterCount { get; private set; }

        public int RestoreCount { get; private set; }

        public GameStateSnapshot? LastSnapshot { get; private set; }

        public Task<GameStateSnapshot> EnterGameModeAsync(CancellationToken cancellationToken)
        {
            EnterCount++;
            return Task.FromResult(new GameStateSnapshot("scheme-x", []));
        }

        public Task RestoreAsync(GameStateSnapshot snapshot, CancellationToken cancellationToken)
        {
            RestoreCount++;
            LastSnapshot = snapshot;
            return Task.CompletedTask;
        }
    }

    private static (WempDbContext Db, GameSessionService Service, FakeSwitcher Switcher) CreateHarness(
        bool detectGame = true)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<WempDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new WempDbContext(options);
        db.Database.EnsureCreated();

        var switcher = new FakeSwitcher();
        var service = new GameSessionService(db, switcher, new FakeDetector(detectGame));
        return (db, service, switcher);
    }

    [Fact]
    public async Task StartSession_creates_record_and_switches_state()
    {
        var (db, service, switcher) = CreateHarness();
        var pid = Environment.ProcessId;

        var session = await service.StartSessionAsync(pid);

        Assert.NotNull(session);
        Assert.Equal(pid, session!.ProcessId);
        Assert.Equal(1, switcher.EnterCount);
        Assert.NotNull(service.CurrentSession);

        var record = await db.GameSessions.SingleAsync();
        Assert.Equal(pid, record.ProcessId);
        Assert.Null(record.EndedAt);

        var audit = await db.AuditLogs.SingleAsync();
        Assert.Equal("game.session.start", audit.Action);
    }

    [Fact]
    public async Task StartSession_ignores_non_game_process()
    {
        var (db, service, switcher) = CreateHarness(detectGame: false);

        var session = await service.StartSessionAsync(Environment.ProcessId);

        Assert.Null(session);
        Assert.Null(service.CurrentSession);
        Assert.Equal(0, switcher.EnterCount);
        Assert.Equal(0, await db.GameSessions.CountAsync());
    }

    [Fact]
    public async Task StartSession_ignores_duplicate_session()
    {
        var (db, service, switcher) = CreateHarness();
        var pid = Environment.ProcessId;

        var first = await service.StartSessionAsync(pid);
        var second = await service.StartSessionAsync(pid);

        Assert.NotNull(first);
        Assert.Null(second);
        Assert.Single(await db.GameSessions.ToListAsync());
    }

    [Fact]
    public async Task EndSession_fills_duration_and_restores_state()
    {
        var (db, service, switcher) = CreateHarness();
        await service.StartSessionAsync(Environment.ProcessId);

        await Task.Delay(1200);
        var ended = await service.EndCurrentSessionAsync();

        Assert.NotNull(ended);
        Assert.NotNull(ended!.EndedAt);
        Assert.True(ended.DurationSeconds >= 1);
        Assert.Null(service.CurrentSession);
        Assert.Equal(1, switcher.RestoreCount);
        Assert.Equal("scheme-x", switcher.LastSnapshot!.OriginalScheme);

        var record = await db.GameSessions.SingleAsync();
        Assert.NotNull(record.EndedAt);
        Assert.True(record.DurationSeconds >= 1);

        var audit = await db.AuditLogs.FirstAsync(a => a.Action == "game.session.end");
        Assert.Equal("success", audit.Result);
    }

    [Fact]
    public async Task AutoMonitor_setting_persists()
    {
        var (db, service, _) = CreateHarness();

        Assert.False(service.IsAutoMonitorEnabled);

        service.IsAutoMonitorEnabled = true;
        Assert.True(service.IsAutoMonitorEnabled);

        var setting = await db.AppSettings.FirstAsync(s => s.Key == "gamemode.autoMonitor");
        Assert.Equal("true", setting.Value);
        Assert.Equal("GameMode", setting.Module);
    }

    [Fact]
    public async Task GetHistory_returns_latest_first()
    {
        var (db, service, _) = CreateHarness();
        var pid = Environment.ProcessId;

        await service.StartSessionAsync(pid);
        await service.EndCurrentSessionAsync();
        await service.StartSessionAsync(pid);
        await service.EndCurrentSessionAsync();

        var history = await service.GetHistoryAsync(10);

        Assert.Equal(2, history.Count);
        Assert.True(history[0].StartedAt >= history[1].StartedAt);
    }

    [Fact]
    public async Task Dispose_ends_active_session()
    {
        var (db, service, _) = CreateHarness();
        await service.StartSessionAsync(Environment.ProcessId);

        await service.DisposeAsync();

        var record = await db.GameSessions.SingleAsync();
        Assert.NotNull(record.EndedAt);
    }
}
