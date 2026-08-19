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

        // サブアセット（FBX内のメッシュなど）を区別するためのローカルファイルID。
        // 旧データには存在しないため、0 の場合はメインアセットとして扱う
        [SerializeField] private long _localId;

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

            // アセットの場合はGUIDとローカルファイルIDを取得
            if (UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    obj, out var guid, out long localId))
            {
                _objectGuid = guid;
                _localId = localId;
            }
            else
            {
                _objectGuid = string.Empty;
                _localId = 0;
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
            // アセットはGUIDを最優先で解決する。
            // InstanceID はエディタのセッションごとに振り直されるため、
            // 保存済みのIDを先に引くと、次のセッションで再利用されたIDが
            // まったく別のオブジェクトを返してしまう。
            if (!string.IsNullOrEmpty(_objectGuid))
            {
                return AssetEntryResolver.Resolve(_objectGuid, _localId);
            }

            // GUIDを持たない＝シーン上のオブジェクト。InstanceIDでのみ解決できる
            return UnityEditor.EditorUtility.InstanceIDToObject(_instanceId);
        }

        /// <summary>
        /// オブジェクトがまだ有効かどうか
        /// </summary>
        public bool IsValid()
        {
            return GetObject() != null;
        }

        /// <summary>
        /// InstanceIDを更新する。
        /// 保存済みの古いIDが別オブジェクトを指したままにならないよう、
        /// 解決できなかった場合は 0 にリセットする。
        /// </summary>
        public void RefreshInstanceId()
        {
            var obj = GetObject();
            _instanceId = obj != null ? obj.GetInstanceID() : 0;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is FavoriteEntry other)) return false;

            bool hasGuid = !string.IsNullOrEmpty(_objectGuid);
            bool otherHasGuid = !string.IsNullOrEmpty(other._objectGuid);

            // 片方だけがアセットなら別物。
            // InstanceID を先に見ると、セッションを跨いで再利用されたIDが
            // 無関係のオブジェクトと一致してしまうため、GUIDを優先する。
            if (hasGuid || otherHasGuid)
            {
                return hasGuid && otherHasGuid
                    && _objectGuid == other._objectGuid
                    && _localId == other._localId;
            }

            // どちらもシーンオブジェクト。InstanceIDでのみ判定できる
            return _instanceId != 0 && _instanceId == other._instanceId;
        }

        public override int GetHashCode()
        {
            if (!string.IsNullOrEmpty(_objectGuid))
                return _objectGuid.GetHashCode() ^ _localId.GetHashCode();
            return _instanceId.GetHashCode();
        }
    }
}
