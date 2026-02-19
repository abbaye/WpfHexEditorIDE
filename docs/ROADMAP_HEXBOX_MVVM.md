# 🎯 Plan de Refactorisation MVVM - HexBox

**Status:** 📋 Planification
**Date:** 15 février 2026
**Auteur:** Derek Tremblay / Claude Sonnet 4.5
**Objectif:** Refactoriser HexBox en MVVM tout en maintenant la compatibilité Legacy

---

## 📊 État Actuel

### Architecture Actuelle (Legacy)
```
HexBox.xaml.cs (UserControl)
├── Events Click directement dans code-behind
├── DependencyProperties (LongValue, MaximumValue)
├── Logique métier mélangée avec UI
├── Pas de séparation des responsabilités
└── TextBox_TextChanged, UpButton_Click, etc.
```

### Fichiers Existants
- [HexBox.xaml](../Sources/WPFHexaEditor/HexBox.xaml) - 87 lignes
- [HexBox.xaml.cs](../Sources/WPFHexaEditor/HexBox.xaml.cs) - 170 lignes
- Dépendances: `ByteConverters`, `KeyValidator`

### Architecture V2 Existante (Référence)
Le projet a déjà:
- ✅ [HexEditorViewModel.cs](../Sources/WPFHexaEditor/ViewModels/HexEditorViewModel.cs) - Architecture MVVM complète
- ✅ [Models/](../Sources/WPFHexaEditor/Models/) - HexLine, ByteData, Position, EditMode
- ✅ SearchModule avec ViewModels
- ✅ Pattern INotifyPropertyChanged
- ✅ Services (UndoRedoService, ClipboardService, etc.)

---

## 🎯 Objectifs de Refactorisation

### 1. Compatibilité Totale
- ✅ **Legacy API maintenue** - Tous les contrôles existants continuent de fonctionner
- ✅ **Backward Compatible** - Aucun breaking change pour utilisateurs existants
- ✅ **Progressive Migration** - Possibilité de migrer progressivement vers V2

### 2. Architecture V2 Pure MVVM
- ✅ **Séparation View/ViewModel** - Logique dans ViewModel, UI dans View
- ✅ **Testabilité** - ViewModel testable sans dépendances UI
- ✅ **Binding bidirectionnel** - Synchronisation automatique View ↔ ViewModel
- ✅ **Commands** - RelayCommand/DelegateCommand pour actions

### 3. Fonctionnalités Améliorées
- ✅ **Validation** - Validation des entrées dans ViewModel
- ✅ **Undo/Redo** - Intégration avec système existant
- ✅ **Multilingue** - Support ResourceManager (comme HexEditor)
- ✅ **Accessibilité** - Meilleur support keyboard/screen readers

---

## 📐 Architecture Cible

### Structure des Fichiers (V2)

```
WPFHexaEditor/
├── ViewModels/
│   └── HexBoxViewModel.cs          ✨ NOUVEAU - ViewModel MVVM
├── Models/
│   └── HexBoxState.cs              ✨ NOUVEAU - État du contrôle
├── Commands/
│   └── RelayCommand.cs             ✨ NOUVEAU (ou réutiliser existant)
├── Converters/
│   └── LongToHexConverter.cs       ✨ NOUVEAU - Conversion affichage
├── HexBox.xaml                     🔄 MODIFIÉ - Bindings MVVM
├── HexBox.xaml.cs                  🔄 MODIFIÉ - Code-behind minimal
├── HexBoxLegacy.xaml               ✨ NOUVEAU - Wrapper Legacy (optionnel)
└── HexBoxLegacy.xaml.cs            ✨ NOUVEAU - Adapter Legacy
```

### Diagramme de Classes

```
┌─────────────────────────────────┐
│      HexBox (View)              │
│  - UserControl XAML             │
│  - DataContext = HexBoxViewModel│
└────────────┬────────────────────┘
             │ Binding
             ▼
┌─────────────────────────────────┐
│    HexBoxViewModel              │
│  - INotifyPropertyChanged       │
│  ────────────────────────       │
│  Properties:                    │
│    + LongValue : long           │
│    + HexValue : string          │
│    + MaximumValue : long        │
│    + IsReadOnly : bool          │
│  ────────────────────────       │
│  Commands:                      │
│    + IncrementCommand           │
│    + DecrementCommand           │
│    + CopyHexCommand             │
│    + CopyDecimalCommand         │
│  ────────────────────────       │
│  Events:                        │
│    + ValueChanged               │
└────────────┬────────────────────┘
             │ Uses
             ▼
┌─────────────────────────────────┐
│    HexBoxState (Model)          │
│  - Plain C# class               │
│  ────────────────────────       │
│    + Value : long               │
│    + Maximum : long             │
│    + IsValid : bool             │
└─────────────────────────────────┘
```

---

## 🚀 Plan d'Implémentation

### Phase 1: Infrastructure MVVM (Priorité 1)

#### 1.1 Créer HexBoxViewModel
**Fichier:** `ViewModels/HexBoxViewModel.cs`

```csharp
//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
//////////////////////////////////////////////

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WpfHexaEditor.Core.Bytes;

namespace WpfHexaEditor.ViewModels
{
    /// <summary>
    /// ViewModel for HexBox control (V2 MVVM architecture)
    /// Manages state and business logic for hex value input/display
    /// </summary>
    public class HexBoxViewModel : INotifyPropertyChanged
    {
        #region Fields

        private long _longValue;
        private long _maximumValue = long.MaxValue;
        private bool _isReadOnly;

        #endregion

        #region Properties

        /// <summary>
        /// Current decimal value
        /// </summary>
        public long LongValue
        {
            get => _longValue;
            set
            {
                if (_longValue != value)
                {
                    // Coerce value to valid range
                    var coercedValue = CoerceValue(value);

                    if (_longValue != coercedValue)
                    {
                        _longValue = coercedValue;
                        OnPropertyChanged();
                        OnPropertyChanged(nameof(HexValue));
                        OnPropertyChanged(nameof(DisplayValue));
                        ValueChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }

        /// <summary>
        /// Current hexadecimal value (read/write)
        /// </summary>
        public string HexValue
        {
            get => ByteConverters.LongToHex(_longValue).TrimStart('0') ?? "0";
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    var (success, val) = ByteConverters.HexLiteralToLong(value);
                    if (success)
                    {
                        LongValue = val;
                    }
                }
            }
        }

        /// <summary>
        /// Display value for TextBox (formatted hex without leading zeros)
        /// </summary>
        public string DisplayValue
        {
            get
            {
                var hex = ByteConverters.LongToHex(_longValue);
                return hex == "00000000" ? "0" : hex.TrimStart('0').ToUpperInvariant();
            }
            set
            {
                HexValue = value;
            }
        }

        /// <summary>
        /// Maximum allowed value
        /// </summary>
        public long MaximumValue
        {
            get => _maximumValue;
            set
            {
                if (_maximumValue != value)
                {
                    _maximumValue = value;
                    OnPropertyChanged();

                    // Revalidate current value
                    if (_longValue > _maximumValue)
                    {
                        LongValue = _maximumValue;
                    }
                }
            }
        }

        /// <summary>
        /// Read-only mode
        /// </summary>
        public bool IsReadOnly
        {
            get => _isReadOnly;
            set
            {
                if (_isReadOnly != value)
                {
                    _isReadOnly = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Commands

        public ICommand IncrementCommand { get; }
        public ICommand DecrementCommand { get; }
        public ICommand CopyHexCommand { get; }
        public ICommand CopyDecimalCommand { get; }

        #endregion

        #region Events

        /// <summary>
        /// Raised when value changes
        /// </summary>
        public event EventHandler ValueChanged;

        #endregion

        #region Constructor

        public HexBoxViewModel()
        {
            IncrementCommand = new RelayCommand(Increment, CanIncrement);
            DecrementCommand = new RelayCommand(Decrement, CanDecrement);
            CopyHexCommand = new RelayCommand(CopyHex);
            CopyDecimalCommand = new RelayCommand(CopyDecimal);
        }

        #endregion

        #region Command Methods

        private void Increment()
        {
            if (_longValue < _maximumValue)
            {
                LongValue++;
            }
        }

        private bool CanIncrement()
        {
            return !_isReadOnly && _longValue < _maximumValue;
        }

        private void Decrement()
        {
            if (_longValue > 0)
            {
                LongValue--;
            }
        }

        private bool CanDecrement()
        {
            return !_isReadOnly && _longValue > 0;
        }

        private void CopyHex()
        {
            System.Windows.Clipboard.SetText($"0x{HexValue}");
        }

        private void CopyDecimal()
        {
            System.Windows.Clipboard.SetText(_longValue.ToString());
        }

        #endregion

        #region Helper Methods

        private long CoerceValue(long value)
        {
            if (value < 0) return 0;
            if (value > _maximumValue) return _maximumValue;
            return value;
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
```

**Avantages:**
- ✅ Logique métier séparée de la UI
- ✅ Testable unitairement sans WPF
- ✅ Commands avec CanExecute automatique
- ✅ Validation centralisée (CoerceValue)

#### 1.2 Créer RelayCommand (si n'existe pas déjà)
**Fichier:** `Commands/RelayCommand.cs`

```csharp
//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
//////////////////////////////////////////////

using System;
using System.Windows.Input;

namespace WpfHexaEditor.Commands
{
    /// <summary>
    /// Generic command implementation for MVVM pattern
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute();
        }

        public void Execute(object parameter)
        {
            _execute();
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>
    /// Generic command with parameter
    /// </summary>
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool> _canExecute;

        public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute((T)parameter);
        }

        public void Execute(object parameter)
        {
            _execute((T)parameter);
        }
    }
}
```

#### 1.3 Créer HexBoxState (Model)
**Fichier:** `Models/HexBoxState.cs`

```csharp
//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
//////////////////////////////////////////////

namespace WpfHexaEditor.Models
{
    /// <summary>
    /// Represents the state of a HexBox control (immutable value object)
    /// </summary>
    public class HexBoxState
    {
        public long Value { get; init; }
        public long MaximumValue { get; init; }
        public bool IsReadOnly { get; init; }

        public HexBoxState(long value, long maximumValue, bool isReadOnly = false)
        {
            Value = value;
            MaximumValue = maximumValue;
            IsReadOnly = isReadOnly;
        }

        public bool IsValid => Value >= 0 && Value <= MaximumValue;

        public bool CanIncrement => !IsReadOnly && Value < MaximumValue;

        public bool CanDecrement => !IsReadOnly && Value > 0;
    }
}
```

---

### Phase 2: Refactoriser HexBox.xaml (Priorité 1)

#### 2.1 HexBox.xaml - Version MVVM
**Fichier:** `HexBox.xaml`

```xml
<!--
    Apache 2.0  - 2026
    Author : Derek Tremblay (derektremblay666@gmail.com)
    V2 MVVM Architecture - Data binding instead of code-behind events
-->

<UserControl
    x:Class="WpfHexaEditor.HexBox"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    xmlns:p="clr-namespace:WpfHexaEditor.Properties"
    xmlns:vm="clr-namespace:WpfHexaEditor.ViewModels"
    Width="100"
    Height="24"
    mc:Ignorable="d">

    <!-- DataContext set in code-behind or externally -->

    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="13" />
            <ColumnDefinition Width="65*" />
            <ColumnDefinition Width="22" />
        </Grid.ColumnDefinitions>
        <Grid.RowDefinitions>
            <RowDefinition />
            <RowDefinition />
        </Grid.RowDefinitions>

        <!-- Up Button - Bound to Command -->
        <RepeatButton
            Grid.Column="2"
            Padding="0"
            BorderBrush="#FFCDCBCB"
            Command="{Binding IncrementCommand}"
            IsEnabled="{Binding IsReadOnly, Converter={StaticResource InverseBoolConverter}}">
            <Grid>
                <TextBlock
                    Margin="0,-1,0,0"
                    FontSize="8"
                    Text="&#x25b2;" />
            </Grid>
        </RepeatButton>

        <!-- Down Button - Bound to Command -->
        <RepeatButton
            Grid.Row="1"
            Grid.Column="2"
            Padding="0"
            BorderBrush="#FFCDCBCB"
            Command="{Binding DecrementCommand}"
            IsEnabled="{Binding IsReadOnly, Converter={StaticResource InverseBoolConverter}}">
            <Grid>
                <TextBlock
                    Margin="0,-1,0,0"
                    FontSize="8"
                    Text="&#x25bc;" />
            </Grid>
        </RepeatButton>

        <!-- TextBox - Two-way binding to DisplayValue -->
        <TextBox
            x:Name="HexTextBox"
            Grid.Row="0"
            Grid.RowSpan="2"
            Grid.Column="1"
            VerticalContentAlignment="Center"
            BorderBrush="{x:Null}"
            Focusable="True"
            IsReadOnly="{Binding IsReadOnly}"
            MaxLength="15"
            MaxLines="1"
            PreviewKeyDown="HexTextBox_PreviewKeyDown"
            TabIndex="1"
            Text="{Binding DisplayValue, UpdateSourceTrigger=PropertyChanged, Mode=TwoWay}"
            ToolTip="{Binding LongValue}">
            <TextBox.ContextMenu>
                <ContextMenu>
                    <MenuItem
                        Command="{Binding CopyHexCommand}"
                        Header="{x:Static p:Resources.CopyAsHexadecimalString}" />
                    <MenuItem
                        Command="{Binding CopyDecimalCommand}"
                        Header="{x:Static p:Resources.CopyAsDecimalString}" />
                </ContextMenu>
            </TextBox.ContextMenu>
        </TextBox>

        <!-- 0x Label -->
        <Label
            Grid.Row="0"
            Grid.RowSpan="2"
            Grid.Column="0"
            Padding="0"
            HorizontalContentAlignment="Center"
            VerticalContentAlignment="Center"
            Content="0x" />
    </Grid>
</UserControl>
```

**Changements clés:**
- ❌ **Supprimé:** `Click="UpButton_Click"`, `TextChanged="HexTextBox_TextChanged"`
- ✅ **Ajouté:** `Command="{Binding IncrementCommand}"`, `Text="{Binding DisplayValue}"`
- ✅ **Binding bidirectionnel:** UpdateSourceTrigger=PropertyChanged
- ✅ **ToolTip dynamique:** `ToolTip="{Binding LongValue}"`

#### 2.2 HexBox.xaml.cs - Code-behind minimal
**Fichier:** `HexBox.xaml.cs`

```csharp
//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// V2 MVVM Architecture - Minimal code-behind
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexaEditor.Core;
using WpfHexaEditor.ViewModels;

namespace WpfHexaEditor
{
    /// <summary>
    /// HexBox control - V2 MVVM Architecture
    /// Uses HexBoxViewModel for all business logic
    /// </summary>
    public partial class HexBox : UserControl
    {
        #region Dependency Properties (V1 Compatibility)

        /// <summary>
        /// LongValue DependencyProperty for backward compatibility
        /// Syncs with ViewModel
        /// </summary>
        public static readonly DependencyProperty LongValueProperty =
            DependencyProperty.Register(
                nameof(LongValue),
                typeof(long),
                typeof(HexBox),
                new FrameworkPropertyMetadata(
                    0L,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    LongValue_Changed));

        public long LongValue
        {
            get => (long)GetValue(LongValueProperty);
            set => SetValue(LongValueProperty, value);
        }

        private static void LongValue_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexBox control && control.ViewModel != null)
            {
                // Sync DependencyProperty → ViewModel
                control.ViewModel.LongValue = (long)e.NewValue;
            }
        }

        /// <summary>
        /// MaximumValue DependencyProperty for backward compatibility
        /// </summary>
        public static readonly DependencyProperty MaximumValueProperty =
            DependencyProperty.Register(
                nameof(MaximumValue),
                typeof(long),
                typeof(HexBox),
                new FrameworkPropertyMetadata(long.MaxValue, MaximumValue_Changed));

        public long MaximumValue
        {
            get => (long)GetValue(MaximumValueProperty);
            set => SetValue(MaximumValueProperty, value);
        }

        private static void MaximumValue_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexBox control && control.ViewModel != null)
            {
                control.ViewModel.MaximumValue = (long)e.NewValue;
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// ViewModel instance (V2 architecture)
        /// </summary>
        public HexBoxViewModel ViewModel { get; private set; }

        /// <summary>
        /// V1 Compatibility: Get hexadecimal value
        /// </summary>
        public string HexValue => ViewModel?.HexValue ?? "0";

        #endregion

        #region Events (V1 Compatibility)

        /// <summary>
        /// Raised when value changes (V1 compatibility)
        /// </summary>
        public event EventHandler ValueChanged;

        #endregion

        #region Constructor

        public HexBox()
        {
            // Create ViewModel
            ViewModel = new HexBoxViewModel();
            DataContext = ViewModel;

            InitializeComponent();

            // Subscribe to ViewModel events
            ViewModel.ValueChanged += OnViewModelValueChanged;
            ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        #endregion

        #region Event Handlers

        private void OnViewModelValueChanged(object sender, EventArgs e)
        {
            // Sync ViewModel → DependencyProperty (for V1 compatibility)
            if (LongValue != ViewModel.LongValue)
            {
                SetValue(LongValueProperty, ViewModel.LongValue);
            }

            // Raise V1 event for backward compatibility
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Sync ViewModel → DependencyProperties
            if (e.PropertyName == nameof(HexBoxViewModel.LongValue))
            {
                if (LongValue != ViewModel.LongValue)
                {
                    SetValue(LongValueProperty, ViewModel.LongValue);
                }
            }
            else if (e.PropertyName == nameof(HexBoxViewModel.MaximumValue))
            {
                if (MaximumValue != ViewModel.MaximumValue)
                {
                    SetValue(MaximumValueProperty, ViewModel.MaximumValue);
                }
            }
        }

        /// <summary>
        /// Validate keyboard input (V1 logic preserved)
        /// Only allow hex keys, backspace, delete, arrows, tab, enter
        /// </summary>
        private void HexTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Allow all navigation/editing keys
            if (KeyValidator.IsHexKey(e.Key) ||
                KeyValidator.IsBackspaceKey(e.Key) ||
                KeyValidator.IsDeleteKey(e.Key) ||
                KeyValidator.IsArrowKey(e.Key) ||
                KeyValidator.IsTabKey(e.Key) ||
                KeyValidator.IsEnterKey(e.Key))
            {
                e.Handled = false;
                return;
            }

            // Block all other keys
            e.Handled = true;
        }

        #endregion

        #region Public Methods (V1 Compatibility)

        /// <summary>
        /// Increment value (V1 compatibility)
        /// </summary>
        public void AddOne()
        {
            if (ViewModel.IncrementCommand.CanExecute(null))
            {
                ViewModel.IncrementCommand.Execute(null);
            }
        }

        /// <summary>
        /// Decrement value (V1 compatibility)
        /// </summary>
        public void SubstractOne()
        {
            if (ViewModel.DecrementCommand.CanExecute(null))
            {
                ViewModel.DecrementCommand.Execute(null);
            }
        }

        #endregion
    }
}
```

**Architecture:**
- ✅ **ViewModel-first:** Logique dans HexBoxViewModel
- ✅ **DependencyProperties maintenues:** API V1 compatible
- ✅ **Synchronisation bidirectionnelle:** DP ↔ ViewModel
- ✅ **Keyboard validation:** PreviewKeyDown conservé (UI concern)
- ✅ **Events V1:** ValueChanged toujours disponible

---

### Phase 3: Adapter Legacy (Priorité 2)

#### 3.1 HexBoxLegacy (Wrapper optionnel)
**Fichier:** `HexBoxLegacy.xaml.cs`

```csharp
//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Legacy Wrapper - 100% backward compatible
//////////////////////////////////////////////

using System;
using System.Windows;

namespace WpfHexaEditor
{
    /// <summary>
    /// Legacy wrapper for HexBox - ensures 100% backward compatibility
    /// Uses new MVVM HexBox internally but exposes old API
    /// </summary>
    public partial class HexBoxLegacy : HexBox
    {
        // All logic inherited from HexBox
        // This class exists only for explicit legacy usage
        // New code should use HexBox directly with MVVM pattern

        public HexBoxLegacy() : base()
        {
            // Same behavior as V1
        }
    }
}
```

---

### Phase 4: Tests & Validation (Priorité 1)

#### 4.1 Tests Unitaires ViewModel
**Fichier:** `Tests/ViewModels/HexBoxViewModelTests.cs`

```csharp
using Xunit;
using WpfHexaEditor.ViewModels;

namespace WpfHexaEditor.Tests.ViewModels
{
    public class HexBoxViewModelTests
    {
        [Fact]
        public void LongValue_SetsHexValue()
        {
            var vm = new HexBoxViewModel();
            vm.LongValue = 255;
            Assert.Equal("FF", vm.HexValue);
        }

        [Fact]
        public void HexValue_SetsLongValue()
        {
            var vm = new HexBoxViewModel();
            vm.HexValue = "FF";
            Assert.Equal(255, vm.LongValue);
        }

        [Fact]
        public void LongValue_RespectMaximum()
        {
            var vm = new HexBoxViewModel { MaximumValue = 100 };
            vm.LongValue = 200;
            Assert.Equal(100, vm.LongValue);
        }

        [Fact]
        public void IncrementCommand_IncreasesValue()
        {
            var vm = new HexBoxViewModel { LongValue = 10 };
            vm.IncrementCommand.Execute(null);
            Assert.Equal(11, vm.LongValue);
        }

        [Fact]
        public void DecrementCommand_DecreasesValue()
        {
            var vm = new HexBoxViewModel { LongValue = 10 };
            vm.DecrementCommand.Execute(null);
            Assert.Equal(9, vm.LongValue);
        }

        [Fact]
        public void ValueChanged_RaisedOnChange()
        {
            var vm = new HexBoxViewModel();
            bool eventRaised = false;
            vm.ValueChanged += (s, e) => eventRaised = true;

            vm.LongValue = 42;

            Assert.True(eventRaised);
        }

        [Theory]
        [InlineData(-1, 0)]           // Negative → 0
        [InlineData(0, 0)]            // Zero → Zero
        [InlineData(100, 100)]        // Valid → Valid
        [InlineData(1000, 100)]       // Over max → Max (with MaximumValue=100)
        public void LongValue_CoercesValueCorrectly(long input, long expected)
        {
            var vm = new HexBoxViewModel { MaximumValue = 100 };
            vm.LongValue = input;
            Assert.Equal(expected, vm.LongValue);
        }
    }
}
```

#### 4.2 Tests d'Intégration UI
**Fichier:** `Tests/Controls/HexBoxIntegrationTests.cs`

```csharp
using Xunit;
using WpfHexaEditor;

namespace WpfHexaEditor.Tests.Controls
{
    public class HexBoxIntegrationTests
    {
        [WpfFact]
        public void HexBox_LongValueProperty_SyncsWithViewModel()
        {
            var hexBox = new HexBox();
            hexBox.LongValue = 255;

            Assert.Equal(255, hexBox.ViewModel.LongValue);
            Assert.Equal("FF", hexBox.ViewModel.HexValue);
        }

        [WpfFact]
        public void HexBox_MaximumValueProperty_SyncsWithViewModel()
        {
            var hexBox = new HexBox();
            hexBox.MaximumValue = 1000;

            Assert.Equal(1000, hexBox.ViewModel.MaximumValue);
        }

        [WpfFact]
        public void HexBox_ValueChanged_RaisedOnChange()
        {
            var hexBox = new HexBox();
            bool eventRaised = false;
            hexBox.ValueChanged += (s, e) => eventRaised = true;

            hexBox.LongValue = 42;

            Assert.True(eventRaised);
        }

        [WpfFact]
        public void HexBox_AddOne_IncrementsValue()
        {
            var hexBox = new HexBox { LongValue = 10 };
            hexBox.AddOne();
            Assert.Equal(11, hexBox.LongValue);
        }

        [WpfFact]
        public void HexBox_SubstractOne_DecrementsValue()
        {
            var hexBox = new HexBox { LongValue = 10 };
            hexBox.SubstractOne();
            Assert.Equal(9, hexBox.LongValue);
        }
    }
}
```

---

## 📋 Checklist d'Implémentation

### Phase 1: Infrastructure ✨
- [ ] Créer `ViewModels/HexBoxViewModel.cs`
- [ ] Créer `Commands/RelayCommand.cs` (ou vérifier si existe)
- [ ] Créer `Models/HexBoxState.cs`
- [ ] Créer `Converters/InverseBoolConverter.cs` (si n'existe pas)

### Phase 2: Refactoring ✨
- [ ] Modifier `HexBox.xaml` - Ajouter bindings MVVM
- [ ] Modifier `HexBox.xaml.cs` - Code-behind minimal + sync DP/ViewModel
- [ ] Tester compatibilité avec code existant

### Phase 3: Tests ✅
- [ ] Tests unitaires `HexBoxViewModel`
- [ ] Tests d'intégration `HexBox` control
- [ ] Tests de régression V1 API
- [ ] Tests de performance (binding vs events)

### Phase 4: Documentation 📚
- [ ] Mettre à jour XML docs
- [ ] Créer guide migration V1 → V2
- [ ] Exemples d'utilisation XAML/code-behind
- [ ] Patterns MVVM best practices

### Phase 5: Multilingue 🌍
- [ ] Ajouter Resources pour tooltips
- [ ] Support ResourceManager dans ViewModel
- [ ] Tester changement de langue instantané

---

## 🎨 Exemples d'Utilisation

### V1 Legacy (toujours supporté)
```xaml
<local:HexBox
    x:Name="MyHexBox"
    LongValue="255"
    MaximumValue="1000"
    ValueChanged="MyHexBox_ValueChanged" />
```

```csharp
private void MyHexBox_ValueChanged(object sender, EventArgs e)
{
    var value = MyHexBox.LongValue;
    var hex = MyHexBox.HexValue;
}
```

### V2 MVVM (nouveau pattern)
```xaml
<local:HexBox LongValue="{Binding MyValue, Mode=TwoWay}" />
```

```csharp
public class MyViewModel : INotifyPropertyChanged
{
    private long _myValue;
    public long MyValue
    {
        get => _myValue;
        set { _myValue = value; OnPropertyChanged(); }
    }
}
```

### V2 MVVM Avancé (accès direct ViewModel)
```xaml
<local:HexBox x:Name="MyHexBox" />
```

```csharp
// Accès direct au ViewModel pour contrôle avancé
MyHexBox.ViewModel.LongValue = 255;
MyHexBox.ViewModel.IncrementCommand.Execute(null);
MyHexBox.ViewModel.ValueChanged += OnValueChanged;
```

---

## 🚀 Migration Guide

### Scénario 1: Code Legacy existant
**Aucun changement requis!** Le HexBox refactorisé maintient 100% de compatibilité.

```csharp
// ✅ Ce code continue de fonctionner tel quel
var hexBox = new HexBox();
hexBox.LongValue = 255;
hexBox.ValueChanged += OnValueChanged;
```

### Scénario 2: Nouvelle application MVVM
**Utiliser pattern V2** avec bindings

```xaml
<!-- MainWindow.xaml -->
<Window.DataContext>
    <vm:MainViewModel />
</Window.DataContext>

<local:HexBox LongValue="{Binding OffsetValue, Mode=TwoWay}" />
```

### Scénario 3: Migration progressive
**Étape par étape** vers MVVM

1. **Remplacer événements par Commands**
   ```xaml
   <!-- Avant (V1) -->
   <Button Click="Button_Click" />

   <!-- Après (V2) -->
   <Button Command="{Binding MyCommand}" />
   ```

2. **Remplacer DependencyProperties par Bindings**
   ```xaml
   <!-- Avant (V1) -->
   <local:HexBox x:Name="MyHexBox" LongValue="255" />

   <!-- Après (V2) -->
   <local:HexBox LongValue="{Binding MyValue}" />
   ```

---

## ⚡ Optimisations & Bonnes Pratiques

### Performance
1. **Binding UpdateSourceTrigger**
   - `PropertyChanged` pour feedback instantané
   - `LostFocus` si performance critique (moins de updates)

2. **Command CanExecute**
   - WPF vérifie automatiquement via `CommandManager.RequerySuggested`
   - Pas besoin de refresh manuel

3. **INotifyPropertyChanged**
   - Utiliser `[CallerMemberName]` pour éviter magic strings
   - Notify uniquement si valeur change (`if (_field != value)`)

### Testabilité
1. **ViewModel sans dépendances WPF**
   - Pas de `DependencyProperty` dans ViewModel
   - Pas de `MessageBox`, `Dispatcher`, etc.
   - Interface pour services (IClipboardService)

2. **Mock-friendly**
   ```csharp
   // ViewModel injecte services via constructor
   public HexBoxViewModel(IClipboardService clipboard = null)
   {
       _clipboard = clipboard ?? new ClipboardService();
   }
   ```

### MVVM Patterns
1. **Commands vs Events**
   - Commands pour actions utilisateur (Click, KeyDown)
   - Events pour notifications (ValueChanged)

2. **Two-Way Binding**
   ```xaml
   <!-- Synchronisation automatique View ↔ ViewModel -->
   <TextBox Text="{Binding DisplayValue, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
   ```

---

## 📊 Comparaison V1 vs V2

| Aspect | V1 Legacy | V2 MVVM | Avantages V2 |
|--------|-----------|---------|--------------|
| **Architecture** | Code-behind | ViewModel | Séparation concerns |
| **Testabilité** | ❌ UI tests seulement | ✅ Unit tests ViewModel | Tests plus rapides |
| **Binding** | ❌ Événements manuels | ✅ Data binding WPF | Moins de code |
| **Validation** | ⚠️ Dispersée | ✅ Centralisée ViewModel | Logique unique |
| **Réutilisabilité** | ❌ Couplé à UI | ✅ ViewModel réutilisable | DRY principle |
| **Maintenance** | ⚠️ Code-behind verbose | ✅ Déclaratif XAML | Plus lisible |
| **Performance** | 🟢 Bonne | 🟢 Excellente | WPF binding optimisé |

---

## 🎯 Prochaines Étapes

### Court Terme (1-2 semaines)
1. ✅ Implémenter HexBoxViewModel
2. ✅ Refactoriser HexBox.xaml/cs
3. ✅ Tests unitaires & intégration
4. ✅ Valider compatibilité V1

### Moyen Terme (1 mois)
1. Créer exemples Sample app (V1 vs V2)
2. Documentation complète
3. Patterns avancés (Undo/Redo, Validation)
4. Support multilingue

### Long Terme (3 mois)
1. Refactoriser autres contrôles en MVVM
2. Créer bibliothèque Controls réutilisables
3. Templates/Styles customizables
4. Performance profiling & optimisations

---

## 📝 Notes Techniques

### Synchronisation DependencyProperty ↔ ViewModel
```csharp
// DP → ViewModel
private static void LongValue_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
{
    if (d is HexBox control)
        control.ViewModel.LongValue = (long)e.NewValue;
}

// ViewModel → DP
private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
{
    if (e.PropertyName == nameof(HexBoxViewModel.LongValue))
        SetValue(LongValueProperty, ViewModel.LongValue);
}
```

**Attention:** Éviter boucles infinies!
- Vérifier `if (oldValue != newValue)` avant SetValue
- Utiliser flag `_isSyncing` si nécessaire

### Validation Input
**Option 1: PreviewKeyDown (UI concern)**
```csharp
private void HexTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
{
    e.Handled = !KeyValidator.IsHexKey(e.Key);
}
```

**Option 2: ValidationRule (MVVM approach)**
```csharp
public class HexValueValidationRule : ValidationRule
{
    public override ValidationResult Validate(object value, CultureInfo cultureInfo)
    {
        if (value is string str && ByteConverters.HexLiteralToLong(str).success)
            return ValidationResult.ValidResult;
        return new ValidationResult(false, "Invalid hex value");
    }
}
```

---

## 🤝 Contributeurs

- **Derek Tremblay** - Architecture & Implémentation
- **Claude Sonnet 4.5** - Design patterns & Documentation

---

## 📞 Support

Questions ou suggestions?
- GitHub Issues: [WpfHexEditorControl/issues](https://github.com/...)
- Documentation: [docs/mvvm-refactoring.md](docs/mvvm-refactoring.md)

---

**Dernière mise à jour:** 15 février 2026
**Status:** 📋 Plan approuvé - Prêt pour implémentation
**Estimation:** 2-3 jours développement + 1 jour tests
