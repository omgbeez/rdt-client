using Microsoft.Extensions.Logging;
using RdtClient.Data.Enums;
using RdtClient.Data.Models.Data;
using RdtClient.Data.Models.Internal;
using RdtClient.Data.Models.Sabnzbd;
using RdtClient.Service.Helpers;

namespace RdtClient.Service.Services;

public class Sabnzbd(ILogger<Sabnzbd> logger, Torrents torrents, AppSettings appSettings)
{
    public virtual async Task<SabnzbdQueue> GetQueue()
    {
        var allTorrents = await torrents.Get();
        var activeTorrents = allTorrents.Where(t => t.Type == DownloadType.Nzb && t.Completed == null).ToList();

        var queue = new SabnzbdQueue
        {
            NoOfSlots = activeTorrents.Count,
            Slots = activeTorrents.Select((t, index) => new SabnzbdQueueSlot
            {
                Index = index,
                NzoId = t.Hash,
                Filename = t.RdName ?? t.Hash,
                Size = FileSizeHelper.FormatSize(t.Downloads.Sum(d => d.BytesTotal)),
                SizeLeft = FileSizeHelper.FormatSize(t.Downloads.Sum(d => d.BytesTotal - d.BytesDone)),
                Percentage = (t.RdProgress ?? 0).ToString("0"),
                
                Status = t.RdStatus switch
                {
                    TorrentStatus.Processing => "Propagating",
                    TorrentStatus.Finished => "Completed",
                    TorrentStatus.Downloading => "Downloading",
                    TorrentStatus.WaitingForFileSelection => "Propagating",
                    TorrentStatus.Error => "Failed",
                    TorrentStatus.Queued => "Queued",
                    _ => ""
                },
                Category = t.Category ?? "*",
                Priority = "Normal",
                TimeLeft = "0:00:00"
            }).ToList()
        };

        return queue;
    }

    public virtual async Task<SabnzbdHistory> GetHistory()
    {
        var allTorrents = await torrents.Get();
        var completedTorrents = allTorrents.Where(t => t.Type == DownloadType.Nzb && t.Completed != null).ToList();

        var history = new SabnzbdHistory
        {
            NoOfSlots = completedTorrents.Count,
            TotalSlots = completedTorrents.Count,
            Slots = completedTorrents.Select(t => new SabnzbdHistorySlot
            {
                NzoId = t.Hash,
                Name = t.RdName ?? t.Hash,
                Size = FileSizeHelper.FormatSize(t.Downloads.Sum(d => d.BytesTotal)),
                Status = "Completed",
                Category = t.Category ?? "Default",
                Path = t.Category ?? "Default"
            }).ToList()
        };

        return history;
    }

    public virtual async Task<String> AddFile(Byte[] fileBytes, String? category, Int32? priority)
    {
        logger.LogDebug($"Add file {category}");

        var torrent = new Torrent
        {
            Category = category,
            DownloadClient = Settings.Get.DownloadClient.Client,
            HostDownloadAction = Settings.Get.Integrations.Default.HostDownloadAction,
            FinishedActionDelay = Settings.Get.Integrations.Default.FinishedActionDelay,
            DownloadAction = Settings.Get.Integrations.Default.OnlyDownloadAvailableFiles ? TorrentDownloadAction.DownloadAvailableFiles : TorrentDownloadAction.DownloadAll,
            FinishedAction = TorrentFinishedAction.None,
            DownloadMinSize = Settings.Get.Integrations.Default.MinFileSize,
            IncludeRegex = Settings.Get.Integrations.Default.IncludeRegex,
            ExcludeRegex = Settings.Get.Integrations.Default.ExcludeRegex,
            TorrentRetryAttempts = Settings.Get.Integrations.Default.TorrentRetryAttempts,
            DownloadRetryAttempts = Settings.Get.Integrations.Default.DownloadRetryAttempts,
            DeleteOnError = Settings.Get.Integrations.Default.DeleteOnError,
            Lifetime = Settings.Get.Integrations.Default.TorrentLifetime,
            Priority = priority ?? (Settings.Get.Integrations.Default.Priority > 0 ? Settings.Get.Integrations.Default.Priority : null)
        };

        var result = await torrents.AddNzbFileToDebridQueue(fileBytes, torrent);
        return result.Hash;
    }

    public virtual async Task<String> AddUrl(String url, String? category, Int32? priority)
    {
        logger.LogDebug($"Add url {category}");

        var torrent = new Torrent
        {
            Category = category,
            DownloadClient = Settings.Get.DownloadClient.Client,
            HostDownloadAction = Settings.Get.Integrations.Default.HostDownloadAction,
            FinishedActionDelay = Settings.Get.Integrations.Default.FinishedActionDelay,
            DownloadAction = Settings.Get.Integrations.Default.OnlyDownloadAvailableFiles ? TorrentDownloadAction.DownloadAvailableFiles : TorrentDownloadAction.DownloadAll,
            FinishedAction = TorrentFinishedAction.None,
            DownloadMinSize = Settings.Get.Integrations.Default.MinFileSize,
            IncludeRegex = Settings.Get.Integrations.Default.IncludeRegex,
            ExcludeRegex = Settings.Get.Integrations.Default.ExcludeRegex,
            TorrentRetryAttempts = Settings.Get.Integrations.Default.TorrentRetryAttempts,
            DownloadRetryAttempts = Settings.Get.Integrations.Default.DownloadRetryAttempts,
            DeleteOnError = Settings.Get.Integrations.Default.DeleteOnError,
            Lifetime = Settings.Get.Integrations.Default.TorrentLifetime,
            Priority = priority ?? (Settings.Get.Integrations.Default.Priority > 0 ? Settings.Get.Integrations.Default.Priority : null)
        };

        var result = await torrents.AddNzbLinkToDebridQueue(url, torrent);
        return result.Hash;
    }

    public virtual async Task Delete(String hash)
    {
        var torrent = await torrents.GetByHash(hash);

        if (torrent != null)
        {
            await torrents.Delete(torrent.TorrentId, true, true, true);
        }
    }

    public virtual List<String> GetCategories()
    {
        var categoryList = (Settings.Get.General.Categories ?? "")
                           .Split(",", StringSplitOptions.RemoveEmptyEntries)
                           .Select(m => m.Trim())
                           .Where(m => m != "*")
                           .Distinct(StringComparer.CurrentCultureIgnoreCase)
                           .ToList();

        categoryList.Insert(0, "*");

        return categoryList;
    }

    public virtual SabnzbdConfig GetConfig()
    {
        var savePath = Settings.AppDefaultSavePath;

        var categoryList = GetCategories();

        var categories = categoryList.Select((c, i) => new SabnzbdCategory
        {
            Name = c,
            Order = i,
            Dir = c == "*" ? "" : Path.Combine(savePath, c)
        }).ToList();

        var config = new SabnzbdConfig
        {
            Misc = new SabnzbdMisc
            {
                CompleteDir = savePath,
                DownloadDir = savePath,
                Port = appSettings.Port.ToString(),
                Version = "4.4.0"
            },
            Categories = categories
        };

        return config;
    }
}
