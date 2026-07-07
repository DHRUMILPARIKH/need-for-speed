using UnityEngine;

namespace ApexRush.Race
{
    /// <summary>
    /// A gate on the track. Needs a BoxCollider with Is Trigger ON, wide and tall
    /// enough that no car can drive around or under it.
    ///
    /// Checkpoints live as children of one "Checkpoints" parent; RaceManager assigns
    /// Index from sibling order at startup, so you never type indices by hand —
    /// just keep the children ordered in driving direction. Child 0 is the
    /// start/finish line.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class Checkpoint : MonoBehaviour
    {
        public int Index { get; internal set; }

        private void Reset()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            // Cars are compound colliders; attachedRigidbody finds the car root.
            if (other.attachedRigidbody == null) return;
            var participant = other.attachedRigidbody.GetComponent<RaceParticipant>();
            if (participant != null) participant.OnCheckpointPassed(this);
        }

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }
    }
}
