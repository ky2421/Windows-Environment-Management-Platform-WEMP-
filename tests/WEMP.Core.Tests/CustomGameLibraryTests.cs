using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WEMP.GameMode.Detection;
using WEMP.Infrastructure.Data;

namespace WEMP.Core.Tests;

/// <summary>自定义游戏库测试：增删管理 + 与内置库合并检测。</summary>
public class CustomGameLibraryTests
{
    private static (WempDbContext Db, CustomGameLibraryService Library) CreateHarness()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<WempDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new WempDbContext(options);
        db.Database.EnsureCreated();

        return (db, new CustomGameLibraryService(new TestDbFactory(connection)));
    }

    [Fact]
    public async Task Add_then_detector_recognizes_custom_process()
    {
        var (db, library) = CreateHarness();
        var detector = new GameLibraryDetector(library);

        await library.AddAsync("我的游戏", "mygame");

        // 服务层：裸进程名、带 .exe、大小写变体均命中
        Assert.True(library.IsCustomGame("mygame"));
        Assert.True(library.IsCustomGame("mygame.exe"));
        Assert.True(library.IsCustomGame("MyGame"));

        // 检测器层：与内置库合并后识别
        Assert.True(detector.IsGameProcess("mygame"));
        Assert.False(detector.IsGameProcess("not_a_game_anywhere"));

        Assert.Single(await db.CustomGames.ToListAsync());
    }

    [Fact]
    public async Task Add_duplicate_process_rejected()
    {
        var (_, library) = CreateHarness();
        await library.AddAsync("游戏甲", "sameproc");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => library.AddAsync("游戏乙", "sameproc.exe"));
        Assert.Contains("已在自定义游戏库中", ex.Message);
    }

    [Fact]
    public async Task Add_empty_input_rejected()
    {
        var (_, library) = CreateHarness();

        await Assert.ThrowsAsync<InvalidOperationException>(() => library.AddAsync("", "game"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => library.AddAsync("游戏", "  "));
    }

    [Fact]
    public async Task Remove_deletes_and_stops_recognizing()
    {
        var (db, library) = CreateHarness();
        var game = await library.AddAsync("临时游戏", "tempgame");
        Assert.True(library.IsCustomGame("tempgame"));

        var removed = await library.RemoveAsync(game.Id);

        Assert.True(removed);
        Assert.False(library.IsCustomGame("tempgame"));
        Assert.Empty(await db.CustomGames.ToListAsync());
    }

    [Fact]
    public async Task Remove_missing_id_returns_false()
    {
        var (_, library) = CreateHarness();

        Assert.False(await library.RemoveAsync(999));
    }

    [Fact]
    public async Task Detector_merges_builtin_and_custom_library()
    {
        var (_, library) = CreateHarness();
        var detector = new GameLibraryDetector(library);
        await library.AddAsync("自定义求生", "left4dead3");

        // 内置进程名不受自定义库影响
        Assert.True(detector.IsGameProcess("csgo"));
        Assert.True(detector.IsGameProcess("valorant.exe"));
        // 自定义进程名被合并识别
        Assert.True(detector.IsGameProcess("left4dead3"));
        // 均未命中
        Assert.False(detector.IsGameProcess("totally_unknown_proc"));
    }

    [Fact]
    public async Task Detector_refreshes_via_library_changed_event()
    {
        var (_, library) = CreateHarness();
        var detector = new GameLibraryDetector(library);

        // 构造时快照：自定义库为空
        Assert.False(detector.IsGameProcess("mygame"));

        // 新增触发 LibraryChanged，检测器缓存自动刷新
        await library.AddAsync("我的游戏", "mygame");
        Assert.True(detector.IsGameProcess("mygame"));

        // 删除同样触发刷新
        var game = library.GetAll().Single(g => g.ProcessName == "mygame");
        await library.RemoveAsync(game.Id);
        Assert.False(detector.IsGameProcess("mygame"));
    }
}
