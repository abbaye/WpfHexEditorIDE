# Resources - Ressources Intégrées

Ressources XAML et icônes intégrées au projet.

## 📁 Structure

```
Resources/
├── Dictionary/  → Dictionnaires XAML (styles, templates)
└── Icon/        → Icônes du projet
```

## 🎨 Dictionary/

Dictionnaires de ressources XAML pour styles et templates.

**Voir:** **[Dictionary/README.md](./Dictionary/README.md)**

## 🖼️ Icon/

Icônes et images utilisées dans le projet.

**Voir:** **[Icon/README.md](./Icon/README.md)**

## 💡 Utilisation

**Référencer un dictionnaire:**
```xml
<Window.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="/WPFHexaEditor;component/Resources/Dictionary/Styles.xaml" />
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Window.Resources>
```

**Référencer une icône:**
```xml
<Image Source="/WPFHexaEditor;component/Resources/Icon/app.ico" />
```

---

✨ Documentation par Derek Tremblay et Claude Sonnet 4.5
