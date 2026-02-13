using System;
using InspectorManager.Controllers;
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
        [SerializeField] private int _rotationMode = 1;

        // ── ブロック設定: カテゴリA（デフォルト ON）──
        [SerializeField] private bool _blockDefaultAsset = true;
        [SerializeField] private bool _blockAsmDef = true;
        [SerializeField] private bool _blockNativePlugin = true;

        // ── ブロック設定: カテゴリB（デフォルト OFF）──
        [SerializeField] private bool _blockTextAsset = false;
        [SerializeField] private bool _blockLightingSettings = false;
        [SerializeField] private bool _blockShader = false;
        [SerializeField] private bool _blockFont = false;

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
        /// ローテーションモード (Cycle / History)
        /// </summary>
        public RotationMode RotationMode
        {
            get => (RotationMode)_rotationMode;
            set => _rotationMode = (int)value;
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

        // ── ブロック設定プロパティ ──

        public bool BlockDefaultAsset
        {
            get => _blockDefaultAsset;
            set => _blockDefaultAsset = value;
        }

        public bool BlockAsmDef
        {
            get => _blockAsmDef;
            set => _blockAsmDef = value;
        }

        public bool BlockNativePlugin
        {
            get => _blockNativePlugin;
            set => _blockNativePlugin = value;
        }

        public bool BlockTextAsset
        {
            get => _blockTextAsset;
            set => _blockTextAsset = value;
        }

        public bool BlockLightingSettings
        {
            get => _blockLightingSettings;
            set => _blockLightingSettings = value;
        }

        public bool BlockShader
        {
            get => _blockShader;
            set => _blockShader = value;
        }

        public bool BlockFont
        {
            get => _blockFont;
            set => _blockFont = value;
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
                _autoFocusOnUpdate = true,
                _rotationMode = 0,
                // カテゴリA
                _blockDefaultAsset = true,
                _blockAsmDef = true,
                _blockNativePlugin = true,
                // カテゴリB
                _blockTextAsset = false,
                _blockLightingSettings = false,
                _blockShader = false,
                _blockFont = false
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
                _autoFocusOnUpdate = _autoFocusOnUpdate,
                _rotationMode = _rotationMode,
                _blockDefaultAsset = _blockDefaultAsset,
                _blockAsmDef = _blockAsmDef,
                _blockNativePlugin = _blockNativePlugin,
                _blockTextAsset = _blockTextAsset,
                _blockLightingSettings = _blockLightingSettings,
                _blockShader = _blockShader,
                _blockFont = _blockFont
            };
        }
    }
}
