using System;
using UnityEditor;

namespace InspectorManager.Core
{
    /// <summary>
    /// Selection.selectionChanged の購読を一箇所に集約し、
    /// EventBus 経由で SelectionChangedEvent として配信する。
    ///
    /// 各コンポーネントが個別に Selection.selectionChanged を購読すると
    /// 呼び出し順が未定義になり、「履歴ナビゲーション中は記録しない」のような
    /// 横断的な制御ができない。購読を1本にまとめることで、
    /// EventBus の登録順＝処理順として決定的に扱える。
    /// </summary>
    public class SelectionCoordinator : IDisposable
    {
        private bool _disposed;

        public SelectionCoordinator()
        {
            Selection.selectionChanged += OnSelectionChanged;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Selection.selectionChanged -= OnSelectionChanged;
        }

        private void OnSelectionChanged()
        {
            EventBus.Instance.Publish(new SelectionChangedEvent
            {
                SelectedObjects = Selection.objects,
                ActiveObject = Selection.activeObject,
            });
        }
    }
}
