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
        private readonly Services.ILocalizationService _localizationService;
        private readonly Controllers.RotationLockController _rotationLockController;
        private const string OverlayName = "InspectorManagerOverlay";
        private const string LockButtonName = "InspectorManagerLockButton";
        private const string NextBadgeName = "InspectorManagerNextBadge";
        
        // 処理済みInspectorと追加したエレメントの追跡
        private Dictionary<EditorWindow, VisualElement> _activeOverlays = new Dictionary<EditorWindow, VisualElement>();
        
        // 定期更新の間引き用
        private double _lastUpdateTime;
        private const double updateInterval = 0.5f;

        // フラッシュ中のInspectorを追跡
        private HashSet<EditorWindow> _flashingInspectors = new HashSet<EditorWindow>();

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
            // 更新されたInspectorのオーバーレイをフラッシュ
            if (_activeOverlays.TryGetValue(evt.UpdatedInspector, out var overlay))
            {
                var button = overlay.Q<Button>(LockButtonName);
                if (button != null)
                {
                    _flashingInspectors.Add(evt.UpdatedInspector);

                    // フラッシュエフェクト（アクセントグリーン）
                    button.style.backgroundColor = new StyleColor(
                        new Color(0.20f, 0.78f, 0.35f, 1f)); // AccentGreen
                    button.style.borderBottomWidth = 2;
                    button.style.borderBottomColor = new StyleColor(
                        new Color(0.20f, 0.78f, 0.35f, 0.8f));

                    // 1.5秒後に元に戻す
                    button.schedule.Execute(() => 
                    {
                        _flashingInspectors.Remove(evt.UpdatedInspector);
                        UpdateOverlay(evt.UpdatedInspector, -1); 
                    }).ExecuteLater(1500);
                }
            }

            // 全オーバーレイの状態（Nextバッジなど）を更新
            RefreshOverlays();
        }

        private void OnUpdate()
        {
            if (EditorApplication.timeSinceStartup - _lastUpdateTime < updateInterval) return;
            _lastUpdateTime = EditorApplication.timeSinceStartup;

            RefreshOverlays();
        }

        private void RefreshOverlays()
        {
            var inspectors = _inspectorService.GetAllInspectors();
            
            // 削除されたInspectorのクリーンアップ
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

            // 現在のInspectorに対してオーバーレイ更新
            for (int i = 0; i < inspectors.Count; i++)
            {
                UpdateOverlay(inspectors[i], i);
            }
        }

        private void UpdateOverlay(EditorWindow inspector, int index)
        {
            if (inspector == null) return;
            var root = inspector.rootVisualElement;
            if (root == null) return;

            // 既存オーバーレイを確認
            VisualElement overlay = null;
            if (_activeOverlays.TryGetValue(inspector, out var cachedOverlay))
            {
                overlay = cachedOverlay;
                if (overlay.parent == null)
                {
                    root.Insert(0, overlay);
                }
            }
            else
            {
                overlay = root.Q(OverlayName);
                if (overlay == null)
                {
                    overlay = CreateOverlayElement(inspector);
                    root.Insert(0, overlay);
                }
                _activeOverlays[inspector] = overlay;
            }

            // フラッシュ中はスキップ（フラッシュの色を維持するため）
            bool isFlashing = _flashingInspectors.Contains(inspector);

            // UI状態更新
            var isLocked = _inspectorService.IsLocked(inspector);
            var button = overlay.Q<Button>(LockButtonName);
            if (button != null)
            {
                string statusText = isLocked 
                    ? _localizationService.GetString("Overlay_Locked") 
                    : _localizationService.GetString("Overlay_Unlocked");
                
                string displayText = _localizationService.GetString("Overlay_Format", index + 1, statusText);

                button.text = displayText;

                // フラッシュ中でなければ通常色を適用
                if (!isFlashing)
                {
                    var bgColor = isLocked 
                        ? new Color(0.55f, 0.20f, 0.20f, 1f)   // 落ち着いた赤
                        : new Color(0.20f, 0.20f, 0.20f, 1f);  // ダーク

                    button.style.backgroundColor = new StyleColor(bgColor);
                    button.style.borderBottomWidth = 2;
                    button.style.borderBottomColor = new StyleColor(
                        isLocked 
                            ? new Color(0.92f, 0.34f, 0.34f, 0.6f) 
                            : new Color(0.30f, 0.30f, 0.30f, 1f));
                }

                // Nextバッジの表示制御
                var nextBadge = overlay.Q<Label>(NextBadgeName);
                bool isNext = _rotationLockController != null && _rotationLockController.IsNextTarget(inspector);
                
                if (nextBadge != null)
                {
                    nextBadge.style.display = isNext ? DisplayStyle.Flex : DisplayStyle.None;
                }
                else if (isNext)
                {
                    nextBadge = new Label("▶ NEXT");
                    nextBadge.name = NextBadgeName;
                    nextBadge.style.backgroundColor = new StyleColor(new Color(0.26f, 0.52f, 0.96f, 1f)); // AccentBlue
                    nextBadge.style.color = new StyleColor(Color.white);
                    nextBadge.style.paddingLeft = 6;
                    nextBadge.style.paddingRight = 6;
                    nextBadge.style.paddingTop = 2;
                    nextBadge.style.paddingBottom = 2;
                    nextBadge.style.marginLeft = 2;
                    nextBadge.style.borderTopRightRadius = 4;
                    nextBadge.style.borderBottomRightRadius = 4;
                    nextBadge.style.borderTopLeftRadius = 4;
                    nextBadge.style.borderBottomLeftRadius = 4;
                    nextBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
                    nextBadge.style.fontSize = 10;
                    
                    overlay.Add(nextBadge);
                }
            }
        }

        private VisualElement CreateOverlayElement(EditorWindow inspector)
        {
            var overlay = new VisualElement
            {
                name = OverlayName,
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexShrink = 0,
                    height = 26,
                    backgroundColor = new StyleColor(new Color(0.14f, 0.14f, 0.14f, 1f)),
                    borderBottomWidth = 1,
                    borderBottomColor = new StyleColor(new Color(0.10f, 0.10f, 0.10f, 1f)),
                    paddingTop = 2,
                    paddingBottom = 2,
                    paddingLeft = 2,
                    paddingRight = 2,
                    alignItems = Align.Center,
                }
            };

            var button = new Button(() => 
            {
                bool current = _inspectorService.IsLocked(inspector);
                _inspectorService.SetLocked(inspector, !current);
                inspector.Repaint();
                
                var allInspectors = _inspectorService.GetAllInspectors();
                int idx = -1;
                for(int i=0; i < allInspectors.Count; i++)
                {
                    if (allInspectors[i] == inspector)
                    {
                        idx = i;
                        break;
                    }
                }
                
                if (idx >= 0)
                {
                    UpdateOverlay(inspector, idx);
                }
            })
            {
                name = LockButtonName,
                style =
                {
                    flexGrow = 1,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = new StyleColor(new Color(0.90f, 0.90f, 0.90f, 1f)),
                    fontSize = 11,
                    borderTopLeftRadius = 3,
                    borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3,
                    borderBottomRightRadius = 3,
                    marginLeft = 1,
                    marginRight = 1,
                    paddingLeft = 8,
                    paddingRight = 8,
                }
            };

            overlay.Add(button);
            return overlay;
        }

        private void RemoveAllOverlays()
        {
            foreach (var kvp in _activeOverlays)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.RemoveFromHierarchy();
                }
            }
            _activeOverlays.Clear();
            _flashingInspectors.Clear();
            
            // 念のため全Inspectorから検索して削除
            var inspectors = _inspectorService.GetAllInspectors();
            foreach(var ins in inspectors)
            {
                var root = ins.rootVisualElement;
                if(root != null)
                {
                    var overlay = root.Q(OverlayName);
                    overlay?.RemoveFromHierarchy();
                }
            }
        }
    }
}
