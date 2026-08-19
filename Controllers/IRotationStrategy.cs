using System.Collections.Generic;
using UnityEditor;

namespace InspectorManager.Controllers
{
    /// <summary>
    /// 戦略が RotationLockController に対して行える操作。
    /// 戦略側はロック制御やフォールバック判定を意識しない。
    /// </summary>
    public interface IRotationContext
    {
        /// <summary>現在のローテーション順序（読み取り専用）</summary>
        IReadOnlyList<EditorWindow> RotationOrder { get; }

        /// <summary>
        /// Inspectorに表示対象を割り当てる。
        /// 既に同じ内容を表示している場合は再構築せず true を返す。
        /// </summary>
        bool Assign(EditorWindow inspector, UnityEngine.Object[] objects);

        /// <summary>ローテーション順序を1つ進める（更新済みを末尾へ回す）</summary>
        void AdvanceRotation(EditorWindow updated);

        /// <summary>更新完了を通知する（フラッシュ演出・自動フォーカス）</summary>
        void NotifyUpdated(EditorWindow inspector, UnityEngine.Object[] selection);
    }

    /// <summary>
    /// ローテーション更新の戦略。
    /// 「どのInspectorに何を表示するか」だけを担い、
    /// ロック制御・フォールバック判定・タイムアウト管理は
    /// RotationLockController 側が持つ。
    /// </summary>
    public interface IRotationStrategy
    {
        RotationMode Mode { get; }

        /// <summary>
        /// 選択変更を反映する。直接更新が使える場合にのみ呼ばれる。
        /// </summary>
        /// <param name="isNavigation">
        /// 履歴の戻る/進む由来の場合 true。
        /// 新しい選択として積まず、現在の表示を差し替える。
        /// </param>
        void Apply(IRotationContext context, UnityEngine.Object[] selection, bool isNavigation);

        /// <summary>
        /// ローテーション開始時の状態を与える。
        /// 開始直後は全Inspectorがその時の選択を表示しているため、
        /// それを初期状態として扱わないと表示と内部状態がずれる。
        /// </summary>
        void Seed(UnityEngine.Object[] selection);

        /// <summary>内部状態を破棄する（ローテーション終了・モード切替時）</summary>
        void Reset();
    }
}
