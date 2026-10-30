using System.Collections.Generic;
using UnityEngine;

namespace SeamlessPortals2D
{
    public readonly struct PortalPlacement
    {
        public readonly Vector2 Position;
        public readonly Vector2 Normal;
        public readonly Collider2D[] Supports;
        public PortalPlacement(Vector2 position, Vector2 normal, Collider2D[] supports)
        { Position = position; Normal = normal; Supports = supports; }
    }

    /// <summary>Surface fitting shared by preview, firing and saved-portal restoration.</summary>
    public static class PortalPlacement2D
    {
        private const float SurfaceTolerance = 0.025f;
        private const float SampleStep = 0.025f;

        public static bool IsSurface(Collider2D collider) => collider != null && collider.enabled &&
            !collider.isTrigger && collider.GetComponent<PortalCollisionProxy2D>() == null &&
            !collider.usedByEffector && (collider.attachedRigidbody == null || collider.attachedRigidbody.bodyType == RigidbodyType2D.Static);

        public static bool TryFit(Vector2 point, Vector2 normal, float length, LayerMask mask,
            out PortalPlacement placement, float cornerSnapDistance = 0.65f)
        {
            placement = default;
            if (normal.sqrMagnitude < 0.5f || length <= 0) return false;
            normal.Normalize();
            var tangent = new Vector2(normal.y, -normal.x);
            if (!Sample(point, normal, mask, out _)) return false;
            float half = length * 0.5f;
            float scan = length + cornerSnapDistance;
            float left = Extent(point, normal, -tangent, scan, mask);
            float right = Extent(point, normal, tangent, scan, mask);
            if (left + right < length - 0.005f) return false;
            float low = -left + half;
            float high = right - half;
            if (low > high + 0.005f) return false;
            float shift = Mathf.Clamp(0, low, Mathf.Max(low, high));
            // Prefer the nearest end/corner: a wall opening can sit flush with the floor.
            if (left < scan - SampleStep && Mathf.Abs(low) <= cornerSnapDistance) shift = low;
            if (right < scan - SampleStep && Mathf.Abs(high) <= cornerSnapDistance &&
                (Mathf.Abs(high) < Mathf.Abs(low) || Mathf.Abs(low) > cornerSnapDistance)) shift = high;
            var center = point + tangent * shift;
            var supports = new HashSet<Collider2D>();
            int count = Mathf.CeilToInt(length / SampleStep);
            for (int i = 0; i <= count; i++)
            {
                float t = Mathf.Lerp(-half + 0.006f, half - 0.006f, (float)i / count);
                if (!Sample(center + tangent * t, normal, mask, out var support)) return false;
                supports.Add(support);
                // The front lip must remain clear, including at concave corners.
                var front = center + tangent * t + normal * 0.04f;
                foreach (var overlap in Physics2D.OverlapPointAll(front, mask))
                    if (IsSurface(overlap)) return false;
            }
            var array = new Collider2D[supports.Count]; supports.CopyTo(array);
            placement = new PortalPlacement(center, normal, array);
            return true;
        }

        private static float Extent(Vector2 point, Vector2 normal, Vector2 direction, float limit, LayerMask mask)
        {
            float last = 0;
            for (float d = SampleStep; d <= limit; d += SampleStep)
            {
                if (!Sample(point + direction * d, normal, mask, out _) || FrontBlocked(point + direction * d, normal, mask))
                {
                    float lo = last, hi = d;
                    for (int i = 0; i < 9; i++)
                    {
                        float mid = (lo + hi) * 0.5f;
                        if (Sample(point + direction * mid, normal, mask, out _) && !FrontBlocked(point + direction * mid, normal, mask)) lo = mid;
                        else hi = mid;
                    }
                    return lo;
                }
                last = d;
            }
            return limit;
        }
        private static bool FrontBlocked(Vector2 point, Vector2 normal, LayerMask mask)
        {
            foreach (var c in Physics2D.OverlapPointAll(point + normal * 0.04f, mask))
                if (IsSurface(c)) return true;
            return false;
        }
        private static bool Sample(Vector2 point, Vector2 normal, LayerMask mask, out Collider2D support)
        {
            support = null;
            foreach (var hit in Physics2D.RaycastAll(point + normal * 0.12f, -normal, 0.24f, mask))
            {
                if (!IsSurface(hit.collider) || hit.distance <= 0.001f) continue;
                if (Vector2.Dot(hit.normal, normal) < 0.999f || Mathf.Abs(Vector2.Dot(hit.point - point, normal)) > SurfaceTolerance) return false;
                support = hit.collider;
                return true;
            }
            return false;
        }
    }
}
