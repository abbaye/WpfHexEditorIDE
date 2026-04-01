# Properties - Métadonnées et Ressources du Projet

Fichiers de métadonnées et ressources multilingues du projet.

## 📁 Contenu (8 fichiers)

| Fichier | Description |
|---------|-------------|
| **AssemblyInfo.cs** | Métadonnées de l'assembly |
| **Resources.Designer.cs** | Ressources anglaises (défaut) |
| **Resources.fr-CA.Designer.cs** | Ressources françaises (Canada) |
| **Resources.pl-PL.Designer.cs** | Ressources polonaises |
| **Resources.pt-BR.Designer.cs** | Ressources portugaises (Brésil) |
| **Resources.ru-RU.Designer.cs** | Ressources russes |
| **Resources.zh-CN1.Designer.cs** | Ressources chinoises (simplifié) |
| **Settings.Designer.cs** | Paramètres application |

## 🌐 Support Multilingue

**6 langues supportées:**
- 🇺🇸 Anglais (EN) - Défaut
- 🇫🇷 Français (FR-CA)
- 🇵🇱 Polonais (PL-PL)
- 🇧🇷 Portugais (PT-BR)
- 🇷🇺 Russe (RU-RU)
- 🇨🇳 Chinois (ZH-CN)

## 💡 Utilisation

**Changer la langue:**
```csharp
Thread.CurrentThread.CurrentUICulture = new CultureInfo("fr-CA");
```

**Accéder aux ressources:**
```csharp
string text = Properties.Resources.ButtonSave;
```

## 📊 Structure des Ressources

```
Properties/
├── AssemblyInfo.cs (Version, Copyright)
├── Resources.resx (EN - default)
├── Resources.fr-CA.resx
├── Resources.pl-PL.resx
├── Resources.pt-BR.resx
├── Resources.ru-RU.resx
└── Resources.zh-CN1.resx
```

---

✨ Documentation par Derek Tremblay et Claude Sonnet 4.5
