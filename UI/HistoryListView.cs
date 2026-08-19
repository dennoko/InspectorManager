using InspectorManager.Models;
using InspectorManager.Services;
using UnityEditor;
using UnityEngine;

namespace InspectorManager.UI
{
    /// <summary>
    /// 履歴リスト表示UI
    /// </summary>
    public class HistoryListView
    {
        private readonly IHistoryService _historyService;
        private readonly IFavoritesService _favoritesService;
        private readonly ILocalizationService _localizationService;
        private readonly FeedbackRenderer _feedback;
        private Vector2 _scrollPosition;

        public HistoryListView(IHistoryService historyService, IFavoritesService favoritesService, ILocalizationService localizationService)
        {
            _historyService = historyService;
            _favoritesService = favoritesService;
            _localizationService = localizationService;
            _feedback = new FeedbackRenderer(localizationService);
        }

        public void Draw()
        {
            // ツールバー
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                GUILayout.Space(4);

                // 戻る/進むボタン
                EditorGUI.BeginDisabledGroup(!_historyService.CanGoBack);
                if (GUILayout.Button(Styles.BackIcon, EditorStyles.toolbarButton, GUILayout.Width(28)))
                {
                    _historyService.GoBack();
                }
                EditorGUI.EndDisabledGroup();

                EditorGUI.BeginDisabledGroup(!_historyService.CanGoForward);
                if (GUILayout.Button(Styles.ForwardIcon, EditorStyles.toolbarButton, GUILayout.Width(28)))
                {
                    _historyService.GoForward();
                }
                EditorGUI.EndDisabledGroup();

                GUILayout.FlexibleSpace();

                // エントリ数表示
                var history = _historyService.GetHistory();
                GUILayout.Label($"{history.Count}", Styles.HeaderBadge);
                GUILayout.Space(4);

                // クリアボタン
                if (GUILayout.Button(Styles.TrashIcon, EditorStyles.toolbarButton, GUILayout.Width(28)))
                {
                    // モーダルダイアログとリスト変更を描画中に行うとレイアウトが崩れる
                    EditorApplication.delayCall += () =>
                    {
                        if (EditorUtility.DisplayDialog(
                            _localizationService.GetString("History_Clear"),
                            _localizationService.GetString("Confirm_ClearHistory"),
                            _localizationService.GetString("Button_Clear"),
                            _localizationService.GetString("Button_Cancel")))
                        {
                            _historyService.ClearHistory();
                        }
                    };
                }

                GUILayout.Space(4);
            }
            EditorGUILayout.EndHorizontal();

            // 履歴リスト
            var historyList = _historyService.GetHistory();
            var currentIndex = _historyService.CurrentIndex;

            if (historyList.Count == 0)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(12);
                EditorGUILayout.HelpBox(_localizationService.GetString("History_Empty"), MessageType.Info);
                GUILayout.Space(12);
                EditorGUILayout.EndHorizontal();
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            {
                for (int i = historyList.Count - 1; i >= 0; i--)
                {
                    DrawHistoryEntry(historyList[i], i, i == currentIndex);
                }
            }
            EditorGUILayout.EndScrollView();

            // トースト通知
            _feedback.DrawToast();
            _feedback.UpdateState();
        }

        private void DrawHistoryEntry(HistoryEntry entry, int index, bool isCurrent)
        {
            var style = isCurrent ? Styles.ListItemSelected : Styles.ListItem;

            // 背景色の計算
            Color bgColor;
            if (_feedback.IsFlashing(index))
            {
                float t = _feedback.GetFlashAlpha();
                bgColor = Color.Lerp(
                    index % 2 == 0 ? Styles.Colors.RowEven : Styles.Colors.RowOdd,
                    _feedback.GetFlashColor(),
                    t);
            }
            else if (isCurrent)
            {
                bgColor = Styles.Colors.SelectionHighlight;
            }
            else
            {
                bgColor = index % 2 == 0 ? Styles.Colors.RowEven : Styles.Colors.RowOdd;
            }

            var rect = EditorGUILayout.BeginHorizontal(style);
            {
                EditorGUI.DrawRect(rect, bgColor);

                // 現在位置インジケーター
                if (isCurrent)
                {
                    var barRect = new Rect(rect.x, rect.y + 2, 3, rect.height - 4);
                    EditorGUI.DrawRect(barRect, Styles.Colors.AccentBlue);
                }

                GUILayout.Space(6);

                // お気に入りボタン
                // GetObject() は行ごとに1回だけ呼ぶ（IsValid() も内部で呼ぶため）
                var obj = entry.GetObject();
                var isValid = obj != null;
                var isFavorite = isValid && _favoritesService.IsFavorite(obj);

                if (isFavorite)
                {
                    var favBtnRect = GUILayoutUtility.GetRect(22, 22, GUILayout.Width(22));
                    var glowRect = new Rect(favBtnRect.x - 1, favBtnRect.y - 1, favBtnRect.width + 2, favBtnRect.height + 2);
                    EditorGUI.DrawRect(glowRect, Styles.Colors.FavoriteGlow);
                    
                    if (GUI.Button(favBtnRect, Styles.FavoriteIcon, Styles.IconButton))
                    {
                        ToggleFavoriteDeferred(obj, index, entry.ObjectName, false);
                    }
                }
                else
                {
                    if (GUILayout.Button(Styles.FavoriteEmptyIcon, Styles.IconButton))
                    {
                        ToggleFavoriteDeferred(obj, index, entry.ObjectName, true);
                    }
                }

                // オブジェクト情報
                EditorGUI.BeginDisabledGroup(!isValid);
                {
                    var content = new GUIContent(
                        entry.ObjectName,
                        $"Type: {entry.ObjectType}\nRecorded: {entry.RecordedAt:HH:mm:ss}"
                    );

                    // ウィンドウ幅から固定要素の幅を引いた残りをクリック領域に割り当てる。
                    // BeginHorizontal が返す rect はレイアウトパスでは未確定なので、
                    // 両パスで同じ値になる currentViewWidth を使う。
                    // 固定要素: 左パディング(6) + お気に入りアイコン(22) + 型名(60) + 右パディング(4) ≈ 92
                    // スクロールバーとリストアイテムのマージン分も差し引く
                    float availableWidth = EditorGUIUtility.currentViewWidth - 92f - 24f;
                    if (availableWidth < 80f) availableWidth = 80f;

                    if (GUILayout.Button(content, EditorStyles.label, GUILayout.Width(availableWidth)))
                    {
                        if (isValid)
                        {
                            Selection.activeObject = obj;
                        }
                    }

                    // ドラッグ対応
                    if (Event.current.type == EventType.MouseDrag && isValid)
                    {
                        var lastRect = GUILayoutUtility.GetLastRect();
                        if (lastRect.Contains(Event.current.mousePosition))
                        {
                            DragAndDrop.PrepareStartDrag();
                            DragAndDrop.objectReferences = new Object[] { obj };
                            DragAndDrop.StartDrag(entry.ObjectName);
                            Event.current.Use();
                        }
                    }
                }
                EditorGUI.EndDisabledGroup();

                // 型名（キャッシュ済みスタイル使用）
                GUILayout.Label(entry.ObjectType, Styles.TypeLabel, GUILayout.Width(60));

                GUILayout.Space(4);
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// お気に入りの追加/削除を描画完了後に実行する。
        /// 描画中に切り替えると、同じフレームの Layout パスと Repaint パスで
        /// 星アイコンの分岐が変わり、レイアウトが崩れる。
        /// </summary>
        private void ToggleFavoriteDeferred(Object obj, int index, string objectName, bool add)
        {
            if (obj == null) return;

            EditorApplication.delayCall += () =>
            {
                if (obj == null) return;

                if (add) _favoritesService.AddFavorite(obj);
                else _favoritesService.RemoveFavorite(obj);
            };

            _feedback.Trigger(index, objectName, add);
        }
    }
}
