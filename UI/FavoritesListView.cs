using System.Collections.Generic;
using InspectorManager.Models;
using InspectorManager.Services;
using UnityEditor;
using UnityEngine;

namespace InspectorManager.UI
{
    /// <summary>
    /// お気に入りリスト表示UI
    /// </summary>
    public class FavoritesListView
    {
        private readonly IFavoritesService _favoritesService;
        private readonly ILocalizationService _localizationService;
        private Vector2 _scrollPosition;
        private int _dragFromIndex = -1;

        public FavoritesListView(IFavoritesService favoritesService, ILocalizationService localizationService)
        {
            _favoritesService = favoritesService;
            _localizationService = localizationService;
        }

        public void Draw()
        {
            // ツールバー
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                GUILayout.Space(4);
                GUILayout.Label(_localizationService.GetString("Header_Favorites"), EditorStyles.toolbarButton);
                GUILayout.FlexibleSpace();

                // エントリ数バッジ
                var favorites = _favoritesService.GetFavorites();
                GUILayout.Label($"{favorites.Count}", Styles.HeaderBadge);
                GUILayout.Space(4);

                // クリーンアップボタン
                if (GUILayout.Button(Styles.RefreshIcon, EditorStyles.toolbarButton, GUILayout.Width(28)))
                {
                    _favoritesService.CleanupInvalidEntries();
                }

                GUILayout.Space(4);
            }
            EditorGUILayout.EndHorizontal();

            var favList = _favoritesService.GetFavorites();

            if (favList.Count == 0)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(12);
                EditorGUILayout.HelpBox(
                    _localizationService.GetString("Favorites_Empty") + "\n" + _localizationService.GetString("Favorites_AddHint"),
                    MessageType.Info);
                GUILayout.Space(12);
                EditorGUILayout.EndHorizontal();
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            {
                for (int i = 0; i < favList.Count; i++)
                {
                    DrawFavoriteEntry(favList[i], i);
                }
            }
            EditorGUILayout.EndScrollView();

            // ドラッグ終了処理
            if (Event.current.type == EventType.DragExited)
            {
                _dragFromIndex = -1;
            }
        }

        private void DrawFavoriteEntry(FavoriteEntry entry, int index)
        {
            var bgColor = index % 2 == 0 ? Styles.Colors.RowEven : Styles.Colors.RowOdd;
            var rect = EditorGUILayout.BeginHorizontal(Styles.ListItem);
            {
                EditorGUI.DrawRect(rect, bgColor);

                // 星アイコン左バー
                var barRect = new Rect(rect.x, rect.y + 2, 3, rect.height - 4);
                EditorGUI.DrawRect(barRect, Styles.Colors.WarningOrange);

                GUILayout.Space(6);

                // 削除ボタン（星アイコン）
                if (GUILayout.Button(Styles.FavoriteIcon, Styles.IconButton))
                {
                    var obj = entry.GetObject();
                    if (obj != null)
                    {
                        _favoritesService.RemoveFavorite(obj);
                    }
                }

                // オブジェクト情報
                var isValid = entry.IsValid();
                EditorGUI.BeginDisabledGroup(!isValid);
                {
                    var content = new GUIContent(
                        entry.DisplayName,
                        $"Type: {entry.ObjectType}"
                    );

                    if (GUILayout.Button(content, EditorStyles.label))
                    {
                        if (isValid)
                        {
                            Selection.activeObject = entry.GetObject();
                        }
                    }

                    // ドラッグによる並び替え
                    HandleDragAndDrop(entry, index, rect);
                }
                EditorGUI.EndDisabledGroup();

                GUILayout.FlexibleSpace();

                // 型名（キャッシュ済みスタイル使用）
                GUILayout.Label(entry.ObjectType, Styles.TypeLabel, GUILayout.Width(80));

                GUILayout.Space(4);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void HandleDragAndDrop(FavoriteEntry entry, int index, Rect rect)
        {
            var evt = Event.current;

            switch (evt.type)
            {
                case EventType.MouseDrag:
                    if (rect.Contains(evt.mousePosition))
                    {
                        _dragFromIndex = index;

                        DragAndDrop.PrepareStartDrag();
                        var obj = entry.GetObject();
                        if (obj != null)
                        {
                            DragAndDrop.objectReferences = new Object[] { obj };
                        }
                        DragAndDrop.SetGenericData("FavoriteIndex", index);
                        DragAndDrop.StartDrag(entry.DisplayName);
                        evt.Use();
                    }
                    break;

                case EventType.DragUpdated:
                    if (rect.Contains(evt.mousePosition) && _dragFromIndex >= 0 && _dragFromIndex != index)
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                        evt.Use();
                    }
                    break;

                case EventType.DragPerform:
                    if (rect.Contains(evt.mousePosition) && _dragFromIndex >= 0 && _dragFromIndex != index)
                    {
                        DragAndDrop.AcceptDrag();
                        _favoritesService.ReorderFavorite(_dragFromIndex, index);
                        _dragFromIndex = -1;
                        evt.Use();
                    }
                    break;
            }
        }
    }
}
