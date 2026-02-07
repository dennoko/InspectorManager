using System.Collections.Generic;
using InspectorManager.Models;

namespace InspectorManager.Services
{
    /// <summary>
    /// お気に入りサービスのインターフェース
    /// </summary>
    public interface IFavoritesService
    {
        /// <summary>
        /// お気に入りを取得
        /// </summary>
        IReadOnlyList<FavoriteEntry> GetFavorites();

        /// <summary>
        /// お気に入りに追加
        /// </summary>
        void AddFavorite(UnityEngine.Object obj);

        /// <summary>
        /// お気に入りから削除
        /// </summary>
        void RemoveFavorite(UnityEngine.Object obj);

        /// <summary>
        /// お気に入りかどうか
        /// </summary>
        bool IsFavorite(UnityEngine.Object obj);

        /// <summary>
        /// 並び順を変更
        /// </summary>
        void ReorderFavorite(int fromIndex, int toIndex);

        /// <summary>
        /// 無効なエントリを削除
        /// </summary>
        void CleanupInvalidEntries();
    }
}
