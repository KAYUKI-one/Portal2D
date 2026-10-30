using System.Collections.Generic;
using UnityEngine;

namespace SeamlessPortals2D
{
    /// <summary>Local +Y is the outward normal; local X spans the aperture. Use unit scale.</summary>
    [DisallowMultipleComponent]
    public sealed class Portal2D : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float apertureLength = 2.2f;
        [SerializeField] private Portal2D partner;
        [SerializeField] private Collider2D[] supportColliders = new Collider2D[0];
        [SerializeField] private Material sliceMaterial;
        public float Length => apertureLength;
        public float HalfSpan => apertureLength * 0.5f;
        public Vector2 Normal => transform.up;
        public Vector2 FacingNormal => Normal;
        public Vector2 Tangent => transform.right;
        public Portal2D Partner => partner;
        public IReadOnlyList<Collider2D> Supports => supportColliders;
        public Material SliceMaterial => sliceMaterial;
        public bool IsLinked => partner != null && partner != this && partner.isActiveAndEnabled &&
            partner.partner == this && Mathf.Abs(partner.Length - Length) < 0.001f;

        public void Configure(float length, Material material)
        {
            apertureLength = Mathf.Max(0.1f, length);
            sliceMaterial = material;
        }

        public void Link(Portal2D other)
        {
            if (other == this) return;
            Unlink();
            if (other == null) return;
            other.Unlink();
            partner = other;
            other.partner = this;
        }

        public void Unlink()
        {
            PortalSimulation2D.Cancel(this);
            var previous = partner;
            partner = null;
            if (previous != null && previous.partner == this) previous.partner = null;
        }

        public void Place(Vector2 position, Vector2 normal, Collider2D[] supports)
        {
            PortalSimulation2D.Cancel(this);
            transform.SetPositionAndRotation(position,
                Quaternion.Euler(0, 0, Vector2.SignedAngle(Vector2.up, normal.normalized)));
            transform.localScale = Vector3.one;
            supportColliders = supports ?? new Collider2D[0];
        }

        public float SignedDistance(Vector2 point) => Vector2.Dot(point - (Vector2)transform.position, Normal);
        public float RotationTo(Portal2D destination) => Vector2.SignedAngle(Normal, -destination.Normal);
        public Vector2 MapVector(Vector2 vector, Portal2D destination) =>
            (Vector2)(Quaternion.Euler(0, 0, RotationTo(destination)) * vector);
        public Vector2 MapPoint(Vector2 point, Portal2D destination) =>
            (Vector2)destination.transform.position + MapVector(point - (Vector2)transform.position, destination);

        private void OnEnable()
        {
            if (Application.isPlaying) PortalSimulation2D.Register(this);
        }
        private void OnDisable() { PortalSimulation2D.Unregister(this); }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position - (Vector3)Tangent * HalfSpan, transform.position + (Vector3)Tangent * HalfSpan);
            Gizmos.DrawRay(transform.position, Normal * 0.6f);
        }
    }
}
