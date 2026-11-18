using System.Collections.Generic;
using InspectorManager.Core;
using UnityEditor;

namespace InspectorManager.Services
{
    /// <summary>
    /// Inspectorウィンドウ管理サービスの実装。
    ///
    /// Resources.FindObjectsOfTypeAll の返却順は保証されていないため、
    /// 「初めて観測した順」を SessionState に記録して安定した並び順を提供する。
    /// これにより UI 上の固定番号 (#1, #2...) がウィンドウの開閉やドメインリロードで
    /// 入れ替わらなくなる。
    /// EditorWindow のインスタンスIDはドメインリロードを跨いで維持されるため、
    /// SessionState（エディタ起動中のみ保持）と寿命が一致する。
    /// </summary>
    public class InspectorWindowService : IInspectorWindowService
    {
        private const string OrderStateKey = "InspectorManager.WindowOrder";
        private const char OrderSeparator = ',';

        /// <summary>初観測順に並んだInspectorのインスタンスID</summary>
        private readonly List<int> _knownOrder = new List<int>();

        /// <summary>並び替え作業用バッファ</summary>
        private readonly List<EditorWindow> _ordered = new List<EditorWindow>();

        public InspectorWindowService()
        {
            LoadOrder();
        }

        public bool IsAvailable => InspectorReflection.IsAvailable;

        public IReadOnlyList<EditorWindow> GetAllInspectors()
        {
            var found = InspectorReflection.GetAllInspectorWindows();

            _ordered.Clear();

            // 既知の順序に従って並べつつ、閉じられたウィンドウを除去する
            for (int i = _knownOrder.Count - 1; i >= 0; i--)
            {
                if (IndexOfId(found, _knownOrder[i]) < 0)
                {
                    _knownOrder.RemoveAt(i);
                }
            }

            // 未観測のウィンドウを末尾に追加する。
            // 同一フレームで複数見つかった場合でも決定的になるようインスタンスID順にする。
            var newcomers = new List<EditorWindow>();
            foreach (var window in found)
            {
                if (window == null) continue;
                if (_knownOrder.Contains(window.GetInstanceID())) continue;
                newcomers.Add(window);
            }
            newcomers.Sort((a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));

            bool changed = newcomers.Count > 0;
            foreach (var window in newcomers)
            {
                _knownOrder.Add(window.GetInstanceID());
            }

            foreach (var id in _knownOrder)
            {
                int index = IndexOfId(found, id);
                if (index >= 0) _ordered.Add(found[index]);
            }

            if (changed || _knownOrder.Count != _ordered.Count)
            {
                SaveOrder();
            }

            // 内部バッファをそのまま返すと、呼び出し側が反復中に
            // 再度 GetAllInspectors() を呼んだ際にバッファが作り替えられてしまう。
            // （UI は行ごとに固定番号を引くためこの再入が実際に起きる）
            return new List<EditorWindow>(_ordered);
        }

        private static int IndexOfId(List<EditorWindow> windows, int instanceId)
        {
            for (int i = 0; i < windows.Count; i++)
            {
                if (windows[i] != null && windows[i].GetInstanceID() == instanceId) return i;
            }
            return -1;
        }

        private void LoadOrder()
        {
            _knownOrder.Clear();

            var raw = SessionState.GetString(OrderStateKey, string.Empty);
            if (string.IsNullOrEmpty(raw)) return;

            foreach (var part in raw.Split(OrderSeparator))
            {
                if (int.TryParse(part, out var id) && !_knownOrder.Contains(id))
                {
                    _knownOrder.Add(id);
                }
            }
        }

        private void SaveOrder()
        {
            SessionState.SetString(OrderStateKey, string.Join(OrderSeparator.ToString(), _knownOrder));
        }

        public bool IsLocked(EditorWindow inspector)
        {
            return InspectorReflection.GetLockedState(inspector);
        }

        public void SetLocked(EditorWindow inspector, bool locked)
        {
            if (inspector == null) return;

            var currentState = IsLocked(inspector);
            if (currentState != locked)
            {
                InspectorReflection.SetLockedState(inspector, locked);

                // イベントを発行
                EventBus.Instance.Publish(new InspectorLockChangedEvent
                {
                    Inspector = inspector,
                    IsLocked = locked
                });
            }
        }

        public void LockAll()
        {
            var inspectors = GetAllInspectors();
            foreach (var inspector in inspectors)
            {
                SetLocked(inspector, true);
            }
        }

        public void UnlockAll()
        {
            var inspectors = GetAllInspectors();
            foreach (var inspector in inspectors)
            {
                SetLocked(inspector, false);
            }
        }

        public UnityEngine.Object GetInspectedObject(EditorWindow inspector)
        {
            return InspectorReflection.GetInspectedObject(inspector);
        }
    }
}
