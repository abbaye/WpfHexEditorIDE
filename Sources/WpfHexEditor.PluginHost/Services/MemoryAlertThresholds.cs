using System;

namespace WpfHexEditor.PluginHost.Services
{
    /// <summary>
    /// Configuration des seuils d'alerte mémoire pour le monitoring des plugins.
    /// </summary>
    public class MemoryAlertThresholds
    {
        /// <summary>
        /// Seuil d'avertissement en MB (jaune). Par défaut 500 MB.
        /// </summary>
        public int WarningThresholdMB { get; set; } = 500;

        /// <summary>
        /// Seuil élevé en MB (orange). Par défaut 750 MB.
        /// </summary>
        public int HighThresholdMB { get; set; } = 750;

        /// <summary>
        /// Seuil critique en MB (rouge). Par défaut 1000 MB.
        /// </summary>
        public int CriticalThresholdMB { get; set; } = 1000;

        /// <summary>
        /// Indique si les alertes mémoire sont activées.
        /// </summary>
        public bool EnableAlerts { get; set; } = true;

        /// <summary>
        /// Indique si la gradation de couleurs doit être affichée dans les monitors.
        /// </summary>
        public bool ShowColorGradation { get; set; } = true;

        /// <summary>
        /// Valide la cohérence des seuils configurés.
        /// </summary>
        /// <returns>True si la configuration est valide.</returns>
        public bool Validate()
        {
            // Les seuils doivent être positifs et dans l'ordre croissant
            return WarningThresholdMB > 0 &&
                   HighThresholdMB > WarningThresholdMB &&
                   CriticalThresholdMB > HighThresholdMB;
        }

        /// <summary>
        /// Crée une instance avec les valeurs par défaut recommandées.
        /// </summary>
        public static MemoryAlertThresholds CreateDefault()
        {
            return new MemoryAlertThresholds
            {
                WarningThresholdMB = 500,
                HighThresholdMB = 750,
                CriticalThresholdMB = 1000,
                EnableAlerts = true,
                ShowColorGradation = true
            };
        }

        /// <summary>
        /// Clone cette instance.
        /// </summary>
        public MemoryAlertThresholds Clone()
        {
            return new MemoryAlertThresholds
            {
                WarningThresholdMB = this.WarningThresholdMB,
                HighThresholdMB = this.HighThresholdMB,
                CriticalThresholdMB = this.CriticalThresholdMB,
                EnableAlerts = this.EnableAlerts,
                ShowColorGradation = this.ShowColorGradation
            };
        }
    }
}
