# SearchModule/Views - Interfaces Utilisateur de Recherche

Interfaces WPF pour recherche/remplacement avec architecture MVVM et data binding.

## 📁 Contenu

- **[FindReplaceDialog.xaml.cs](#-findreplacedialogxamlcs)** - Dialogue modal complet
- **[QuickSearchBar.xaml.cs](#-quicksearchbarxamlcs)** - Barre de recherche rapide
- **[SearchPanel.xaml.cs](#-searchpanelxamlcs)** - Panneau latéral

---

## 🎯 Interfaces

### 🔍 FindReplaceDialog.xaml.cs

**Dialogue modal complet** pour recherche/remplacement avec toutes les options.

#### Caractéristiques

- Onglets Recherche/Remplacement
- Support texte et hexadécimal
- Options avancées (wildcards, case sensitive, wrap around)
- Barre de progression pour opérations longues
- Liste des résultats cliquable
- Bouton d'annulation

#### Utilisation

```csharp
var dialog = new FindReplaceDialog
{
    Owner = this,
    DataContext = new SearchViewModel
    {
        ByteProvider = hexEditor.ByteProvider
    }
};

if (dialog.ShowDialog() == true)
{
    // Résultats disponibles dans le ViewModel
}
```

#### Binding XAML

```xml
<TabControl>
    <TabItem Header="Recherche">
        <StackPanel>
            <TextBox Text="{Binding SearchText}" />
            <ComboBox ItemsSource="{Binding AvailableEncodings}"
                      SelectedItem="{Binding SelectedEncoding}" />
            <CheckBox IsChecked="{Binding CaseSensitive}" />
            <Button Command="{Binding FindAllCommand}" Content="Tout Trouver" />
        </StackPanel>
    </TabItem>
    <TabItem Header="Remplacement">
        <StackPanel>
            <TextBox Text="{Binding SearchText}" />
            <TextBox Text="{Binding ReplaceText}" />
            <Button Command="{Binding ReplaceAllCommand}" Content="Tout Remplacer" />
        </StackPanel>
    </TabItem>
</TabControl>
```

---

### ⚡ QuickSearchBar.xaml.cs

**Barre de recherche rapide** style Ctrl+F, compacte et intégrée.

#### Caractéristiques

- Apparence/disparition animée (fade in/out)
- Recherche incrémentale (au fur et à mesure de la saisie)
- Navigation Précédent/Suivant
- Compteur de résultats (ex: "3/10")
- Fermeture avec Escape

#### Utilisation

```csharp
var quickSearch = new QuickSearchBar
{
    DataContext = new SearchViewModel
    {
        ByteProvider = hexEditor.ByteProvider
    }
};

// Afficher/masquer
quickSearch.Show();  // Fade in
quickSearch.Hide();  // Fade out

// Raccourci clavier
if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
{
    quickSearch.Show();
    quickSearch.Focus();
}
```

#### Recherche Incrémentale

```csharp
// Auto-search pendant la saisie
searchTextBox.TextChanged += async (s, e) =>
{
    if (searchTextBox.Text.Length >= 3)
    {
        await viewModel.FindAllCommand.ExecuteAsync(null);
    }
};
```

---

### 📋 SearchPanel.xaml.cs

**Panneau latéral non-modal** pour recherches répétées.

#### Caractéristiques

- Ancré sur le côté du HexEditor
- Liste persistante des résultats
- Double-clic pour naviguer
- Groupement par proximité
- Peut rester ouvert pendant l'édition

#### Utilisation

```csharp
var searchPanel = new SearchPanel
{
    DataContext = new SearchViewModel
    {
        ByteProvider = hexEditor.ByteProvider
    }
};

// Ancrer au HexEditor
DockPanel.SetDock(searchPanel, Dock.Right);
mainDockPanel.Children.Add(searchPanel);
```

#### Navigation vers Résultats

```csharp
// Double-clic sur résultat
resultsListBox.MouseDoubleClick += (s, e) =>
{
    var match = (SearchMatch)resultsListBox.SelectedItem;
    hexEditor.SetPosition(match.Position, match.Length);
    hexEditor.Focus();
};
```

---

## 🎨 Design

### Convertisseurs XAML

Chaque View contient des convertisseurs inline:

```csharp
// Dans SearchPanel.xaml.cs
public class BoolToVisibilityConverter : IValueConverter { }
public class PositionToHexStringConverter : IValueConverter { }
public class MatchCountConverter : IValueConverter { }
```

### Styles

```xml
<Style TargetType="TextBox" x:Key="SearchTextBox">
    <Setter Property="Padding" Value="5" />
    <Setter Property="FontSize" Value="12" />
</Style>
```

---

## 📊 Exemples Complets

### Exemple 1 - Dialogue de Recherche Complet

```csharp
private void ShowSearchDialog()
{
    var viewModel = new SearchViewModel
    {
        ByteProvider = hexEditor.ByteProvider
    };

    // Gérer les résultats
    viewModel.PropertyChanged += (s, e) =>
    {
        if (e.PropertyName == nameof(viewModel.TotalMatches))
        {
            statusBar.Text = $"{viewModel.TotalMatches} résultats trouvés";
        }
    };

    var dialog = new FindReplaceDialog
    {
        Owner = this,
        DataContext = viewModel
    };

    dialog.ShowDialog();
}
```

### Exemple 2 - QuickSearch avec Raccourci

```csharp
private QuickSearchBar _quickSearch;

public MainWindow()
{
    InitializeComponent();

    _quickSearch = new QuickSearchBar
    {
        DataContext = new SearchViewModel
        {
            ByteProvider = hexEditor.ByteProvider
        }
    };

    // Ajouter à la grille principale
    mainGrid.Children.Add(_quickSearch);

    // Raccourci Ctrl+F
    this.KeyDown += (s, e) =>
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            _quickSearch.Show();
            e.Handled = true;
        }
    };
}
```

### Exemple 3 - Panel de Recherche Persistant

```csharp
private SearchPanel _searchPanel;

private void ShowSearchPanel()
{
    if (_searchPanel == null)
    {
        _searchPanel = new SearchPanel
        {
            Width = 300,
            DataContext = new SearchViewModel
            {
                ByteProvider = hexEditor.ByteProvider
            }
        };

        DockPanel.SetDock(_searchPanel, Dock.Right);
        mainDockPanel.Children.Add(_searchPanel);
    }

    _searchPanel.Visibility = Visibility.Visible;
}
```

---

## 🔗 Intégration

### Avec HexEditor

```csharp
// Intégration dans HexEditor
hexEditor.ShowFindDialog = () => new FindReplaceDialog
{
    DataContext = new SearchViewModel { ByteProvider = hexEditor.ByteProvider }
}.ShowDialog();
```

### Avec ViewModels

```csharp
// Le ViewModel est partagé entre les vues
var sharedViewModel = new SearchViewModel { ByteProvider = byteProvider };

quickSearch.DataContext = sharedViewModel;
searchPanel.DataContext = sharedViewModel;
```

---

## 📚 Ressources Connexes

- **[ViewModels/README.md](../ViewModels/README.md)** - SearchViewModel, ReplaceViewModel
- **[Models/README.md](../Models/README.md)** - SearchOptions, SearchResult
- **[Services/README.md](../Services/README.md)** - SearchEngine

---

✨ Documentation par Derek Tremblay et Claude Sonnet 4.5
