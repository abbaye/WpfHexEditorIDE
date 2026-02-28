# Commands - Implémentation du Pattern Command

Implémentation de `ICommand` pour pattern MVVM.

## 📁 Contenu

- **RelayCommand.cs** - Implémentation ICommand (générique et non-générique)

## 🎯 Classes

### RelayCommand

```csharp
// Non-générique
public class RelayCommand : ICommand
{
    public RelayCommand(Action execute, Func<bool> canExecute = null);
    public void Execute(object parameter);
    public bool CanExecute(object parameter);
    public event EventHandler CanExecuteChanged;
}

// Générique
public class RelayCommand<T> : ICommand
{
    public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null);
}

// Async
public class AsyncRelayCommand : ICommand
{
    public AsyncRelayCommand(Func<Task> executeAsync, Func<bool> canExecute = null);
}
```

## 💡 Exemples

**Commande simple:**
```csharp
public class MyViewModel
{
    public ICommand SaveCommand { get; }

    public MyViewModel()
    {
        SaveCommand = new RelayCommand(
            execute: () => Save(),
            canExecute: () => IsModified
        );
    }

    private void Save() { /* ... */ }
    public bool IsModified { get; set; }
}
```

**Binding XAML:**
```xml
<Button Command="{Binding SaveCommand}" Content="Save" />
```

**Commande async:**
```csharp
SearchCommand = new AsyncRelayCommand(
    executeAsync: async () => await SearchAsync(),
    canExecute: () => !IsSearching
);
```

## 🔗 Ressources

- **[ViewModels/README.md](../ViewModels/README.md)** - ViewModels utilisent RelayCommand
- **[SearchModule/ViewModels/README.md](../SearchModule/ViewModels/README.md)** - Exemples

---

✨ Documentation par Derek Tremblay et Claude Sonnet 4.5
