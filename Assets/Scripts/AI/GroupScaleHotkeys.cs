using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Misadventures.AI;

namespace Misadventures.AI
{
    /// Press [ and ] (or your chosen keys) to scale ONLY the enemies
    /// that belong to the currently selected EnemyGroup.Active.
    public class GroupScaleHotkeys : MonoBehaviour
    {
        public KeyCode growKey = KeyCode.UpArrow; // ]
        public KeyCode shrinkKey = KeyCode.DownArrow; // [
        public float scaleStep = 0.1f;
        public float minScale = 0.3f;
        public float maxScale = 3.0f;

        void Update()
        {
            var g = EnemyGroup.Active;
            if (g == null) return;

            float? delta = null;
            if (Input.GetKeyDown(growKey)) delta = +scaleStep;
            if (Input.GetKeyDown(shrinkKey)) delta = -scaleStep;
            if (delta == null) return;

            // Scale only this group's members
            foreach (var ai in g.Members)
            {
                if (ai == null) continue;

                // clamp uniform scale
                var cur = ai.transform.localScale.x;
                var next = Mathf.Clamp(cur + delta.Value, minScale, maxScale);

                // apply uniform scale
                ai.transform.localScale = new Vector3(next, next, next);

                // optional: keep AI perception/attack in sync if your EnemyAI exposes a method for it.
                // If you have EnemyAI.SetScaleFactor(next), call it here.
                // ai.SetScaleFactor(next);
            }
        }
    }
}
