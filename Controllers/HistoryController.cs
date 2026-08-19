using System;
using InspectorManager.Models;
using InspectorManager.Services;
using UnityEditor;

namespace InspectorManager.Controllers
{
    /// <summary>
    /// 履歴・お気に入り操作のコーディネート
    /// </summary>
    public class HistoryController : IDisposable
    {
        private readonly IHistoryService _historyService;
        private readonly IFavoritesService _favoritesService;

        // 「すべてリセット」で設定インスタンスごと差し替わるため readonly にはできない
        private InspectorManagerSettings _settings;

public HistoryController(
            IHistoryService historyService,
            IFavoritesService favoritesService,
            InspectorManagerSettings settings)
        {
            _historyService = historyService;
            _favoritesService = favoritesService;
            _settings = settings;

            // 選択変更イベントを購読
            Selection.selectionChanged += OnSelectionChanged;

            // アセットの追加/削除/移動で履歴・お気に入りのエントリが無効になりうる
            EditorApplication.projectChanged += OnProjectChanged;

            AutoCleanIfEnabled();
        }

        /// <summary>
        /// 設定インスタンスの差し替えに追従する
        /// </summary>
        public void ApplySettings(InspectorManagerSettings settings)
        {
            if (settings == null) return;
            _settings = settings;
        }

        private void OnProjectChanged()
        {
            AutoCleanIfEnabled();
        }

        /// <summary>
        /// 「無効なエントリを自動削除」が有効なら、失われたオブジェクトを指す
        /// 履歴・お気に入りを取り除く。
        /// </summary>
        private void AutoCleanIfEnabled()
        {
            if (_settings == null || !_settings.AutoCleanInvalidHistory) return;
            CleanupAll();
        }

        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.projectChanged -= OnProjectChanged;
        }

        private void OnSelectionChanged()
        {
            if (_settings == null) return;

            var activeObject = Selection.activeObject;
            if (activeObject == null) return;

            // 設定に基づいて記録するかどうかを判断
            var isAsset = !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(activeObject));

            if (isAsset && !_settings.RecordAssets) return;
            if (!isAsset && !_settings.RecordSceneObjects) return;

            _historyService.RecordSelection(activeObject);
        }

        /// <summary>
        /// 履歴エントリに対応するオブジェクトを選択
        /// </summary>
        public void SelectFromHistory(HistoryEntry entry)
        {
            if (entry == null) return;

            var obj = entry.GetObject();
            if (obj != null)
            {
                Selection.activeObject = obj;
            }
        }

        /// <summary>
        /// お気に入りエントリに対応するオブジェクトを選択
        /// </summary>
        public void SelectFromFavorite(FavoriteEntry entry)
        {
            if (entry == null) return;

            var obj = entry.GetObject();
            if (obj != null)
            {
                Selection.activeObject = obj;
            }
        }

        /// <summary>
        /// 現在の選択をお気に入りに追加
        /// </summary>
        public void AddCurrentToFavorites()
        {
            var activeObject = Selection.activeObject;
            if (activeObject != null)
            {
                _favoritesService.AddFavorite(activeObject);
            }
        }

        /// <summary>
        /// 履歴を戻る
        /// </summary>
        public void GoBack()
        {
            _historyService.GoBack();
        }

        /// <summary>
        /// 履歴を進む
        /// </summary>
        public void GoForward()
        {
            _historyService.GoForward();
        }

        /// <summary>
        /// 履歴をクリア
        /// </summary>
        public void ClearHistory()
        {
            _historyService.ClearHistory();
        }

        /// <summary>
        /// 無効なエントリをクリーンアップ
        /// </summary>
        public void CleanupAll()
        {
            _historyService.CleanupInvalidEntries();
            _favoritesService.CleanupInvalidEntries();
        }
    }
}
