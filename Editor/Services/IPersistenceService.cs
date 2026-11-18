namespace InspectorManager.Services
{
    /// <summary>
    /// データ永続化サービスのインターフェース
    /// </summary>
    public interface IPersistenceService
    {
        /// <summary>
        /// データを保存する
        /// </summary>
        void Save<T>(string key, T data);

        /// <summary>
        /// データを読み込む
        /// </summary>
        T Load<T>(string key, T defaultValue = default);

        /// <summary>
        /// データを削除する
        /// </summary>
        void Delete(string key);

        /// <summary>
        /// キーが存在するか確認
        /// </summary>
        bool HasKey(string key);
    }
}
