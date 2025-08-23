using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Misadventures.AI;

namespace Misadventures.AI
{
    /// <summary>
    /// Listens for hotkeys and uniformly scales all members of the currently active EnemyGroup.
    /// Only affects transforms of agents referenced by EnemyGroup.Active.Members.
    /// </summary>
    public class GroupScaleHotkeys : MonoBehaviour
    {
        // Key used to increase the scale of group members
        public KeyCode growKey = KeyCode.UpArrow;

        // Key used to decrease the scale of group members
        public KeyCode shrinkKey = KeyCode.DownArrow;

        // Amount added or subtracted from the current uniform scale per key press
        public float scaleStep = 0.1f;

        // Minimum allowed uniform scale
        public float minScale = 0.3f;

        // Maximum allowed uniform scale
        public float maxScale = 3.0f;

        private void Update()
        {
            // Retrieve the active group; exit when no active group is set
            var g = EnemyGroup.Active;
            if (g == null) return;

            // Determine scale delta based on hotkey input for this frame
            float? delta = null;
            if (Input.GetKeyDown(growKey)) delta = +scaleStep;
            if (Input.GetKeyDown(shrinkKey)) delta = -scaleStep;
            if (delta == null) return;

            // Apply uniform scaling to each valid member of the active group
            foreach (var ai in g.Members)
            {
                if (ai == null) continue;

                // Read current uniform scale (assumes X/Y/Z are equal)
                float current = ai.transform.localScale.x;

                // Compute next scale and clamp within allowed range
                float next = Mathf.Clamp(current + delta.Value, minScale, maxScale);

                // Write back as a uniform scale vector
                ai.transform.localScale = new Vector3(next, next, next);

            }
        }
    }
}
