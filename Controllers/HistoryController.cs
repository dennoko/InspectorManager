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
        private readonly InspectorManagerSettings _settings;

        private bool _isRecordingEnabled = true;

        public bool IsRecordingEnabled
        {
            get => _isRecordingEnabled;
            set => _isRecordingEnabled = value;
        }

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
        }

        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Selection.selectionChanged -= OnSelectionChanged;
        }

        private void OnSelectionChanged()
        {
            if (!_isRecordingEnabled) return;

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
