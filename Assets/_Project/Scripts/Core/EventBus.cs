using System;
using System.Collections.Generic;

namespace NO404.Core
{
    /// <summary>
    /// Typed global event bus. Only struct events are allowed so that no string based
    /// messaging or boxing-heavy payloads enter the codebase (GDD 20.8).
    /// A handler that throws is logged and isolated so the remaining subscribers still run.
    /// </summary>
    public static class EventBus
    {
        static readonly Dictionary<Type, List<Delegate>> Handlers = new Dictionary<Type, List<Delegate>>();
        static readonly List<Delegate> Dispatch = new List<Delegate>(8);

        public static void Subscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null) return;
            var type = typeof(T);
            if (!Handlers.TryGetValue(type, out var list))
            {
                list = new List<Delegate>(4);
                Handlers[type] = list;
            }

            if (!list.Contains(handler)) list.Add(handler);
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null) return;
            if (Handlers.TryGetValue(typeof(T), out var list)) list.Remove(handler);
        }

        public static void Publish<T>(T evt) where T : struct
        {
            if (!Handlers.TryGetValue(typeof(T), out var list) || list.Count == 0) return;

            // Copy first: handlers are allowed to subscribe/unsubscribe during dispatch.
            Dispatch.Clear();
            Dispatch.AddRange(list);

            for (int i = 0; i < Dispatch.Count; i++)
            {
                var typed = Dispatch[i] as Action<T>;
                if (typed == null) continue;
                try
                {
                    typed(evt);
                }
                catch (Exception e)
                {
                    Log.Error("EventBus", "Handler for " + typeof(T).Name + " threw: " + e);
                }
            }

            Dispatch.Clear();
        }

        public static int SubscriberCount<T>() where T : struct
        {
            return Handlers.TryGetValue(typeof(T), out var list) ? list.Count : 0;
        }

        /// <summary>Used on shutdown and by tests to guarantee a clean slate.</summary>
        public static void Clear()
        {
            Handlers.Clear();
            Dispatch.Clear();
        }
    }
}
