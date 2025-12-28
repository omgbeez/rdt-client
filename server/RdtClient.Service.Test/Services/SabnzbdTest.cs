using Microsoft.Extensions.Logging;
using Moq;
using RdtClient.Data.Enums;
using RdtClient.Data.Models.Data;
using RdtClient.Data.Models.Internal;
using RdtClient.Service.Services;

namespace RdtClient.Service.Test.Services;

public class SabnzbdTest
{
    private readonly Mock<ILogger<Sabnzbd>> _loggerMock = new();
    private readonly Mock<Torrents> _torrentsMock;
    private readonly AppSettings _appSettings = new() { Port = 6500 };

    public SabnzbdTest()
    {
        _torrentsMock = new Mock<Torrents>(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);
        _torrentsMock.Setup(t => t.Get()).ReturnsAsync(new List<Torrent>());
    }

    [Fact]
    public async Task GetQueue_ShouldReturnCorrectStructure()
    {
        // Arrange
        var torrentList = new List<Torrent>
        {
            new()
            {
                Hash = "hash1",
                RdName = "Name 1",
                RdProgress = 50,
                Type = DownloadType.Nzb,
                Downloads = new List<Download>
                {
                    new()
                        { BytesTotal = 1000, BytesDone = 500 }
                }
            }
        };

        _torrentsMock.Setup(t => t.Get()).ReturnsAsync(torrentList);

        var sabnzbd = new Sabnzbd(_loggerMock.Object, _torrentsMock.Object, _appSettings);

        // Act
        var result = await sabnzbd.GetQueue();

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Slots);
        Assert.Equal("hash1", result.Slots[0].NzoId);
        Assert.Equal("Name 1", result.Slots[0].Filename);
        Assert.Equal("50", result.Slots[0].Percentage);
    }

    [Fact]
    public async Task GetQueue_ShouldOnlyReturnNzbs()
    {
        // Arrange
        var torrentList = new List<Torrent>
        {
            new()
            {
                Hash = "hash1",
                RdName = "NZB 1",
                Type = DownloadType.Nzb,
                Downloads = new List<Download>()
            },
            new()
            {
                Hash = "hash2",
                RdName = "Torrent 1",
                Type = DownloadType.Torrent,
                Downloads = new List<Download>()
            }
        };

        _torrentsMock.Setup(t => t.Get()).ReturnsAsync(torrentList);

        var sabnzbd = new Sabnzbd(_loggerMock.Object, _torrentsMock.Object, _appSettings);

        // Act
        var result = await sabnzbd.GetQueue();

        // Assert
        Assert.Single(result.Slots);
        Assert.Equal("hash1", result.Slots[0].NzoId);
    }

    [Fact]
    public async Task GetHistory_ShouldOnlyReturnNzbs()
    {
        // Arrange
        var torrentList = new List<Torrent>
        {
            new()
            {
                Hash = "hash1",
                RdName = "NZB 1",
                Type = DownloadType.Nzb,
                Completed = DateTimeOffset.UtcNow,
                Downloads = new List<Download>()
            },
            new()
            {
                Hash = "hash2",
                RdName = "Torrent 1",
                Type = DownloadType.Torrent,
                Completed = DateTimeOffset.UtcNow,
                Downloads = new List<Download>()
            }
        };

        _torrentsMock.Setup(t => t.Get()).ReturnsAsync(torrentList);

        var sabnzbd = new Sabnzbd(_loggerMock.Object, _torrentsMock.Object, _appSettings);

        // Act
        var result = await sabnzbd.GetHistory();

        // Assert
        Assert.Single(result.Slots);
        Assert.Equal("hash1", result.Slots[0].NzoId);
    }

    [Fact]
    public void GetConfig_ShouldReturnCorrectConfig()
    {
        // Arrange
        var sabnzbd = new Sabnzbd(_loggerMock.Object, _torrentsMock.Object, _appSettings);

        // Act
        var result = sabnzbd.GetConfig();

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Misc);
        Assert.Equal("6500", result.Misc.Port);
        Assert.NotEmpty(result.Categories);
        Assert.Contains(result.Categories, c => c.Name == "*");
    }

    [Fact]
    public void GetCategories_ShouldOnlyReturnSettingsCategories()
    {
        // Arrange
        var torrentList = new List<Torrent>
        {
            new()
            {
                Hash = "hash1",
                Category = "Movie",
                Type = DownloadType.Nzb,
                Downloads = new List<Download>()
            }
        };

        _torrentsMock.Setup(t => t.Get()).ReturnsAsync(torrentList);

        Data.Data.SettingData.Get.General.Categories = "TV, Music, *";

        var sabnzbd = new Sabnzbd(_loggerMock.Object, _torrentsMock.Object, _appSettings);

        // Act
        var result = sabnzbd.GetCategories();

        // Assert
        Assert.Equal("*", result[0]);
        Assert.Contains("TV", result);
        Assert.Contains("Music", result);
        Assert.DoesNotContain("Movie", result);
        Assert.Equal(3, result.Count);
    }
}
