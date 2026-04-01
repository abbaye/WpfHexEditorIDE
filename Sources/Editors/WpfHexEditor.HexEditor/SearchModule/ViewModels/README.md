# SearchModule/ViewModels - ViewModels MVVM pour Recherche

ViewModels MVVM avec support complet de data binding pour les interfaces de recherche/remplacement.

## 📁 Contenu

- **[SearchViewModel.cs](#-searchviewmodelcs)** - ViewModel pour recherche
- **[ReplaceViewModel.cs](#-replaceviewmodelcs)** - ViewModel pour remplacement

---

## 🔍 SearchViewModel.cs

**ViewModel MVVM** pour opérations de recherche avec support complet de binding.

### Propriétés Bindables

```csharp
// Texte de recherche
public string SearchText { get; set; }
public string SearchHex { get; set; }

// Mode de recherche
public SearchMode SelectedSearchMode { get; set; } // Text ou Hex
public Encoding SelectedEncoding { get; set; }     // UTF8, ASCII, etc.
public ObservableCollection<EncodingInfo> AvailableEncodings { get; }

// Options
public bool CaseSensitive { get; set; }
public bool UseWildcard { get; set; }
public bool SearchBackward { get; set; }
public bool WrapAround { get; set; }

// État
public bool IsSearching { get; set; }
public string StatusMessage { get; set; }
public int CurrentMatchIndex { get; set; }
public int TotalMatches { get; set; }

// Résultats
public ObservableCollection<SearchMatch> Matches { get; set; }

// ByteProvider
public Core.Bytes.ByteProvider ByteProvider { get; set; }
```

### Commandes

```csharp
public ICommand FindFirstCommand { get; }      // Trouver premier
public ICommand FindNextCommand { get; }       // Suivant
public ICommand FindPreviousCommand { get; }   // Précédent
public ICommand FindAllCommand { get; }        // Tout trouver
public ICommand CancelCommand { get; }         // Annuler
public ICommand ClearResultsCommand { get; }   // Effacer résultats
```

### Énumérations

```csharp
public enum SearchMode
{
    Text,  // Recherche texte (avec encodage)
    Hex    // Recherche hexadécimale
}
```

---

## 🔄 ReplaceViewModel.cs

**ViewModel pour remplacement** (hérite de SearchViewModel).

### Propriétés Additionnelles

```csharp
// Texte de remplacement
public string ReplaceText { get; set; }
public string ReplaceHex { get; set; }

// Statistiques
public int ReplacedCount { get; set; }

// Options
public bool ConfirmEachReplace { get; set; }
```

### Commandes Additionnelles

```csharp
public ICommand ReplaceCurrentCommand { get; }  // Remplacer courant
public ICommand ReplaceAllCommand { get; }      // Tout remplacer
public ICommand ReplaceNextCommand { get; }     // Remplacer et suivant
```

---

## 📊 Exemples d'Utilisation

### Exemple 1 - ViewModel de Recherche Simple

```csharp
var viewModel = new SearchViewModel
{
    ByteProvider = hexEditor.ByteProvider
};

// Lier aux événements
viewModel.PropertyChanged += (s, e) =>
{
    if (e.PropertyName == nameof(viewModel.StatusMessage))
        UpdateStatusBar(viewModel.StatusMessage);
};

// Effectuer recherche
viewModel.SearchText = "Hello World";
viewModel.SelectedSearchMode = SearchMode.Text;
viewModel.SelectedEncoding = Encoding.UTF8;
await viewModel.FindAllCommand.ExecuteAsync(null);

// Résultats disponibles
Console.WriteLine($"Trouvé {viewModel.TotalMatches} résultats");
```

### Exemple 2 - Binding XAML

```xml
<Window xmlns:vm="clr-namespace:WpfHexaEditor.SearchModule.ViewModels">
    <Window.DataContext>
        <vm:SearchViewModel />
    </Window.DataContext>

    <!-- Texte de recherche -->
    <TextBox Text="{Binding SearchText, UpdateSourceTrigger=PropertyChanged}" />

    <!-- Mode de recherche -->
    <ComboBox ItemsSource="{Binding AvailableEncodings}"
              SelectedItem="{Binding SelectedEncoding}"
              DisplayMemberPath="DisplayName" />

    <!-- Options -->
    <CheckBox IsChecked="{Binding CaseSensitive}" Content="Sensible à la casse" />
    <CheckBox IsChecked="{Binding UseWildcard}" Content="Utiliser wildcards" />
    <CheckBox IsChecked="{Binding SearchBackward}" Content="Recherche arrière" />

    <!-- Commandes -->
    <Button Command="{Binding FindFirstCommand}" Content="Trouver Premier" />
    <Button Command="{Binding FindNextCommand}" Content="Suivant" />
    <Button Command="{Binding FindAllCommand}" Content="Tout Trouver" />

    <!-- Status -->
    <TextBlock Text="{Binding StatusMessage}" />
    <TextBlock Text="{Binding TotalMatches, StringFormat='Résultats: {0}'}" />
    <ProgressBar IsIndeterminate="{Binding IsSearching}" />

    <!-- Résultats -->
    <ListBox ItemsSource="{Binding Matches}">
        <ListBox.ItemTemplate>
            <DataTemplate>
                <TextBlock Text="{Binding Position, StringFormat='0x{0:X8}'}" />
            </DataTemplate>
        </ListBox.ItemTemplate>
    </ListBox>
</Window>
```

### Exemple 3 - Remplacement

```csharp
var replaceVM = new ReplaceViewModel
{
    ByteProvider = hexEditor.ByteProvider,
    SearchText = "old",
    ReplaceText = "new",
    ConfirmEachReplace = false
};

// Remplacer tout
await replaceVM.ReplaceAllCommand.ExecuteAsync(null);

Console.WriteLine($"{replaceVM.ReplacedCount} remplacements effectués");
```

### Exemple 4 - Recherche Incrémentale

```csharp
// Recherche au fur et à mesure de la saisie
viewModel.PropertyChanged += async (s, e) =>
{
    if (e.PropertyName == nameof(viewModel.SearchText))
    {
        if (viewModel.SearchText.Length >= 3)
        {
            await viewModel.FindAllCommand.ExecuteAsync(null);
        }
    }
};
```

### Exemple 5 - Navigation dans les Résultats

```csharp
// Naviguer au résultat suivant
viewModel.FindNextCommand.Execute(null);

// Position courante
var currentMatch = viewModel.Matches[viewModel.CurrentMatchIndex];
hexEditor.SetPosition(currentMatch.Position, currentMatch.Length);
```

---

## 🏗️ Architecture MVVM

### Séparation des Responsabilités

```
View (XAML)
    ↓ Data Binding
ViewModel (SearchViewModel)
    ↓ Business Logic
Service (SearchEngine)
    ↓ Data Access
ByteProvider
```

### Patterns Utilisés

1. **INotifyPropertyChanged** - Notification de changement
2. **ICommand** - Encapsulation des actions
3. **ObservableCollection** - Collections réactives
4. **Async/Await** - Opérations asynchrones

---

## 🔗 Intégration

### Avec Views

```csharp
var dialog = new FindReplaceDialog
{
    DataContext = new SearchViewModel { ByteProvider = byteProvider }
};
dialog.ShowDialog();
```

### Avec HexEditor

```csharp
hexEditor.ViewModel.SearchViewModel = new SearchViewModel
{
    ByteProvider = hexEditor.ByteProvider
};
```

---

## 📚 Ressources Connexes

- **[Models/README.md](../Models/README.md)** - SearchOptions, SearchResult
- **[Services/README.md](../Services/README.md)** - SearchEngine
- **[Views/README.md](../Views/README.md)** - Interfaces UI
- **[Commands/README.md](../../Commands/README.md)** - RelayCommand

---

✨ Documentation par Derek Tremblay et Claude Sonnet 4.5
