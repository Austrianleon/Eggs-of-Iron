using UnityEngine;

namespace Render.Common
{
    internal static class UnityObjectLifetime
    {
        public static void Destroy(Object instance)
        {
            if (!instance) return;
            if (Application.isPlaying) Object.Destroy(instance);
            else Object.DestroyImmediate(instance);
        }
    }
}
