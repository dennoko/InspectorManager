using System.Collections.Generic;
using UnityEditor;
using InspectorManager.Services;

namespace InspectorManager.Controllers
{
    /// <summary>
    /// ローテーション除外リストの管理を担当
    /// </summary>
    public class ExclusionManager
    {
        private readonly List<EditorWindow> _excludedWindows = new List<EditorWindow>();
        private readonly IInspectorWindowService _inspectorService;

        public ExclusionManager(IInspectorWindowService inspectorService)
        {
            _inspectorService = inspectorService;
        }

        /// <summary>
        /// Inspectorの除外状態を設定
        /// </summary>
        /// <param name="inspector">対象Inspector</param>
        /// <param name="isExcluded">除外するか</param>
        /// <param name="rotationOrder">ローテーション順序リスト（除外時にリストから削除）</param>
        /// <param name="syncAction">除外解除時の再同期コールバック</param>
        public void SetExcluded(EditorWindow inspector, bool isExcluded,
            List<EditorWindow> rotationOrder, System.Action syncAction = null)
        {
            if (inspector == null) return;

            if (isExcluded)
            {
                if (!_excludedWindows.Contains(inspector))
                {
                    _excludedWindows.Add(inspector);
                    rotationOrder?.Remove(inspector);
                    // 除外したInspectorはロック状態を維持
                    _inspectorService.SetLocked(inspector, true);
                }
            }
            else
            {
                if (_excludedWindows.Remove(inspector))
                {
                    syncAction?.Invoke();
                }
            }
        }

        /// <summary>
        /// 指定Inspectorが除外されているかどうかを返す
        /// </summary>
        public bool IsExcluded(EditorWindow inspector)
        {
            return inspector != null && _excludedWindows.Contains(inspector);
        }

        /// <summary>
        /// 除外リストをクリア
        /// </summary>
        public void Clear()
        {
            _excludedWindows.Clear();
        }

        /// <summary>
        /// 除外リストから無効な参照を除去
        /// </summary>
        public void CleanupInvalid()
        {
            _excludedWindows.RemoveAll(w => w == null);
        }
    }
}
