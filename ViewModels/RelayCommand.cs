using System;
using System.Windows.Input;

namespace wolle.ViewModels;

/// <summary>
/// A command that relays its functionality to other objects by invoking delegates
/// </summary>
/// <remarks>
/// This is a non-generic implementation of the RelayCommand pattern for WPF applications
/// </remarks>
public class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    private readonly Action _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    private readonly Func<bool>? _canExecute = canExecute;

    /// <summary>
    /// Occurs when changes occur that affect whether or not the command should execute
    /// </summary>
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    /// <summary>
    /// Determines whether the non-generic command can execute in its current state
    /// </summary>
    /// <param name="parameter">Data used by the command (not used in this non-generic version)</param>
    /// <returns>True if this command can be executed; otherwise, false</returns>
    public bool CanExecute(object? parameter)
    {
        return _canExecute?.Invoke() ?? true;
    }

    /// <summary>
    /// Executes the non-generic command
    /// </summary>
    /// <param name="parameter">Data used by the command (not used in this non-generic version)</param>
    public void Execute(object? parameter)
    {
        _execute();
    }
}

/// <summary>
/// A generic command that relays its functionality to other objects by invoking delegates
/// </summary>
/// <remarks>
/// This is a generic implementation of the RelayCommand pattern for WPF applications
/// that allows passing a parameter of type T to the execute and canExecute methods
/// </remarks>
/// <typeparam name="T">The type of the parameter passed to the command</typeparam>
public class RelayCommand<T>(Action<T?> execute, Func<T?, bool>? canExecute = null) : ICommand
{
    private readonly Action<T?> _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    private readonly Func<T?, bool>? _canExecute = canExecute;

    /// <summary>
    /// Occurs when changes occur that affect whether or not the command should execute
    /// </summary>
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    /// <summary>
    /// Determines whether the generic command can execute in its current state
    /// </summary>
    /// <param name="parameter">Data used by the command. If the command does not require data, this object can be set to null</param>
    /// <returns>True if this command can be executed; otherwise, false</returns>
    public bool CanExecute(object? parameter)
    {
        return _canExecute?.Invoke((T?)parameter) ?? true;
    }

    /// <summary>
    /// Executes the generic command
    /// </summary>
    /// <param name="parameter">Data used by the command. If the command does not require data, this object can be set to null</param>
    public void Execute(object? parameter)
    {
        _execute((T?)parameter);
    }
}