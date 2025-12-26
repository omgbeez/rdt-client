using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using RdtClient.Data.Enums;
using RdtClient.Data.Models.Data;
using RdtClient.Service.Services;
using RdtClient.Service.Services.DebridClients;
using TorBoxNET;

namespace RdtClient.Service.Test.Services.TorrentClients;

public class TorBoxDebridClientTest
{
    private readonly Mock<ILogger<TorBoxDebridClient>> _loggerMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<IDownloadableFileFilter> _fileFilterMock;

    public TorBoxDebridClientTest()
    {
        _loggerMock = new Mock<ILogger<TorBoxDebridClient>>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _fileFilterMock = new Mock<IDownloadableFileFilter>();
        
        var httpClient = new HttpClient();
        _httpClientFactoryMock.Setup(m => m.CreateClient(It.IsAny<String>())).Returns(httpClient);
        
        Settings.Get.Provider.ApiKey = "test-api-key";
        Settings.Get.Provider.Timeout = 100;
    }

    [Fact]
    public async Task GetDownloads_ReturnsTorrentsAndNzbsWithCorrectType()
    {
        // Arrange
        var torrents = new List<TorrentInfoResult>
        {
            new() { Hash = "hash1", Name = "torrent1", Size = 1000, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        };
        var nzbs = new List<UsenetInfoResult>
        {
            new() { Hash = "hash2", Name = "nzb1", Size = 2000, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        };

        var clientMock = new Mock<TorBoxDebridClient>(_loggerMock.Object, _httpClientFactoryMock.Object, _fileFilterMock.Object);
        clientMock.Protected().Setup<Task<IEnumerable<TorrentInfoResult>?>>("GetCurrentTorrents").ReturnsAsync(torrents);
        clientMock.Protected().Setup<Task<IEnumerable<TorrentInfoResult>?>>("GetQueuedTorrents").ReturnsAsync(new List<TorrentInfoResult>());
        clientMock.Protected().Setup<Task<IEnumerable<UsenetInfoResult>?>>("GetCurrentUsenet").ReturnsAsync(nzbs);
        clientMock.Protected().Setup<Task<IEnumerable<UsenetInfoResult>?>>("GetQueuedUsenet").ReturnsAsync(new List<UsenetInfoResult>());

        // Act
        var result = await clientMock.Object.GetDownloads();

        // Assert
        Assert.Equal(2, result.Count);
        
        var torrentResult = result.FirstOrDefault(r => r.Id == "hash1");
        Assert.NotNull(torrentResult);
        Assert.Equal(DownloadType.Torrent, torrentResult.Type);
        
        var nzbResult = result.FirstOrDefault(r => r.Id == "hash2");
        Assert.NotNull(nzbResult);
        Assert.Equal(DownloadType.Nzb, nzbResult.Type);
    }

    [Fact]
    public async Task GetAvailableFiles_ReturnsTorrentFiles_WhenTorrentFound()
    {
        // Arrange
        var hash = "test-hash";
        var availability = new Response<List<AvailableTorrent?>>
        {
            Data = new List<AvailableTorrent?>
            {
                new()
                {
                    Files = new List<AvailableTorrentFile>
                    {
                        new() { Name = "file1.mkv", Size = 100 },
                        new() { Name = "file2.txt", Size = 10 }
                    }
                }
            }
        };

        var clientMock = new Mock<TorBoxDebridClient>(_loggerMock.Object, _httpClientFactoryMock.Object, _fileFilterMock.Object);
        clientMock.Protected().Setup<Task<Response<List<AvailableTorrent?>>>>("GetTorrentAvailability", hash).ReturnsAsync(availability);

        // Act
        var result = await clientMock.Object.GetAvailableFiles(hash);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("file1.mkv", result[0].Filename);
        Assert.Equal(100, result[0].Filesize);
        Assert.Equal("file2.txt", result[1].Filename);
        Assert.Equal(10, result[1].Filesize);
    }

    [Fact]
    public async Task GetAvailableFiles_ReturnsUsenetFiles_WhenTorrentNotFoundButUsenetFound()
    {
        // Arrange
        var hash = "test-hash";
        var torrentAvailability = new Response<List<AvailableTorrent?>> { Data = new List<AvailableTorrent?>() };
        var usenetAvailability = new Response<List<AvailableUsenet?>>
        {
            Data = new List<AvailableUsenet?>
            {
                new()
                {
                    Files = new List<AvailableUsenetFile>
                    {
                        new() { Name = "file1.nzb", Size = 200 }
                    }
                }
            }
        };

        var clientMock = new Mock<TorBoxDebridClient>(_loggerMock.Object, _httpClientFactoryMock.Object, _fileFilterMock.Object);
        clientMock.Protected().Setup<Task<Response<List<AvailableTorrent?>>>>("GetTorrentAvailability", hash).ReturnsAsync(torrentAvailability);
        clientMock.Protected().Setup<Task<Response<List<AvailableUsenet?>>>>("GetUsenetAvailability", hash).ReturnsAsync(usenetAvailability);

        // Act
        var result = await clientMock.Object.GetAvailableFiles(hash);

        // Assert
        Assert.Single(result);
        Assert.Equal("file1.nzb", result[0].Filename);
        Assert.Equal(200, result[0].Filesize);
    }

    [Fact]
    public async Task GetAvailableFiles_ReturnsEmptyList_WhenNeitherFound()
    {
        // Arrange
        var hash = "test-hash";
        var torrentAvailability = new Response<List<AvailableTorrent?>> { Data = new List<AvailableTorrent?>() };
        var usenetAvailability = new Response<List<AvailableUsenet?>> { Data = new List<AvailableUsenet?>() };

        var clientMock = new Mock<TorBoxDebridClient>(_loggerMock.Object, _httpClientFactoryMock.Object, _fileFilterMock.Object);
        clientMock.Protected().Setup<Task<Response<List<AvailableTorrent?>>>>("GetTorrentAvailability", hash).ReturnsAsync(torrentAvailability);
        clientMock.Protected().Setup<Task<Response<List<AvailableUsenet?>>>>("GetUsenetAvailability", hash).ReturnsAsync(usenetAvailability);

        // Act
        var result = await clientMock.Object.GetAvailableFiles(hash);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task Delete_CallsTorrentsControl_WhenTypeIsTorrent()
    {
        // Arrange
        var torrent = new Torrent
        {
            RdId = "torrent-id",
            Type = DownloadType.Torrent
        };

        var torrentsApiMock = new Mock<ITorrentsApi>();
        var clientMock = new Mock<TorBoxDebridClient>(_loggerMock.Object, _httpClientFactoryMock.Object, _fileFilterMock.Object);
        var torBoxClientMock = new Mock<ITorBoxNetClient>();
        
        torBoxClientMock.Setup(m => m.Torrents).Returns(torrentsApiMock.Object);
        clientMock.Protected().Setup<ITorBoxNetClient>("GetClient").Returns(torBoxClientMock.Object);

        // Act
        await clientMock.Object.Delete(torrent);

        // Assert
        torrentsApiMock.Verify(m => m.ControlAsync("torrent-id", "delete", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_CallsUsenetControl_WhenTypeIsNzb()
    {
        // Arrange
        var torrent = new Torrent
        {
            RdId = "nzb-id",
            Type = DownloadType.Nzb
        };

        var usenetApiMock = new Mock<IUsenetApi>();
        var clientMock = new Mock<TorBoxDebridClient>(_loggerMock.Object, _httpClientFactoryMock.Object, _fileFilterMock.Object);
        var torBoxClientMock = new Mock<ITorBoxNetClient>();
        
        torBoxClientMock.Setup(m => m.Usenet).Returns(usenetApiMock.Object);
        clientMock.Protected().Setup<ITorBoxNetClient>("GetClient").Returns(torBoxClientMock.Object);

        // Act
        await clientMock.Object.Delete(torrent);

        // Assert
        usenetApiMock.Verify(m => m.ControlAsync("nzb-id", "delete", false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Unrestrict_CallsTorrentsRequestDownload_WhenTypeIsTorrent()
    {
        // Arrange
        var torrent = new Torrent
        {
            RdId = "torrent-id",
            Type = DownloadType.Torrent
        };
        var link = "https://torbox.app/d/123/456";

        var torrentsApiMock = new Mock<ITorrentsApi>();
        var clientMock = new Mock<TorBoxDebridClient>(_loggerMock.Object, _httpClientFactoryMock.Object, _fileFilterMock.Object);
        var torBoxClientMock = new Mock<ITorBoxNetClient>();
        
        torBoxClientMock.Setup(m => m.Torrents).Returns(torrentsApiMock.Object);
        clientMock.Protected().Setup<ITorBoxNetClient>("GetClient").Returns(torBoxClientMock.Object);
        
        torrentsApiMock.Setup(m => m.RequestDownloadAsync(123, 456, false, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new Response<string> { Data = "https://unrestricted-link" });

        // Act
        var result = await clientMock.Object.Unrestrict(torrent, link);

        // Assert
        Assert.Equal("https://unrestricted-link", result);
        torrentsApiMock.Verify(m => m.RequestDownloadAsync(123, 456, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Unrestrict_CallsUsenetRequestDownload_WhenTypeIsNzb()
    {
        // Arrange
        var torrent = new Torrent
        {
            RdId = "nzb-id",
            Type = DownloadType.Nzb
        };
        var link = "https://torbox.app/d/123/456";

        var usenetApiMock = new Mock<IUsenetApi>();
        var clientMock = new Mock<TorBoxDebridClient>(_loggerMock.Object, _httpClientFactoryMock.Object, _fileFilterMock.Object);
        var torBoxClientMock = new Mock<ITorBoxNetClient>();
        
        torBoxClientMock.Setup(m => m.Usenet).Returns(usenetApiMock.Object);
        clientMock.Protected().Setup<ITorBoxNetClient>("GetClient").Returns(torBoxClientMock.Object);
        
        usenetApiMock.Setup(m => m.RequestDownloadAsync(123, 456, false, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new Response<string> { Data = "https://unrestricted-link-nzb" });

        // Act
        var result = await clientMock.Object.Unrestrict(torrent, link);

        // Assert
        Assert.Equal("https://unrestricted-link-nzb", result);
        usenetApiMock.Verify(m => m.RequestDownloadAsync(123, 456, false, It.IsAny<CancellationToken>()), Times.Once);
    }
}
