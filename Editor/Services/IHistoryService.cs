using System.Collections.Generic;
using InspectorManager.Models;

namespace InspectorManager.Services
{
    /// <summary>
    /// 選択履歴サービスのインターフェース
    /// </summary>
    public interface IHistoryService
    {
        /// <summary>
        /// 履歴を取得
        /// </summary>
        IReadOnlyList<HistoryEntry> GetHistory();

        /// <summary>
        /// 選択を記録
        /// </summary>
        void RecordSelection(UnityEngine.Object obj);

        /// <summary>
        /// 履歴をクリア
        /// </summary>
        void ClearHistory();

        /// <summary>
        /// 無効なエントリを削除
        /// </summary>
        void CleanupInvalidEntries();

        /// <summary>
        /// 履歴の最大保持数
        /// </summary>
        int MaxHistoryCount { get; set; }

        /// <summary>
        /// 現在の履歴インデックス（戻る/進む操作用）
        /// </summary>
        int CurrentIndex { get; }

        /// <summary>
        /// 履歴を戻る
        /// </summary>
        HistoryEntry GoBack();

        /// <summary>
        /// 履歴を進む
        /// </summary>
        HistoryEntry GoForward();

        /// <summary>
        /// 戻れるか
        /// </summary>
        bool CanGoBack { get; }

        /// <summary>
        /// 進めるか
        /// </summary>
        bool CanGoForward { get; }
    }
}
