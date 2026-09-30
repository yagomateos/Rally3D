using UnityEngine;

namespace Rally.Systems
{
    /// <summary>
    /// Settings applied at start by <see cref="GameBootstrap"/>: the phone-only physics guard, then the chosen
    /// <see cref="GraphicsQuality"/> level (resolution, edge smoothing, shadows, draw distances, weather particles and
    /// post effects; MEDIA by default on phones).
    /// </summary>
    public static class MobilePerformance
    {
        public static bool Active => Application.isMobilePlatform;

        public static void Apply()
        {
            // Never simulate more than 50 ms of physics per frame on a slow phone (avoids the "spiral of death").
            if (Active) Time.maximumDeltaTime = 0.05f;
            GraphicsQuality.Apply();
        }
    }
}
