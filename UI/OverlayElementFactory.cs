using InspectorManager.Services;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace InspectorManager.UI
{
    /// <summary>
    /// InspectorオーバーレイのVisualElement生成を担当
    /// </summary>
    public static class OverlayElementFactory
    {
        public const string OverlayName = "InspectorManagerOverlay";
        public const string LockButtonName = "LockButton";

        /// <summary>
        /// オーバーレイのルートVisualElementを生成
        /// </summary>
        public static VisualElement Create(
            EditorWindow inspector,
            IInspectorWindowService inspectorService,
            System.Action<EditorWindow> onLockToggled)
        {
            var overlay = new VisualElement
            {
                name = OverlayName,
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexShrink = 0,
                    height = 26,
                    backgroundColor = new StyleColor(new Color(0.14f, 0.14f, 0.14f, 1f)),
                    borderBottomWidth = 1,
                    borderBottomColor = new StyleColor(new Color(0.10f, 0.10f, 0.10f, 1f)),
                    paddingTop = 2,
                    paddingBottom = 2,
                    paddingLeft = 2,
                    paddingRight = 2,
                    alignItems = Align.Center,
                }
            };

            var button = new Button(() =>
            {
                bool current = inspectorService.IsLocked(inspector);
                inspectorService.SetLocked(inspector, !current);
                inspector.Repaint();
                onLockToggled?.Invoke(inspector);
            })
            {
                name = LockButtonName,
                style =
                {
                    flexGrow = 1,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = new StyleColor(new Color(0.90f, 0.90f, 0.90f, 1f)),
                    fontSize = 11,
                    borderTopLeftRadius = 3,
                    borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3,
                    borderBottomRightRadius = 3,
                    marginLeft = 1,
                    marginRight = 1,
                    paddingLeft = 8,
                    paddingRight = 8,
                }
            };

            overlay.Add(button);
            return overlay;
        }

        /// <summary>
        /// NEXTバッジ用のラベルを生成
        /// </summary>
        public static Label CreateNextBadge(string text)
        {
            return new Label(text)
            {
                style =
                {
                    backgroundColor = new StyleColor(Styles.Colors.AccentBlue),
                    color = new StyleColor(Color.white),
                    fontSize = 9,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    borderTopLeftRadius = 3,
                    borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3,
                    borderBottomRightRadius = 3,
                    paddingLeft = 4,
                    paddingRight = 4,
                    paddingTop = 1,
                    paddingBottom = 1,
                    marginLeft = 2,
                }
            };
        }
    }
}
