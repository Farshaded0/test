using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiScraperApp.Services;

namespace MauiScraperApp.ViewModels;

public partial class ConnectionViewModel : ObservableObject
{
    private readonly RemoteClientService _remoteClient;

    [ObservableProperty] private string _serverIp = "";
    [ObservableProperty] private string _serverPort = "5000";
    [ObservableProperty] private bool _isConnecting;
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _statusMessage = "";

    // DEBUG MODE: Set to TRUE to see alerts on the iPhone
    private bool _debugMode = true;

    public ObservableCollection<string> DiscoveredServers { get; } = new();

    public ConnectionViewModel(RemoteClientService remoteClient)
    {
        _remoteClient = remoteClient;
        var (savedIp, savedPort) = _remoteClient.GetSavedConnectionInfo();
        
        if (!string.IsNullOrEmpty(savedIp))
        {
            ServerIp = savedIp;
            ServerPort = savedPort.ToString();
            StatusMessage = "Last connected: " + savedIp;
        }
        else
        {
            StatusMessage = "Enter PC IP address";
        }
        IsConnected = _remoteClient.IsConnected;
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (string.IsNullOrWhiteSpace(ServerIp) || !int.TryParse(ServerPort, out int port))
        {
            await Shell.Current.DisplayAlert("Error", "Invalid IP or Port", "OK");
            return;
        }

        try
        {
            IsConnecting = true;
            StatusMessage = "Connecting...";

            bool success = await _remoteClient.ConnectAsync(ServerIp, port);

            if (success)
            {
                IsConnected = true;
                StatusMessage = $"Connected to {ServerIp}:{port}";
                
                if (_debugMode) 
                    await Shell.Current.DisplayAlert("Debug", "Connection OK. Navigating...", "OK");
                
                // Auto-Navigate - Force MainThread for safety
                MainThread.BeginInvokeOnMainThread(async () => 
                {
                    await Shell.Current.GoToAsync("//MainTabs");
                });
            }
            else
            {
                IsConnected = false;
                StatusMessage = "Connection failed";
                await Shell.Current.DisplayAlert("Failed", "Could not connect to Bridge.\nCheck IP/Port and Firewall.", "OK");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            IsConnecting = false;
        }
    }

    [RelayCommand]
    private async Task ContinueToApp()
    {
        // DEBUG: Proves the button is being clicked
        if (_debugMode) 
            await Shell.Current.DisplayAlert("Debug", $"Button Clicked.\nIsConnected: {IsConnected}", "OK");

        if (IsConnected)
        {
            // Force MainThread to prevent any background thread navigation crashes
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    // Absolute routing to the TabBar
                    await Shell.Current.GoToAsync("//MainTabs");
                }
                catch (Exception ex)
                {
                    // Catch routing errors (e.g., if MainTabs isn't found)
                    await Shell.Current.DisplayAlert("Nav Error", ex.Message, "OK");
                }
            });
        }
        else
        {
            await Shell.Current.DisplayAlert("Disconnected", "Please connect to the PC first.", "OK");
        }
    }

    [RelayCommand]
    private async Task ScanNetworkAsync()
    {
        try
        {
            IsScanning = true;
            StatusMessage = "Scanning network...";
            DiscoveredServers.Clear();

            // Explicit List<string> to avoid ambiguity
            List<string> servers = await _remoteClient.DiscoverServersAsync();

            foreach (var server in servers)
            {
                DiscoveredServers.Add(server);
            }

            if (servers.Count > 0)
                StatusMessage = $"Found {servers.Count} server(s)";
            else
                StatusMessage = "No servers found";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private void SelectServer(string ip)
    {
        ServerIp = ip;
        StatusMessage = $"Selected {ip}";
    }

    [RelayCommand]
    private void Disconnect()
    {
        _remoteClient.Disconnect();
        IsConnected = false;
        StatusMessage = "Disconnected";
    }
}
