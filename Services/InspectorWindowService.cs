using System.Collections.Generic;
using InspectorManager.Core;
using UnityEditor;

namespace InspectorManager.Services
{
    /// <summary>
    /// Inspectorウィンドウ管理サービスの実装
    /// </summary>
    public class InspectorWindowService : IInspectorWindowService
    {
        public bool IsAvailable => InspectorReflection.IsAvailable;

        public IReadOnlyList<EditorWindow> GetAllInspectors()
        {
            return InspectorReflection.GetAllInspectorWindows();
        }

        public bool IsLocked(EditorWindow inspector)
        {
            return InspectorReflection.GetLockedState(inspector);
        }

        public void SetLocked(EditorWindow inspector, bool locked)
        {
            if (inspector == null) return;

            var currentState = IsLocked(inspector);
            if (currentState != locked)
            {
                InspectorReflection.SetLockedState(inspector, locked);

                // イベントを発行
                EventBus.Instance.Publish(new InspectorLockChangedEvent
                {
                    Inspector = inspector,
                    IsLocked = locked
                });
            }
        }

        public void LockAll()
        {
            var inspectors = GetAllInspectors();
            foreach (var inspector in inspectors)
            {
                SetLocked(inspector, true);
            }
        }

        public void UnlockAll()
        {
            var inspectors = GetAllInspectors();
            foreach (var inspector in inspectors)
            {
                SetLocked(inspector, false);
            }
        }

        public UnityEngine.Object GetInspectedObject(EditorWindow inspector)
        {
            return InspectorReflection.GetInspectedObject(inspector);
        }
    }
}
