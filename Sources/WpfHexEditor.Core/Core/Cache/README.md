# Core/Cache - Cache LRU pour Performances

**Cache LRU (Least Recently Used)** pour optimiser les recherches répétées. **Nouveau v2.2+**

## 📁 Contenu (2 fichiers)

| Fichier | Description |
|---------|-------------|
| **LRUCache.cs** | Implémentation LRU générique O(1) |
| **SearchCacheKey.cs** | Clé de cache avec hachage polynomial |

## 🎯 Classes

### LRUCache<TKey, TValue>

```csharp
public class LRUCache<TKey, TValue>
{
    public LRUCache(int capacity);

    public bool TryGetValue(TKey key, out TValue value);
    public void Add(TKey key, TValue value);
    public void Clear();

    public int Count { get; }
    public int Capacity { get; }
    public double UsagePercentage { get; }
}
```

**Complexité:** O(1) pour Get et Add

### SearchCacheKey

```csharp
public class SearchCacheKey : IEquatable<SearchCacheKey>
{
    public byte[] Pattern { get; set; }
    public long StartPosition { get; set; }
    public bool UseWildcard { get; set; }

    // Hachage polynomial pour performance
    public override int GetHashCode();
}
```

## 💡 Exemples

**Utilisation basique:**
```csharp
var cache = new LRUCache<string, List<long>>(capacity: 20);

// Ajouter
cache.Add("pattern1", results1);

// Récupérer
if (cache.TryGetValue("pattern1", out var cachedResults))
{
    Console.WriteLine("Cache hit!");
    return cachedResults;
}
```

**Avec SearchEngine:**
```csharp
// Le cache est utilisé automatiquement
var service = new FindReplaceService(cacheCapacity: 50);

// Premier appel: 18ms (recherche complète)
var results1 = service.FindAllCachedOptimized(provider, pattern, 0);

// Second appel: 0.2ms (cache hit - 90x plus rapide!)
var results2 = service.FindAllCachedOptimized(provider, pattern, 0);
```

## ⚡ Performance

**Benchmarks (recherche répétée):**
- Sans cache: 18ms chaque fois
- Avec cache (hit): 0.2ms (**90x plus rapide**)

**Amélioration:** **10-100x** pour recherches répétées

## 🔗 Ressources

- **[Services/README.md](../../Services/README.md)** - FindReplaceService utilise LRUCache
- **[SearchModule/Services/README.md](../../SearchModule/Services/README.md)** - SearchEngine

---

✨ Documentation par Derek Tremblay et Claude Sonnet 4.5
