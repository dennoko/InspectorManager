using System;

namespace InspectorManager.Services
{
    public interface ILocalizationService
    {
        string CurrentLanguage { get; set; }
        event Action OnLanguageChanged;
        
        string GetString(string key);
        string GetString(string key, params object[] args);
    }
}
