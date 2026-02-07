using UnityEditor;
using UnityEngine;

namespace InspectorManager.UI
{
    /// <summary>
    /// 共通GUIスタイル定義
    /// </summary>
    public static class Styles
    {
        private static bool _initialized;
        private static GUIStyle _headerLabel;
        private static GUIStyle _listItem;
        private static GUIStyle _listItemSelected;
        private static GUIStyle _iconButton;
        private static GUIStyle _miniButton;
        private static GUIStyle _toolbarToggle;
        private static GUIStyle _centeredLabel;

        public static GUIStyle HeaderLabel
        {
            get
            {
                EnsureInitialized();
                return _headerLabel;
            }
        }

        public static GUIStyle ListItem
        {
            get
            {
                EnsureInitialized();
                return _listItem;
            }
        }

        public static GUIStyle ListItemSelected
        {
            get
            {
                EnsureInitialized();
                return _listItemSelected;
            }
        }

        public static GUIStyle IconButton
        {
            get
            {
                EnsureInitialized();
                return _iconButton;
            }
        }

        public static GUIStyle MiniButton
        {
            get
            {
                EnsureInitialized();
                return _miniButton;
            }
        }

        public static GUIStyle ToolbarToggle
        {
            get
            {
                EnsureInitialized();
                return _toolbarToggle;
            }
        }

        public static GUIStyle CenteredLabel
        {
            get
            {
                EnsureInitialized();
                return _centeredLabel;
            }
        }

        // アイコン
        public static GUIContent LockIcon => EditorGUIUtility.IconContent("LockIcon-On");
        public static GUIContent UnlockIcon => EditorGUIUtility.IconContent("LockIcon");
        public static GUIContent FavoriteIcon => EditorGUIUtility.IconContent("Favorite Icon");
        public static GUIContent FavoriteEmptyIcon => EditorGUIUtility.IconContent("Favorite");
        public static GUIContent RefreshIcon => EditorGUIUtility.IconContent("Refresh");
        public static GUIContent TrashIcon => EditorGUIUtility.IconContent("TreeEditor.Trash");
        public static GUIContent BackIcon => EditorGUIUtility.IconContent("back");
        public static GUIContent ForwardIcon => EditorGUIUtility.IconContent("forward");
        public static GUIContent SettingsIcon => EditorGUIUtility.IconContent("Settings");

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            _headerLabel = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                margin = new RectOffset(4, 4, 8, 4)
            };

            _listItem = new GUIStyle("PR Label")
            {
                padding = new RectOffset(4, 4, 4, 4),
                margin = new RectOffset(0, 0, 1, 1),
                fixedHeight = 22
            };

            _listItemSelected = new GUIStyle(_listItem);
            _listItemSelected.normal.background = CreateColorTexture(new Color(0.24f, 0.49f, 0.91f, 0.5f));

            _iconButton = new GUIStyle("IconButton")
            {
                padding = new RectOffset(2, 2, 2, 2),
                margin = new RectOffset(2, 2, 2, 2),
                fixedWidth = 20,
                fixedHeight = 20
            };

            _miniButton = new GUIStyle(EditorStyles.miniButton)
            {
                padding = new RectOffset(8, 8, 2, 2)
            };

            _toolbarToggle = new GUIStyle(EditorStyles.toolbarButton)
            {
                padding = new RectOffset(8, 8, 2, 2)
            };

            _centeredLabel = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter
            };
        }

        private static Texture2D CreateColorTexture(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        // 色定数
        public static class Colors
        {
            public static readonly Color LockedBackground = new Color(1f, 0.7f, 0.7f, 0.3f);
            public static readonly Color UnlockedBackground = new Color(0.7f, 1f, 0.7f, 0.3f);
            public static readonly Color Separator = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        }
    }
}
