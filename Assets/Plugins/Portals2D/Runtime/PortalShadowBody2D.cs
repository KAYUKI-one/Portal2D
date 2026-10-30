using System.Collections.Generic;
using UnityEngine;

namespace SeamlessPortals2D
{
    // A collision-only copy of a REMOTE obstacle, never a second controlled character.
    [AddComponentMenu("")]
    public sealed class PortalShadowBody2D : MonoBehaviour
    {
        internal readonly HashSet<Rigidbody2D> Owners = new();
        internal Portal2D Entry;
        internal Rigidbody2D Original;
        internal Rigidbody2D Body;
        internal readonly Dictionary<Collider2D,CustomCollider2D> Shapes = new();
        private Vector2 initialVelocity, initialCenter;
        private float initialSpin, initialAngle;
        private bool responsePending;

        internal void Prepare()
        {
            var exit=Entry.Partner;
            if(Original != null)
            {
                Body.position=exit.MapPoint(Original.position,Entry);
                Body.rotation=Original.rotation+exit.RotationTo(Entry);
                Body.bodyType=Original.bodyType;
                Body.constraints=Original.constraints & RigidbodyConstraints2D.FreezeRotation;
                if((Original.constraints & RigidbodyConstraints2D.FreezePositionX)!=0) FreezeMappedAxis(exit.MapVector(Vector2.right,Entry));
                if((Original.constraints & RigidbodyConstraints2D.FreezePositionY)!=0) FreezeMappedAxis(exit.MapVector(Vector2.up,Entry));
                Body.mass=Original.mass;
                Body.centerOfMass=Original.centerOfMass;
                if(Original.inertia>0) Body.inertia=Original.inertia;
                if(Body.bodyType!=RigidbodyType2D.Static)
                {
                    Body.linearVelocity=exit.MapVector(Original.linearVelocity,Entry);
                    Body.angularVelocity=Original.angularVelocity;
                }
            }
            initialVelocity=Body.linearVelocity; initialSpin=Body.angularVelocity;
            initialCenter=Body.worldCenterOfMass; initialAngle=Body.rotation;
            responsePending=true;
        }
        private void FreezeMappedAxis(Vector2 axis)
        {
            if(Mathf.Abs(axis.x)>0.999f) Body.constraints |= RigidbodyConstraints2D.FreezePositionX;
            if(Mathf.Abs(axis.y)>0.999f) Body.constraints |= RigidbodyConstraints2D.FreezePositionY;
        }
        private void OnCollisionEnter2D(Collision2D collision) => ApplyResponse(collision);
        private void OnCollisionStay2D(Collision2D collision) => ApplyResponse(collision);
        private void ApplyResponse(Collision2D collision)
        {
            if(!responsePending || Original == null || Original.bodyType!=RigidbodyType2D.Dynamic ||
                !Original.simulated || !Owners.Contains(collision.rigidbody) || Entry == null || !Entry.IsLinked) return;
            responsePending=false; // All contacts were solved together: relay the aggregate only once.
            var exit=Entry.Partner;
            Vector2 Constrain(Vector2 delta)
            {
                if((Original.constraints & RigidbodyConstraints2D.FreezePositionX)!=0) delta.x=0;
                if((Original.constraints & RigidbodyConstraints2D.FreezePositionY)!=0) delta.y=0;
                return delta;
            }
            Original.linearVelocity += Constrain(Entry.MapVector(Body.linearVelocity-initialVelocity,exit));
            Original.angularVelocity += Body.angularVelocity-initialSpin;
            // Relay only the contact-induced displacement; ordinary free motion is already simulated.
            Original.position += Constrain(Entry.MapVector(Body.worldCenterOfMass-initialCenter-initialVelocity*Time.fixedDeltaTime,exit));
            Original.rotation += Mathf.DeltaAngle(initialAngle+initialSpin*Time.fixedDeltaTime,Body.rotation);
        }
        internal void Stop()
        {
            responsePending=false; Owners.Clear(); Body.simulated=false;
            foreach(var c in Shapes.Values) if(c!=null) c.enabled=false;
            Destroy(gameObject);
        }
    }
}
