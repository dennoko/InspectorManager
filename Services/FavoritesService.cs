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
        private readonly IReadOnlyList<FavoriteEntry> _favoritesView;
        private readonly IPersistenceService _persistence;

        // IsFavorite() 用の索引。リスト描画では行ごと・フレームごとに問い合わせが
        // 来るため、毎回 FavoriteEntry を生成して線形探索するとアセットパス解決と
        // GUID変換が大量に走ってしまう。
        private readonly HashSet<int> _instanceIdIndex = new HashSet<int>();

        private const string FavoritesKey = "Favorites";

        public FavoritesService(IPersistenceService persistence)
        {
            _favoritesView = _favorites.AsReadOnly();
            _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            LoadFavorites();
        }

        /// <summary>
        /// お気に入り一覧を取得する。
        /// 返されるのは内部リストのライブビューであり、反復中にサービスを変更してはならない。
        /// </summary>
        public IReadOnlyList<FavoriteEntry> GetFavorites()
        {
            return _favoritesView;
        }

        public void AddFavorite(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (IsFavorite(obj)) return;

            var entry = new FavoriteEntry(obj);
            entry.SortOrder = _favorites.Count;
            _favorites.Add(entry);

            RebuildIndex();
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
                RebuildIndex();
                SaveFavorites();
                EventBus.Instance.Publish(new FavoritesUpdatedEvent());
            }
        }

        public bool IsFavorite(UnityEngine.Object obj)
        {
            if (obj == null) return false;

            // 索引はエントリ生成時と読み込み時に解決済みのInstanceIDで構築される
            return _instanceIdIndex.Contains(obj.GetInstanceID());
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

        public void ClearAll()
        {
            if (_favorites.Count == 0) return;

            _favorites.Clear();
            RebuildIndex();
            SaveFavorites();
            EventBus.Instance.Publish(new FavoritesUpdatedEvent());
        }

        public void CleanupInvalidEntries()
        {
            var removed = _favorites.RemoveAll(e => !e.IsValid());
            if (removed > 0)
            {
                UpdateSortOrders();
                RebuildIndex();
                SaveFavorites();
                EventBus.Instance.Publish(new FavoritesUpdatedEvent());
            }
        }

        /// <summary>
        /// IsFavorite() 用のInstanceID索引を作り直す
        /// </summary>
        private void RebuildIndex()
        {
            _instanceIdIndex.Clear();
            foreach (var entry in _favorites)
            {
                if (entry.InstanceId != 0) _instanceIdIndex.Add(entry.InstanceId);
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

                // 保存済みのInstanceIDはセッションを跨ぐと無効なので、
                // GUIDから解決し直したオブジェクトのIDで更新する
                foreach (var entry in _favorites)
                {
                    entry.RefreshInstanceId();
                }
            }

            RebuildIndex();
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
