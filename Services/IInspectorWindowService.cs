using System.Collections.Generic;
using UnityEditor;

namespace InspectorManager.Services
{
    /// <summary>
    /// Inspectorウィンドウ管理サービスのインターフェース
    /// </summary>
    public interface IInspectorWindowService
    {
        /// <summary>
        /// すべてのInspectorウィンドウを取得
        /// </summary>
        IReadOnlyList<EditorWindow> GetAllInspectors();

        /// <summary>
        /// 指定したInspectorがロックされているか
        /// </summary>
        bool IsLocked(EditorWindow inspector);

        /// <summary>
        /// 指定したInspectorのロック状態を設定
        /// </summary>
        void SetLocked(EditorWindow inspector, bool locked);

        /// <summary>
        /// すべてのInspectorをロック
        /// </summary>
        void LockAll();

        /// <summary>
        /// すべてのInspectorをアンロック
        /// </summary>
        void UnlockAll();

        /// <summary>
        /// Inspectorウィンドウが表示しているオブジェクトを取得
        /// </summary>
        UnityEngine.Object GetInspectedObject(EditorWindow inspector);

        /// <summary>
        /// リフレクションが有効か
        /// </summary>
        bool IsAvailable { get; }
    }
}
