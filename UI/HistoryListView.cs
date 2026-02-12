using System.Collections.Generic;
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
        private Vector2 _scrollPosition;

        // お気に入りフィードバック用
        private int _flashedEntryIndex = -1;
        private double _flashEndTime;
        private const double FlashDuration = 1.0;

        // トースト通知用
        private string _toastMessage;
        private double _toastEndTime;
        private const double ToastDuration = 2.0;
        private bool _toastIsAdd; // true = 追加, false = 削除

        public HistoryListView(IHistoryService historyService, IFavoritesService favoritesService, ILocalizationService localizationService)
        {
            _historyService = historyService;
            _favoritesService = favoritesService;
            _localizationService = localizationService;
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
                    if (EditorUtility.DisplayDialog(
                        _localizationService.GetString("History_Clear"),
                        _localizationService.GetString("Confirm_ClearHistory"),
                        _localizationService.GetString("Button_Clear"),
                        _localizationService.GetString("Button_Cancel")))
                    {
                        _historyService.ClearHistory();
                    }
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
                // 新しい順に表示
                for (int i = historyList.Count - 1; i >= 0; i--)
                {
                    DrawHistoryEntry(historyList[i], i, i == currentIndex);
                }
            }
            EditorGUILayout.EndScrollView();

            // トースト通知
            DrawToast();

            // フラッシュ中はRepaintを要求
            if (_flashedEntryIndex >= 0 && EditorApplication.timeSinceStartup < _flashEndTime)
            {
                // EditorWindowのRepaintを間接的に要求
                EditorApplication.delayCall += () => { };
            }
            else if (_flashedEntryIndex >= 0)
            {
                _flashedEntryIndex = -1;
            }
        }

        private void DrawHistoryEntry(HistoryEntry entry, int index, bool isCurrent)
        {
            var style = isCurrent ? Styles.ListItemSelected : Styles.ListItem;

            // フラッシュ判定
            bool isFlashing = _flashedEntryIndex == index && EditorApplication.timeSinceStartup < _flashEndTime;
            
            // 背景色の計算
            Color bgColor;
            if (isFlashing)
            {
                float remaining = (float)(_flashEndTime - EditorApplication.timeSinceStartup);
                float t = Mathf.Clamp01(remaining / (float)FlashDuration);
                var flashColor = _toastIsAdd 
                    ? new Color(0.96f, 0.65f, 0.14f, 0.45f)  // オレンジ（追加時）
                    : new Color(0.5f, 0.5f, 0.5f, 0.3f);      // グレー（削除時）
                bgColor = Color.Lerp(
                    index % 2 == 0 ? Styles.Colors.RowEven : Styles.Colors.RowOdd,
                    flashColor,
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
                var obj = entry.GetObject();
                var isFavorite = obj != null && _favoritesService.IsFavorite(obj);

                // お気に入り済みの場合、星を目立たせる
                if (isFavorite)
                {
                    // 背景にオレンジのハイライト
                    var favBtnRect = GUILayoutUtility.GetRect(22, 22, GUILayout.Width(22));
                    var glowRect = new Rect(favBtnRect.x - 1, favBtnRect.y - 1, favBtnRect.width + 2, favBtnRect.height + 2);
                    EditorGUI.DrawRect(glowRect, new Color(0.96f, 0.65f, 0.14f, 0.25f));
                    
                    if (GUI.Button(favBtnRect, Styles.FavoriteIcon, Styles.IconButton))
                    {
                        if (obj != null)
                        {
                            _favoritesService.RemoveFavorite(obj);
                            TriggerFeedback(index, entry.ObjectName, false);
                        }
                    }
                }
                else
                {
                    if (GUILayout.Button(Styles.FavoriteEmptyIcon, Styles.IconButton))
                    {
                        if (obj != null)
                        {
                            _favoritesService.AddFavorite(obj);
                            TriggerFeedback(index, entry.ObjectName, true);
                        }
                    }
                }

                // オブジェクト情報
                var isValid = entry.IsValid();
                EditorGUI.BeginDisabledGroup(!isValid);
                {
                    var content = new GUIContent(
                        entry.ObjectName,
                        $"Type: {entry.ObjectType}\nRecorded: {entry.RecordedAt:HH:mm:ss}"
                    );

                    if (GUILayout.Button(content, EditorStyles.label, GUILayout.ExpandWidth(true)))
                    {
                        if (isValid)
                        {
                            Selection.activeObject = obj;
                        }
                    }

                    // ドラッグ対応
                    if (Event.current.type == EventType.MouseDrag && isValid && obj != null)
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

                // 型名（薄い文字で右寄せ）
                var typeStyle = new GUIStyle(EditorStyles.miniLabel);
                typeStyle.normal.textColor = Styles.Colors.TextSecondary;
                GUILayout.Label(entry.ObjectType, typeStyle, GUILayout.Width(80));

                GUILayout.Space(4);
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// お気に入り追加/削除時のフィードバックをトリガー
        /// </summary>
        private void TriggerFeedback(int entryIndex, string objectName, bool isAdd)
        {
            // 行フラッシュ
            _flashedEntryIndex = entryIndex;
            _flashEndTime = EditorApplication.timeSinceStartup + FlashDuration;
            _toastIsAdd = isAdd;

            // トースト通知
            if (isAdd)
            {
                _toastMessage = $"★ {objectName}";
            }
            else
            {
                _toastMessage = $"☆ {objectName}";
            }
            _toastEndTime = EditorApplication.timeSinceStartup + ToastDuration;
        }

        /// <summary>
        /// 画面下部にトースト通知を表示
        /// </summary>
        private void DrawToast()
        {
            if (string.IsNullOrEmpty(_toastMessage)) return;
            if (EditorApplication.timeSinceStartup >= _toastEndTime)
            {
                _toastMessage = null;
                return;
            }

            // フェードアウト計算
            float remaining = (float)(_toastEndTime - EditorApplication.timeSinceStartup);
            float alpha = Mathf.Clamp01(remaining / 0.5f); // 最後の0.5秒でフェードアウト

            EditorGUILayout.Space(4);

            // トーストバー
            var toastRect = EditorGUILayout.BeginHorizontal();
            {
                var toastBgColor = _toastIsAdd
                    ? new Color(0.96f, 0.65f, 0.14f, 0.20f * alpha)  // オレンジ
                    : new Color(0.5f, 0.5f, 0.5f, 0.15f * alpha);     // グレー

                EditorGUI.DrawRect(toastRect, toastBgColor);

                // 左のカラーバー
                var barColor = _toastIsAdd
                    ? new Color(0.96f, 0.65f, 0.14f, alpha)
                    : new Color(0.5f, 0.5f, 0.5f, alpha);
                var barRect = new Rect(toastRect.x, toastRect.y, 3, toastRect.height);
                EditorGUI.DrawRect(barRect, barColor);

                GUILayout.Space(10);

                // メッセージ
                var msgStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                };
                msgStyle.normal.textColor = new Color(
                    Styles.Colors.TextPrimary.r, 
                    Styles.Colors.TextPrimary.g, 
                    Styles.Colors.TextPrimary.b, 
                    alpha);
                
                GUILayout.Label(_toastMessage, msgStyle);

                GUILayout.FlexibleSpace();

                // 追加/削除テキスト
                var actionText = _toastIsAdd
                    ? (_localizationService?.GetString("Toast_FavoriteAdded") ?? "Added to Favorites")
                    : (_localizationService?.GetString("Toast_FavoriteRemoved") ?? "Removed from Favorites");
                
                var actionStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontStyle = FontStyle.Italic,
                };
                actionStyle.normal.textColor = new Color(
                    Styles.Colors.TextSecondary.r,
                    Styles.Colors.TextSecondary.g,
                    Styles.Colors.TextSecondary.b,
                    alpha);
                
                GUILayout.Label(actionText, actionStyle);
                GUILayout.Space(8);
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
