using System;
using UnityEngine;

namespace InspectorManager.Models
{
    /// <summary>
    /// Inspector Managerの設定
    /// </summary>
    [Serializable]
    public class InspectorManagerSettings
    {
        [SerializeField] private bool _rotationLockEnabled;
        [SerializeField] private int _maxHistoryCount = 50;
        [SerializeField] private bool _recordSceneObjects = true;
        [SerializeField] private bool _recordAssets = true;
        [SerializeField] private bool _autoCleanInvalidHistory = true;
        [SerializeField] private bool _blockFolderSelection = true;
        [SerializeField] private string _language = "ja";
        [SerializeField] private bool _autoFocusOnUpdate = true;

        /// <summary>
        /// 言語設定 ("ja" or "en")
        /// </summary>
        public string Language
        {
            get => _language;
            set => _language = value;
        }

        /// <summary>
        /// ローテーション更新時に自動でInspectorにフォーカスするか
        /// </summary>
        public bool AutoFocusOnUpdate
        {
            get => _autoFocusOnUpdate;
            set => _autoFocusOnUpdate = value;
        }

        /// <summary>
        /// フォルダ選択時にInspector更新をブロックするか
        /// </summary>
        public bool BlockFolderSelection
        {
            get => _blockFolderSelection;
            set => _blockFolderSelection = value;
        }

        /// <summary>
        /// ローテーションロック機能が有効かどうか
        /// </summary>
        public bool RotationLockEnabled
        {
            get => _rotationLockEnabled;
            set => _rotationLockEnabled = value;
        }

        /// <summary>
        /// 履歴の最大保持数
        /// </summary>
        public int MaxHistoryCount
        {
            get => _maxHistoryCount;
            set => _maxHistoryCount = Mathf.Clamp(value, 10, 200);
        }

        /// <summary>
        /// シーンオブジェクトを履歴に記録するか
        /// </summary>
        public bool RecordSceneObjects
        {
            get => _recordSceneObjects;
            set => _recordSceneObjects = value;
        }

        /// <summary>
        /// アセットを履歴に記録するか
        /// </summary>
        public bool RecordAssets
        {
            get => _recordAssets;
            set => _recordAssets = value;
        }

        /// <summary>
        /// 無効なエントリを自動的にクリーンアップするか
        /// </summary>
        public bool AutoCleanInvalidHistory
        {
            get => _autoCleanInvalidHistory;
            set => _autoCleanInvalidHistory = value;
        }

        /// <summary>
        /// デフォルト設定を作成
        /// </summary>
        public static InspectorManagerSettings CreateDefault()
        {
            return new InspectorManagerSettings
            {
                _rotationLockEnabled = false,
                _maxHistoryCount = 50,
                _recordSceneObjects = true,
                _recordAssets = true,
                _autoCleanInvalidHistory = true,
                _blockFolderSelection = true,
                _language = "ja",
                _autoFocusOnUpdate = true
            };
        }

        /// <summary>
        /// 設定をコピー
        /// </summary>
        public InspectorManagerSettings Clone()
        {
            return new InspectorManagerSettings
            {
                _rotationLockEnabled = _rotationLockEnabled,
                _maxHistoryCount = _maxHistoryCount,
                _recordSceneObjects = _recordSceneObjects,
                _recordAssets = _recordAssets,
                _autoCleanInvalidHistory = _autoCleanInvalidHistory,
                _blockFolderSelection = _blockFolderSelection,
                _language = _language,
                _autoFocusOnUpdate = _autoFocusOnUpdate
            };
        }
    }
}
