using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace InspectorManager.Services
{
    public class LocalizationService : ILocalizationService
    {
        private const string FallbackLanguage = "ja";

        private readonly IPersistenceService _persistence;
        private readonly Dictionary<string, string> _translations = new Dictionary<string, string>();
        private string _currentLanguage = FallbackLanguage;

        /// <summary>拡張機能のルートフォルダ（Assets からの相対パス）</summary>
        private static string _rootPath;

        public event Action OnLanguageChanged;

        public string CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                if (_currentLanguage != value)
                {
                    _currentLanguage = value;
                    LoadTranslations();
                    OnLanguageChanged?.Invoke();
                }
            }
        }

        public LocalizationService(IPersistenceService persistence)
        {
            _persistence = persistence;
        }

        public void Initialize(string languageCode)
        {
            _currentLanguage = string.IsNullOrEmpty(languageCode) ? FallbackLanguage : languageCode;
            LoadTranslations();
        }

        /// <summary>
        /// 拡張機能のルートフォルダを、このスクリプト自身の位置から解決する。
        /// パスをハードコードするとフォルダを移動しただけで壊れるため、
        /// MonoScript のアセットパスを基準にする。
        /// </summary>
        private static string GetRootPath()
        {
            if (!string.IsNullOrEmpty(_rootPath)) return _rootPath;

            foreach (var guid in AssetDatabase.FindAssets("LocalizationService t:MonoScript"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith("/LocalizationService.cs", StringComparison.Ordinal)) continue;

                // <root>/Services/LocalizationService.cs -> <root>
                var servicesDir = Path.GetDirectoryName(path);
                var root = Path.GetDirectoryName(servicesDir);
                if (string.IsNullOrEmpty(root)) continue;

                _rootPath = root.Replace('\\', '/');
                return _rootPath;
            }

            return null;
        }

        private void LoadTranslations()
        {
            _translations.Clear();

            string fileName = _currentLanguage == "en" ? "en" : FallbackLanguage;

            var root = GetRootPath();
            if (string.IsNullOrEmpty(root))
            {
                Debug.LogError("[InspectorManager] Could not locate the Inspector Manager folder.");
                return;
            }

            string path = root + "/Resources/Localize/" + fileName + ".json";

            // File.ReadAllText ではなくアセットとして読み込む
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (asset == null)
            {
                Debug.LogError("[InspectorManager] Localization file not found: " + path);
                return;
            }

            try
            {
                ParseJson(asset.text);
            }
            catch (Exception ex)
            {
                Debug.LogError("[InspectorManager] Failed to load localization: " + ex.Message);
            }
        }

        /// <summary>
        /// フラットな {"key": "value", ...} 形式のJSONを読み込む。
        /// ネストや文字列以外の値には対応しない。
        /// </summary>
        private void ParseJson(string json)
        {
            // 文字列トークンを順に取り出し、キーと値のペアとして解釈する。
            // 以前の簡易正規表現は、空文字列の値やエスケープされた
            // ダブルクォートを含む値を取りこぼしていた。
            var tokens = ReadStringTokens(json);

            for (int i = 0; i + 1 < tokens.Count; i += 2)
            {
                _translations[tokens[i]] = tokens[i + 1];
            }
        }

        /// <summary>
        /// JSON中の文字列リテラルを、エスケープを解決しながら順に取り出す。
        /// </summary>
        private static List<string> ReadStringTokens(string json)
        {
            var tokens = new List<string>();
            if (string.IsNullOrEmpty(json)) return tokens;

            var sb = new StringBuilder();
            bool inString = false;

            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];

                if (!inString)
                {
                    if (c == '"')
                    {
                        inString = true;
                        sb.Length = 0;
                    }
                    continue;
                }

                if (c == '\\' && i + 1 < json.Length)
                {
                    i++;
                    switch (json[i])
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'u':
                            if (i + 4 < json.Length && int.TryParse(
                                    json.Substring(i + 1, 4),
                                    NumberStyles.HexNumber,
                                    CultureInfo.InvariantCulture,
                                    out int code))
                            {
                                sb.Append((char)code);
                                i += 4;
                            }
                            break;
                        default: sb.Append(json[i]); break;
                    }
                    continue;
                }

                if (c == '"')
                {
                    inString = false;
                    tokens.Add(sb.ToString());
                    continue;
                }

                sb.Append(c);
            }

            return tokens;
        }

        public string GetString(string key)
        {
            if (key != null && _translations.TryGetValue(key, out var value))
            {
                return value;
            }
            return key; // キーが見つからない場合はキーを返す
        }

        public string GetString(string key, params object[] args)
        {
            string format = GetString(key);
            try
            {
                return string.Format(format, args);
            }
            catch
            {
                return format;
            }
        }
    }
}
