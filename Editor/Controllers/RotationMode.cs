namespace InspectorManager.Controllers
{
    /// <summary>
    /// ローテーションの更新方式。
    /// 実際の更新処理は IRotationStrategy の実装が担う。
    /// </summary>
    public enum RotationMode
    {
        /// <summary>最も古いInspectorを順番に更新するサイクル方式</summary>
        Cycle = 0,
        /// <summary>各タブが固定位置の履歴を表示する方式</summary>
        History = 1
    }
}
