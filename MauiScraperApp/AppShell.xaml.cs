namespace MauiScraperApp;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // FIX: Removed "MainTabs" registration. 
        // Since it is defined in XAML, registering it here breaks iOS navigation.
        
        // Register other specific pages if needed (e.g., specific detail pages), 
        // but NOT the primary tab bar.
    }
}
