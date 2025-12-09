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
                
                // FORCE NAV on Main Thread using CurrentItem (Object-based, not String-based)
                MainThread.BeginInvokeOnMainThread(() => 
                {
                    try 
                    {
                        var tabBar = Shell.Current.Items.FirstOrDefault(i => i.Route?.Contains("MainTabs") == true);
                        if (tabBar != null)
                        {
                            Shell.Current.CurrentItem = tabBar;
                        }
                        else
                        {
                             // Fallback if route finding fails - just take the second item (index 1) which is usually the TabBar
                             if (Shell.Current.Items.Count > 1) 
                                 Shell.Current.CurrentItem = Shell.Current.Items[1];
                        }
                    }
                    catch (Exception ex)
                    {
                         Shell.Current.DisplayAlert("Auto-Connect Nav Error", ex.Message, "OK");
                    }
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
        if (IsConnected)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    if (_debugMode) await Shell.Current.DisplayAlert("Info", "Attempting Object-Switch to MainTabs", "OK");
                    
                    var tabBar = Shell.Current.Items.FirstOrDefault(i => i.Route?.Contains("MainTabs") == true);
                    if (tabBar != null)
                    {
                         Shell.Current.CurrentItem = tabBar;
                    }
                    else
                    {
                         // Fallback by Index
                         if (Shell.Current.Items.Count > 1) 
                             Shell.Current.CurrentItem = Shell.Current.Items[1];
                         else
                             await Shell.Current.DisplayAlert("Error", "Could not find MainTabs in Shell Items", "OK");
                    }
                }
                catch (Exception ex)
                {
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
