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
                // 戻る/進むボタン
                EditorGUI.BeginDisabledGroup(!_historyService.CanGoBack);
                if (GUILayout.Button(Styles.BackIcon, EditorStyles.toolbarButton, GUILayout.Width(24)))
                {
                    _historyService.GoBack();
                }
                EditorGUI.EndDisabledGroup();

                EditorGUI.BeginDisabledGroup(!_historyService.CanGoForward);
                if (GUILayout.Button(Styles.ForwardIcon, EditorStyles.toolbarButton, GUILayout.Width(24)))
                {
                    _historyService.GoForward();
                }
                EditorGUI.EndDisabledGroup();

                GUILayout.FlexibleSpace();

                // クリアボタン
                if (GUILayout.Button(Styles.TrashIcon, EditorStyles.toolbarButton, GUILayout.Width(24)))
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
            }
            EditorGUILayout.EndHorizontal();

            // 履歴リスト
            var history = _historyService.GetHistory();
            var currentIndex = _historyService.CurrentIndex;

            if (history.Count == 0)
            {
                EditorGUILayout.HelpBox(_localizationService.GetString("History_Empty"), MessageType.Info);
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            {
                // 新しい順に表示
                for (int i = history.Count - 1; i >= 0; i--)
                {
                    DrawHistoryEntry(history[i], i, i == currentIndex);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawHistoryEntry(HistoryEntry entry, int index, bool isCurrent)
        {
            var style = isCurrent ? Styles.ListItemSelected : Styles.ListItem;
            var rect = EditorGUILayout.BeginHorizontal(style);
            {
                // お気に入りボタン
                var obj = entry.GetObject();
                var isFavorite = obj != null && _favoritesService.IsFavorite(obj);
                var favIcon = isFavorite ? Styles.FavoriteIcon : Styles.FavoriteEmptyIcon;

                if (GUILayout.Button(favIcon, Styles.IconButton))
                {
                    if (obj != null)
                    {
                        if (isFavorite)
                            _favoritesService.RemoveFavorite(obj);
                        else
                            _favoritesService.AddFavorite(obj);
                    }
                }

                // オブジェクト情報
                var isValid = entry.IsValid();
                EditorGUI.BeginDisabledGroup(!isValid);
                {
                    // クリックでオブジェクトを選択
                    var content = new GUIContent(
                        entry.ObjectName,
                        $"Type: {entry.ObjectType}\nRecorded: {entry.RecordedAt:HH:mm:ss}"
                    );

                    if (GUILayout.Button(content, EditorStyles.label))
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

                GUILayout.FlexibleSpace();

                // 型名
                GUILayout.Label(entry.ObjectType, EditorStyles.miniLabel, GUILayout.Width(80));
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
