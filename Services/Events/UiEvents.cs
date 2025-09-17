using System;
using System.Windows;

namespace wolle.Services.Events;

/// <summary>
/// Base class for all UI events
/// </summary>
public abstract record UiEvent;

/// <summary>
/// Event for displaying messages to the user
/// </summary>
/// <param name="Message">The message to display</param>
/// <param name="IsError">Whether this is an error message</param>
/// <param name="Duration">Duration to display the message (in milliseconds)</param>
public record ShowMessageEvent(string Message, bool IsError = false, int Duration = 3000) : UiEvent;

/// <summary>
/// Event for showing the main window
/// </summary>
/// <param name="Owner">Optional owner window</param>
public record ShowWindowEvent(Window? Owner = null) : UiEvent;

/// <summary>
/// Event for hiding the main window
/// </summary>
public record HideWindowEvent : UiEvent;

/// <summary>
/// Event for closing the main window
/// </summary>
/// <param name="ForceClose">Whether to force close without cleanup</param>
public record CloseWindowEvent(bool ForceClose = false) : UiEvent;

/// <summary>
/// Event for setting window position
/// </summary>
/// <param name="X">X coordinate</param>
/// <param name="Y">Y coordinate</param>
public record SetWindowPositionEvent(double X, double Y) : UiEvent;

/// <summary>
/// Event for updating progress
/// </summary>
/// <param name="IsVisible">Whether progress is visible</param>
/// <param name="ProgressValue">Progress value (0-100)</param>
/// <param name="Message">Progress message</param>
/// <param name="IsIndeterminate">Whether progress is indeterminate</param>
public record UpdateProgressEvent(bool IsVisible, double ProgressValue = 0, string? Message = null, bool IsIndeterminate = false) : UiEvent;

/// <summary>
/// Event for updating status
/// </summary>
/// <param name="Status">The status text</param>
/// <param name="IsError">Whether this indicates an error status</param>
public record UpdateStatusEvent(string Status, bool IsError = false) : UiEvent;

/// <summary>
/// Event for showing settings panel
/// </summary>
/// <param name="IsVisible">Whether settings panel should be visible</param>
public record ShowSettingsEvent(bool IsVisible) : UiEvent;

/// <summary>
/// Event for updating response content
/// </summary>
/// <param name="Content">The response content</param>
/// <param name="IsComplete">Whether the response is complete</param>
/// <param name="Append">Whether to append to existing content</param>
public record UpdateResponseEvent(string Content, bool IsComplete = false, bool Append = false) : UiEvent;

/// <summary>
/// Event for clearing response content
/// </summary>
public record ClearResponseEvent : UiEvent;

/// <summary>
/// Event for requesting window focus
/// </summary>
public record RequestFocusEvent : UiEvent;

/// <summary>
/// Event for setting window title
/// </summary>
/// <param name="Title">The window title</param>
public record SetWindowTitleEvent(string Title) : UiEvent;