using System;
using System.Windows.Media;

namespace WpfHexEditor.PluginHost.Services
{
    /// <summary>
    /// Niveau d'alerte mémoire pour un plugin.
    /// </summary>
    public enum MemoryAlertLevel
    {
        /// <summary>
        /// Utilisation normale, en dessous du seuil d'avertissement.
        /// </summary>
        Normal = 0,

        /// <summary>
        /// Avertissement, entre le seuil d'avertissement et le seuil élevé.
        /// </summary>
        Warning = 1,

        /// <summary>
        /// Utilisation élevée, entre le seuil élevé et le seuil critique.
        /// </summary>
        High = 2,

        /// <summary>
        /// Utilisation critique, au-dessus du seuil critique.
        /// </summary>
        Critical = 3
    }

    /// <summary>
    /// Service d'évaluation des alertes mémoire pour les plugins.
    /// </summary>
    public class MemoryAlertService
    {
        private readonly MemoryAlertThresholds _thresholds;

        // Palette de couleurs pour les niveaux d'alerte
        private static readonly SolidColorBrush NormalBrush = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));    // Vert
        private static readonly SolidColorBrush WarningBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));   // Jaune/Ambre
        private static readonly SolidColorBrush HighBrush = new SolidColorBrush(Color.FromRgb(0xF9, 0x73, 0x16));      // Orange
        private static readonly SolidColorBrush CriticalBrush = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));  // Rouge

        static MemoryAlertService()
        {
            // Freeze les brushes pour optimisation
            NormalBrush.Freeze();
            WarningBrush.Freeze();
            HighBrush.Freeze();
            CriticalBrush.Freeze();
        }

        /// <summary>
        /// Initialise une nouvelle instance du service avec les seuils spécifiés.
        /// </summary>
        public MemoryAlertService(MemoryAlertThresholds thresholds)
        {
            _thresholds = thresholds ?? throw new ArgumentNullException(nameof(thresholds));

            if (!_thresholds.Validate())
            {
                throw new ArgumentException("Les seuils configurés ne sont pas valides.", nameof(thresholds));
            }
        }

        /// <summary>
        /// Évalue le niveau d'alerte pour une utilisation mémoire donnée.
        /// </summary>
        /// <param name="memoryMB">Utilisation mémoire en MB.</param>
        /// <returns>Le niveau d'alerte correspondant.</returns>
        public MemoryAlertLevel EvaluateMemoryUsage(int memoryMB)
        {
            if (!_thresholds.EnableAlerts)
                return MemoryAlertLevel.Normal;

            if (memoryMB >= _thresholds.CriticalThresholdMB)
                return MemoryAlertLevel.Critical;

            if (memoryMB >= _thresholds.HighThresholdMB)
                return MemoryAlertLevel.High;

            if (memoryMB >= _thresholds.WarningThresholdMB)
                return MemoryAlertLevel.Warning;

            return MemoryAlertLevel.Normal;
        }

        /// <summary>
        /// Obtient la couleur correspondant à un niveau d'alerte.
        /// </summary>
        /// <param name="level">Niveau d'alerte.</param>
        /// <returns>Brush de la couleur correspondante.</returns>
        public SolidColorBrush GetAlertColor(MemoryAlertLevel level)
        {
            if (!_thresholds.ShowColorGradation)
                return NormalBrush;

            return level switch
            {
                MemoryAlertLevel.Normal => NormalBrush,
                MemoryAlertLevel.Warning => WarningBrush,
                MemoryAlertLevel.High => HighBrush,
                MemoryAlertLevel.Critical => CriticalBrush,
                _ => NormalBrush
            };
        }

        /// <summary>
        /// Obtient l'icône (emoji ou caractère) correspondant à un niveau d'alerte.
        /// </summary>
        /// <param name="level">Niveau d'alerte.</param>
        /// <returns>Chaîne représentant l'icône.</returns>
        public string GetAlertIcon(MemoryAlertLevel level)
        {
            return level switch
            {
                MemoryAlertLevel.Normal => "🟢",
                MemoryAlertLevel.Warning => "🟡",
                MemoryAlertLevel.High => "🟠",
                MemoryAlertLevel.Critical => "🔴",
                _ => "⚪"
            };
        }

        /// <summary>
        /// Obtient le code hexadécimal de la couleur pour un niveau d'alerte.
        /// </summary>
        /// <param name="level">Niveau d'alerte.</param>
        /// <returns>Code couleur hexadécimal (ex: "#22C55E").</returns>
        public string GetAlertColorHex(MemoryAlertLevel level)
        {
            if (!_thresholds.ShowColorGradation)
                return "#22C55E";

            return level switch
            {
                MemoryAlertLevel.Normal => "#22C55E",
                MemoryAlertLevel.Warning => "#F59E0B",
                MemoryAlertLevel.High => "#F97316",
                MemoryAlertLevel.Critical => "#EF4444",
                _ => "#22C55E"
            };
        }

        /// <summary>
        /// Obtient un message descriptif pour un niveau d'alerte.
        /// </summary>
        /// <param name="level">Niveau d'alerte.</param>
        /// <param name="memoryMB">Utilisation mémoire en MB.</param>
        /// <returns>Message descriptif.</returns>
        public string GetAlertMessage(MemoryAlertLevel level, int memoryMB)
        {
            return level switch
            {
                MemoryAlertLevel.Normal => $"Utilisation normale ({memoryMB} MB)",
                MemoryAlertLevel.Warning => $"Avertissement: Utilisation élevée ({memoryMB} MB ≥ {_thresholds.WarningThresholdMB} MB)",
                MemoryAlertLevel.High => $"Attention: Utilisation très élevée ({memoryMB} MB ≥ {_thresholds.HighThresholdMB} MB)",
                MemoryAlertLevel.Critical => $"Critique: Utilisation excessive ({memoryMB} MB ≥ {_thresholds.CriticalThresholdMB} MB)",
                _ => $"Utilisation: {memoryMB} MB"
            };
        }

        /// <summary>
        /// Obtient les seuils configurés actuels.
        /// </summary>
        public MemoryAlertThresholds Thresholds => _thresholds;
    }
}
