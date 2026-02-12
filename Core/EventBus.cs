using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace InspectorManager.Core
{
    /// <summary>
    /// 疎結合なイベント通知システム。
    /// Publish-Subscribeパターンによりコンポーネント間の通信を実現。
    /// </summary>
    public class EventBus
    {
        private static EventBus _instance;
        public static EventBus Instance => _instance ??= new EventBus();

        private readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>();
        private readonly object _lock = new object();

        /// <summary>
        /// イベントを購読する
        /// </summary>
        public void Subscribe<T>(Action<T> handler)
        {
            if (handler == null) return;

            lock (_lock)
            {
                var type = typeof(T);
                if (!_handlers.TryGetValue(type, out var list))
                {
                    list = new List<Delegate>();
                    _handlers[type] = list;
                }
                list.Add(handler);
            }
        }

        /// <summary>
        /// イベントの購読を解除する
        /// </summary>
        public void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null) return;

            lock (_lock)
            {
                var type = typeof(T);
                if (_handlers.TryGetValue(type, out var list))
                {
                    list.Remove(handler);
                }
            }
        }

        /// <summary>
        /// イベントを発行する
        /// </summary>
        public void Publish<T>(T eventData)
        {
            List<Delegate> handlersCopy;

            lock (_lock)
            {
                var type = typeof(T);
                if (!_handlers.TryGetValue(type, out var list) || list.Count == 0)
                {
                    return;
                }
                handlersCopy = new List<Delegate>(list);
            }

            foreach (var handler in handlersCopy)
            {
                try
                {
                    ((Action<T>)handler)?.Invoke(eventData);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogException(ex);
                }
            }
        }

        /// <summary>
        /// すべての購読を解除する
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _handlers.Clear();
            }
        }
    }

    #region Event Types

    /// <summary>
    /// Inspectorのロック状態が変更された
    /// </summary>
    public struct InspectorLockChangedEvent
    {
        public UnityEditor.EditorWindow Inspector { get; set; }
        public bool IsLocked { get; set; }
    }

    /// <summary>
    /// 選択が変更された
    /// </summary>
    public struct SelectionChangedEvent
    {
        public UnityEngine.Object[] SelectedObjects { get; set; }
    }

    /// <summary>
    /// ローテーションロックの状態が変更された
    /// </summary>
    public struct RotationLockStateChangedEvent
    {
        public bool IsEnabled { get; set; }
    }

    /// <summary>
    /// 履歴が更新された
    /// </summary>
    public struct HistoryUpdatedEvent
    {
    }

    /// <summary>
    /// お気に入りが更新された
    /// </summary>
    public struct FavoritesUpdatedEvent
    {
    }

    /// <summary>
    /// ローテーション更新が完了した
    /// </summary>
    public struct RotationUpdateCompletedEvent
    {
        public EditorWindow UpdatedInspector { get; set; }
        public UnityEngine.Object DisplayedObject { get; set; }
    }

    #endregion
}
