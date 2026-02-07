using System;
using UnityEngine;

namespace InspectorManager.Models
{
    /// <summary>
    /// お気に入りエントリ
    /// </summary>
    [Serializable]
    public class FavoriteEntry
    {
        [SerializeField] private string _objectGuid;
        [SerializeField] private int _instanceId;
        [SerializeField] private string _displayName;
        [SerializeField] private string _objectType;
        [SerializeField] private int _sortOrder;

        /// <summary>
        /// アセットのGUID（シーンオブジェクトの場合は空）
        /// </summary>
        public string ObjectGuid => _objectGuid;

        /// <summary>
        /// オブジェクトのInstanceID
        /// </summary>
        public int InstanceId => _instanceId;

        /// <summary>
        /// 表示名
        /// </summary>
        public string DisplayName
        {
            get => _displayName;
            set => _displayName = value;
        }

        /// <summary>
        /// オブジェクトの型名
        /// </summary>
        public string ObjectType => _objectType;

        /// <summary>
        /// 並び順
        /// </summary>
        public int SortOrder
        {
            get => _sortOrder;
            set => _sortOrder = value;
        }

        public FavoriteEntry(UnityEngine.Object obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            _instanceId = obj.GetInstanceID();
            _displayName = obj.name;
            _objectType = obj.GetType().Name;
            _sortOrder = 0;

            // アセットの場合はGUIDを取得
            var assetPath = UnityEditor.AssetDatabase.GetAssetPath(obj);
            if (!string.IsNullOrEmpty(assetPath))
            {
                _objectGuid = UnityEditor.AssetDatabase.AssetPathToGUID(assetPath);
            }
            else
            {
                _objectGuid = string.Empty;
            }
        }

        /// <summary>
        /// デシリアライズ用コンストラクタ
        /// </summary>
        public FavoriteEntry()
        {
        }

        /// <summary>
        /// このエントリに対応するオブジェクトを取得
        /// </summary>
        public UnityEngine.Object GetObject()
        {
            // まずInstanceIDで検索
            var obj = UnityEditor.EditorUtility.InstanceIDToObject(_instanceId);
            if (obj != null) return obj;

            // GUIDがあればアセットとして検索
            if (!string.IsNullOrEmpty(_objectGuid))
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(_objectGuid);
                if (!string.IsNullOrEmpty(path))
                {
                    return UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                }
            }

            return null;
        }

        /// <summary>
        /// オブジェクトがまだ有効かどうか
        /// </summary>
        public bool IsValid()
        {
            return GetObject() != null;
        }

        /// <summary>
        /// InstanceIDを更新（セッション間でIDが変わる場合用）
        /// </summary>
        public void RefreshInstanceId()
        {
            var obj = GetObject();
            if (obj != null)
            {
                _instanceId = obj.GetInstanceID();
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is FavoriteEntry other)
            {
                if (_instanceId == other._instanceId && _instanceId != 0)
                    return true;

                if (!string.IsNullOrEmpty(_objectGuid) &&
                    _objectGuid == other._objectGuid)
                    return true;
            }
            return false;
        }

        public override int GetHashCode()
        {
            if (!string.IsNullOrEmpty(_objectGuid))
                return _objectGuid.GetHashCode();
            return _instanceId.GetHashCode();
        }
    }
}
