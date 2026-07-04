using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Civil3DAIAgent.UI.Mvvm
{
    /// <summary>
    /// Minimal <see cref="INotifyPropertyChanged"/> base class for view models. Provides
    /// <see cref="SetProperty{T}"/> to raise change notifications with the least boilerplate.
    /// </summary>
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        /// <inheritdoc />
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>Raises <see cref="PropertyChanged"/> for the given property name.</summary>
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        /// <summary>
        /// Sets <paramref name="field"/> to <paramref name="value"/> and raises change notification if
        /// the value actually changed. Returns true when a change occurred.
        /// </summary>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
