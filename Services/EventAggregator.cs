using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace wolle.Services;

/// <summary>
/// Event aggregator for implementing event-based communication between components
/// to break circular dependencies in the application.
/// </summary>
public interface IEventAggregator : IDisposable
{
    /// <summary>
    /// Subscribes to events of type TEvent
    /// </summary>
    /// <typeparam name="TEvent">The event type to subscribe to</typeparam>
    /// <param name="handler">The event handler</param>
    /// <returns>A subscription token that can be used to unsubscribe</returns>
    IDisposable Subscribe<TEvent>(Action<TEvent> handler);

    /// <summary>
    /// Publishes an event of type TEvent
    /// </summary>
    /// <typeparam name="TEvent">The event type to publish</typeparam>
    /// <param name="event">The event data to publish</param>
    void Publish<TEvent>(TEvent @event);

    /// <summary>
    /// Unsubscribes from events using the subscription token
    /// </summary>
    /// <param name="subscription">The subscription token to unsubscribe</param>
    void Unsubscribe(IDisposable subscription);
}

/// <summary>
/// Implementation of IEventAggregator using thread-safe concurrent collections with weak references
/// to prevent memory leaks and automatic cleanup of dead subscribers
/// </summary>
public class EventAggregator : IEventAggregator, IDisposable
{
    private readonly ConcurrentDictionary<Type, ConcurrentBag<WeakReference>> _handlers = new();
    private readonly ConcurrentDictionary<IDisposable, (Type EventType, WeakReference HandlerRef)> _subscriptions = new();
    private readonly Timer _cleanupTimer;
    private bool _disposed = false;

    public EventAggregator()
    {
        // Clean up dead references every 30 seconds
        _cleanupTimer = new Timer(CleanupDeadReferences, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Subscribes to events of type TEvent
    /// </summary>
    public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        var eventType = typeof(TEvent);
        var handlerRef = new WeakReference(handler);

        _handlers.AddOrUpdate(eventType,
            addValueFactory: _ => new ConcurrentBag<WeakReference> { handlerRef },
            updateValueFactory: (_, existingHandlers) =>
            {
                existingHandlers.Add(handlerRef);
                return existingHandlers;
            });

        var subscription = new Subscription(() => UnsubscribeHandler<TEvent>(handlerRef));
        _subscriptions.TryAdd(subscription, (eventType, handlerRef));

        return subscription;
    }

    /// <summary>
    /// Publishes an event of type TEvent
    /// </summary>
    public void Publish<TEvent>(TEvent @event)
    {
        if (_handlers.TryGetValue(typeof(TEvent), out var handlers))
        {
            // Create a copy of handlers to avoid modification during enumeration
            WeakReference[] handlersCopy = handlers.ToArray();

            foreach (var handlerRef in handlersCopy)
            {
                try
                {
                    if (handlerRef.IsAlive && handlerRef.Target is Action<TEvent> handler)
                    {
                        handler(@event);
                    }
                }
                catch (Exception ex)
                {
                    // Log the exception but continue with other handlers
                    System.Diagnostics.Debug.WriteLine($"Event handler failed: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Unsubscribes from events using the subscription token
    /// </summary>
    public void Unsubscribe(IDisposable subscription)
    {
        if (_subscriptions.TryRemove(subscription, out var subscriptionInfo))
        {
            UnsubscribeHandler(subscriptionInfo.EventType, subscriptionInfo.HandlerRef);
        }
    }

    private void UnsubscribeHandler<TEvent>(WeakReference handlerRef)
    {
        UnsubscribeHandler(typeof(TEvent), handlerRef);
    }

    private void UnsubscribeHandler(Type eventType, WeakReference handlerRef)
    {
        if (_handlers.TryGetValue(eventType, out var handlers))
        {
            // For ConcurrentBag, we need to recreate the bag without the handler
            // since ConcurrentBag doesn't support direct removal
            var newHandlers = new ConcurrentBag<WeakReference>();
            foreach (var existingHandlerRef in handlers.Where(h => h != handlerRef))
            {
                newHandlers.Add(existingHandlerRef);
            }
            _handlers.TryUpdate(eventType, newHandlers, handlers);
        }
    }

    /// <summary>
    /// Cleans up dead references to prevent memory leaks
    /// </summary>
    private void CleanupDeadReferences(object? state)
    {
        if (_disposed) return;

        foreach (var eventType in _handlers.Keys.ToList())
        {
            if (_handlers.TryGetValue(eventType, out var handlers))
            {
                var liveHandlers = new ConcurrentBag<WeakReference>();
                foreach (var handlerRef in handlers)
                {
                    if (handlerRef.IsAlive)
                    {
                        liveHandlers.Add(handlerRef);
                    }
                }

                if (liveHandlers.Count != handlers.Count)
                {
                    _handlers.TryUpdate(eventType, liveHandlers, handlers);
                }
            }
        }

        // Also clean up dead subscriptions
        var deadSubscriptions = _subscriptions.Where(kv => !kv.Value.HandlerRef.IsAlive).ToList();
        foreach (var deadSubscription in deadSubscriptions)
        {
            _subscriptions.TryRemove(deadSubscription.Key, out _);
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
            
            // Clear all handlers and subscriptions
            _handlers.Clear();
            _subscriptions.Clear();
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