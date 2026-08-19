using System.Collections.Generic;
using UnityEditor;

namespace InspectorManager.Core
{
    /// <summary>
    /// EditorWindow の一覧をエディタセッション内で永続化するためのユーティリティ。
    ///
    /// EditorWindow のインスタンスIDはドメインリロード（スクリプト再コンパイル、
    /// プレイモードの往復）を跨いで維持されるが、エディタを再起動すると失われる。
    /// SessionState の寿命と一致するため、インスタンスIDの配列を SessionState に
    /// 保存することでリロード後も同じウィンドウを復元できる。
    /// </summary>
    public static class WindowStateStore
    {
        private const char Separator = ',';

        public static void Save(string key, IEnumerable<EditorWindow> windows)
        {
            var ids = new List<string>();
            if (windows != null)
            {
                foreach (var window in windows)
                {
                    if (window == null) continue;
                    ids.Add(window.GetInstanceID().ToString());
                }
            }

            SessionState.SetString(key, string.Join(Separator.ToString(), ids));
        }

        /// <summary>
        /// 保存された並び順を復元する。
        /// candidates に存在しない（＝すでに閉じられた）ウィンドウは除外される。
        /// </summary>
        public static List<EditorWindow> Load(string key, IReadOnlyList<EditorWindow> candidates)
        {
            var result = new List<EditorWindow>();

            var raw = SessionState.GetString(key, string.Empty);
            if (string.IsNullOrEmpty(raw) || candidates == null) return result;

            foreach (var part in raw.Split(Separator))
            {
                if (!int.TryParse(part, out var id)) continue;

                for (int i = 0; i < candidates.Count; i++)
                {
                    var candidate = candidates[i];
                    if (candidate == null || candidate.GetInstanceID() != id) continue;
                    if (result.Contains(candidate)) break;

                    result.Add(candidate);
                    break;
                }
            }

            return result;
        }

        public static void Clear(string key)
        {
            SessionState.EraseString(key);
        }
    }
}
