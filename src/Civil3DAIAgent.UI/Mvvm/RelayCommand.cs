using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Civil3DAIAgent.UI.Mvvm
{
    /// <summary>A basic synchronous <see cref="ICommand"/> backed by delegates.</summary>
    public sealed class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        /// <summary>Creates the command.</summary>
        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <inheritdoc />
        public event EventHandler CanExecuteChanged;

        /// <inheritdoc />
        public bool CanExecute(object parameter) => _canExecute == null || _canExecute();

        /// <inheritdoc />
        public void Execute(object parameter) => _execute();

        /// <summary>Requests WPF to re-query <see cref="CanExecute"/>.</summary>
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// An asynchronous <see cref="ICommand"/> that disables itself while its task is running so the
    /// user cannot start a second run. Used for the Start button.
    /// </summary>
    public sealed class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _executeAsync;
        private readonly Func<bool> _canExecute;
        private bool _isRunning;

        /// <summary>Creates the command.</summary>
        public AsyncRelayCommand(Func<Task> executeAsync, Func<bool> canExecute = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
        }

        /// <inheritdoc />
        public event EventHandler CanExecuteChanged;

        /// <inheritdoc />
        public bool CanExecute(object parameter) => !_isRunning && (_canExecute == null || _canExecute());

        /// <inheritdoc />
        public async void Execute(object parameter)
        {
            if (!CanExecute(parameter)) return;
            _isRunning = true;
            RaiseCanExecuteChanged();
            try
            {
                await _executeAsync();
            }
            finally
            {
                _isRunning = false;
                RaiseCanExecuteChanged();
            }
        }

        /// <summary>Requests WPF to re-query <see cref="CanExecute"/>.</summary>
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
