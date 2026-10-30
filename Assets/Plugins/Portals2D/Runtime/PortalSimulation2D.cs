using System.Collections.Generic;
using UnityEngine;

namespace SeamlessPortals2D
{
    [DefaultExecutionOrder(10000), AddComponentMenu("")]
    public sealed class PortalSimulation2D : MonoBehaviour
    {
        private sealed class Traveller
        {
            public Rigidbody2D Body;
            public Collider2D[] Colliders;
            public Portal2D Portal;
            public PortalCollisionScope Collision;
            public PortalSlicedVisuals Visual;
            public RigidbodyInterpolation2D Interpolation;
        }
        private static PortalSimulation2D instance;
        private readonly HashSet<Portal2D> portals = new();
        private readonly Dictionary<Rigidbody2D, Traveller> travellers = new();
        private readonly HashSet<Portal2D> collisionWarnings = new();
        private readonly PortalShadowWorld2D shadowWorld = new();
        private readonly Dictionary<Rigidbody2D, RigidbodyInterpolation2D> interpolationAfterWarp = new();
        public static void Register(Portal2D portal)
        {
            if (instance == null) instance = new GameObject("[Portal physics]").AddComponent<PortalSimulation2D>();
            instance.portals.Add(portal);
        }
        public static void Unregister(Portal2D portal)
        {
            if (instance == null) return;
            Cancel(portal); instance.portals.Remove(portal);
        }
        public static void Cancel(Portal2D portal)
        {
            if (instance == null) return;
            instance.collisionWarnings.Remove(portal);
            var remove = new List<Rigidbody2D>();
            foreach (var pair in instance.travellers)
                if (pair.Value.Portal == portal || pair.Value.Portal.Partner == portal) remove.Add(pair.Key);
            foreach (var key in remove) instance.End(key, true);
        }
        public static void ReleaseBody(Rigidbody2D body)
        {
            if (instance != null && body != null) instance.End(body, true);
        }
        public static bool IsTravelling(Rigidbody2D body) => instance != null && instance.travellers.ContainsKey(body);
        private void FixedUpdate()
        {
            // Let one destination-side physics step rebuild interpolation history before enabling it.
            RestoreWarpInterpolation();
            var remove = new List<Rigidbody2D>();
            foreach (var t in new List<Traveller>(travellers.Values))
            {
                var body = t.Body; var p = t.Portal;
                if (!travellers.ContainsKey(body)) continue;
                if (body == null || !body.simulated || !body.gameObject.activeInHierarchy || p == null || !p.IsLinked)
                { remove.Add(body); continue; }
                Project(t.Colliders, p.Normal, out float minNormal, out float maxNormal);
                float plane = Vector2.Dot(p.transform.position, p.Normal);
                // The original body owns gravity and forces until its trailing edge clears the plane.
                if (maxNormal < plane - 0.001f)
                {
                    Vector2 previous = body.position;
                    var destination = p.Partner;
                    float rotation = p.RotationTo(destination);
                    body.interpolation = RigidbodyInterpolation2D.None;
                    interpolationAfterWarp[body] = t.Interpolation;
                    body.position = p.MapPoint(body.position, destination);
                    body.rotation += rotation;
                    body.linearVelocity = p.MapVector(body.linearVelocity, destination);
                    // Rigidbody setters do not synchronously move the rendered Transform. The clone
                    // is removed below, so both poses must agree before publishing the handoff.
                    body.transform.SetPositionAndRotation(new Vector3(body.position.x, body.position.y, body.transform.position.z),
                        Quaternion.Euler(0, 0, body.rotation));
                    Physics2D.SyncTransforms();
                    t.Portal = destination;
                    PortalEvents.Publish(new PortalTransitEvent(PortalTransitPhase.Teleported, body, p, destination, previous, rotation));
                    if (!travellers.ContainsKey(body)) continue;
                    End(body, false);
                    continue;
                }
                Project(t.Colliders, p.Tangent, out float minTangent, out float maxTangent);
                float mid = Vector2.Dot(p.transform.position, p.Tangent);
                if (minNormal > plane + 0.04f || maxTangent < mid - p.HalfSpan || minTangent > mid + p.HalfSpan)
                    remove.Add(body);
                else t.Collision.IgnoreOtherBodies();
            }
            foreach (var body in remove) End(body, false);
            if (portals.Count == 0) { shadowWorld.Dispose(); return; }
            foreach (var body in FindObjectsByType<Rigidbody2D>())
            {
                if (body.bodyType == RigidbodyType2D.Static || !body.simulated || travellers.ContainsKey(body) ||
                    body.GetComponent<PortalCollisionProxy2D>() != null) continue;
                var all = body.GetComponentsInChildren<Collider2D>();
                var solids = new List<Collider2D>();
                foreach (var c in all) if (c.enabled && !c.isTrigger && c.attachedRigidbody == body) solids.Add(c);
                if (solids.Count == 0) continue;
                var colliders = solids.ToArray();
                foreach (var p in portals)
                {
                    if (p == null || !p.IsLinked) continue;
                    float bodyRadius = 0;
                    foreach (var c in colliders) bodyRadius = Mathf.Max(bodyRadius, Vector2.Distance(c.bounds.center, body.worldCenterOfMass) + c.bounds.extents.magnitude);
                    float reach = bodyRadius + p.HalfSpan + body.linearVelocity.magnitude * Time.fixedDeltaTime + 0.1f;
                    if (((Vector2)p.transform.position - body.worldCenterOfMass).sqrMagnitude > reach * reach) continue;
                    float distance = p.SignedDistance(body.worldCenterOfMass);
                    float speed = Vector2.Dot(body.linearVelocity, p.Normal);
                    if (distance < -0.001f || speed > 0.05f) continue;
                    Project(colliders, p.Normal, out float near, out _);
                    float gap = near - Vector2.Dot(p.transform.position, p.Normal);
                    if (gap > Mathf.Max(0.06f, -speed * Time.fixedDeltaTime + 0.04f)) continue;
                    Project(colliders, p.Tangent, out float min, out float max);
                    float center = Vector2.Dot(p.transform.position, p.Tangent);
                    if (min < center - p.HalfSpan - 0.005f || max > center + p.HalfSpan + 0.005f) continue;
                    if (!PortalCollisionScope.TryCreate(body, colliders, p, out var collision, out var failure))
                    {
                        if (collisionWarnings.Add(p)) Debug.LogWarning($"Portal entry kept solid: {failure}", p);
                        continue;
                    }
                    collisionWarnings.Remove(p);
                    var t = new Traveller { Body = body, Colliders = colliders, Portal = p,
                        Interpolation = body.interpolation,
                        Collision = collision };
                    try { t.Visual = new PortalSlicedVisuals(body, p.SliceMaterial); }
                    catch { collision.Dispose(); throw; }
                    travellers.Add(body, t);
                    PortalEvents.Publish(new PortalTransitEvent(PortalTransitPhase.Entered, body, p, p.Partner, body.position));
                    break;
                }
            }
            var active = new Dictionary<Rigidbody2D,Portal2D>();
            foreach (var t in travellers.Values) active.Add(t.Body,t.Portal);
            try { shadowWorld.Refresh(active); }
            catch (System.Exception error)
            {
                // Keep failures closed: no traveller may continue through a missing remote collider.
                foreach (var pair in active)
                {
                    if (collisionWarnings.Add(pair.Value)) Debug.LogWarning($"Portal entry kept solid: {error.Message}",pair.Value);
                    End(pair.Key,true);
                }
                shadowWorld.Dispose();
            }
        }
        internal static void Project(Collider2D[] colliders, Vector2 axis, out float min, out float max)
        {
            min = float.PositiveInfinity; max = float.NegativeInfinity;
            var group = new PhysicsShapeGroup2D(); var vertices = new List<Vector2>();
            foreach (var c in colliders)
            {
                if (c == null || !c.enabled) continue;
                group.Clear(); c.GetShapes(group);
                for (int i = 0; i < group.shapeCount; i++)
                {
                    var shape = group.GetShape(i); vertices.Clear(); group.GetShapeVertices(i, vertices);
                    foreach (var v in vertices)
                    {
                        float d = Vector2.Dot(group.localToWorldMatrix.MultiplyPoint3x4(v), axis);
                        min = Mathf.Min(min, d - shape.radius); max = Mathf.Max(max, d + shape.radius);
                    }
                }
            }
        }
        private void LateUpdate()
        {
            foreach (var t in travellers.Values) t.Visual.Update(t.Portal);
        }
        private void End(Rigidbody2D body, bool cancelled)
        {
            if (!travellers.TryGetValue(body, out var t)) return;
            travellers.Remove(body);
            shadowWorld.Release(body);
            if (body != null)
            {
                if (cancelled && t.Portal != null)
                {
                    Project(t.Colliders, t.Portal.Normal, out float min, out _);
                    float plane = Vector2.Dot(t.Portal.transform.position, t.Portal.Normal);
                    if (min < plane + 0.02f)
                    {
                        body.position += t.Portal.Normal * (plane + 0.02f - min);
                        float inward = Vector2.Dot(body.linearVelocity, t.Portal.Normal);
                        if (inward < 0) body.linearVelocity -= t.Portal.Normal * inward;
                    }
                }
                if (!interpolationAfterWarp.ContainsKey(body)) body.interpolation = t.Interpolation;
            }
            t.Collision.Dispose(); t.Visual.Dispose();
            if (body != null) PortalEvents.Publish(new PortalTransitEvent(cancelled ? PortalTransitPhase.Cancelled : PortalTransitPhase.Exited,
                body, t.Portal, t.Portal != null ? t.Portal.Partner : null, body.position));
        }
        private void OnDestroy()
        {
            foreach (var body in new List<Rigidbody2D>(travellers.Keys)) End(body, true);
            RestoreWarpInterpolation();
            shadowWorld.Dispose();
            if (instance == this) instance = null;
        }
        private void RestoreWarpInterpolation()
        {
            foreach (var pair in interpolationAfterWarp) if (pair.Key != null) pair.Key.interpolation = pair.Value;
            interpolationAfterWarp.Clear();
        }
    }
}
