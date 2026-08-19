using System.Collections.Generic;

namespace InspectorManager.Controllers
{
    /// <summary>
    /// 履歴モード。
    /// インスペクタリストの上から順に履歴を割り当てる
    /// （1番目＝最新、2番目＝1つ前...）。ウィンドウの並び順は固定のまま。
    /// </summary>
    public class HistoryRotationStrategy : IRotationStrategy
    {
        /// <summary>Inspector数に対して余分に保持する履歴の数</summary>
        private const int HistoryMargin = 5;

        /// <summary>選択履歴。1エントリ＝1回分の選択集合（複数選択もそのまま保持）</summary>
        private readonly List<UnityEngine.Object[]> _history = new List<UnityEngine.Object[]>();

        public RotationMode Mode => RotationMode.History;

        public void Apply(IRotationContext context, UnityEngine.Object[] selection, bool isNavigation)
        {
            var order = context.RotationOrder;
            if (order.Count == 0) return;

            // 履歴の戻る/進む由来でも同じように積む。
            // 「先頭だけ差し替える」方式だと、戻った先が既に2番目にある場合に
            // 1番目と2番目が同じものになってしまう。
            _history.Insert(0, selection);

            // 同じ対象が複数のInspectorに並ぶことに意味はないので、
            // 後方にある同一の履歴は取り除く。
            // これで「戻る」で重複するケースも、A→B→A と選び直したときに
            // 1番目と3番目が同じになるケースも同時に防げる。
            RemoveDuplicatesAfterHead();

            // 破棄済みオブジェクトを取り除いて詰める。
            // 詰めずにスキップすると、そのInspectorだけ古い表示が残り
            // 「最新／1つ前／2つ前」の対応関係が崩れてしまう。
            Compact();
            Trim(order.Count + HistoryMargin);

            ApplyLayout(context);

            context.NotifyUpdated(order[0], selection);
        }

        /// <summary>
        /// 現在の履歴をそのままInspectorへ割り当てる（履歴は積まない）。
        /// </summary>
        public void ApplyLayout(IRotationContext context)
        {
            var order = context.RotationOrder;

            for (int i = 0; i < order.Count; i++)
            {
                if (i >= _history.Count) break;

                // 内容が変わっていないInspectorは Assign 側でスキップされる
                context.Assign(order[i], _history[i]);
            }
        }

        public void Seed(IReadOnlyList<UnityEngine.Object[]> recent)
        {
            _history.Clear();
            if (recent == null) return;

            // ローテーション開始時、全Inspectorはその時点の選択を表示している。
            // ここで過去の選択を種として入れておかないと、2番目以降のInspectorは
            // 履歴が溜まるまで開始時の対象を表示したままになり、
            // 「2番目と3番目が同じ」状態になる。
            for (int i = 0; i < recent.Count; i++)
            {
                var entry = recent[i];
                if (entry == null || entry.Length == 0) continue;
                if (IndexOf(entry) >= 0) continue;

                _history.Add(entry);
            }
        }

        public void Reset()
        {
            _history.Clear();
        }

        /// <summary>
        /// 先頭と同じ内容の履歴を後方から取り除く
        /// </summary>
        private void RemoveDuplicatesAfterHead()
        {
            if (_history.Count < 2) return;

            var head = _history[0];
            for (int i = _history.Count - 1; i >= 1; i--)
            {
                if (IsSameSelection(_history[i], head)) _history.RemoveAt(i);
            }
        }

        private int IndexOf(UnityEngine.Object[] entry)
        {
            for (int i = 0; i < _history.Count; i++)
            {
                if (IsSameSelection(_history[i], entry)) return i;
            }
            return -1;
        }

        /// <summary>
        /// 2つの選択集合が同一かどうか（順序も含めて比較）
        /// </summary>
        private static bool IsSameSelection(UnityEngine.Object[] a, UnityEngine.Object[] b)
        {
            if (a == null || b == null) return ReferenceEquals(a, b);
            if (a.Length != b.Length) return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        private void Trim(int maxCount)
        {
            while (_history.Count > maxCount)
            {
                _history.RemoveAt(_history.Count - 1);
            }
        }

        /// <summary>
        /// 破棄済みオブジェクトを取り除き、空になったエントリを詰める。
        /// </summary>
        private void Compact()
        {
            for (int i = _history.Count - 1; i >= 0; i--)
            {
                var entry = _history[i];

                if (entry == null || entry.Length == 0)
                {
                    _history.RemoveAt(i);
                    continue;
                }

                int aliveCount = 0;
                for (int j = 0; j < entry.Length; j++)
                {
                    if (entry[j] != null) aliveCount++;
                }

                if (aliveCount == entry.Length) continue;

                if (aliveCount == 0)
                {
                    _history.RemoveAt(i);
                    continue;
                }

                var alive = new UnityEngine.Object[aliveCount];
                int k = 0;
                for (int j = 0; j < entry.Length; j++)
                {
                    if (entry[j] != null) alive[k++] = entry[j];
                }
                _history[i] = alive;
            }
        }
    }
}
