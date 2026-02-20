# SearchModule/Models - Classes de Données

Classes de données pour la configuration et les résultats de recherche. Ces modèles sont utilisés par le `SearchEngine` et les ViewModels.

## 📁 Contenu

- **[SearchOptions.cs](#-searchoptionscs)** - Configuration de recherche
- **[SearchResult.cs](#-searchresultcs)** - Résultats de recherche avec métadonnées

---

## 🎯 Classes

### 📋 SearchOptions.cs

**Configuration de recherche** passée au `SearchEngine`.

#### Propriétés

```csharp
public class SearchOptions
{
    // Motif à rechercher (requis)
    public byte[] Pattern { get; set; }

    // Position de départ (0 = début du fichier)
    public long StartPosition { get; set; }

    // Position de fin (-1 = fin du fichier)
    public long EndPosition { get; set; } = -1;

    // Recherche arrière (du bas vers le haut)
    public bool SearchBackward { get; set; }

    // Utiliser des wildcards
    public bool UseWildcard { get; set; }

    // Octet joker (par défaut 0xFF)
    public byte WildcardByte { get; set; } = 0xFF;

    // Nombre maximum de résultats (0 = illimité)
    public int MaxResults { get; set; } = 0;

    // Recherche parallèle (auto-activé si > 10 MB)
    public bool UseParallelSearch { get; set; }

    // Taille des chunks pour recherche parallèle (1 MB par défaut)
    public int ParallelChunkSize { get; set; } = 1048576;
}
```

#### Méthodes

```csharp
// Valide les options
public bool IsValid() => Pattern != null && Pattern.Length > 0;

// Clone les options
public SearchOptions Clone();
```

#### Exemples

**Recherche simple:**
```csharp
var options = new SearchOptions
{
    Pattern = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }, // "Hello"
    StartPosition = 0,
    SearchBackward = false
};
```

**Recherche avec wildcards:**
```csharp
var options = new SearchOptions
{
    Pattern = new byte[] { 0x48, 0xFF, 0x6C, 0x6C, 0x6F }, // "H?llo"
    UseWildcard = true,
    WildcardByte = 0xFF
};
```

**Recherche avec limite:**
```csharp
var options = new SearchOptions
{
    Pattern = pattern,
    StartPosition = 0x1000,
    EndPosition = 0x5000,  // Rechercher seulement dans cette plage
    MaxResults = 10        // S'arrêter après 10 résultats
};
```

---

### 📊 SearchResult.cs

**Résultats de recherche** retournés par le `SearchEngine`.

#### Classes

```csharp
// Résultat principal
public class SearchResult
{
    // Succès de la recherche
    public bool Success { get; set; }

    // Liste des matches trouvés
    public List<SearchMatch> Matches { get; set; }

    // Temps d'exécution en millisecondes
    public long ElapsedMs { get; set; }

    // Nombre d'octets recherchés
    public long SearchedBytes { get; set; }

    // Message d'erreur (si échec)
    public string ErrorMessage { get; set; }

    // Options utilisées
    public SearchOptions Options { get; set; }
}

// Match individuel
public class SearchMatch
{
    // Position dans le fichier
    public long Position { get; set; }

    // Longueur du match
    public int Length { get; set; }

    // Octets matchés
    public byte[] MatchedBytes { get; set; }
}
```

#### Méthodes Statiques

```csharp
// Créer un résultat de succès
public static SearchResult CreateSuccess(List<SearchMatch> matches, long ms, long bytes);

// Créer un résultat d'erreur
public static SearchResult CreateError(string message);

// Créer un résultat annulé
public static SearchResult CreateCancelled();
```

#### Exemples

**Utilisation des résultats:**
```csharp
var result = searchEngine.Search(options, cancellationToken);

if (result.Success)
{
    Console.WriteLine($"Trouvé {result.Matches.Count} occurrences");
    Console.WriteLine($"Temps: {result.ElapsedMs}ms");
    Console.WriteLine($"Octets recherchés: {result.SearchedBytes:N0}");

    foreach (var match in result.Matches)
    {
        Console.WriteLine($"  0x{match.Position:X8}: {BitConverter.ToString(match.MatchedBytes)}");
    }
}
else
{
    Console.WriteLine($"Erreur: {result.ErrorMessage}");
}
```

**Création de résultats personnalisés:**
```csharp
var matches = new List<SearchMatch>
{
    new SearchMatch
    {
        Position = 0x100,
        Length = 5,
        MatchedBytes = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }
    }
};

var result = SearchResult.CreateSuccess(matches, 42, 1000000);
```

---

## 🔗 Utilisation

### Par SearchEngine

```csharp
var options = new SearchOptions { Pattern = pattern };
var result = searchEngine.Search(options, cancellationToken);
```

### Par ViewModel

```csharp
var viewModel = new SearchViewModel();
viewModel.SearchText = "Hello";
await viewModel.FindAllCommand.ExecuteAsync(null);

// Résultats disponibles dans viewModel.Matches
```

---

## 📚 Ressources Connexes

- **[SearchModule/README.md](../README.md)** - Vue d'ensemble du module
- **[Services/README.md](../Services/README.md)** - SearchEngine qui utilise ces modèles
- **[ViewModels/README.md](../ViewModels/README.md)** - ViewModels qui utilisent ces modèles

---

✨ Documentation par Derek Tremblay et Claude Sonnet 4.5
