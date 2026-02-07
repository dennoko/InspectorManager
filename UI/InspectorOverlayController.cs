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
        private const string OverlayName = "InspectorManagerOverlay";
        private const string LockButtonName = "InspectorManagerLockButton";
        
        // 処理済みInspectorと追加したエレメントの追跡
        private Dictionary<EditorWindow, VisualElement> _activeOverlays = new Dictionary<EditorWindow, VisualElement>();
        
        // 定期更新の間引き用
        private double _lastUpdateTime;
        private const double updateInterval = 0.5f; // 0.5秒おきにチェック

        public InspectorOverlayController(IInspectorWindowService inspectorService)
        {
            _inspectorService = inspectorService;
            EditorApplication.update += OnUpdate;
        }

        public void Dispose()
        {
            EditorApplication.update -= OnUpdate;
            RemoveAllOverlays();
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

            // 既存オーバーレイを確認（Dictionaryになくても、ヒエラルキーにあるかもしれないのでチェック）
            VisualElement overlay = null;
            if (_activeOverlays.TryGetValue(inspector, out var cachedOverlay))
            {
                overlay = cachedOverlay;
                // コンテナから削除されていたら再追加
                if (overlay.parent == null)
                {
                    root.Insert(0, overlay);
                }
            }
            else
            {
                // まだ管理されていない場合、ヒエラルキーからも探す（再コンパイル後など）
                overlay = root.Q(OverlayName);
                if (overlay == null)
                {
                    overlay = CreateOverlayElement(inspector);
                    root.Insert(0, overlay);
                }
                _activeOverlays[inspector] = overlay;
            }

            // UI状態更新
            var isLocked = _inspectorService.IsLocked(inspector);
            var button = overlay.Q<Button>(LockButtonName);
            if (button != null)
            {
                string statusText = isLocked ? "🔒 Locked" : "🔓 Unlocked";
                string displayText = $"Ispr #{index + 1} | {statusText}";

                if (button.text != displayText)
                {
                    button.text = displayText;
                    // 色の更新
                    var color = isLocked 
                        ? new Color(0.6f, 0.2f, 0.2f, 1f)  // 赤
                        : new Color(0.2f, 0.2f, 0.2f, 1f); // グレー
                    
                    button.style.backgroundColor = color;
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
                    flexShrink = 0, // 縮まない
                    height = 24,
                    backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f),
                    borderBottomWidth = 1,
                    borderBottomColor = new Color(0.1f, 0.1f, 0.1f, 1f),
                    paddingTop = 2,
                    paddingBottom = 2,
                    paddingLeft = 2,
                    paddingRight = 2
                }
            };

            var button = new Button(() => 
            {
                // クリック時処理
                bool current = _inspectorService.IsLocked(inspector);
                _inspectorService.SetLocked(inspector, !current);
                inspector.Repaint();
                
                // インデックスを手動で計算
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
                    color = Color.white
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
