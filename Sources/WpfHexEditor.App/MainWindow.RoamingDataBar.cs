// ==========================================================
// Project: WpfHexEditor.App
// File: MainWindow.RoamingDataBar.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-05-20
// Description:
//     Partial class that manages the RoamingDataInfoBar — the VS/Office-style
//     warning banner shown at startup when a roaming data file fails validation.
//
// Architecture Notes:
//     Pattern mirrors MainWindow.FileChangeBar.cs.
//     [Backup & Reset] → CreateBackup + DeleteManagedFiles + restart prompt.
//     [Ignore] and [×] simply dismiss the bar without modifying any files.
// ==========================================================

using System.IO;
using System.Windows;
using WpfHexEditor.App.Properties;
using WpfHexEditor.App.Services;

namespace WpfHexEditor.App;

public partial class MainWindow
{
    // --- Show / Hide -------------------------------------------------------

    private void ShowRoamingDataInfoBar(IReadOnlyList<string> corruptFiles)
    {
        var names = string.Join(", ", corruptFiles.Select(Path.GetFileName));
        RoamingDataInfoBarMessage.Text = string.Format(
            AppResources.App_RoamingDataInfoBar_Message, names);
        RoamingDataInfoBar.Visibility = Visibility.Visible;
    }

    private void HideRoamingDataInfoBar()
        => RoamingDataInfoBar.Visibility = Visibility.Collapsed;

    // --- Button handlers ---------------------------------------------------

    private void OnRoamingDataInfoBarReset(object sender, RoutedEventArgs e)
    {
        HideRoamingDataInfoBar();

        RoamingDataBackupService.CreateBackup();
        var (_, errors) = RoamingDataBackupService.DeleteManagedFiles();

        if (errors.Count > 0)
            OutputLogger.Warn($"[RoamingDataBar] Some files could not be deleted: {string.Join("; ", errors)}");

        var result = _dialogService.Show(
            AppResources.App_ClearRoamingData_RestartPrompt,
            AppResources.App_ClearRoamingData_Title,
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Information);

        if (result == System.Windows.MessageBoxResult.Yes)
            System.Windows.Application.Current.Shutdown();
    }

    private void OnRoamingDataInfoBarIgnore(object sender, RoutedEventArgs e)
        => HideRoamingDataInfoBar();

    private void OnRoamingDataInfoBarDismiss(object sender, RoutedEventArgs e)
        => HideRoamingDataInfoBar();

    // --- Menu: Restore from Backup -----------------------------------------

    private void OnRestoreRoamingBackup(object sender, RoutedEventArgs e)
    {
        var latest = RoamingDataBackupService.GetLatestBackup();
        if (latest is null)
        {
            _dialogService.Show(AppResources.App_RestoreRoamingBackup_NoBackup,
                AppResources.App_ClearRoamingData_Title,
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return;
        }

        var confirm = _dialogService.Show(
            string.Format(AppResources.App_RestoreRoamingBackup_Confirm, Path.GetFileName(latest)),
            AppResources.App_ClearRoamingData_Title,
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        var (restored, errors) = RoamingDataBackupService.RestoreLatestBackup(latest);

        if (errors.Count > 0)
            OutputLogger.Error($"[RestoreBackup] Errors: {string.Join("; ", errors)}");

        OutputLogger.Info($"[RestoreBackup] Restored {restored} file(s) from '{latest}'.");

        var restart = _dialogService.Show(
            AppResources.App_RestoreRoamingBackup_Done,
            AppResources.App_ClearRoamingData_Title,
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Information);

        if (restart == System.Windows.MessageBoxResult.Yes)
            System.Windows.Application.Current.Shutdown();
    }
}
