using UnityEditor;
using UnityEngine;

namespace InspectorManager.UI
{
    /// <summary>
    /// 共通GUIスタイル定義 — 洗練されたエディタUI向けデザインシステム
    /// </summary>
    public static class Styles
    {
        private static bool _initialized;

        // ── 基本スタイル ──
        private static GUIStyle _sectionHeader;
        private static GUIStyle _subSectionHeader;
        private static GUIStyle _listItem;
        private static GUIStyle _listItemSelected;
        private static GUIStyle _iconButton;
        private static GUIStyle _miniButton;
        private static GUIStyle _centeredLabel;

        // ── ヘッダー用 ──
        private static GUIStyle _headerToggle;
        private static GUIStyle _headerBadge;

        // ── タブ用 ──
        private static GUIStyle _tabActive;
        private static GUIStyle _tabInactive;

        // ── ボタン ──
        private static GUIStyle _actionButton;
        private static GUIStyle _dangerButton;

        // ── 設定 ──
        private static GUIStyle _languageToolbar;

        // ── バッジ ──
        private static GUIStyle _badgeNext;
        private static GUIStyle _badgeOrder;
        private static GUIStyle _badgeExcluded;

        // ── キャッシュ済みラベル（パフォーマンス用）──
        private static GUIStyle _typeLabel;
        private static GUIStyle _toastMessage;
        private static GUIStyle _toastAction;

        // ──────────────── プロパティ ────────────────

        public static GUIStyle SectionHeader { get { EnsureInitialized(); return _sectionHeader; } }
        public static GUIStyle SubSectionHeader { get { EnsureInitialized(); return _subSectionHeader; } }
        public static GUIStyle ListItem { get { EnsureInitialized(); return _listItem; } }
        public static GUIStyle ListItemSelected { get { EnsureInitialized(); return _listItemSelected; } }
        public static GUIStyle IconButton { get { EnsureInitialized(); return _iconButton; } }
        public static GUIStyle MiniButton { get { EnsureInitialized(); return _miniButton; } }
        public static GUIStyle CenteredLabel { get { EnsureInitialized(); return _centeredLabel; } }
        public static GUIStyle HeaderToggle { get { EnsureInitialized(); return _headerToggle; } }
        public static GUIStyle HeaderBadge { get { EnsureInitialized(); return _headerBadge; } }
        public static GUIStyle TabActive { get { EnsureInitialized(); return _tabActive; } }
        public static GUIStyle TabInactive { get { EnsureInitialized(); return _tabInactive; } }
        public static GUIStyle ActionButton { get { EnsureInitialized(); return _actionButton; } }
        public static GUIStyle DangerButton { get { EnsureInitialized(); return _dangerButton; } }
        public static GUIStyle LanguageToolbar { get { EnsureInitialized(); return _languageToolbar; } }
        public static GUIStyle BadgeNext { get { EnsureInitialized(); return _badgeNext; } }
        public static GUIStyle BadgeOrder { get { EnsureInitialized(); return _badgeOrder; } }
        public static GUIStyle BadgeExcluded { get { EnsureInitialized(); return _badgeExcluded; } }
        public static GUIStyle TypeLabel { get { EnsureInitialized(); return _typeLabel; } }
        public static GUIStyle ToastMessage { get { EnsureInitialized(); return _toastMessage; } }
        public static GUIStyle ToastAction { get { EnsureInitialized(); return _toastAction; } }

        // 旧互換プロパティ
        public static GUIStyle HeaderLabel => SectionHeader;
        public static GUIStyle ToolbarToggle { get { EnsureInitialized(); return _headerToggle; } }

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

            // ── セクションヘッダー ──
            _sectionHeader = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                margin = new RectOffset(4, 4, 4, 4),
                padding = new RectOffset(2, 0, 0, 0),
            };
            _sectionHeader.normal.textColor = Colors.TextPrimary;

            // ── サブセクションヘッダー ──
            _subSectionHeader = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                margin = new RectOffset(4, 4, 4, 2),
                padding = new RectOffset(0, 0, 0, 0),
            };
            _subSectionHeader.normal.textColor = Colors.AccentBlue;

            // ── リストアイテム ──
            _listItem = new GUIStyle()
            {
                padding = new RectOffset(4, 4, 5, 5),
                margin = new RectOffset(4, 4, 1, 1),
                fixedHeight = 28,
                border = new RectOffset(4, 4, 4, 4),
            };
            _listItem.normal.textColor = Colors.TextPrimary;

            _listItemSelected = new GUIStyle(_listItem);
            _listItemSelected.normal.background = CreateRoundedTexture(Colors.SelectionHighlight, 3);

            // ── アイコンボタン ──
            _iconButton = new GUIStyle("IconButton")
            {
                padding = new RectOffset(2, 2, 2, 2),
                margin = new RectOffset(2, 2, 2, 2),
                fixedWidth = 22,
                fixedHeight = 22
            };

            // ── ミニボタン ──
            _miniButton = new GUIStyle(EditorStyles.miniButton)
            {
                padding = new RectOffset(10, 10, 3, 3),
                margin = new RectOffset(2, 2, 2, 2),
                fontSize = 10,
                fixedHeight = 22,
            };

            // ── 中央ラベル ──
            _centeredLabel = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter
            };

            // ── ヘッダートグル ──
            _headerToggle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                padding = new RectOffset(4, 8, 2, 2),
                fixedHeight = 22,
            };
            _headerToggle.normal.textColor = Colors.TextBright;
            _headerToggle.onNormal.textColor = Colors.TextBright;

            // ── ヘッダーバッジ ──
            _headerBadge = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                padding = new RectOffset(6, 6, 2, 2),
                fixedHeight = 18,
            };
            _headerBadge.normal.textColor = Colors.TextBright;
            _headerBadge.normal.background = CreateRoundedTexture(new Color(1f, 1f, 1f, 0.15f), 8);

            // ── タブ (アクティブ) ──
            _tabActive = new GUIStyle(EditorStyles.toolbarButton)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                fixedHeight = 26,
                padding = new RectOffset(10, 10, 4, 4),
                margin = new RectOffset(0, 0, 0, 0),
            };
            _tabActive.normal.textColor = Colors.AccentBlue;
            _tabActive.normal.background = CreateColorTexture(Colors.CardBackground);

            // ── タブ (非アクティブ) ──
            _tabInactive = new GUIStyle(EditorStyles.toolbarButton)
            {
                fontSize = 11,
                fixedHeight = 26,
                padding = new RectOffset(10, 10, 4, 4),
                margin = new RectOffset(0, 0, 0, 0),
            };
            _tabInactive.normal.textColor = Colors.TextSecondary;
            _tabInactive.normal.background = CreateColorTexture(new Color(0.18f, 0.18f, 0.18f, 1f));
            _tabInactive.hover.textColor = Colors.TextPrimary;

            // ── アクションボタン ──
            _actionButton = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 11,
                padding = new RectOffset(12, 12, 4, 4),
                margin = new RectOffset(2, 2, 2, 2),
                fixedHeight = 24,
            };
            _actionButton.normal.textColor = Colors.TextPrimary;

            // ── 危険ボタン ──
            _dangerButton = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 11,
                padding = new RectOffset(12, 12, 4, 4),
                margin = new RectOffset(2, 2, 2, 2),
                fixedHeight = 24,
            };
            _dangerButton.normal.textColor = Colors.DangerRed;

            // ── 言語ツールバー ──
            _languageToolbar = new GUIStyle(EditorStyles.toolbarButton)
            {
                fontSize = 11,
                fixedHeight = 24,
                padding = new RectOffset(12, 12, 4, 4),
            };

            // ── バッジ: Next ──
            _badgeNext = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                fixedHeight = 16,
                padding = new RectOffset(0, 0, 0, 0),
            };
            _badgeNext.normal.textColor = Colors.AccentBlue;

            // ── バッジ: 順序番号 ──
            _badgeOrder = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 10,
                fixedHeight = 16,
                padding = new RectOffset(2, 2, 1, 1),
            };
            _badgeOrder.normal.textColor = Colors.TextBright;

            // ── バッジ: 除外 ──
            _badgeExcluded = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 10,
                fontStyle = FontStyle.Italic,
                fixedHeight = 16,
                padding = new RectOffset(2, 2, 1, 1),
            };
            _badgeExcluded.normal.textColor = Colors.TextMuted;

            // ── 型名ラベル（キャッシュ）──
            _typeLabel = new GUIStyle(EditorStyles.miniLabel);
            _typeLabel.normal.textColor = Colors.TextSecondary;

            // ── トーストメッセージ ──
            _toastMessage = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
            };
            _toastMessage.normal.textColor = Colors.TextPrimary;

            // ── トーストアクション ──
            _toastAction = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Italic,
            };
            _toastAction.normal.textColor = Colors.TextSecondary;
        }

        private static Texture2D CreateColorTexture(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// 角丸風テクスチャ（簡易版 — 小さいサイズで角を丸くしたテクスチャ）
        /// </summary>
        private static Texture2D CreateRoundedTexture(Color color, int radius)
        {
            int size = Mathf.Max(radius * 2 + 2, 8);
            var texture = new Texture2D(size, size);
            texture.hideFlags = HideFlags.HideAndDontSave;
            
            var transparent = new Color(color.r, color.g, color.b, 0f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 各コーナーからの距離をチェック
                    bool inCorner = false;
                    float dist = 0;
                    
                    if (x < radius && y < radius) { dist = Vector2.Distance(new Vector2(x, y), new Vector2(radius, radius)); inCorner = true; }
                    else if (x >= size - radius && y < radius) { dist = Vector2.Distance(new Vector2(x, y), new Vector2(size - radius - 1, radius)); inCorner = true; }
                    else if (x < radius && y >= size - radius) { dist = Vector2.Distance(new Vector2(x, y), new Vector2(radius, size - radius - 1)); inCorner = true; }
                    else if (x >= size - radius && y >= size - radius) { dist = Vector2.Distance(new Vector2(x, y), new Vector2(size - radius - 1, size - radius - 1)); inCorner = true; }

                    if (inCorner && dist > radius)
                        texture.SetPixel(x, y, transparent);
                    else
                        texture.SetPixel(x, y, color);
                }
            }
            
            texture.Apply();
            texture.filterMode = FilterMode.Bilinear;
            return texture;
        }

        // ──────────────── 色定数 ────────────────
        public static class Colors
        {
            // ── プライマリカラーパレット ──
            public static readonly Color AccentBlue = new Color(0.26f, 0.52f, 0.96f, 1f);        // #4285F4
            public static readonly Color AccentGreen = new Color(0.20f, 0.78f, 0.35f, 1f);        // #34C759
            public static readonly Color StatusGreen = new Color(0.20f, 0.78f, 0.35f, 1f);        // #34C759 (Alias)
            public static readonly Color DangerRed = new Color(0.92f, 0.34f, 0.34f, 1f);          // #EB5757
            public static readonly Color WarningOrange = new Color(0.96f, 0.65f, 0.14f, 1f);      // #F5A623

            // ── 背景 ──
            public static readonly Color CardBackground = new Color(0.22f, 0.22f, 0.22f, 1f);
            public static readonly Color RowEven = new Color(0.21f, 0.21f, 0.21f, 1f);
            public static readonly Color RowOdd = new Color(0.24f, 0.24f, 0.24f, 1f);
            public static readonly Color SelectionHighlight = new Color(0.26f, 0.52f, 0.96f, 0.30f);
            public static readonly Color FlashHighlight = new Color(0.20f, 0.78f, 0.35f, 0.40f);

            // ── テキスト ──
            public static readonly Color TextBright = new Color(0.95f, 0.95f, 0.95f, 1f);
            public static readonly Color TextPrimary = new Color(0.82f, 0.82f, 0.82f, 1f);
            public static readonly Color TextSecondary = new Color(0.58f, 0.58f, 0.58f, 1f);
            public static readonly Color TextMuted = new Color(0.45f, 0.45f, 0.45f, 1f);

            // ── ボーダー ──
            public static readonly Color Separator = new Color(0.30f, 0.30f, 0.30f, 1f);
            public static readonly Color Border = new Color(0.15f, 0.15f, 0.15f, 1f);

            // ── フラッシュエフェクト ──
            public static readonly Color FavoriteAddFlash = new Color(0.96f, 0.65f, 0.14f, 0.45f);
            public static readonly Color FavoriteRemoveFlash = new Color(0.5f, 0.5f, 0.5f, 0.3f);
            public static readonly Color FavoriteGlow = new Color(0.96f, 0.65f, 0.14f, 0.25f);

            // ── ロック状態 ──
            public static readonly Color LockedBackground = new Color(0.92f, 0.34f, 0.34f, 0.15f);
            public static readonly Color UnlockedBackground = new Color(0.20f, 0.78f, 0.35f, 0.10f);
        }
    }
}
