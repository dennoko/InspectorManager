using System;
using UnityEngine;

namespace InspectorManager.Models
{
    /// <summary>
    /// 選択履歴のエントリ
    /// </summary>
    [Serializable]
    public class HistoryEntry
    {
        [SerializeField] private string _objectGuid;
        [SerializeField] private int _instanceId;
        [SerializeField] private string _objectName;
        [SerializeField] private string _objectType;
        [SerializeField] private long _recordedAtTicks;

        /// <summary>
        /// アセットのGUID（シーンオブジェクトの場合は空）
        /// </summary>
        public string ObjectGuid => _objectGuid;

        /// <summary>
        /// オブジェクトのInstanceID（セッション内でのみ有効）
        /// </summary>
        public int InstanceId => _instanceId;

        /// <summary>
        /// オブジェクト名
        /// </summary>
        public string ObjectName => _objectName;

        /// <summary>
        /// オブジェクトの型名
        /// </summary>
        public string ObjectType => _objectType;

        /// <summary>
        /// 記録日時
        /// </summary>
        public DateTime RecordedAt => new DateTime(_recordedAtTicks);

        public HistoryEntry(UnityEngine.Object obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            _instanceId = obj.GetInstanceID();
            _objectName = obj.name;
            _objectType = obj.GetType().Name;
            _recordedAtTicks = DateTime.Now.Ticks;

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
        /// このエントリに対応するオブジェクトを取得
        /// </summary>
        public UnityEngine.Object GetObject()
        {
            // まずInstanceIDで検索（セッション内なら高速）
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

        public override bool Equals(object obj)
        {
            if (obj is HistoryEntry other)
            {
                // 同じInstanceIDなら同じ
                if (_instanceId == other._instanceId && _instanceId != 0)
                    return true;

                // 同じGUIDなら同じ
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
