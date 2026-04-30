// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: ConnectionManagerPopup.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-04-02
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     VS-style connection manager Window for configuring API keys per provider.
//     Code-behind Window (no XAML), same pattern as AICommandPalette.
//     Auto-saves keys with DPAPI encryption, live connection testing.
// ==========================================================
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using WpfHexEditor.Plugins.AIAssistant.Api;
using WpfHexEditor.Plugins.AIAssistant.Options;
using WpfHexEditor.Plugins.AIAssistant.Properties;
using WpfHexEditor.Plugins.AIAssistant.Providers.ClaudeCode;
using WpfTextBlock = System.Windows.Controls.TextBlock;

namespace WpfHexEditor.Plugins.AIAssistant.Panel.ConnectionManager;

public sealed class ConnectionManagerPopup : Window
{
    private sealed record ProviderCardInfo(
        string ProviderId, string DisplayName, string IconGlyph,
        bool NeedsApiKey, bool NeedsEndpoint, bool NeedsDeployment, bool IsLocalUrl);

    private static readonly ProviderCardInfo[] s_providers =
    [
        new("claude-code",  "Claude Code (CLI)", "\uE756", false, false, false, false),
        new("anthropic",    "Anthropic Claude",  "\uE774", true,  false, false, false),
        new("openai",       "OpenAI",            "\uE945", true,  false, false, false),
        new("gemini",       "Google Gemini",     "\uE771", true,  false, false, false),
        new("ollama",       "Ollama (Local)",    "\uE839", false, false, false, true),
        new("azure-openai", "Azure OpenAI",      "\uE753", true,  true,  true,  false)
    ];

    private readonly ModelRegistry _registry;
    private readonly Dictionary<string, DispatcherTimer> _saveTimers = new();
    private readonly Dictionary<string, CancellationTokenSource> _testCts = new();
    private readonly Dictionary<string, Ellipse> _statusDots = new();
    private readonly Dictionary<string, WpfTextBlock> _resultTexts = new();
    private readonly Dictionary<string, WpfTextBlock> _errorTexts = new();
    private readonly Dictionary<string, WpfTextBlock> _spinners = new();
    private readonly WpfTextBlock _summaryText;
    private bool _closing;

    public ConnectionManagerPopup(
        ModelRegistry registry,
        Window? owner = null,
        Point? anchor = null)
    {
        _registry = registry;

        // ── Window chrome (same as AICommandPalette) ────────────────────
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Width = 400;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 600;

        if (owner is not null)
        {
            Owner = owner;
            WindowStartupLocation = WindowStartupLocation.Manual;

            if (anchor.HasValue)
            {
                Left = anchor.Value.X - Width / 2;
                Top = anchor.Value.Y;
            }
            else
            {
                Left = owner.Left + (owner.Width - Width) / 2;
                Top = owner.Top + owner.Height * 0.18;
            }
        }

        Deactivated += (_, _) => SafeClose();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { SafeClose(); e.Handled = true; }
        };

        // ── Root border ─────────────────────────────────────────────────────
        var rootBorder = new Border
        {
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1.5),
            Padding = new Thickness(12),
            Effect = new DropShadowEffect
            {
                Direction = 315, ShadowDepth = 6, BlurRadius = 18, Opacity = 0.55, Color = Colors.Black
            }
        };
        rootBorder.SetResourceReference(Border.BackgroundProperty, "DockBackgroundBrush");
        rootBorder.SetResourceReference(Border.BorderBrushProperty, "CA_AccentBrandingBrush");

        var outerStack = new StackPanel();

        // ── Header ──────────────────────────────────────────────────────────
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = new WpfTextBlock
        {
            Text = "Manage Connections",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center
        };
        title.SetResourceReference(WpfTextBlock.ForegroundProperty, "DockMenuForegroundBrush");
        Grid.SetColumn(title, 0);
        headerGrid.Children.Add(title);

        var closeBtn = new Border
        {
            Width = 24, Height = 24,
            CornerRadius = new CornerRadius(3),
            Cursor = Cursors.Hand,
            Child = new WpfTextBlock
            {
                Text = "\uE8BB",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        ((WpfTextBlock)closeBtn.Child).SetResourceReference(WpfTextBlock.ForegroundProperty, "DockMenuForegroundBrush");
        closeBtn.MouseEnter += (s, _) => ((Border)s!).SetResourceReference(Border.BackgroundProperty, "DockTabHoverBrush");
        closeBtn.MouseLeave += (s, _) => ((Border)s!).Background = Brushes.Transparent;
        closeBtn.MouseLeftButtonDown += (_, _) => SafeClose();
        Grid.SetColumn(closeBtn, 1);
        headerGrid.Children.Add(closeBtn);
        outerStack.Children.Add(headerGrid);

        // ── Summary ─────────────────────────────────────────────────────────
        _summaryText = new WpfTextBlock
        {
            FontSize = 11,
            Opacity = 0.5,
            Margin = new Thickness(0, 2, 0, 8)
        };
        _summaryText.SetResourceReference(WpfTextBlock.ForegroundProperty, "DockMenuForegroundBrush");
        outerStack.Children.Add(_summaryText);

        // ── Separator ───────────────────────────────────────────────────────
        var sep = new Border { Height = 1, Margin = new Thickness(0, 0, 0, 8) };
        sep.SetResourceReference(Border.BackgroundProperty, "DockBorderBrush");
        outerStack.Children.Add(sep);

        // ── Provider cards ──────────────────────────────────────────────────
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 440
        };

        var cardsStack = new StackPanel();
        PasswordBox? firstEmptyBox = null;
        TextBox? firstEmptyTextBox = null;

        foreach (var info in s_providers)
        {
            var card = BuildProviderCard(info, ref firstEmptyBox, ref firstEmptyTextBox);
            cardsStack.Children.Add(card);
        }

        scroll.Content = cardsStack;
        outerStack.Children.Add(scroll);

        // ── Footer ──────────────────────────────────────────────────────────
        var footer = new WpfTextBlock
        {
            Text = "\U0001F512 Keys encrypted with Windows DPAPI",
            FontSize = 9,
            Opacity = 0.35,
            Margin = new Thickness(0, 8, 0, 0)
        };
        footer.SetResourceReference(WpfTextBlock.ForegroundProperty, "DockMenuForegroundBrush");
        outerStack.Children.Add(footer);

        rootBorder.Child = outerStack;
        Content = rootBorder;

        UpdateSummary();

        // Focus first empty input on load
        var focusTarget = (UIElement?)firstEmptyBox ?? firstEmptyTextBox;
        if (focusTarget != null)
            Loaded += (_, _) => focusTarget.Dispatcher.InvokeAsync(() => focusTarget.Focus(), DispatcherPriority.Input);
    }

    private void SafeClose()
    {
        if (_closing) return;
        _closing = true;
        Close();
    }

    private Border BuildProviderCard(ProviderCardInfo info, ref PasswordBox? firstEmptyPwBox, ref TextBox? firstEmptyTxtBox)
    {
        var opts = AIAssistantOptions.Instance;
        var cardBorder = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 6)
        };
        cardBorder.SetResourceReference(Border.BackgroundProperty, "CA_ToolCallBackgroundBrush");

        var cardStack = new StackPanel();

        // ── Header row: icon + name + status dot ────────────────────────────
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = new WpfTextBlock
        {
            Text = info.IconGlyph,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 14,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        icon.SetResourceReference(WpfTextBlock.ForegroundProperty, "CA_AccentBrandingBrush");
        Grid.SetColumn(icon, 0);
        headerGrid.Children.Add(icon);

        var name = new WpfTextBlock
        {
            Text = info.DisplayName,
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        name.SetResourceReference(WpfTextBlock.ForegroundProperty, "DockMenuForegroundBrush");
        Grid.SetColumn(name, 1);
        headerGrid.Children.Add(name);

        var statusDot = new Ellipse
        {
            Width = 8, Height = 8,
            VerticalAlignment = VerticalAlignment.Center
        };
        var hasKey = info.NeedsApiKey && !string.IsNullOrEmpty(opts.GetApiKey(info.ProviderId));
        var hasUrl = info.IsLocalUrl && !string.IsNullOrEmpty(opts.OllamaBaseUrl);
        var hasCli = info.ProviderId == "claude-code" && ClaudeCodeModelProvider.FindClaudeExecutable() is not null;
        var isConfigured = hasKey || hasUrl || hasCli;
        statusDot.Fill = isConfigured
            ? new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xB0))
            : (Brush)Application.Current.TryFindResource("DockBorderBrush") ?? Brushes.Gray;
        statusDot.ToolTip = isConfigured ? "Configured" : "Not configured";
        _statusDots[info.ProviderId] = statusDot;
        Grid.SetColumn(statusDot, 2);
        headerGrid.Children.Add(statusDot);

        cardStack.Children.Add(headerGrid);

        // ── Input field(s) ──────────────────────────────────────────────────
        if (info.ProviderId == "claude-code")
        {
            var cliPath = ClaudeCodeModelProvider.FindClaudeExecutable();
            var infoText = new WpfTextBlock
            {
                Text = cliPath is not null
                    ? $"Uses your claude.ai subscription via CLI\n{cliPath}"
                    : "Claude Code CLI not found.\nInstall: npm install -g @anthropic-ai/claude-code",
                FontSize = 10.5,
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.7,
                Margin = new Thickness(0, 6, 0, 0)
            };
            infoText.SetResourceReference(WpfTextBlock.ForegroundProperty, "DockMenuForegroundBrush");
            cardStack.Children.Add(infoText);
        }

        if (info.NeedsApiKey)
        {
            var pwBox = CreatePasswordBox("Enter API key...", info.ProviderId);
            var existingKey = opts.GetApiKey(info.ProviderId);
            if (!string.IsNullOrEmpty(existingKey))
                pwBox.Password = "\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022";
            cardStack.Children.Add(pwBox);

            if (string.IsNullOrEmpty(existingKey) && firstEmptyPwBox is null)
                firstEmptyPwBox = pwBox;
        }

        if (info.IsLocalUrl)
        {
            var urlBox = CreateTextBox(opts.OllamaBaseUrl, "http://localhost:11434", info.ProviderId, "url");
            cardStack.Children.Add(urlBox);

            if (string.IsNullOrEmpty(opts.OllamaBaseUrl) && firstEmptyTxtBox is null)
                firstEmptyTxtBox = urlBox;
        }

        if (info.NeedsEndpoint)
        {
            var epBox = CreateTextBox(opts.AzureOpenAIEndpoint, "Endpoint URL", info.ProviderId, "endpoint");
            cardStack.Children.Add(epBox);
        }

        if (info.NeedsDeployment)
        {
            var depBox = CreateTextBox(opts.AzureOpenAIDeployment, "Deployment name", info.ProviderId, "deployment");
            cardStack.Children.Add(depBox);
        }

        // ── Action row: test button + result ────────────────────────────────
        var actionGrid = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var testBtn = new Border
        {
            Padding = new Thickness(10, 4, 10, 4),
            CornerRadius = new CornerRadius(3),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Child = new WpfTextBlock
            {
                Text = "Test Connection",
                FontSize = 10.5,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        testBtn.SetResourceReference(Border.BorderBrushProperty, "CA_AccentBrandingBrush");
        ((WpfTextBlock)testBtn.Child).SetResourceReference(WpfTextBlock.ForegroundProperty, "DockMenuForegroundBrush");
        testBtn.MouseEnter += (s, _) => ((Border)s!).SetResourceReference(Border.BackgroundProperty, "DockTabHoverBrush");
        testBtn.MouseLeave += (s, _) => ((Border)s!).Background = Brushes.Transparent;
        testBtn.MouseLeftButtonDown += (_, _) => SafeGuard.RunAsync(() => TestConnectionAsync(info.ProviderId));
        Grid.SetColumn(testBtn, 0);
        actionGrid.Children.Add(testBtn);

        // Spinner
        var spinner = new WpfTextBlock
        {
            Text = "\uE895",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 12,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform()
        };
        spinner.SetResourceReference(WpfTextBlock.ForegroundProperty, "CA_AccentBrandingBrush");
        _spinners[info.ProviderId] = spinner;
        Grid.SetColumn(spinner, 1);
        actionGrid.Children.Add(spinner);

        // Result text
        var resultText = new WpfTextBlock
        {
            FontSize = 10.5,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        _resultTexts[info.ProviderId] = resultText;
        Grid.SetColumn(resultText, 2);
        actionGrid.Children.Add(resultText);

        cardStack.Children.Add(actionGrid);

        // ── Error detail ────────────────────────────────────────────────────
        var errorText = new WpfTextBlock
        {
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0),
            Visibility = Visibility.Collapsed
        };
        errorText.SetResourceReference(WpfTextBlock.ForegroundProperty, "CA_ErrorBrush");
        _errorTexts[info.ProviderId] = errorText;
        cardStack.Children.Add(errorText);

        cardBorder.Child = cardStack;
        return cardBorder;
    }

    // ── Input helpers ───────────────────────────────────────────────────────

    private PasswordBox CreatePasswordBox(string placeholder, string providerId)
    {
        var box = new PasswordBox
        {
            FontSize = 11.5,
            Padding = new Thickness(6, 4, 6, 4),
            Margin = new Thickness(0, 6, 0, 0),
            MaxLength = 256,
            Tag = providerId
        };
        box.SetResourceReference(PasswordBox.BackgroundProperty, "CA_InputBackgroundBrush");
        box.SetResourceReference(PasswordBox.ForegroundProperty, "CA_InputForegroundBrush");
        box.SetResourceReference(PasswordBox.BorderBrushProperty, "CA_InputBorderBrush");
        box.ToolTip = placeholder;

        box.PasswordChanged += (s, _) => SafeGuard.Run(() =>
        {
            var pw = ((PasswordBox)s!).Password;
            if (pw == "\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022") return;
            DebounceSave(providerId, () =>
            {
                if (!string.IsNullOrWhiteSpace(pw))
                    AIAssistantOptions.Instance.SetApiKey(providerId, pw);
                AIAssistantOptions.Instance.Save();
                UpdateStatusDot(providerId);
                UpdateSummary();
            });
        });

        return box;
    }

    private TextBox CreateTextBox(string currentValue, string placeholder, string providerId, string fieldTag)
    {
        var box = new TextBox
        {
            Text = currentValue,
            FontSize = 11.5,
            Padding = new Thickness(6, 4, 6, 4),
            Margin = new Thickness(0, 6, 0, 0),
            Tag = $"{providerId}:{fieldTag}"
        };
        box.SetResourceReference(TextBox.BackgroundProperty, "CA_InputBackgroundBrush");
        box.SetResourceReference(TextBox.ForegroundProperty, "CA_InputForegroundBrush");
        box.SetResourceReference(TextBox.BorderBrushProperty, "CA_InputBorderBrush");

        if (string.IsNullOrEmpty(currentValue))
        {
            box.Text = placeholder;
            box.Opacity = 0.5;
            box.GotFocus += (s, _) =>
            {
                var tb = (TextBox)s!;
                if (tb.Text == placeholder) { tb.Text = ""; tb.Opacity = 1.0; }
            };
            box.LostFocus += (s, _) =>
            {
                var tb = (TextBox)s!;
                if (string.IsNullOrEmpty(tb.Text)) { tb.Text = placeholder; tb.Opacity = 0.5; }
            };
        }

        box.TextChanged += (s, _) => SafeGuard.Run(() =>
        {
            var text = ((TextBox)s!).Text;
            if (text == placeholder) return;
            var opts = AIAssistantOptions.Instance;
            DebounceSave($"{providerId}:{fieldTag}", () =>
            {
                switch (fieldTag)
                {
                    case "url": opts.OllamaBaseUrl = text; break;
                    case "endpoint": opts.AzureOpenAIEndpoint = text; break;
                    case "deployment": opts.AzureOpenAIDeployment = text; break;
                }
                opts.Save();
                UpdateStatusDot(providerId);
                UpdateSummary();
            });
        });

        return box;
    }

    // ── Debounce ────────────────────────────────────────────────────────────

    private void DebounceSave(string key, Action action)
    {
        if (_saveTimers.TryGetValue(key, out var existing))
            existing.Stop();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _saveTimers.Remove(key);
            action();
        };
        _saveTimers[key] = timer;
        timer.Start();
    }

    // ── Connection test ─────────────────────────────────────────────────────

    private async Task TestConnectionAsync(string providerId)
    {
        if (_testCts.TryGetValue(providerId, out var oldCts))
        {
            await oldCts.CancelAsync();
            oldCts.Dispose();
        }

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        _testCts[providerId] = cts;

        var provider = _registry.GetProvider(providerId);
        if (provider is null)
        {
            ShowResult(providerId, false, "Provider not registered");
            return;
        }

        // Flush pending saves first
        if (_saveTimers.TryGetValue(providerId, out var pendingTimer))
        {
            pendingTimer.Stop();
            _saveTimers.Remove(providerId);
        }

        ShowSpinner(providerId, true);
        ShowResult(providerId, null, null);
        _errorTexts[providerId].Visibility = Visibility.Collapsed;
        UpdateStatusDot(providerId, testing: true);

        var sw = Stopwatch.StartNew();
        try
        {
            var success = await provider.TestConnectionAsync(cts.Token);
            sw.Stop();

            if (cts.IsCancellationRequested) return;

            ShowSpinner(providerId, false);
            ShowResult(providerId, success, success ? $"Connected ({sw.ElapsedMilliseconds}ms)" : "Connection failed");
            UpdateStatusDot(providerId, success: success);
        }
        catch (OperationCanceledException)
        {
            ShowSpinner(providerId, false);
            ShowResult(providerId, false, "Timed out");
            UpdateStatusDot(providerId);
        }
        catch (Exception ex)
        {
            sw.Stop();
            ShowSpinner(providerId, false);
            ShowResult(providerId, false, "Error");
            _errorTexts[providerId].Text = ex.Message;
            _errorTexts[providerId].Visibility = Visibility.Visible;
            UpdateStatusDot(providerId, success: false);
        }
    }

    // ── UI helpers ──────────────────────────────────────────────────────────

    private static readonly SolidColorBrush s_greenBrush = CreateFrozen(Color.FromRgb(0x4E, 0xC9, 0xB0));
    private static readonly SolidColorBrush s_yellowBrush = CreateFrozen(Color.FromRgb(0xFF, 0xD7, 0x00));
    private static readonly SolidColorBrush s_redBrush = CreateFrozen(Color.FromRgb(0xF4, 0x48, 0x47));

    private static SolidColorBrush CreateFrozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    private void ShowSpinner(string providerId, bool show)
    {
        if (!_spinners.TryGetValue(providerId, out var spinner)) return;
        spinner.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

        if (show)
        {
            var rotation = new DoubleAnimation(0, 360, new Duration(TimeSpan.FromSeconds(1)))
            {
                RepeatBehavior = RepeatBehavior.Forever
            };
            ((RotateTransform)spinner.RenderTransform).BeginAnimation(RotateTransform.AngleProperty, rotation);
        }
        else
        {
            ((RotateTransform)spinner.RenderTransform).BeginAnimation(RotateTransform.AngleProperty, null);
        }
    }

    private void ShowResult(string providerId, bool? success, string? text)
    {
        if (!_resultTexts.TryGetValue(providerId, out var tb)) return;

        if (text is null)
        {
            tb.Visibility = Visibility.Collapsed;
            return;
        }

        tb.Text = text;
        tb.Foreground = success == true ? s_greenBrush : s_redBrush;
        tb.Visibility = Visibility.Visible;
    }

    private void UpdateStatusDot(string providerId, bool? success = null, bool testing = false)
    {
        if (!_statusDots.TryGetValue(providerId, out var dot)) return;

        if (testing)
        {
            dot.Fill = s_yellowBrush;
            dot.ToolTip = AIAssistantResources.AIAssistant_Testing;
            return;
        }

        if (success == true)
        {
            dot.Fill = s_greenBrush;
            dot.ToolTip = AIAssistantResources.AIAssistant_Connected;
            return;
        }

        if (success == false)
        {
            dot.Fill = s_redBrush;
            dot.ToolTip = AIAssistantResources.AIAssistant_ConnectionFailed;
            return;
        }

        var opts = AIAssistantOptions.Instance;
        var info = Array.Find(s_providers, p => p.ProviderId == providerId);
        if (info is null) return;

        var configured = providerId == "claude-code"
            ? ClaudeCodeModelProvider.FindClaudeExecutable() is not null
            : info.NeedsApiKey
                ? !string.IsNullOrEmpty(opts.GetApiKey(providerId))
                : !string.IsNullOrEmpty(opts.OllamaBaseUrl);

        dot.Fill = configured ? s_greenBrush : (Brush)Application.Current.TryFindResource("DockBorderBrush") ?? Brushes.Gray;
        dot.ToolTip = configured ? "Configured" : "Not configured";
    }

    private void UpdateSummary()
    {
        var opts = AIAssistantOptions.Instance;
        int configured = 0;

        foreach (var info in s_providers)
        {
            if (info.ProviderId == "claude-code" && ClaudeCodeModelProvider.FindClaudeExecutable() is not null)
                configured++;
            else if (info.NeedsApiKey && !string.IsNullOrEmpty(opts.GetApiKey(info.ProviderId)))
                configured++;
            else if (info.IsLocalUrl && !string.IsNullOrEmpty(opts.OllamaBaseUrl))
                configured++;
        }

        _summaryText.Text = $"{configured} of {s_providers.Length} providers configured";
    }
}
