using InspectorManager.Models;
using InspectorManager.Services;
using UnityEditor;

namespace InspectorManager.Core
{
    /// <summary>
    /// サービスとコントローラーの初期化を担当
    /// </summary>
    public static class ServiceInitializer
    {
        public struct InitializationResult
        {
            public IPersistenceService PersistenceService;
            public ILocalizationService LocalizationService;
            public IInspectorWindowService InspectorService;
            public IHistoryService HistoryService;
            public IFavoritesService FavoritesService;
            public InspectorManagerSettings Settings;
        }

        /// <summary>
        /// 全サービスを初期化して ServiceLocator に登録する
        /// </summary>
        public static InitializationResult InitializeAll()
        {
            // 既存サービスが残っていれば先にクリア（ドメインリロード後の二重登録防止）
            ServiceLocator.Instance.Clear();
            EventBus.Instance.Clear();

            var persistence = new EditorPrefsPersistence();
            ServiceLocator.Instance.Register<IPersistenceService, EditorPrefsPersistence>(persistence);

            var localization = new LocalizationService(persistence);
            ServiceLocator.Instance.Register<ILocalizationService, LocalizationService>(localization);

            var inspector = new InspectorWindowService();
            ServiceLocator.Instance.Register<IInspectorWindowService, InspectorWindowService>(inspector);

            var history = new HistoryService(persistence);
            ServiceLocator.Instance.Register<IHistoryService, HistoryService>(history);

            var favorites = new FavoritesService(persistence);
            ServiceLocator.Instance.Register<IFavoritesService, FavoritesService>(favorites);

            // 設定の読み込み
            var settings = persistence.Load("Settings", InspectorManagerSettings.CreateDefault());

            // 言語設定の反映
            localization.Initialize(settings.Language);

            return new InitializationResult
            {
                PersistenceService = persistence,
                LocalizationService = localization,
                InspectorService = inspector,
                HistoryService = history,
                FavoritesService = favorites,
                Settings = settings
            };
        }
    }
}
