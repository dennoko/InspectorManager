using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace InspectorManager.Services
{
    public class LocalizationService : ILocalizationService
    {
        private readonly IPersistenceService _persistence;
        private Dictionary<string, string> _translations = new Dictionary<string, string>();
        private string _currentLanguage = "ja";

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
            _currentLanguage = languageCode;
            LoadTranslations();
        }

        private void LoadTranslations()
        {
            _translations.Clear();
            string fileName = _currentLanguage == "en" ? "en" : "ja";
            string path = $"Assets/Editor/InspectorManager/Resources/Localize/{fileName}.json";

            if (!File.Exists(path))
            {
                Debug.LogError($"[InspectorManager] Localization file not found: {path}");
                return;
            }

            try
            {
                string jsonText = File.ReadAllText(path);
                ParseJson(jsonText);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InspectorManager] Failed to load localization: {ex.Message}");
            }
        }

        private void ParseJson(string json)
        {
            // 簡易的なJSONパース ({"key": "value"})
            // ネストには対応しない
            var matches = Regex.Matches(json, "\"([^\"]+)\"\\s*:\\s*\"([^\"]+)\"");
            foreach (Match match in matches)
            {
                if (match.Groups.Count >= 3)
                {
                    string key = match.Groups[1].Value;
                    string value = match.Groups[2].Value;
                    _translations[key] = value;
                }
            }
        }

        public string GetString(string key)
        {
            if (_translations.TryGetValue(key, out var value))
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
