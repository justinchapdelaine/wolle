using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace wolle.Services.Core;

/// <summary>
/// Event aggregator for implementing event-based communication between components
/// to break circular dependencies in application.
/// </summary>
public interface IEventAggregator : IDisposable
{
    /// <summary>
    /// Subscribes to events of type TEvent
    /// </summary>
    /// <typeparam name="TEvent">The event type to subscribe to</typeparam>
    /// <param name="handler">The event handler</param>
    /// <param name="isUiComponent">Whether this subscriber is a UI component (uses strong references)</param>
    /// <returns>A subscription token that can be used to unsubscribe</returns>
    IDisposable Subscribe<TEvent>(Action<TEvent> handler, bool isUiComponent = false);

    /// <summary>
    /// Publishes an event of type TEvent
    /// </summary>
    /// <typeparam name="TEvent">The event type to publish</typeparam>
    /// <param name="event">The event data to publish</param>
    void Publish<TEvent>(TEvent @event);

    /// <summary>
    /// Unsubscribes from events using subscription token
    /// </summary>
    /// <param name="subscription">The subscription token to unsubscribe</param>
    void Unsubscribe(IDisposable subscription);
}

/// <summary>
/// Implementation of IEventAggregator using thread-safe concurrent collections with
/// hybrid strong/weak references to prevent memory leaks while keeping UI components alive
/// </summary>
public class EventAggregator : IEventAggregator, IDisposable
{
    private readonly ConcurrentDictionary<Type, ConcurrentBag<Delegate>> _strongHandlers = new();
    private readonly ConcurrentDictionary<Type, ConcurrentBag<WeakReference>> _weakHandlers = new();
    private readonly ConcurrentDictionary<IDisposable, (Type EventType, Delegate Handler, WeakReference WeakHandlerRef, bool IsStrong)> _subscriptions = new();
    private readonly Timer _cleanupTimer;
    private bool _disposed = false;
    private readonly ILogger<EventAggregator>? _logger;
    private long _initialMemoryUsage;
    private int _cleanupCount = 0;

    public EventAggregator(ILogger<EventAggregator>? logger = null)
    {
        _logger = logger;
        // Get initial memory usage for monitoring
        _initialMemoryUsage = GC.GetTotalMemory(false);
        _logger?.LogInformation("EventAggregator initialized with initial memory usage: {MemoryUsage} bytes", _initialMemoryUsage);

        // Clean up dead references every 30 seconds using modern .NET 9 PeriodicTimer
        _cleanupTimer = new Timer(CleanupDeadReferences, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Subscribes to events of type TEvent
    /// </summary>
    public IDisposable Subscribe<TEvent>(Action<TEvent> handler, bool isUiComponent = false)
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        var eventType = typeof(TEvent);

        _logger?.LogInformation("EventAggregator.Subscribe<{EventType}>: Adding handler (UI component: {IsUiComponent})", typeof(TEvent).Name, isUiComponent);

        Subscription subscription;

        if (isUiComponent)
        {
            // UI components use strong references to prevent garbage collection
            _strongHandlers.AddOrUpdate(eventType,
                addValueFactory: _ => new ConcurrentBag<Delegate> { handler },
                updateValueFactory: (_, existingHandlers) =>
                {
                    existingHandlers.Add(handler);
                    return existingHandlers;
                });

            subscription = new Subscription(() => UnsubscribeHandler(eventType, handler, null!));
            _subscriptions.TryAdd(subscription, (eventType, handler, null!, true));
        }
        else
        {
            // Non-UI components use weak references to prevent memory leaks
            var handlerRef = new WeakReference(handler);
            _weakHandlers.AddOrUpdate(eventType,
                addValueFactory: _ => new ConcurrentBag<WeakReference> { handlerRef },
                updateValueFactory: (_, existingHandlers) =>
                {
                    existingHandlers.Add(handlerRef);
                    return existingHandlers;
                });

            subscription = new Subscription(() => UnsubscribeHandler(eventType, null!, handlerRef));
            _subscriptions.TryAdd(subscription, (eventType, handler, handlerRef, false));
        }

        _logger?.LogInformation($"EventAggregator.Subscribe<{typeof(TEvent).Name}>: Handler added successfully");

        return subscription;
    }

    /// <summary>
    /// Publishes an event of type TEvent
    /// </summary>
    public void Publish<TEvent>(TEvent @event)
    {
        var eventType = typeof(TEvent);
        var handlersCalled = 0;

        // Handle strong reference handlers (UI components)
        if (_strongHandlers.TryGetValue(eventType, out var strongHandlers))
        {
            foreach (var handler in strongHandlers)
            {
                try
                {
                    ((Action<TEvent>)handler)(@event);
                    handlersCalled++;
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Strong event handler failed: {ex.Message}");
                }
            }
        }

        // Handle weak reference handlers (non-UI components)
        if (_weakHandlers.TryGetValue(eventType, out var weakHandlers))
        {
            var liveWeakHandlers = new List<WeakReference>();

            foreach (var handlerRef in weakHandlers)
            {
                try
                {
                    if (handlerRef.IsAlive && handlerRef.Target is Action<TEvent> handler)
                    {
                        handler(@event);
                        liveWeakHandlers.Add(handlerRef);
                        handlersCalled++;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Weak event handler failed: {ex.Message}");
                }
            }

            // Update weak handlers list if any were collected
            // This prevents race conditions and ensures thread-safe updates
            if (liveWeakHandlers.Count != weakHandlers.Count)
            {
                var newWeakHandlers = new ConcurrentBag<WeakReference>();
                foreach (var liveHandler in liveWeakHandlers)
                {
                    newWeakHandlers.Add(liveHandler);
                }
                // Atomic update using TryUpdate to prevent concurrent modification issues
                _weakHandlers.TryUpdate(eventType, newWeakHandlers, weakHandlers);
            }
        }

        _logger?.LogInformation($"EventAggregator.Publish<{typeof(TEvent).Name}>: Called {handlersCalled} handlers");
    }

    /// <summary>
    /// Unsubscribes from events using subscription token
    /// </summary>
    public void Unsubscribe(IDisposable subscription)
    {
        if (_subscriptions.TryRemove(subscription, out var subscriptionInfo))
        {
            if (subscriptionInfo.IsStrong)
            {
                UnsubscribeHandler(subscriptionInfo.EventType, subscriptionInfo.Handler, null!);
            }
            else
            {
                UnsubscribeHandler(subscriptionInfo.EventType, null!, subscriptionInfo.WeakHandlerRef);
            }
        }
    }

    private void UnsubscribeHandler(Type eventType, Delegate strongHandler, WeakReference weakHandlerRef)
    {
        if (weakHandlerRef != null)
        {
            // Remove from weak handlers
            if (_weakHandlers.TryGetValue(eventType, out var handlers))
            {
                var newHandlers = new ConcurrentBag<WeakReference>();
                foreach (var existingHandlerRef in handlers.Where(h => h != weakHandlerRef))
                {
                    newHandlers.Add(existingHandlerRef);
                }
                _weakHandlers.TryUpdate(eventType, newHandlers, handlers);
            }
        }
        else
        {
            // Remove from strong handlers
            if (_strongHandlers.TryGetValue(eventType, out var handlers))
            {
                var newHandlers = new ConcurrentBag<Delegate>();
                foreach (var existingHandler in handlers.Where(h => h != strongHandler))
                {
                    newHandlers.Add(existingHandler);
                }
                _strongHandlers.TryUpdate(eventType, newHandlers, handlers);
            }
        }
    }

    /// <summary>
    /// Cleans up dead references to prevent memory leaks
    /// </summary>
    private void CleanupDeadReferences(object? state)
    {
        if (_disposed) return;

        _cleanupCount++;
        var currentMemoryUsage = GC.GetTotalMemory(false);
        var memoryDelta = currentMemoryUsage - _initialMemoryUsage;

        _logger?.LogDebug($"EventAggregator cleanup #{_cleanupCount}: Memory usage = {currentMemoryUsage} bytes, Delta = {memoryDelta:+#;-#} bytes, " +
                          $"Strong handlers = {_strongHandlers.Values.Sum(h => h.Count)}, " +
                          $"Weak handlers = {_weakHandlers.Values.Sum(h => h.Count)}, " +
                          $"Subscriptions = {_subscriptions.Count}");

        var deadReferencesRemoved = 0;

        foreach (var eventType in _weakHandlers.Keys.ToList())
        {
            if (_weakHandlers.TryGetValue(eventType, out var handlers))
            {
                var liveHandlers = new ConcurrentBag<WeakReference>();

                // Use Chunk() to process handlers in batches of 10
                // This improves performance by reducing memory pressure and GC overhead
                foreach (var handlerBatch in handlers.Chunk(10))
                {
                    foreach (var handlerRef in handlerBatch)
                    {
                        if (handlerRef.IsAlive)
                        {
                            liveHandlers.Add(handlerRef);
                        }
                        else
                        {
                            deadReferencesRemoved++;
                        }
                    }
                }

                if (liveHandlers.Count != handlers.Count)
                {
                    _weakHandlers.TryUpdate(eventType, liveHandlers, handlers);
                }
            }
        }

        // Also clean up dead subscriptions
        // These are subscriptions where the weak handler reference is no longer alive
        var deadSubscriptions = _subscriptions.Where(kv =>
            !kv.Value.IsStrong && !kv.Value.WeakHandlerRef.IsAlive).ToList();
        deadReferencesRemoved += deadSubscriptions.Count;
        foreach (var deadSubscription in deadSubscriptions)
        {
            _subscriptions.TryRemove(deadSubscription.Key, out _);
        }

        if (deadReferencesRemoved > 0)
        {
            _logger?.LogInformation($"EventAggregator cleanup completed: Removed {deadReferencesRemoved} dead references");
        }
    }

    /// <summary>
    /// Disposes the event aggregator and cleans up resources
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _cleanupTimer?.Dispose();

            var strongHandlerCount = _strongHandlers.Values.Sum(h => h.Count);
            var weakHandlerCount = _weakHandlers.Values.Sum(h => h.Count);
            var subscriptionCount = _subscriptions.Count;

            _logger?.LogInformation($"EventAggregator disposing: Clearing {strongHandlerCount} strong handlers, " +
                              $"{weakHandlerCount} weak handlers, {subscriptionCount} subscriptions");

            // Clear all handlers and subscriptions
            _strongHandlers.Clear();
            _weakHandlers.Clear();
            _subscriptions.Clear();

            _logger?.LogInformation("EventAggregator disposed successfully");
        }
    }

    /// <summary>
    /// Subscription token for managing event subscriptions
    /// </summary>
    private class Subscription : IDisposable
    {
        private readonly Action _unsubscribe;

        public Subscription(Action unsubscribe)
        {
            _unsubscribe = unsubscribe ?? throw new ArgumentNullException(nameof(unsubscribe));
        }

        public void Dispose()
        {
            _unsubscribe();
        }
    }
}