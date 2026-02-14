using System;
using System.Collections.Generic;
using System.Linq;
using InspectorManager.Services;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace InspectorManager.UI
{
    /// <summary>
    /// 各Inspectorウィンドウにヘッダーオーバーレイを表示するコントローラー
    /// </summary>
    public class InspectorOverlayController : IDisposable
    {
        private readonly IInspectorWindowService _inspectorService;
        private readonly ILocalizationService _localizationService;
        private readonly Controllers.RotationLockController _rotationLockController;
        private const string NextBadgeName = "InspectorManagerNextBadge";
        private const long FlashDurationMs = 1500;
        
        private readonly Dictionary<EditorWindow, VisualElement> _activeOverlays = new Dictionary<EditorWindow, VisualElement>();
        
        private double _lastUpdateTime;
        private const double UpdateInterval = 0.5;

        private readonly HashSet<EditorWindow> _flashingInspectors = new HashSet<EditorWindow>();

        public InspectorOverlayController(
            IInspectorWindowService inspectorService, 
            ILocalizationService localizationService,
            Controllers.RotationLockController rotationLockController = null)
        {
            _inspectorService = inspectorService;
            _localizationService = localizationService;
            _rotationLockController = rotationLockController;
            
            EditorApplication.update += OnUpdate;
            if (_localizationService != null)
            {
                _localizationService.OnLanguageChanged += RefreshOverlays;
            }

            Core.EventBus.Instance.Subscribe<Core.RotationUpdateCompletedEvent>(OnRotationUpdateCompleted);
        }

        public void Dispose()
        {
            EditorApplication.update -= OnUpdate;
            if (_localizationService != null)
            {
                _localizationService.OnLanguageChanged -= RefreshOverlays;
            }
            Core.EventBus.Instance.Unsubscribe<Core.RotationUpdateCompletedEvent>(OnRotationUpdateCompleted);
            RemoveAllOverlays();
        }

        private void OnRotationUpdateCompleted(Core.RotationUpdateCompletedEvent evt)
        {
            if (!_activeOverlays.TryGetValue(evt.UpdatedInspector, out var overlay)) return;

            var button = overlay.Q<Button>(OverlayElementFactory.LockButtonName);
            if (button == null) return;

            _flashingInspectors.Add(evt.UpdatedInspector);

            // フラッシュエフェクト
            button.style.backgroundColor = new StyleColor(new Color(0.20f, 0.78f, 0.35f, 1f));
            button.style.borderBottomWidth = 2;
            button.style.borderBottomColor = new StyleColor(new Color(0.20f, 0.78f, 0.35f, 0.8f));

            button.schedule.Execute(() => 
            {
                _flashingInspectors.Remove(evt.UpdatedInspector);
                UpdateOverlay(evt.UpdatedInspector, -1); 
            }).ExecuteLater(FlashDurationMs);

            RefreshOverlays();
        }

        private void OnUpdate()
        {
            if (EditorApplication.timeSinceStartup - _lastUpdateTime < UpdateInterval) return;
            _lastUpdateTime = EditorApplication.timeSinceStartup;

            if (_rotationLockController != null && _rotationLockController.IsEnabled)
            {
                _rotationLockController.SyncInspectorList();
            }

            RefreshOverlays();
        }

        private void RefreshOverlays()
        {
            var inspectors = _inspectorService.GetAllInspectors();
            
            // 閉じられたInspectorのクリーンアップ
            var closedInspectors = new List<EditorWindow>();
            foreach (var kvp in _activeOverlays)
            {
                if (kvp.Key == null || !inspectors.Contains(kvp.Key))
                {
                    closedInspectors.Add(kvp.Key);
                }
            }
            
            foreach (var closed in closedInspectors)
            {
                if (_activeOverlays.TryGetValue(closed, out var element))
                {
                    element?.RemoveFromHierarchy();
                }
                _activeOverlays.Remove(closed);
                _flashingInspectors.Remove(closed);
            }

            for (int i = 0; i < inspectors.Count; i++)
            {
                // 固定番号（ウィンドウ順）を表示
                UpdateOverlay(inspectors[i], i + 1);
            }
        }

        private void UpdateOverlay(EditorWindow inspector, object index)
        {
            if (inspector == null) return;
            var root = inspector.rootVisualElement;
            if (root == null) return;

            // 既存オーバーレイを確認・作成
            if (!_activeOverlays.TryGetValue(inspector, out var overlay))
            {
                overlay = root.Q(OverlayElementFactory.OverlayName);
                if (overlay == null)
                {
                    overlay = OverlayElementFactory.Create(inspector, _inspectorService, OnLockToggled);
                    root.Insert(0, overlay);
                }
                _activeOverlays[inspector] = overlay;
            }
            else if (overlay.parent == null)
            {
                root.Insert(0, overlay);
            }

            bool isFlashing = _flashingInspectors.Contains(inspector);
            var isLocked = _inspectorService.IsLocked(inspector);
            var button = overlay.Q<Button>(OverlayElementFactory.LockButtonName);
            if (button == null) return;

            // テキスト更新
            string statusText;
            if (_rotationLockController != null && _rotationLockController.IsExcluded(inspector))
            {
                statusText = _localizationService.GetString("Status_Excluded");
            }
            else
            {
                statusText = isLocked 
                    ? _localizationService.GetString("Overlay_Locked") 
                    : _localizationService.GetString("Overlay_Unlocked");
            }
            button.text = _localizationService.GetString("Overlay_Format", index, statusText);

            // フラッシュ中でなければ通常色を適用
            if (!isFlashing)
            {
                var bgColor = isLocked 
                    ? new Color(0.55f, 0.20f, 0.20f, 1f)
                    : new Color(0.20f, 0.20f, 0.20f, 1f);

                button.style.backgroundColor = new StyleColor(bgColor);
                button.style.borderBottomWidth = 2;
                button.style.borderBottomColor = new StyleColor(
                    isLocked 
                        ? new Color(0.92f, 0.34f, 0.34f, 0.6f) 
                        : new Color(0.30f, 0.30f, 0.30f, 1f));
            }

            // Nextバッジ
            UpdateNextBadge(overlay, inspector);
        }

        private void OnLockToggled(EditorWindow inspector)
        {
            var allInspectors = _inspectorService.GetAllInspectors();
            for (int i = 0; i < allInspectors.Count; i++)
            {
                if (allInspectors[i] == inspector)
                {
                    UpdateOverlay(inspector, i);
                    break;
                }
            }
        }

        private void UpdateNextBadge(VisualElement overlay, EditorWindow inspector)
        {
            var nextBadge = overlay.Q<Label>(NextBadgeName);
            bool isNext = _rotationLockController != null && _rotationLockController.IsNextTarget(inspector);
            
            if (nextBadge != null)
            {
                nextBadge.style.display = isNext ? DisplayStyle.Flex : DisplayStyle.None;
            }
            else if (isNext)
            {
                nextBadge = OverlayElementFactory.CreateNextBadge("▶ NEXT");
                nextBadge.name = NextBadgeName;
                overlay.Add(nextBadge);
            }
        }

        private void RemoveAllOverlays()
        {
            foreach (var kvp in _activeOverlays)
            {
                kvp.Value?.RemoveFromHierarchy();
            }
            _activeOverlays.Clear();
            _flashingInspectors.Clear();
            
            // 念のため全Inspectorから検索して削除
            try
            {
                var inspectors = _inspectorService.GetAllInspectors();
                foreach (var ins in inspectors)
                {
                    var root = ins?.rootVisualElement;
                    root?.Q(OverlayElementFactory.OverlayName)?.RemoveFromHierarchy();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InspectorManager] Overlay cleanup warning: {ex.Message}");
            }
        }
    }
}
