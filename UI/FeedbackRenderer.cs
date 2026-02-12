using InspectorManager.Services;
using UnityEditor;
using UnityEngine;

namespace InspectorManager.UI
{
    /// <summary>
    /// トースト通知とフラッシュエフェクトの描画を担当。
    /// 複数のListViewで再利用可能。
    /// </summary>
    public class FeedbackRenderer
    {
        private int _flashedIndex = -1;
        private double _flashEndTime;
        private const double FlashDuration = 1.0;

        private string _toastMessage;
        private double _toastEndTime;
        private const double ToastDuration = 2.0;
        private bool _isPositiveFeedback;

        private readonly ILocalizationService _localizationService;

        public FeedbackRenderer(ILocalizationService localizationService)
        {
            _localizationService = localizationService;
        }

        /// <summary>
        /// 現在フラッシュ中のインデックス（-1 = なし）
        /// </summary>
        public int FlashedIndex => _flashedIndex;

        /// <summary>
        /// フラッシュ中かどうか判定
        /// </summary>
        public bool IsFlashing(int index)
        {
            return _flashedIndex == index && EditorApplication.timeSinceStartup < _flashEndTime;
        }

        /// <summary>
        /// フラッシュのアルファ値（0～1）を取得
        /// </summary>
        public float GetFlashAlpha()
        {
            float remaining = (float)(_flashEndTime - EditorApplication.timeSinceStartup);
            return Mathf.Clamp01(remaining / (float)FlashDuration);
        }

        /// <summary>
        /// フラッシュの色を取得
        /// </summary>
        public Color GetFlashColor()
        {
            return _isPositiveFeedback ? Styles.Colors.FavoriteAddFlash : Styles.Colors.FavoriteRemoveFlash;
        }

        /// <summary>
        /// フィードバックをトリガー（フラッシュ＋トースト）
        /// </summary>
        public void Trigger(int entryIndex, string objectName, bool isPositive)
        {
            _flashedIndex = entryIndex;
            _flashEndTime = EditorApplication.timeSinceStartup + FlashDuration;
            _isPositiveFeedback = isPositive;

            _toastMessage = isPositive ? $"★ {objectName}" : $"☆ {objectName}";
            _toastEndTime = EditorApplication.timeSinceStartup + ToastDuration;
        }

        /// <summary>
        /// フラッシュ終了チェック（Draw末尾で呼ぶ）
        /// </summary>
        public void UpdateState()
        {
            if (_flashedIndex >= 0 && EditorApplication.timeSinceStartup >= _flashEndTime)
            {
                _flashedIndex = -1;
            }
        }

        /// <summary>
        /// トースト通知を描画
        /// </summary>
        public void DrawToast()
        {
            if (string.IsNullOrEmpty(_toastMessage)) return;
            if (EditorApplication.timeSinceStartup >= _toastEndTime)
            {
                _toastMessage = null;
                return;
            }

            float remaining = (float)(_toastEndTime - EditorApplication.timeSinceStartup);
            float alpha = Mathf.Clamp01(remaining / 0.5f);

            EditorGUILayout.Space(4);

            var toastRect = EditorGUILayout.BeginHorizontal();
            {
                var toastBgColor = _isPositiveFeedback
                    ? new Color(Styles.Colors.FavoriteAddFlash.r, Styles.Colors.FavoriteAddFlash.g, Styles.Colors.FavoriteAddFlash.b, 0.20f * alpha)
                    : new Color(0.5f, 0.5f, 0.5f, 0.15f * alpha);
                EditorGUI.DrawRect(toastRect, toastBgColor);

                // 左のカラーバー
                var barColor = _isPositiveFeedback
                    ? new Color(Styles.Colors.WarningOrange.r, Styles.Colors.WarningOrange.g, Styles.Colors.WarningOrange.b, alpha)
                    : new Color(0.5f, 0.5f, 0.5f, alpha);
                var barRect = new Rect(toastRect.x, toastRect.y, 3, toastRect.height);
                EditorGUI.DrawRect(barRect, barColor);

                GUILayout.Space(10);

                // メッセージ（キャッシュ済みスタイル使用）
                var prevColor = Styles.ToastMessage.normal.textColor;
                Styles.ToastMessage.normal.textColor = new Color(prevColor.r, prevColor.g, prevColor.b, alpha);
                GUILayout.Label(_toastMessage, Styles.ToastMessage);
                Styles.ToastMessage.normal.textColor = prevColor;

                GUILayout.FlexibleSpace();

                // アクションテキスト
                var actionText = _isPositiveFeedback
                    ? (_localizationService?.GetString("Toast_FavoriteAdded") ?? "Added to Favorites")
                    : (_localizationService?.GetString("Toast_FavoriteRemoved") ?? "Removed from Favorites");

                var prevActionColor = Styles.ToastAction.normal.textColor;
                Styles.ToastAction.normal.textColor = new Color(prevActionColor.r, prevActionColor.g, prevActionColor.b, alpha);
                GUILayout.Label(actionText, Styles.ToastAction);
                Styles.ToastAction.normal.textColor = prevActionColor;

                GUILayout.Space(8);
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
