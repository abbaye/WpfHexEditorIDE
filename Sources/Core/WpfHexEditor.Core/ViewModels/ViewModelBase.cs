//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////
// Project: WpfHexEditor.Core
// File: ViewModelBase.cs
// Description: Base class for INotifyPropertyChanged — eliminates inline duplication
//              across all ViewModels in the solution.
// Architecture notes: Placed in Core so every project (Shell, App, Plugins) can use it
//                     without adding a new dependency.
//////////////////////////////////////////////

using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfHexEditor.Core.ViewModels
{
    /// <summary>
    /// Base class that provides a standard <see cref="INotifyPropertyChanged"/>
    /// implementation with a <see cref="SetField{T}"/> helper for change-guard setters.
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        /// <inheritdoc />
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Raises <see cref="PropertyChanged"/> for the given property name.</summary>
        protected virtual void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>
        /// Sets <paramref name="field"/> to <paramref name="value"/> and fires
        /// <see cref="PropertyChanged"/> only when the value actually changed.
        /// </summary>
        /// <returns><see langword="true"/> if the value changed and the event was raised.</returns>
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }

        // ── First-load gate ───────────────────────────────────────────────────

        private bool _hasLoadedOnce;

        /// <summary>
        /// Always returns <see langword="false"/> — panel loading overlays are fully suppressed.
        /// The <c>_hasLoadedOnce</c> field is still tracked so callers compile unchanged.
        /// Restore <c>return !_hasLoadedOnce</c> (and flip the assignment) to re-enable the
        /// first-load indicator if ever needed.
        /// </summary>
        protected bool TryFirstLoad()
        {
            _hasLoadedOnce = true;
            return false;
        }
    }
}
