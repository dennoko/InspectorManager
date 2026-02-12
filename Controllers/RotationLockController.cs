using System;
using System.Collections.Generic;
using System.Linq;
using InspectorManager.Core;
using InspectorManager.Models;
using UnityEditor;
using UnityEngine;

namespace InspectorManager.Controllers
{
    /// <summary>
    /// ローテーションロック機能の制御
    /// 常時全Inspectorをロックし、Selection変更時のみ対象Inspectorを一瞬アンロックして更新させる方式。
    /// </summary>
    public class RotationLockController : IDisposable
    {
        private readonly Services.IInspectorWindowService _inspectorService;
        private readonly Services.IPersistenceService _persistence;

        private bool _isEnabled;
        
        // ローテーション順序（EditorWindow参照で管理、インデックス0が更新対象）
        private List<EditorWindow> _rotationOrder = new List<EditorWindow>();
        
        // 更新処理中フラグ
        private bool _isUpdating;
        
        // 最後に認識した選択（無限ループ防止）
        private UnityEngine.Object _lastKnownSelection;

        // delayCallタイムアウト管理
        private double _updateStartTime;
        private const double UpdateTimeout = 1.0; // 1秒でタイムアウト
        private bool _disposed;

        private const string SettingsKey = "RotationLockSettings";

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    SaveSettings();

                    if (_isEnabled)
                    {
                        InitializeRotation();
                    }
                    else
                    {
                        // 無効化時はすべてアンロック
                        _inspectorService.UnlockAll();
                        _rotationOrder.Clear();
                    }

                    EventBus.Instance.Publish(new RotationLockStateChangedEvent { IsEnabled = _isEnabled });
                }
            }
        }
        
        public bool AutoFocusOnUpdate { get; set; }

        public bool IsNextTarget(EditorWindow inspector)
        {
            if (_rotationOrder.Count == 0) return false;
            return _rotationOrder[0] == inspector;
        }

        public int GetRotationOrderIndex(EditorWindow inspector)
        {
            return _rotationOrder.IndexOf(inspector);
        }

        // 現在の更新対象（次に更新されるInspector）のインデックス
        public int CurrentTargetIndex
        {
            get
            {
                if (_rotationOrder.Count == 0) return 0;
                var currentInspectors = _inspectorService.GetAllInspectors();
                var target = _rotationOrder.FirstOrDefault();
                
                if (target != null)
                {
                    for (int i = 0; i < currentInspectors.Count; i++)
                    {
                        if (currentInspectors[i] == target) return i;
                    }
                }
                return 0;
            }
        }

        public RotationLockController(
            Services.IInspectorWindowService inspectorService,
            Services.IPersistenceService persistence)
        {
            _inspectorService = inspectorService;
            _persistence = persistence;

            LoadSettings();

            // 選択変更イベントを購読
            Selection.selectionChanged += OnSelectionChanged;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Selection.selectionChanged -= OnSelectionChanged;
            _rotationOrder.Clear();
            _excludedWindows.Clear();
            _isUpdating = false;
        }

        public void InitializeRotation()
        {
            var inspectors = _inspectorService.GetAllInspectors();
            if (inspectors.Count == 0) return;

            _rotationOrder.Clear();

            // 全てロックして管理リストに追加
            foreach (var inspector in inspectors)
            {
                _inspectorService.SetLocked(inspector, true);
                _rotationOrder.Add(inspector);
            }

            _lastKnownSelection = Selection.activeObject;
        }

        // 除外されたInspector（手動で更新から外したもの）
        private List<EditorWindow> _excludedWindows = new List<EditorWindow>();

        public void SetExcluded(EditorWindow inspector, bool isExcluded)
        {
            if (isExcluded)
            {
                if (!_excludedWindows.Contains(inspector))
                {
                    _excludedWindows.Add(inspector);
                    if (_rotationOrder.Contains(inspector))
                    {
                        _rotationOrder.Remove(inspector);
                    }
                    // 除外したらアンロックする？それともロックのまま？
                    // 要望は「手動で更新から除外」＝固定したい、という意味合いが強いはず。
                    // 元の挙動（更新されない）にするなら、Lock状態を維持（＝今のSelectionのまま）が正しいか、
                    // あるいはUnity標準挙動に戻す（Unlock）か。
                    // 「更新から除外」なので、Unityの更新からも、このツールの更新からも外れるべき。
                    // ロックしておけばUnityの更新からは外れる。ツールの更新からも外す。
                    _inspectorService.SetLocked(inspector, true);
                }
            }
            else
            {
                if (_excludedWindows.Contains(inspector))
                {
                    _excludedWindows.Remove(inspector);
                    SyncInspectorList(); // 再度ローテーションに組み込む
                }
            }
        }

        public bool IsExcluded(EditorWindow inspector)
        {
            return _excludedWindows.Contains(inspector);
        }

        /// <summary>
        /// Inspector数の変更を検出して対応
        /// </summary>
        private void SyncInspectorList()
        {
            var currentInspectors = _inspectorService.GetAllInspectors();
            
            // 削除されたInspectorを除去
            var removedInspectors = _rotationOrder.Where(i => !currentInspectors.Contains(i)).ToList();
            foreach (var removed in removedInspectors)
            {
                _rotationOrder.Remove(removed);
            }

            // 除外リストからも削除されたものを除去
            _excludedWindows.RemoveAll(i => i == null || !currentInspectors.Contains(i));
            
            // 新しく追加されたInspectorを末尾に追加（ロック状態で）
            foreach (var inspector in currentInspectors)
            {
                // 除外されているものは追加しない
                if (_excludedWindows.Contains(inspector)) continue;

                if (!_rotationOrder.Contains(inspector))
                {
                    _rotationOrder.Add(inspector);
                    _inspectorService.SetLocked(inspector, true);
                }
            }

            // タイムアウトチェック: _isUpdatingが長時間trueのままならリセット
            if (_isUpdating && (EditorApplication.timeSinceStartup - _updateStartTime) > UpdateTimeout)
            {
                Debug.LogWarning("[InspectorManager] Rotation update timed out, resetting state.");
                _isUpdating = false;
            }

            // 何らかの理由でアンロックされているものがあればロック（除外されているものは関知しない）
            foreach (var inspector in currentInspectors)
            {
                if (_excludedWindows.Contains(inspector)) continue;

                if (!_inspectorService.IsLocked(inspector) && !_isUpdating)
                {
                    _inspectorService.SetLocked(inspector, true);
                }
            }
        }

        public void RotateToNext()
        {
            if (!_isEnabled) return;
            SyncInspectorList();
            
            // ローテーション順序を一つ進める（現在のSelectionを維持したまま）
            if (_rotationOrder.Count > 0)
            {
                var current = _rotationOrder[0];
                _rotationOrder.RemoveAt(0);
                _rotationOrder.Add(current);
            }
        }

        public bool BlockFolderSelection { get; set; }

        private void OnSelectionChanged()
        {
            if (!_isEnabled) return;
            // 更新処理中は再入を防ぐ
            if (_isUpdating) return;

            var newSelection = Selection.activeObject;
            if (newSelection == null) return;
            
            // フォルダ判定
            if (BlockFolderSelection)
            {
                var path = AssetDatabase.GetAssetPath(newSelection);
                if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path))
                {
                    // フォルダなら更新しない（現在のInspectorの内容を維持）
                    return;
                }
            }
            
            // 同じオブジェクトなら無視
            if (newSelection == _lastKnownSelection) return;
            _lastKnownSelection = newSelection;

            SyncInspectorList();
            if (_rotationOrder.Count == 0) return;

            // ローテーション更新処理を開始
            PerformRotationUpdate(newSelection);
        }

        /// <summary>
        /// 選択変更時のローテーション更新処理
        /// 新方式: InspectorReflection.SetInspectedObject で同期的に更新
        /// フォールバック: 旧方式（アンロック→delayCall→再ロック）
        /// </summary>
        private void PerformRotationUpdate(UnityEngine.Object newSelection)
        {
            if (_rotationOrder.Count == 0) return;

            _isUpdating = true;
            _updateStartTime = EditorApplication.timeSinceStartup;

            try
            {
                // 1. 更新対象のInspectorを取得（リストの先頭）
                var targetInspector = _rotationOrder[0];

                // 2. 新方式: 直接更新を試行
                if (InspectorReflection.IsDirectUpdateAvailable)
                {
                    bool success = InspectorReflection.SetInspectedObject(targetInspector, newSelection);
                    if (success)
                    {
                        // 同期的に完了 — ローテーション順序を更新
                        _rotationOrder.RemoveAt(0);
                        _rotationOrder.Add(targetInspector);
                        _isUpdating = false;

                        // 更新完了イベントを発行（Phase 3のフィードバック用）
                        EventBus.Instance.Publish(new RotationUpdateCompletedEvent
                        {
                            UpdatedInspector = targetInspector,
                            DisplayedObject = newSelection
                        });

                        if (AutoFocusOnUpdate)
                        {
                            targetInspector.Focus();
                        }
                        return;
                    }
                    // 直接更新失敗 → フォールバック
                    Debug.LogWarning("[InspectorManager] Direct update failed, falling back to legacy mode.");
                }

                // 3. フォールバック（旧方式）: アンロック→delayCall→再ロック
                _inspectorService.SetLocked(targetInspector, false);
                targetInspector.Repaint();

                EditorApplication.delayCall += () =>
                {
                    _inspectorService.SetLocked(targetInspector, true);

                    _rotationOrder.RemoveAt(0);
                    _rotationOrder.Add(targetInspector);

                    // 更新完了イベントを発行
                    EventBus.Instance.Publish(new RotationUpdateCompletedEvent
                    {
                        UpdatedInspector = targetInspector,
                        DisplayedObject = newSelection
                    });

                    if (AutoFocusOnUpdate)
                    {
                        targetInspector.Focus();
                    }

                    _isUpdating = false;
                };
            }
            catch
            {
                _isUpdating = false;
                throw;
            }
        }

        /// <summary>
        /// 特定のInspectorを次の更新対象に設定
        /// </summary>
        public void SetNextTargetInspector(int index)
        {
            if (!_isEnabled) return;
            SyncInspectorList();
            
            var inspectors = _inspectorService.GetAllInspectors();
            if (index < 0 || index >= inspectors.Count) return;

            var targetInspector = inspectors[index];

            // ローテーション順序を再構築（指定Inspectorを先頭に）
            if (_rotationOrder.Contains(targetInspector))
            {
                _rotationOrder.Remove(targetInspector);
                _rotationOrder.Insert(0, targetInspector);
            }
        }

        private void LoadSettings()
        {
            var settings = _persistence.Load<RotationLockSettings>(SettingsKey, null);
            if (settings != null)
            {
                _isEnabled = settings.IsEnabled;
            }
        }

        private void SaveSettings()
        {
            var settings = new RotationLockSettings { IsEnabled = _isEnabled };
            _persistence.Save(SettingsKey, settings);
        }

        [System.Serializable]
        private class RotationLockSettings
        {
            public bool IsEnabled;
        }
    }
}
