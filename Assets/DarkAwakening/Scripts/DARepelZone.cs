using UnityEngine;

namespace DarkAwakening
{
    /// <summary>A gap/vent the Krasue peeks through. The player repels her by aiming here while holding the trigger.</summary>
    public class DARepelZone : MonoBehaviour
    {
        public enum Need { Fire, Thorns }
        public Need need = Need.Fire;
        public Collider zoneCollider;
        public Transform peekPoint;        // where her head hovers (outside the wall)
        public Vector3 facing = Vector3.back; // direction she faces (toward the room)
        public float holdSeconds = 1.8f;
    }
}
