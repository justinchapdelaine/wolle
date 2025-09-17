using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace wolle.Services;

/// <summary>
/// Event aggregator for implementing event-based communication between components
/// to break circular dependencies in the application.
/// </summary>
public interface IEventAggregator
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
/// Implementation of IEventAggregator using thread-safe concurrent collections
/// </summary>
public class EventAggregator : IEventAggregator
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();
    private readonly ConcurrentDictionary<IDisposable, (Type EventType, Delegate Handler)> _subscriptions = new();

    /// <summary>
    /// Subscribes to events of type TEvent
    /// </summary>
    public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        var eventType = typeof(TEvent);
        _handlers.AddOrUpdate(eventType,
            addValueFactory: _ => new List<Delegate> { handler },
            updateValueFactory: (_, existingHandlers) =>
            {
                lock (existingHandlers)
                {
                    existingHandlers.Add(handler);
                    return existingHandlers;
                }
            });

        var subscription = new Subscription(() => UnsubscribeHandler<TEvent>(handler));
        _subscriptions.TryAdd(subscription, (eventType, handler));

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
            Delegate[] handlersCopy;
            lock (handlers)
            {
                handlersCopy = handlers.ToArray();
            }

            foreach (var handler in handlersCopy)
            {
                try
                {
                    ((Action<TEvent>)handler)(@event);
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
            UnsubscribeHandler(subscriptionInfo.EventType, subscriptionInfo.Handler);
        }
    }

    private void UnsubscribeHandler<TEvent>(Action<TEvent> handler)
    {
        UnsubscribeHandler(typeof(TEvent), handler);
    }

    private void UnsubscribeHandler(Type eventType, Delegate handler)
    {
        if (_handlers.TryGetValue(eventType, out var handlers))
        {
            lock (handlers)
            {
                handlers.Remove(handler);
            }
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