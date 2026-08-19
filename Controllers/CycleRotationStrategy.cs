namespace InspectorManager.Controllers
{
    /// <summary>
    /// サイクルモード。
    /// 先頭のInspector（＝最も古く更新されたもの）に現在の選択を表示し、
    /// そのInspectorを順序の末尾へ回す。
    /// </summary>
    public class CycleRotationStrategy : IRotationStrategy
    {
        public RotationMode Mode => RotationMode.Cycle;

        public void Apply(IRotationContext context, UnityEngine.Object[] selection, bool isNavigation)
        {
            var order = context.RotationOrder;
            if (order.Count == 0) return;

            var target = order[0];
            if (!context.Assign(target, selection)) return;

            // 履歴の戻る/進む由来の場合はローテーションを進めない。
            // 進めてしまうと、戻るたびに別のInspectorが消費されてしまう。
            if (!isNavigation) context.AdvanceRotation(target);

            context.NotifyUpdated(target, selection);
        }

        public void Reset()
        {
            // 保持する内部状態はない
        }
    }
}
