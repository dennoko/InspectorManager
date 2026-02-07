using InspectorManager.Core;
using InspectorManager.Services;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace InspectorManager.Controllers
{
    /// <summary>
    /// ホットキー・ショートカット処理
    /// </summary>
    public static class HotkeyController
    {
        /// <summary>
        /// アクティブなInspectorのロックを切り替え (Ctrl+L)
        /// </summary>
        [Shortcut("Inspector Manager/Toggle Lock", KeyCode.L, ShortcutModifiers.Action)]
        public static void ToggleActiveLock()
        {
            var inspectorService = ServiceLocator.Instance.TryResolve<IInspectorWindowService>();
            if (inspectorService == null || !inspectorService.IsAvailable) return;

            // フォーカスされているウィンドウがInspectorかチェック
            var focusedWindow = EditorWindow.focusedWindow;
            if (focusedWindow != null && InspectorReflection.IsInspectorWindow(focusedWindow))
            {
                var isLocked = inspectorService.IsLocked(focusedWindow);
                inspectorService.SetLocked(focusedWindow, !isLocked);
            }
            else
            {
                // Inspectorにフォーカスがない場合は、最初のInspectorを切り替え
                var inspectors = inspectorService.GetAllInspectors();
                if (inspectors.Count > 0)
                {
                    var isLocked = inspectorService.IsLocked(inspectors[0]);
                    inspectorService.SetLocked(inspectors[0], !isLocked);
                }
            }
        }

        /// <summary>
        /// 全Inspectorのロックを切り替え (Ctrl+Shift+L)
        /// </summary>
        [Shortcut("Inspector Manager/Toggle All Locks", KeyCode.L, ShortcutModifiers.Action | ShortcutModifiers.Shift)]
        public static void ToggleAllLocks()
        {
            var inspectorService = ServiceLocator.Instance.TryResolve<IInspectorWindowService>();
            if (inspectorService == null || !inspectorService.IsAvailable) return;

            var inspectors = inspectorService.GetAllInspectors();
            if (inspectors.Count == 0) return;

            // 1つでもアンロックがあれば全てロック、そうでなければ全てアンロック
            bool anyUnlocked = false;
            foreach (var inspector in inspectors)
            {
                if (!inspectorService.IsLocked(inspector))
                {
                    anyUnlocked = true;
                    break;
                }
            }

            if (anyUnlocked)
            {
                inspectorService.LockAll();
            }
            else
            {
                inspectorService.UnlockAll();
            }
        }

        /// <summary>
        /// 履歴を戻る (Ctrl+[)
        /// </summary>
        [Shortcut("Inspector Manager/History Back", KeyCode.LeftBracket, ShortcutModifiers.Action)]
        public static void HistoryBack()
        {
            var historyService = ServiceLocator.Instance.TryResolve<IHistoryService>();
            if (historyService != null && historyService.CanGoBack)
            {
                historyService.GoBack();
            }
        }

        /// <summary>
        /// 履歴を進む (Ctrl+])
        /// </summary>
        [Shortcut("Inspector Manager/History Forward", KeyCode.RightBracket, ShortcutModifiers.Action)]
        public static void HistoryForward()
        {
            var historyService = ServiceLocator.Instance.TryResolve<IHistoryService>();
            if (historyService != null && historyService.CanGoForward)
            {
                historyService.GoForward();
            }
        }

        /// <summary>
        /// 現在の選択をお気に入りに追加 (Ctrl+D)
        /// </summary>
        [Shortcut("Inspector Manager/Add to Favorites", KeyCode.D, ShortcutModifiers.Action)]
        public static void AddToFavorites()
        {
            var favoritesService = ServiceLocator.Instance.TryResolve<IFavoritesService>();
            if (favoritesService == null) return;

            var activeObject = Selection.activeObject;
            if (activeObject != null)
            {
                if (favoritesService.IsFavorite(activeObject))
                {
                    favoritesService.RemoveFavorite(activeObject);
                    Debug.Log($"[Inspector Manager] Removed from favorites: {activeObject.name}");
                }
                else
                {
                    favoritesService.AddFavorite(activeObject);
                    Debug.Log($"[Inspector Manager] Added to favorites: {activeObject.name}");
                }
            }
        }
    }
}
