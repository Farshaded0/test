using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiScraperApp.Services;

namespace MauiScraperApp.ViewModels;

public partial class DownloadsViewModel : ObservableObject
{
    private readonly RemoteClientService _remoteClient;
    private Timer _refreshTimer;
    
    // The master list of all data
    private readonly List<RemoteTorrentInfo> _allDownloads = new();

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private string _statusMessage;

    // Filters
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _selectedFilterStatus = "All";

    public List<string> FilterOptions { get; } = new() 
    { 
        "All", "Downloading", "Seeding", "Completed", "Paused", "Error" 
    };

    // The collection the UI actually sees
    public ObservableCollection<RemoteTorrentInfo> FilteredDownloads { get; } = new();

    public DownloadsViewModel(RemoteClientService remoteClient)
    {
        _remoteClient = remoteClient;
        _refreshTimer = new Timer(async _ => await RefreshDownloadsAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
    }

    public async Task InitializeAsync() => await RefreshDownloadsAsync();

    // Triggered when SearchText or SelectedFilterStatus changes
    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnSelectedFilterStatusChanged(string value) => ApplyFilters();

    [RelayCommand]
    private async Task RefreshDownloadsAsync()
    {
        if (!_remoteClient.IsConnected)
        {
            MainThread.BeginInvokeOnMainThread(() => StatusMessage = "Not connected to PC");
            return;
        }

        try
        {
            // Get fresh data (Network call - Background OK)
            var freshData = await _remoteClient.GetTorrentsAsync();

            // UI Updates - MUST be Main Thread
            MainThread.BeginInvokeOnMainThread(() =>
            {
                MergeData(freshData);
                ApplyFilters();
                StatusMessage = $"{_allDownloads.Count} Total | {FilteredDownloads.Count} Visible";
            });
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() => StatusMessage = $"Error: {ex.Message}");
        }
    }

    private void MergeData(List<RemoteTorrentInfo> freshData)
    {
        // 1. Remove items that were deleted remotely
        var toRemove = _allDownloads.Where(old => !freshData.Any(newD => newD.Hash == old.Hash)).ToList();
        foreach (var item in toRemove) _allDownloads.Remove(item);

        // 2. Update existing items OR Add new ones
        foreach (var freshItem in freshData)
        {
            var existingItem = _allDownloads.FirstOrDefault(x => x.Hash == freshItem.Hash);
            
            if (existingItem != null)
            {
                // SMART UPDATE: This prevents the UI from redrawing the row
                existingItem.UpdateFrom(freshItem);
            }
            else
            {
                // New item found
                _allDownloads.Add(freshItem);
            }
        }
    }

    private void ApplyFilters()
    {
        var result = _allDownloads.AsEnumerable();

        // 1. Apply Text Search
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            result = result.Where(x => x.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        // 2. Apply Status Filter
        if (SelectedFilterStatus != "All")
        {
            result = SelectedFilterStatus switch
            {
                "Downloading" => result.Where(x => x.State.ToLower().Contains("dl") || x.State.ToLower().Contains("download")),
                "Seeding" => result.Where(x => x.State.ToLower().Contains("up") || x.State.ToLower().Contains("seed")),
                "Completed" => result.Where(x => x.Progress >= 1.0),
                "Paused" => result.Where(x => x.State.ToLower().Contains("pause")),
                "Error" => result.Where(x => x.State.ToLower().Contains("error")),
                _ => result
            };
        }

        // 3. Sync to ObservableCollection (Smart Sync again to preserve scroll)
        var filteredList = result.ToList();

        // Remove items no longer in filter
        for (int i = FilteredDownloads.Count - 1; i >= 0; i--)
        {
            var item = FilteredDownloads[i];
            if (!filteredList.Contains(item))
                FilteredDownloads.RemoveAt(i);
        }

        // Add items newly entering filter (maintain order roughly)
        foreach (var item in filteredList)
        {
            if (!FilteredDownloads.Contains(item))
                FilteredDownloads.Add(item);
        }
    }

    // ... Keep existing Pause/Resume/Delete commands ...
    [RelayCommand]
    private async Task PauseTorrentAsync(RemoteTorrentInfo torrent)
    {
        await _remoteClient.PauseTorrentAsync(torrent.Hash);
        await RefreshDownloadsAsync();
    }

    [RelayCommand]
    private async Task ResumeTorrentAsync(RemoteTorrentInfo torrent)
    {
        await _remoteClient.ResumeTorrentAsync(torrent.Hash);
        await RefreshDownloadsAsync();
    }

    [RelayCommand]
    private async Task DeleteTorrentAsync(RemoteTorrentInfo torrent)
    {
        var deleteFiles = await Shell.Current.DisplayAlert("Delete", "Delete files too?", "Yes", "No");
        await _remoteClient.DeleteTorrentAsync(torrent.Hash, deleteFiles);
        await RefreshDownloadsAsync();
    }

    public void StopRefreshing() => _refreshTimer?.Change(Timeout.Infinite, Timeout.Infinite);
    public void StartRefreshing() => _refreshTimer?.Change(TimeSpan.Zero, TimeSpan.FromSeconds(2));
}
