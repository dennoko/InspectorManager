using System;
using System.Collections.Generic;
using System.Linq;
using InspectorManager.Core;
using InspectorManager.Models;
using InspectorManager.Services;
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
        private readonly IInspectorWindowService _inspectorService;
        private readonly IPersistenceService _persistence;
        private readonly ExclusionManager _exclusionManager;

        private bool _isEnabled;
        
        // ローテーション順序（EditorWindow参照で管理、インデックス0が更新対象）
        private List<EditorWindow> _rotationOrder = new List<EditorWindow>();
        
        // 更新処理中フラグ
        private bool _isUpdating;
        
        // 最後に認識した選択（無限ループ防止）
        private UnityEngine.Object _lastKnownSelection;

        // delayCallタイムアウト管理
        private double _updateStartTime;
        private const double UpdateTimeout = 1.0;
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
                        _isPaused = false;
                        InitializeRotation();
                    }
                    else
                    {
                        _isPaused = false;
                        _inspectorService.UnlockAll();
                        _rotationOrder.Clear();
                    }

                    EventBus.Instance.Publish(new RotationLockStateChangedEvent { IsEnabled = _isEnabled });
                }
            }
        }
        
        public bool AutoFocusOnUpdate { get; set; }
        public bool BlockFolderSelection { get; set; }

        private bool _isPaused;
        /// <summary>
        /// ローテーションの一時停止。ONのまま更新だけを止める
        /// </summary>
        public bool IsPaused
        {
            get => _isPaused;
            set
            {
                if (_isPaused != value)
                {
                    _isPaused = value;
                    EventBus.Instance.Publish(new RotationPauseChangedEvent { IsPaused = _isPaused });
                }
            }
        }

        /// <summary>
        /// ブロックフィルタリング用の設定参照
        /// </summary>
        public InspectorManagerSettings FilterSettings { get; set; }

        public bool IsNextTarget(EditorWindow inspector)
        {
            if (_rotationOrder.Count == 0) return false;
            return _rotationOrder[0] == inspector;
        }

        public int GetRotationOrderIndex(EditorWindow inspector)
        {
            return _rotationOrder.IndexOf(inspector);
        }

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
            IInspectorWindowService inspectorService,
            IPersistenceService persistence)
        {
            _inspectorService = inspectorService;
            _persistence = persistence;
            _exclusionManager = new ExclusionManager(inspectorService);

            LoadSettings();
            Selection.selectionChanged += OnSelectionChanged;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Selection.selectionChanged -= OnSelectionChanged;
            _rotationOrder.Clear();
            _exclusionManager.Clear();
            _isUpdating = false;
        }

        public void InitializeRotation()
        {
            var inspectors = _inspectorService.GetAllInspectors();
            if (inspectors.Count == 0) return;

            _rotationOrder.Clear();

            foreach (var inspector in inspectors)
            {
                _inspectorService.SetLocked(inspector, true);
                _rotationOrder.Add(inspector);
            }

            _lastKnownSelection = Selection.activeObject;
        }

        public void SetExcluded(EditorWindow inspector, bool isExcluded)
        {
            _exclusionManager.SetExcluded(inspector, isExcluded, _rotationOrder, SyncInspectorList);
        }

        public bool IsExcluded(EditorWindow inspector)
        {
            return _exclusionManager.IsExcluded(inspector);
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

            // 除外リストから無効な参照を除去
            _exclusionManager.CleanupInvalid();
            
            // 新しく追加されたInspectorを末尾に追加（ロック状態で）
            foreach (var inspector in currentInspectors)
            {
                if (_exclusionManager.IsExcluded(inspector)) continue;

                if (!_rotationOrder.Contains(inspector))
                {
                    _rotationOrder.Add(inspector);
                    _inspectorService.SetLocked(inspector, true);
                }
            }

            // タイムアウトチェック
            if (_isUpdating && (EditorApplication.timeSinceStartup - _updateStartTime) > UpdateTimeout)
            {
                Debug.LogWarning("[InspectorManager] Rotation update timed out, resetting state.");
                _isUpdating = false;
            }

            // ロック状態の整合性チェック
            foreach (var inspector in currentInspectors)
            {
                if (_exclusionManager.IsExcluded(inspector)) continue;

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
            
            if (_rotationOrder.Count > 0)
            {
                var current = _rotationOrder[0];
                _rotationOrder.RemoveAt(0);
                _rotationOrder.Add(current);
            }
        }

        private void OnSelectionChanged()
        {
            if (!_isEnabled) return;
            if (_isPaused) return;
            if (_isUpdating) return;

            var newSelection = Selection.activeObject;
            if (newSelection == null) return;
            
            // ブロックフィルタ判定
            if (FilterSettings != null && SelectionFilter.ShouldBlock(newSelection, FilterSettings))
            {
                return;
            }
            
            if (newSelection == _lastKnownSelection) return;
            _lastKnownSelection = newSelection;

            SyncInspectorList();
            if (_rotationOrder.Count == 0) return;

            PerformRotationUpdate(newSelection);
        }

        /// <summary>
        /// 選択変更時のローテーション更新処理
        /// </summary>
        private void PerformRotationUpdate(UnityEngine.Object newSelection)
        {
            if (_rotationOrder.Count == 0) return;

            _isUpdating = true;
            _updateStartTime = EditorApplication.timeSinceStartup;

            try
            {
                var targetInspector = _rotationOrder[0];

                // 新方式: 直接更新を試行
                if (InspectorReflection.IsDirectUpdateAvailable)
                {
                    bool success = InspectorReflection.SetInspectedObject(targetInspector, newSelection);
                    if (success)
                    {
                        _rotationOrder.RemoveAt(0);
                        _rotationOrder.Add(targetInspector);
                        _isUpdating = false;

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
                    Debug.LogWarning("[InspectorManager] Direct update failed, falling back to legacy mode.");
                }

                // フォールバック（旧方式）
                _inspectorService.SetLocked(targetInspector, false);
                targetInspector.Repaint();

                EditorApplication.delayCall += () =>
                {
                    if (_disposed) return;

                    _inspectorService.SetLocked(targetInspector, true);

                    if (_rotationOrder.Count > 0)
                    {
                        _rotationOrder.RemoveAt(0);
                        _rotationOrder.Add(targetInspector);
                    }

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
            catch (Exception ex)
            {
                Debug.LogError($"[InspectorManager] Rotation update failed: {ex.Message}");
                _isUpdating = false;
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
