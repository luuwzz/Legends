using UnityEngine;

namespace DarkAwakening
{
    /// <summary>Makes a quad face the camera (flames, glows, stars...).</summary>
    public class DABillboard : MonoBehaviour
    {
        public bool lockY = false;
        Camera cam;

        void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            Vector3 d = transform.position - cam.transform.position;
            if (lockY) d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(d);
        }
    }
}
