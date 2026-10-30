using Portal2D.Presentation.Views;
using UnityEngine;

namespace Portal2D.Presentation.Amiya
{
    // Attached only to Amiya's Animator states. Reads motion without controlling the character.
    public sealed class AmiyaAnimationState : StateMachineBehaviour
    {
        private static readonly int Speed = Animator.StringToHash("Speed");
        private static readonly int Moving = Animator.StringToHash("IsMoving");
        private static readonly int Grounded = Animator.StringToHash("IsGrounded");
        private static readonly int Airborne = Animator.StringToHash("IsJumping");
        private readonly ContactPoint2D[] contacts = new ContactPoint2D[32];
        private Rigidbody2D body;
        private PlayerEntityView character;
        private SpriteRenderer image;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) => Sample(animator);
        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) => Sample(animator);

        private void Sample(Animator animator)
        {
            if(body==null)
            {
                body=animator.GetComponent<Rigidbody2D>();
                character=animator.GetComponent<PlayerEntityView>();
                image=animator.GetComponent<SpriteRenderer>();
            }
            if(body==null) return;
            bool grounded=!body.simulated;
            if(body.simulated)
            {
                int count=body.GetContacts(contacts);
                for(int i=0;i<count;i++)
                    if(contacts[i].normal.y>0.65f) { grounded=true; break; }
            }
            float speed=body.simulated ? Mathf.Abs(body.linearVelocity.x) : 0;
            animator.SetFloat(Speed,speed);
            animator.SetBool(Moving,speed>0.1f);
            animator.SetBool(Grounded,grounded);
            // Jump is the existing airborne clip, covering both jumping and falling off an edge.
            animator.SetBool(Airborne,!grounded);
            if(image!=null && character!=null) image.flipX=character.Facing<0;
        }
    }
}
