# Events - Arguments d'Événements Personnalisés

Classes d'arguments pour les événements du HexEditor.

## 📁 Contenu (4 fichiers)

| Fichier | Description |
|---------|-------------|
| **HexEditorEventArgs.cs** | 4 EventArgs: ByteModified, PositionChanged, SelectionChanged, Byte |
| **OperationCompletedEventArgs.cs** | Événement fin d'opération |
| **OperationProgressEventArgs.cs** | Événement progression |
| **OperationStateChangedEventArgs.cs** | Événement changement d'état |

## 🎯 Classes d'Événements

### HexEditorEventArgs.cs

```csharp
// Octet modifié
public class ByteModifiedEventArgs : EventArgs
{
    public long Position { get; set; }
    public byte OldValue { get; set; }
    public byte NewValue { get; set; }
    public ByteAction Action { get; set; }
}

// Position changée
public class PositionChangedEventArgs : EventArgs
{
    public long OldPosition { get; set; }
    public long NewPosition { get; set; }
}

// Sélection changée
public class HexSelectionChangedEventArgs : EventArgs
{
    public long SelectionStart { get; set; }
    public long SelectionStop { get; set; }
    public long SelectionLength { get; set; }
}

// Événement octet
public class ByteEventArgs : EventArgs
{
    public byte Value { get; set; }
    public long Position { get; set; }
}
```

### OperationCompletedEventArgs.cs

```csharp
public class OperationCompletedEventArgs : EventArgs
{
    public string OperationName { get; set; }
    public bool Success { get; set; }
    public long ElapsedMs { get; set; }
    public string ErrorMessage { get; set; }
}
```

### OperationProgressEventArgs.cs

```csharp
public class OperationProgressEventArgs : EventArgs
{
    public int ProgressPercentage { get; set; }
    public long ProcessedBytes { get; set; }
    public long TotalBytes { get; set; }
    public string StatusMessage { get; set; }
}
```

## 💡 Exemples

```csharp
// S'abonner aux événements
hexEditor.ByteModified += (s, e) =>
{
    Console.WriteLine($"Octet modifié à 0x{e.Position:X}: {e.OldValue:X2} → {e.NewValue:X2}");
};

hexEditor.SelectionChanged += (s, e) =>
{
    statusBar.Text = $"Sélection: {e.SelectionLength} octets";
};

hexEditor.OperationCompleted += (s, e) =>
{
    if (e.Success)
        MessageBox.Show($"{e.OperationName} terminé en {e.ElapsedMs}ms");
    else
        MessageBox.Show($"Erreur: {e.ErrorMessage}");
};
```

## 🔗 Ressources

- **[Core/EventArguments/README.md](../Core/EventArguments/README.md)** - Autres EventArgs

---

✨ Documentation par Derek Tremblay et Claude Sonnet 4.5
