using System;
using System.Collections.Generic;
using InspectorManager.Core;
using InspectorManager.Models;
using UnityEngine;

namespace InspectorManager.Services
{
    /// <summary>
    /// 選択履歴サービスの実装
    /// </summary>
    public class HistoryService : IHistoryService
    {
        private readonly List<HistoryEntry> _history = new List<HistoryEntry>();
        private readonly IPersistenceService _persistence;
        private int _currentIndex = -1;
        private int _maxHistoryCount = 50;
        private bool _isNavigating;

        private const string HistoryKey = "SelectionHistory";

        public int MaxHistoryCount
        {
            get => _maxHistoryCount;
            set
            {
                _maxHistoryCount = Mathf.Clamp(value, 10, 200);
                TrimHistory();
            }
        }

        public int CurrentIndex => _currentIndex;

        public bool CanGoBack => _currentIndex > 0;
        public bool CanGoForward => _currentIndex < _history.Count - 1;

        public HistoryService(IPersistenceService persistence)
        {
            _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            LoadHistory();
        }

        public IReadOnlyList<HistoryEntry> GetHistory()
        {
            return _history.AsReadOnly();
        }

        public void RecordSelection(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (_isNavigating) return;

            var entry = new HistoryEntry(obj);

            // 最後の項目と同じなら追加しない
            if (_history.Count > 0 && _history[_history.Count - 1].Equals(entry))
            {
                return;
            }

            // 現在位置より後ろの履歴を削除（ブラウザの履歴と同じ挙動）
            if (_currentIndex < _history.Count - 1)
            {
                _history.RemoveRange(_currentIndex + 1, _history.Count - _currentIndex - 1);
            }

            _history.Add(entry);
            _currentIndex = _history.Count - 1;

            TrimHistory();
            SaveHistory();

            EventBus.Instance.Publish(new HistoryUpdatedEvent());
        }

        public void ClearHistory()
        {
            _history.Clear();
            _currentIndex = -1;
            SaveHistory();
            EventBus.Instance.Publish(new HistoryUpdatedEvent());
        }

        public void CleanupInvalidEntries()
        {
            var removed = _history.RemoveAll(e => !e.IsValid());
            if (removed > 0)
            {
                _currentIndex = Mathf.Clamp(_currentIndex, -1, _history.Count - 1);
                SaveHistory();
                EventBus.Instance.Publish(new HistoryUpdatedEvent());
            }
        }

        public HistoryEntry GoBack()
        {
            if (!CanGoBack) return null;

            _isNavigating = true;
            try
            {
                _currentIndex--;
                var entry = _history[_currentIndex];

                // オブジェクトを選択
                var obj = entry.GetObject();
                if (obj != null)
                {
                    UnityEditor.Selection.activeObject = obj;
                }

                EventBus.Instance.Publish(new HistoryUpdatedEvent());
                return entry;
            }
            finally
            {
                _isNavigating = false;
            }
        }

        public HistoryEntry GoForward()
        {
            if (!CanGoForward) return null;

            _isNavigating = true;
            try
            {
                _currentIndex++;
                var entry = _history[_currentIndex];

                // オブジェクトを選択
                var obj = entry.GetObject();
                if (obj != null)
                {
                    UnityEditor.Selection.activeObject = obj;
                }

                EventBus.Instance.Publish(new HistoryUpdatedEvent());
                return entry;
            }
            finally
            {
                _isNavigating = false;
            }
        }

        private void TrimHistory()
        {
            while (_history.Count > _maxHistoryCount)
            {
                _history.RemoveAt(0);
                _currentIndex--;
            }

            _currentIndex = Mathf.Clamp(_currentIndex, -1, _history.Count - 1);
        }

        private void LoadHistory()
        {
            var data = _persistence.Load<HistoryListData>(HistoryKey, null);
            if (data?.Entries != null)
            {
                _history.Clear();
                _history.AddRange(data.Entries);
                _currentIndex = _history.Count - 1;
            }
        }

        private void SaveHistory()
        {
            var data = new HistoryListData { Entries = _history.ToArray() };
            _persistence.Save(HistoryKey, data);
        }

        [Serializable]
        private class HistoryListData
        {
            public HistoryEntry[] Entries;
        }
    }
}
