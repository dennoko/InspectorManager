using System.Collections.Generic;
using UnityEditor;

namespace InspectorManager.Models
{
    /// <summary>
    /// GUID とローカルファイルIDからアセットを解決する共通処理。
    /// HistoryEntry / FavoriteEntry から利用する。
    /// </summary>
    internal static class AssetEntryResolver
    {
        // 解決結果のキャッシュ。履歴・お気に入りのリストは行ごと・フレームごとに
        // 解決を要求するため、キャッシュが無いと GUIDToAssetPath や
        // LoadAllAssetsAtPath が毎フレーム全行分走ってしまう。
        // アセットの追加/削除/移動でのみ無効化すればよい。
        private static readonly Dictionary<string, UnityEngine.Object> _cache =
            new Dictionary<string, UnityEngine.Object>();

        private static bool _invalidationHooked;

        /// <summary>
        /// GUID（＋サブアセットのローカルID）に対応するアセットを取得する。
        /// 見つからない場合は null。
        /// </summary>
        public static UnityEngine.Object Resolve(string guid, long localId)
        {
            if (string.IsNullOrEmpty(guid)) return null;

            EnsureInvalidationHooked();

            var cacheKey = guid + ":" + localId;
            if (_cache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var resolved = ResolveUncached(guid, localId);
            _cache[cacheKey] = resolved;
            return resolved;
        }

        private static void EnsureInvalidationHooked()
        {
            if (_invalidationHooked) return;
            _invalidationHooked = true;
            EditorApplication.projectChanged += ClearCache;
        }

        public static void ClearCache()
        {
            _cache.Clear();
        }

        private static UnityEngine.Object ResolveUncached(string guid, long localId)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return null;

            var main = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);

            // 旧データ（ローカルID未保存）はメインアセットとして扱う
            if (localId == 0) return main;

            // メインアセットが一致すればそれを返す（サブアセット走査を避ける）
            if (main != null
                && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(main, out _, out long mainLocalId)
                && mainLocalId == localId)
            {
                return main;
            }

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset == null) continue;
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out _, out long assetLocalId)
                    && assetLocalId == localId)
                {
                    return asset;
                }
            }

            // ローカルIDが変わった等で見つからない場合はメインアセットで代替する
            return main;
        }
    }
}
