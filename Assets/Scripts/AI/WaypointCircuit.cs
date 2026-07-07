using UnityEngine;

namespace ApexRush.AI
{
    /// <summary>
    /// A looped racing line defined by this object's children, in order.
    /// Authoring: create an empty "AIWaypoints", add empty children along the track
    /// center (one every ~15-25 m, extra ones through corners at the apex line),
    /// and it just works — gizmos draw the loop so you can see it in the Scene view.
    /// </summary>
    public class WaypointCircuit : MonoBehaviour
    {
        private Transform[] points;

        public int Count => Points.Length;

        private Transform[] Points
        {
            get
            {
                if (points == null || points.Length != transform.childCount)
                {
                    points = new Transform[transform.childCount];
                    for (int i = 0; i < transform.childCount; i++)
                        points[i] = transform.GetChild(i);
                }
                return points;
            }
        }

        public Vector3 GetPosition(int index) =>
            Points[((index % Count) + Count) % Count].position;

        /// <summary>Index of the waypoint nearest to a world position (for spawn/reset).</summary>
        public int GetNearestIndex(Vector3 position)
        {
            int best = 0;
            float bestSqr = float.PositiveInfinity;
            for (int i = 0; i < Count; i++)
            {
                float sqr = (Points[i].position - position).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = i; }
            }
            return best;
        }

        private void OnDrawGizmos()
        {
            if (transform.childCount < 2) return;
            Gizmos.color = Color.cyan;
            for (int i = 0; i < transform.childCount; i++)
            {
                Vector3 a = transform.GetChild(i).position;
                Vector3 b = transform.GetChild((i + 1) % transform.childCount).position;
                Gizmos.DrawLine(a, b);
                Gizmos.DrawWireSphere(a, 1f);
            }
        }
    }
}
