using InspectorManager.Models;
using UnityEditor;
using UnityEngine;

namespace InspectorManager.Controllers
{
    /// <summary>
    /// 選択オブジェクトのブロック判定ロジックを集約するユーティリティ
    /// </summary>
    public static class SelectionFilter
    {
        private static readonly string[] NativePluginExtensions = { ".dll", ".so", ".bundle" };
        private static readonly string[] AsmDefExtensions = { ".asmdef", ".asmref" };

        /// <summary>
        /// 指定オブジェクトをブロック対象とするかどうかを判定する
        /// </summary>
        public static bool ShouldBlock(Object obj, InspectorManagerSettings settings)
        {
            if (obj == null || settings == null) return false;

            var path = AssetDatabase.GetAssetPath(obj);

            // フォルダ
            if (settings.BlockFolderSelection)
            {
                if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path))
                    return true;
            }

            // ネイティブプラグイン（DefaultAsset + 特定拡張子）
            // ※ NativePlugin チェックを DefaultAsset より先に行い、
            //    プラグインは BlockNativePlugin で個別制御可能にする
            if (obj is DefaultAsset)
            {
                if (settings.BlockNativePlugin && HasExtension(path, NativePluginExtensions))
                    return true;

                // アセンブリ定義（DefaultAsset の場合もある）
                if (settings.BlockAsmDef && HasExtension(path, AsmDefExtensions))
                    return true;

                // その他の DefaultAsset
                if (settings.BlockDefaultAsset)
                    return true;
            }

            // アセンブリ定義（専用型がある場合）
            if (settings.BlockAsmDef)
            {
#if UNITY_2019_1_OR_NEWER
                if (obj is UnityEditorInternal.AssemblyDefinitionAsset)
                    return true;
#endif
                if (HasExtension(path, AsmDefExtensions))
                    return true;
            }

            // テキストファイル
            if (settings.BlockTextAsset && obj is TextAsset)
                return true;

            // ライティング設定
            if (settings.BlockLightingSettings && obj is LightingSettings)
                return true;

            // シェーダー
            if (settings.BlockShader && (obj is Shader || obj is ComputeShader))
                return true;

            // フォント
            if (settings.BlockFont && obj is Font)
                return true;

            return false;
        }

        private static bool HasExtension(string path, string[] extensions)
        {
            if (string.IsNullOrEmpty(path)) return false;
            var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            for (int i = 0; i < extensions.Length; i++)
            {
                if (ext == extensions[i]) return true;
            }
            return false;
        }
    }
}
