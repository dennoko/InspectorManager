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
    }
}
