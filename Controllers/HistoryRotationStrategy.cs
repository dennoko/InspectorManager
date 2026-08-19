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

            // 履歴の戻る/進む由来の場合は積まずに先頭を差し替える。
            // 積むと同じオブジェクトが重複し、最も古い履歴が押し出されてしまう。
            if (isNavigation && _history.Count > 0)
            {
                _history[0] = selection;
            }
            else
            {
                _history.Insert(0, selection);
            }

            // 破棄済みオブジェクトを取り除いて詰める。
            // 詰めずにスキップすると、そのInspectorだけ古い表示が残り
            // 「最新／1つ前／2つ前」の対応関係が崩れてしまう。
            Compact();
            Trim(order.Count + HistoryMargin);

            for (int i = 0; i < order.Count; i++)
            {
                if (i >= _history.Count) break;

                // 内容が変わっていないInspectorは Assign 側でスキップされる
                context.Assign(order[i], _history[i]);
            }

            context.NotifyUpdated(order[0], selection);
        }

        public void Seed(UnityEngine.Object[] selection)
        {
            _history.Clear();

            // ローテーション開始時、各Inspectorはその時点の選択を表示している。
            // これを履歴の先頭として扱わないと、次の選択で
            // 「2番目のInspectorに1つ前を出す」対応関係が1つずれる。
            if (selection != null && selection.Length > 0)
            {
                _history.Add(selection);
            }
        }

        public void Reset()
        {
            _history.Clear();
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
