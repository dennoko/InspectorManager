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
        
        // 履歴モード用の選択履歴
        private readonly List<UnityEngine.Object> _selectionHistory = new List<UnityEngine.Object>();
        
        // 更新処理中フラグ
        private bool _isUpdating;
        
        // 最後に認識した選択（無限ループ防止）
        private UnityEngine.Object _lastKnownSelection;

        // delayCallタイムアウト管理
        private double _updateStartTime;
        private const double UpdateTimeout = 1.0;
        private bool _disposed;

        private const string SettingsKey = "RotationLockSettings";

        /// <summary>
        /// 現在のローテーションモード
        /// </summary>
        public RotationMode Mode { get; set; } = RotationMode.History;

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

        public List<EditorWindow> GetRotationOrder()
        {
            return new List<EditorWindow>(_rotationOrder);
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

        public List<EditorWindow> GetExcludedInspectors()
        {
            // _exclusionManager doesn't expose list directly, so filter from all inspectors
            // Or better, let ExclusionManager expose it? 
            // Since ExclusionManager is private field here, we can just rely on IsExcluded check against all inspectors
            // But for efficiency, let's ask _inspectorService for all, then filter.
            var all = _inspectorService.GetAllInspectors();
            var excluded = new List<EditorWindow>();
            foreach(var window in all)
            {
                if (IsExcluded(window))
                {
                    excluded.Add(window);
                }
            }
            return excluded;
        }

        public void GetInspectorLists(out List<EditorWindow> rotation, out List<EditorWindow> excluded, out List<EditorWindow> unmanaged)
        {
            var all = _inspectorService.GetAllInspectors();
            rotation = new List<EditorWindow>(_rotationOrder);
            excluded = new List<EditorWindow>();
            unmanaged = new List<EditorWindow>();

            foreach (var w in all)
            {
                if (IsExcluded(w))
                {
                    excluded.Add(w);
                }
                else if (!_rotationOrder.Contains(w))
                {
                    unmanaged.Add(w);
                }
            }
        }

        /// <summary>
        /// 全Inspectorリスト内でのインデックス（1始まり）を取得（固定番号用）
        /// </summary>
        public int GetWindowIndex(EditorWindow inspector)
        {
            var all = _inspectorService.GetAllInspectors();
            int index = -1;
            // リスト内でインスタンスを検索
            for(int i=0; i<all.Count; i++)
            {
                if(all[i] == inspector)
                {
                    index = i;
                    break;
                }
            }
            return index >= 0 ? index + 1 : -1;
        }

        // 履歴モード時の役割ラベルを取得
        public string GetInspectionRoleLabel(EditorWindow inspector)
        {
            if (Mode != RotationMode.History) return null;
            
            int index = _rotationOrder.IndexOf(inspector);
            if (index < 0) return null;

            // 0 = 最新, 1 = 1つ前, 2 = 2つ前 ...
            if (index == 0) return "History_Latest";
            return "History_Previous"; // Needs format arg
        }

        /// <summary>
        /// Inspector数の変更を検出して対応
        /// </summary>
        public void SyncInspectorList()
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

            if (Mode == RotationMode.History)
            {
                PerformHistoryUpdate(newSelection);
            }
            else
            {
                PerformRotationUpdate(newSelection);
            }
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

                        if (AutoFocusOnUpdate && !IsFocusingHierarchyOrProject())
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

                    if (AutoFocusOnUpdate && !IsFocusingHierarchyOrProject())
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

        /// <summary>
        /// ローテーション順序内のInspectorを並び替える
        /// </summary>
        public void ReorderInspector(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= _rotationOrder.Count) return;
            if (toIndex < 0 || toIndex >= _rotationOrder.Count) return;
            if (fromIndex == toIndex) return;

            var item = _rotationOrder[fromIndex];
            _rotationOrder.RemoveAt(fromIndex);
            _rotationOrder.Insert(toIndex, item);
        }

        /// <summary>
        /// Inspector Manager経由で生成されたInspectorをローテーションに追加する
        /// </summary>
        public void AddManagedInspector(EditorWindow inspector)
        {
            if (inspector == null) return;

            _inspectorService.SetLocked(inspector, true);

            if (!_rotationOrder.Contains(inspector))
            {
                _rotationOrder.Add(inspector);
            }

            // 除外リストから確実に除去
            _exclusionManager.SetExcluded(inspector, false, _rotationOrder, SyncInspectorList);
        }

        /// <summary>
        /// 履歴モードのカスケード更新処理
        /// 各Inspectorに固定位置の履歴を表示する
        /// </summary>
        private void PerformHistoryUpdate(UnityEngine.Object newSelection)
        {
            if (_rotationOrder.Count == 0) return;

            _isUpdating = true;

            try
            {
                // 履歴の先頭に新しい選択を追加
                _selectionHistory.Insert(0, newSelection);

                // 履歴をInspector数+余裕分まで保持
                int maxHistory = _rotationOrder.Count + 5;
                while (_selectionHistory.Count > maxHistory)
                {
                    _selectionHistory.RemoveAt(_selectionHistory.Count - 1);
                }

                if (!InspectorReflection.IsDirectUpdateAvailable)
                {
                    // 直接更新が使えない場合はサイクルモードにフォールバック
                    Debug.LogWarning("[InspectorManager] Direct update not available, falling back to Cycle mode.");
                    _selectionHistory.Clear();
                    _isUpdating = false;
                    PerformRotationUpdate(newSelection);
                    return;
                }

                // 各Inspectorに対応する履歴を設定
                for (int i = 0; i < _rotationOrder.Count; i++)
                {
                    if (i >= _selectionHistory.Count) break;

                    var inspector = _rotationOrder[i];
                    var historyObject = _selectionHistory[i];

                    // nullや破棄済みオブジェクトはスキップ
                    if (historyObject == null) continue;

                    InspectorReflection.SetInspectedObject(inspector, historyObject);
                }

                // 最初のInspectorの更新完了を通知
                EventBus.Instance.Publish(new RotationUpdateCompletedEvent
                {
                    UpdatedInspector = _rotationOrder[0],
                    DisplayedObject = newSelection
                });

                if (AutoFocusOnUpdate && !IsFocusingHierarchyOrProject())
                {
                    _rotationOrder[0].Focus();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InspectorManager] History update failed: {ex.Message}");
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private bool IsFocusingHierarchyOrProject()
        {
            if (EditorWindow.focusedWindow == null) return false;
            var windowType = EditorWindow.focusedWindow.GetType().Name;
            return windowType == "SceneHierarchyWindow" || windowType == "ProjectBrowser";
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
