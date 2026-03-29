// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: PartialClasses/Features/HexEditor.BreadcrumbBar.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-29
// Description:
//     Partial class wiring HexBreadcrumbBar to HexEditor events.
//     Initialised in InitializeBreadcrumbBar() called from the HexEditor constructor.
//
// Architecture Notes:
//     Subscribes to SelectionStartChanged + SelectionStopChanged (cursor/selection moves)
//     and FormatDetected (format chip update).
//     ShowBreadcrumbBar DP controls Visibility — no layout cost when hidden.
// ==========================================================

using System;
using System.Windows;
using WpfHexEditor.Core.Events;
using WpfHexEditor.HexEditor.Controls;

namespace WpfHexEditor.HexEditor
{
    public partial class HexEditor
    {
        // ── Dependency Property ───────────────────────────────────────────────

        /// <summary>Shows or hides the breadcrumb bar above the hex viewport.</summary>
        public static readonly DependencyProperty ShowBreadcrumbBarProperty =
            DependencyProperty.Register(
                nameof(ShowBreadcrumbBar),
                typeof(bool),
                typeof(HexEditor),
                new PropertyMetadata(true, OnShowBreadcrumbBarChanged));

        public bool ShowBreadcrumbBar
        {
            get => (bool)GetValue(ShowBreadcrumbBarProperty);
            set => SetValue(ShowBreadcrumbBarProperty, value);
        }

        private static void OnShowBreadcrumbBarChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not HexEditor editor) return;
            if (editor._breadcrumbBar is null) return;
            editor._breadcrumbBar.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }

        // ── State ─────────────────────────────────────────────────────────────

        private HexBreadcrumbBar? _breadcrumbBar;

        // ── Initialisation ────────────────────────────────────────────────────

        private void InitializeBreadcrumbBar()
        {
            _breadcrumbBar = this.FindName("BreadcrumbBar") as HexBreadcrumbBar;
            if (_breadcrumbBar is null) return;

            _breadcrumbBar.Visibility = ShowBreadcrumbBar ? Visibility.Visible : Visibility.Collapsed;

            SelectionStartChanged += OnBreadcrumbSelectionChanged;
            SelectionStopChanged  += OnBreadcrumbSelectionChanged;
            FormatDetected        += OnBreadcrumbFormatDetected;
        }

        // ── Event Handlers ────────────────────────────────────────────────────

        private void OnBreadcrumbSelectionChanged(object? sender, EventArgs e)
        {
            if (_breadcrumbBar is null) return;

            var offset = _viewModel.SelectionStart.IsValid ? _viewModel.SelectionStart.Value : 0L;
            var length = _viewModel.SelectionLength;
            _breadcrumbBar.UpdateOffset(offset, length);
        }

        private void OnBreadcrumbFormatDetected(object? sender, FormatDetectedEventArgs e)
        {
            if (_breadcrumbBar is null) return;
            _breadcrumbBar.SetFormatName(e.Success ? e.Format?.FormatName : null);
        }
    }
}
