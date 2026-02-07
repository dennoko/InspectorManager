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
        private Vector2 _scrollPosition;
        private int _dragFromIndex = -1;

        public FavoritesListView(IFavoritesService favoritesService)
        {
            _favoritesService = favoritesService;
        }

        public void Draw()
        {
            // ツールバー
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                GUILayout.Label("お気に入り", EditorStyles.toolbarButton);
                GUILayout.FlexibleSpace();

                // クリーンアップボタン
                if (GUILayout.Button(Styles.RefreshIcon, EditorStyles.toolbarButton, GUILayout.Width(24)))
                {
                    _favoritesService.CleanupInvalidEntries();
                }
            }
            EditorGUILayout.EndHorizontal();

            var favorites = _favoritesService.GetFavorites();

            if (favorites.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "お気に入りがありません。\n履歴から☆アイコンをクリックして追加できます。",
                    MessageType.Info);
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            {
                for (int i = 0; i < favorites.Count; i++)
                {
                    DrawFavoriteEntry(favorites[i], i);
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
            var rect = EditorGUILayout.BeginHorizontal(Styles.ListItem);
            {
                // 削除ボタン
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

                // 型名
                GUILayout.Label(entry.ObjectType, EditorStyles.miniLabel, GUILayout.Width(80));
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
