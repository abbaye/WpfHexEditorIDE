# 📘 CLAUDE.md — Global Engineering & Governance Rules (v2.3)

---

## 1️⃣ Initialisation Obligatoire du Contexte

**À chaque début de session, Claude doit lire :**

1. `~/.claude/CLAUDE.md` (règles globales)  
2. `CLAUDE.md` du projet courant (racine du repo si présent)  
3. `~/.claude/projects/<ProjectName>/memory/`  
   - `MEMORY.md` et tous fichiers référencés

⚠️ Interdiction de commencer à coder ou exécuter toute action avant cette lecture.

Objectifs : continuité architecturale, respect des décisions passées, cohérence, maintien des conventions.

---

## 2️⃣ 🔐 Git — Gouvernance et Autorisations

### 2.1 Mode Lecture Seule (par défaut)

- Autorisé : `status`, `diff`, `log`, `blame`, `show`  
- Interdit : `commit`, `push`, `pull`, `branch`, `merge`, `rebase`, `tag`, `amend`, `reset`, `cherry-pick`

### 2.2 Mode Plan Approuvé → Autorisation Étendue

Si un plan est **présenté et approuvé** :  

- Toutes les actions Git nécessaires pour le plan sont autorisées **sans redemander confirmation**  
- Retour automatique en lecture seule à la fin du plan ou de la session  

### 2.3 Restrictions

- Pas de modification hors plan  
- Pas de push forcé non prévu  
- Pas de changement d’architecture non planifié  

---

## 3️⃣ Architecture & Separation of Concerns

- 1 fichier = 1 responsabilité  
- Ne jamais mélanger : UI ↔ logique métier, Infrastructure ↔ domaine, BD ↔ contrôleur  
- Respect strict : Controllers, Services, Repositories, Models, Views, Infrastructure  
- Nouveaux éléments → nouvelle couche dédiée  

---

## 4️⃣ Qualité du Code

- Fonctions : 15–25 lignes max, 1 responsabilité, nom explicite, pas d’abréviations  
- Lisibilité : early return > nested if, guard clauses, pas de side-effects cachés, pas de variables globales implicites  

---

## 5️⃣ Patterns & Maintenabilité

- Appliquer patterns si amélioration : Repository, Strategy, Factory, Observer, Mediator, CQRS, Pipeline, State, Adapter  
- Favoriser composition > héritage  
- Respect strict SOLID  
- Documenter le pattern utilisé  

---

## 6️⃣ Discipline de Correction (Root Cause First)

- Identifier la cause racine  
- Évaluer l’impact  
- Corriger proprement  
- Fix temporaire = mention obligatoire + plan correctif définitif  

---

## 7️⃣ Performance & Scalabilité

- Évaluer complexité algorithmique  
- Minimiser allocations et copies mémoire  
- Streaming / lazy loading pour datasets volumineux  
- Virtualisation si nécessaire  
- Benchmarkable et scalable  

--- 

## 7️⃣b WPF Global Theme Enforcement (Mandatory)

- **Tout ce qui est créé ou modifié par Claude doit respecter le thème global du projet.**  
  - Fenêtres (`Window`)  
  - Contrôles (`UserControl`, `CustomControl`)  
  - DataTemplates, Styles, Brushes, ResourceDictionaries  
  - Dialogs et popups  
- Le thème global (Light, Dark, ou personnalisé) **doit être appliqué automatiquement** à chaque composant WPF.  
- Aucun composant WPF **ne peut rester sans thème ni style défini**.  
- Si un contrôle temporaire ou spécifique doit déroger au thème global, il faut :  
  1. Mentionner l’exception dans le **PLAN**  
  2. Documenter la raison dans la **mémoire du projet**  

### Documentation automatique

- Chaque fichier `.CS` ou `.XAML` généré par Claude doit indiquer :  
  - Quel thème global est utilisé  
  - Les ResourceDictionaries appliqués  
  - Les styles et templates particuliers, si besoin  
- Les fichiers `.MD` correspondants sont générés ou mis à jour automatiquement pour refléter la conformité au thème global.  

### Validation

- À la fin de chaque PLAN ou correction (`COR`), Claude doit vérifier que **tous les composants WPF du plan sont conformes au thème global**.  
- Toute violation = plan considéré **incomplet** jusqu’à mise en conformité.

---

## 7️⃣c Dockable Panels — VS-Like Standard

- **Tout nouveau Panel Dockable créé par Claude doit respecter le standard Visual Studio-Like** :  
  - Barre d’outils (`Toolbar`) en haut du panel, configurable via XAML/Code-behind  
  - Possibilité de **drag & drop**, dock, float et tab group  
  - Suivi des **ResourceDictionaries et thèmes globaux** (voir 7️⃣b)  
  - Styles, brushes et icons conformes au thème global du projet  
  - Composants WPF internes doivent respecter la séparation des responsabilités (Controls, ViewModels, Services)  

### Documentation automatique

- Chaque nouveau Panel Dockable doit générer ou mettre à jour un `.MD` associé :  
  - Nom du panel  
  - Fonctionnalité / responsabilité  
  - Composants internes utilisés  
  - Toolbar items et bindings  
  - Conformité thème global (oui/non)  
- Mise à jour automatique à la **fin du PLAN** si le panel a été modifié ou ajouté  

### Validation

- Après exécution d’un PLAN ou correction (`COR`), Claude doit vérifier :  
  - Le panel respecte le **layout VS-Like**  
  - La **toolbar est présente et fonctionnelle**  
  - Le panel est conforme au **thème global WPF**  
- Toute violation = PLAN considéré **incomplet** jusqu’à mise en conformité

---

## 7️⃣d Editor Plug-In — Full IDE Integration Requirement

- **Tout nouveau module Editor (“plug-in”) créé par Claude doit prévoir l’intégration complète dans l’IDE WPFHexEditor.App**.  

### Obligations

Chaque nouveau module doit inclure dans la planification et la génération :  
1. **Pop-up menus / context menus**  
2. **Toolbar / buttons / shortcuts**  
3. **Options Panel / Settings integration**  
4. **Status Bar updates / Personality display**  
5. **Search / Find / Replace functionality**  
6. **Docking panel structure VS-Like**  
7. **Conformité au thème global WPF** (voir 7️⃣b)  
8. **Documentation automatique `.MD` pour le module**  

### Planification obligatoire

- Chaque plan (`PLAN:`) pour un nouveau module **doit inclure** :  
  - Architecture globale du module  
  - Tous composants WPF à créer ou modifier  
  - Liste des services et ViewModels nécessaires  
  - Points de personnalisation (toolbar, statusbar, options)  
  - Intégration dans les workflows existants (Search, Undo/Redo, Docking, Themes)  

### Validation

- À la fin du plan ou d’une correction (`COR:`) :  
  - Claude doit vérifier que **tous les composants listés ci-dessus sont inclus et fonctionnels**  
  - Toute omission = plan considéré **incomplet** jusqu’à correction  
- Chaque fichier `.CS` / `.XAML` créé doit générer ou mettre à jour son `.MD` de documentation  
- Le `README.md` principal doit être révisé automatiquement pour refléter l’ajout du nouveau module  

### Workflow

1. Déclencheur : `PLAN: New Editor Module`  
2. Analyse complète de l’IDE et modules existants  
3. Plan détaillé incluant **tous composants WPF et features**  
4. Exécution et génération des fichiers  
5. Validation des thèmes, docking, toolbar, menus, statusbar, search, options  
6. Mise à jour mémoire projet et documentation `.MD`  

> ⚠️ Aucun module Editor ne doit être créé sans ce workflow complet.

---

## 8️⃣ Workflow Orchestrateur Obligatoire

Pour CHAQUE demande :  

1. **Analyse** : reformuler, identifier contraintes et risques  
2. **Identification des domaines** : .NET, WPF, sécurité, DevOps, BD, performance  
3. **Chargement des skills** : conventions, patterns existants, mémoire projet  
4. **Planification** : découpage clair, fichiers impactés, risques identifiés  
5. **Exécution** : code propre, architecture respectée, séparation stricte  
6. **Validation** : SOLID, lisibilité, performance, absence de duplication  

⚠️ Interdiction de sauter l’orchestrateur.

---

## 8️⃣a Sub-Project Validation — Granularity & Maintainability

- **Objectif** : S’assurer que toute nouvelle fonctionnalité est intégrée dans le(s) sous-projet(s) approprié(s) pour **maximiser la maintenabilité, la granularité et la cohérence**.  

### Étapes obligatoires lors de la planification (`PLAN:`)

1. **Analyse de la fonctionnalité**  
   - Identifier clairement les responsabilités et composants nécessaires  
   - Déterminer l’impact sur l’IDE et les modules existants  

2. **Validation des sous-projets existants**  
   - Vérifier si un sous-projet existant peut accueillir la fonctionnalité  
   - Considérer : séparation des responsabilités, dépendances, testabilité, modularité  

3. **Création de sous-projet(s) si nécessaire**  
   - Si aucun sous-projet existant n’est approprié :  
     - Créer automatiquement un nouveau sous-projet dédié  
     - Nommer le sous-projet de façon descriptive et cohérente  
     - Documenter dans la **mémoire projet** et `.MD` associé  

4. **Documentation dans le plan**  
   - Indiquer le sous-projet choisi ou créé pour la fonctionnalité  
   - Détailler tous les fichiers/folders qui seront impactés  
   - Prévoir l’intégration avec les workflows existants (Docking Panels, Themes WPF, Editor Modules, etc.)  

5. **Validation finale avant exécution**  
   - Vérifier que la granularité et modularité sont respectées  
   - Aucune exécution de code ou modification ne peut se faire tant que la validation de sous-projet n’est pas complétée  

### Résultat attendu

- Chaque fonctionnalité est :  
  - Placée dans le sous-projet le plus logique  
  - Documentée et traçable dans la mémoire projet  
  - Conforme aux standards de l’IDE et aux workflows existants  

> ⚠️ Claude **ne doit jamais placer une nouvelle fonctionnalité de manière arbitraire** dans un projet existant sans cette validation.

---

### 8️⃣b PLAN Execution Approval — Human GO Required

- **Règle stricte** : Claude **ne doit jamais exécuter un plan automatiquement** après l’avoir généré.  
- **Validation obligatoire** :  
  1. Claude élabore le PLAN complet selon le workflow : analyse, identification des domaines, sous-projets, modules, docking panels, thèmes WPF, etc.  
  2. **Attendre explicitement l’instruction `GO`** de l’utilisateur pour démarrer l’exécution.  
- **Implications** :  
  - Aucun code, commit, création de fichiers `.CS/.XAML` ou mise à jour de mémoire n’est permis avant `GO`.  
  - L’approbation humaine garantit que les changements et la documentation sont validés avant exécution.  
- **Workflow intégré** :  
  ```mermaid
  flowchart TD
      PLAN[Generate Plan] --> WAIT[Wait for GO command]
      WAIT -->|GO| EXEC[Execute Plan → Update Memory / Docs / IDE]
      WAIT -->|No GO| HOLD[Hold Execution]

---

## 9️⃣ Mémoire Projet (Claude Memory System)

- Tous plans approuvés ou corrections **doivent** mettre à jour : `~/.claude/projects/<ProjectName>/memory/`  
- Organisation : `architecture-decisions.md`, `performance-notes.md`, `rendering-pipeline.md`, `auth-flow.md`, `db-migrations.md`  
- Contenu minimum : date, objectif, décisions, modifs structurelles, impact perf, points de vigilance  
- Interdiction : fichiers `.md` dans repo source  

---

## 🔟 Mode Excellence

- Code production-ready  
- Niveau senior / staff engineer  
- Standard open-source top mondial  
- Question permanente : “Est-ce acceptable dans un projet top mondial ?”  

---

## 11️⃣ Trigger Words — Workflow Automatisé

| Mot-clé | Effet |
|----------|-------|
| `PLAN:` | Passe en **mode plan** obligatoire avant exécution. Plan structuré + approbation obligatoire. |
| `BUG:`  | Déclenche workflow strict : GitHub issue obligatoire, root cause, plan, mémoire mise à jour. |
| `COR:`  | Correction rapide pour problèmes légers, pas d’issue mais documentation mémoire obligatoire. |

---

### 11.1 PLAN Mode

- Analyse complète, identification des domaines, chargement des skills, planification détaillée  
- Blocage de toute exécution tant que plan non approuvé  
- Après approbation → autorisations Git étendues + mémoire obligatoire  

#### 🔹 Documentation automatique
- Pour chaque fichier `.CS` ajouté ou modifié :  
  - Créer ou mettre à jour automatiquement un fichier `.MD` correspondant  
  - Inclut nom de la classe/fichier, description, méthodes principales, design patterns, exemples  
- Mise à jour automatique à la **fin du plan** si le fichier a changé  
- Après chaque plan, **réviser automatiquement le `README.md` principal**  

---

### 11.2 BUG Workflow

- Création GitHub issue obligatoire : title, description, reproduction steps, root cause, resolution  
- Numéro issue référencé dans la session et commits (`Fixes #<issueNumber>`)  
- Root cause et plan de résolution obligatoire  
- Mise à jour mémoire obligatoire après exécution  

---

### 11.3 COR Workflow

- Pour petits problèmes légers uniquement  
- Correction rapide autorisée  
- Pas d’issue GitHub mais documentation mémoire obligatoire  
- Commentaire explicatif en anglais obligatoire  

---

### 11.4 DOC Trigger — Complete Documentation & Missing Items

- **Mot-clé** : `DOC:`  
- **Effet** : Lorsque ce mot est lancé, Claude doit :  
  1. Scanner **tous les sous-dossiers** de la solution/projet  
  2. Identifier **tous les objets, classes, modules, fichiers `.CS` ou `.XAML`** qui n’ont **pas de documentation `.MD` associée**  
     - Si un fichier ou objet est **sans documentation**, Claude **doit la créer automatiquement**  
  3. Mettre à jour **tous les fichiers de documentation existants** :  
     - Documentation `.MD` des classes/fichiers  
     - `README.md` principal  
     - Architecture overview / diagrammes  
     - WIKI interne si existant  
  4. Vérifier que **tous les liens internes et externes** fonctionnent correctement  
  5. Générer un **résumé global** des changements de documentation  
  6. Mettre à jour la **mémoire du projet** avec le log de la mise à jour complète  

- **Règles** :  
  - Aucune exécution de code ou modification du projet tant que la documentation n’est pas complète si `DOC:` est lancé.  
  - Toutes les nouvelles classes ou objets détectés **doivent avoir leur `.MD` créé automatiquement**, incluant :  
    - Nom de l’objet / classe  
    - Responsabilité principale  
    - Méthodes / propriétés principales  
    - Patterns utilisés  
    - Exemple d’utilisation si applicable  
  - La mise à jour doit inclure les **diagrammes Mermaid**, **ResourceDictionaries WPF**, **panels dockables**, et **modules Editor** pour assurer une cohérence totale de la solution.

---

### 11.5 DOCR Trigger — Rapid Documentation Review (Last 24h)

- **Mot-clé** : `DOCR:`  
- **Effet** : Lorsque ce mot est lancé, Claude doit :  
  1. Scanner **tous les fichiers modifiés dans les 24 dernières heures** selon l’historique des commits Git  
  2. Vérifier et mettre à jour **la documentation `.MD` correspondante** pour ces fichiers uniquement  
  3. Mettre à jour **README.md principal et architecture overview** si des changements récents impactent leur contenu  
  4. Vérifier que **tous les liens internes/externes affectés** par les modifications récentes fonctionnent  
  5. Mettre à jour la **mémoire projet** avec un résumé des modifications de documentation récentes  

- **Règles** :  
  - Pas de scan complet de la solution → économie de tokens et gain de rapidité  
  - Si un fichier récent n’a pas de documentation, Claude **doit la créer automatiquement**  
  - Validation WPF themes, Dockable Panels et modules Editor si impactés par les changements récents  

- **Objectif** : Permettre une **révision rapide de la documentation** sans relancer un scan complet `DOC:`, tout en maintenant la cohérence de la solution.

---

## 12️⃣ File Header Enforcement

Chaque nouveau fichier doit inclure :  

```c
// ==========================================================
// Project: <ProjectName>
// File: <FileName>
// Author: <Auto or Git config>
// Created: <YYYY-MM-DD>
// Description:
//     Clear explanation of file responsibility.
//
// Architecture Notes:
//     Patterns used (if any)
//     Important design decisions
//
// ==========================================================

## 13️⃣ Comment Language Policy

Par défaut : anglais
Explicatif, technique, clair
Abréviations locales ou mélange FR/EN interdits
Exception si explicitement demandé : Write comments in French.

## 14️⃣ Option encore plus stricte (recommandée)

Une session BUG ou un plan approuvé ne peut pas se terminer sans :
1. GitHub issue créé (si applicable)
2. Documentation complète de la cause racine
3. Stratégie de résolution définie
4. Mise à jour mémoire projet obligatoire


## 15️⃣ Workflow Mermaid Diagram — Global Plan/Execution

flowchart TD
    A[Start] --> B[Load Context]
    B --> C{Trigger Word?}
    C -->|PLAN| D[Generate PLAN → Analyze → Domains → Skills → Sub-Project Validation]
    C -->|BUG| E[BUG Workflow]
    C -->|COR| F[Quick Fix]
    C -->|DOC| G[Complete Documentation Update]
    C -->|DOCR| H[Rapid Doc Review - Last 24h]
    
    D --> I[WAIT for Human GO]
    I -->|GO| J[Execute Plan]
    I -->|No GO| K[Hold Execution]
    
    J --> L[Apply Themes WPF, Dockable Panels, Editor Modules]
    L --> M[Generate/Update .MD for .CS/.XAML files → Update README & WIKI → Update Memory]
    E --> N[Open GitHub Issue → Root Cause → Resolution → Update Memory]
    F --> O[Apply Minor Fix → Update Memory]
    G --> P[Scan All Files → Update Docs → README & WIKI → Verify Links → Update Memory]
    H --> Q[Scan Last 24h Commits → Update Docs → Update Memory]
    
    M --> R[Return to Idle / Wait Next Command]
    N --> R
    O --> R
    P --> R
    Q --> R

## 16️⃣ Workflow Mermaid Diagram — Trigger / Documentation Flow

flowchart TD
    PLAN[PLAN Trigger] --> WAITGO[Wait for GO]
    WAITGO -->|GO| DOC[Generate / Update .MD for .CS & .XAML files]
    DOC --> README[Review / Update README.md & WIKI]
    DOC --> THEMES[Check WPF Theme Compliance]
    DOC --> PANELS[Check Dockable Panels VS-Like]
    DOC --> EDITOR[Check Editor Modules Full Integration]
    
    BUG[BUG Trigger] --> ISSUE[Create GitHub Issue → Root Cause → Resolution]
    ISSUE --> MEM[Update Project Memory]
    
    COR[COR Trigger] --> MEM2[Apply Minor Fix → Update Project Memory]
    
    DOC2[DOC Trigger] --> ALLDOCS[Full Doc Scan & Update All Files → README & WIKI → Verify Links → Update Memory]
    DOCR[DOCR Trigger] --> RECENT[Scan Last 24h Commits → Update Docs & Memory]
    
    MEM --> END[End]
    MEM2 --> END
    ALLDOCS --> END
    RECENT --> END
    PLAN --> WAITGO
    DOC --> END

    ##17 Comit restriction
    - Claude ne doit pas comiter le dossier DevPlans