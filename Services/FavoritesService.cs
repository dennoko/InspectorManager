using System;
using System.Collections.Generic;
using System.Linq;
using InspectorManager.Core;
using InspectorManager.Models;

namespace InspectorManager.Services
{
    /// <summary>
    /// お気に入りサービスの実装
    /// </summary>
    public class FavoritesService : IFavoritesService
    {
        private readonly List<FavoriteEntry> _favorites = new List<FavoriteEntry>();
        private readonly IPersistenceService _persistence;

        private const string FavoritesKey = "Favorites";

        public FavoritesService(IPersistenceService persistence)
        {
            _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            LoadFavorites();
        }

        public IReadOnlyList<FavoriteEntry> GetFavorites()
        {
            return _favorites.AsReadOnly();
        }

        public void AddFavorite(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (IsFavorite(obj)) return;

            var entry = new FavoriteEntry(obj);
            entry.SortOrder = _favorites.Count;
            _favorites.Add(entry);

            SaveFavorites();
            EventBus.Instance.Publish(new FavoritesUpdatedEvent());
        }

        public void RemoveFavorite(UnityEngine.Object obj)
        {
            if (obj == null) return;

            var tempEntry = new FavoriteEntry(obj);
            var index = _favorites.FindIndex(e => e.Equals(tempEntry));

            if (index >= 0)
            {
                _favorites.RemoveAt(index);
                UpdateSortOrders();
                SaveFavorites();
                EventBus.Instance.Publish(new FavoritesUpdatedEvent());
            }
        }

        public bool IsFavorite(UnityEngine.Object obj)
        {
            if (obj == null) return false;

            var tempEntry = new FavoriteEntry(obj);
            return _favorites.Any(e => e.Equals(tempEntry));
        }

        public void ReorderFavorite(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= _favorites.Count) return;
            if (toIndex < 0 || toIndex >= _favorites.Count) return;
            if (fromIndex == toIndex) return;

            var item = _favorites[fromIndex];
            _favorites.RemoveAt(fromIndex);
            _favorites.Insert(toIndex, item);

            UpdateSortOrders();
            SaveFavorites();
            EventBus.Instance.Publish(new FavoritesUpdatedEvent());
        }

        public void CleanupInvalidEntries()
        {
            var removed = _favorites.RemoveAll(e => !e.IsValid());
            if (removed > 0)
            {
                UpdateSortOrders();
                SaveFavorites();
                EventBus.Instance.Publish(new FavoritesUpdatedEvent());
            }
        }

        private void UpdateSortOrders()
        {
            for (int i = 0; i < _favorites.Count; i++)
            {
                _favorites[i].SortOrder = i;
            }
        }

        private void LoadFavorites()
        {
            var data = _persistence.Load<FavoritesListData>(FavoritesKey, null);
            if (data?.Entries != null)
            {
                _favorites.Clear();
                _favorites.AddRange(data.Entries.OrderBy(e => e.SortOrder));

                // InstanceIDを更新
                foreach (var entry in _favorites)
                {
                    entry.RefreshInstanceId();
                }
            }
        }

        private void SaveFavorites()
        {
            var data = new FavoritesListData { Entries = _favorites.ToArray() };
            _persistence.Save(FavoritesKey, data);
        }

        [Serializable]
        private class FavoritesListData
        {
            public FavoriteEntry[] Entries;
        }
    }
}
