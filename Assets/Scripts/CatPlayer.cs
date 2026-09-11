using UnityEngine;

namespace MiaCourt
{
    public sealed class CatPlayer : MonoBehaviour
    {
        public int team;
        public string displayName;
        public Transform visual;
        public GameObject selectionRing;
        public float stamina = 1f;
        public float actionCooldown;
        /// <summary>Seconds the current swipe is still live. A reach sweeps, it is not an instant.</summary>
        public float reach;
        public float possessionAge;
        public float distanceTravelled;
        public float jump;
        public Vector3 movement;
        float gait;
        float facing = 180f;
        float jumpVelocity;
        Vector3 visualOrigin;
        Quaternion visualBase = Quaternion.identity;

        public void Initialize(GameObject source, Texture2D texture, int index)
        {
            team = index;
            if (visual != null) Destroy(visual.gameObject);
            if (selectionRing != null) Destroy(selectionRing);
            GameObject model = Instantiate(source, transform);
            model.name = "Character visual";
            visual = model.transform;
            // The FBX importer bakes the Z-up to Y-up correction into the model root's rotation.
            // Animate composes with this instead of overwriting it, which would face-plant the cat.
            visualBase = visual.localRotation;
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
            float scale = 2.35f / Mathf.Max(.1f, bounds.size.y);
            visual.localScale *= scale;
            visual.localPosition -= new Vector3(bounds.center.x - transform.position.x, bounds.min.y - transform.position.y,
                bounds.center.z - transform.position.z) * scale;
            visualOrigin = visual.localPosition;
            Material fur = CourtBuilder.Material("Original character texture", Color.white, .14f);
            fur.mainTexture = texture;
            foreach (Renderer r in renderers) r.sharedMaterial = fur;
            selectionRing = CourtBuilder.Ring("Player marker", transform, Vector3.up * .035f, .7f, .055f,
                index == 0 ? CourtBuilder.Mint : CourtBuilder.Coral, false);
        }

        public void Move(Vector3 direction, bool sprint, float dt)
        {
            direction = Vector3.ClampMagnitude(direction, 1f);
            bool running = sprint && stamina > .04f && direction.sqrMagnitude > .05f;
            float speed = (team == 0 ? 4.7f : 4.05f) * (running ? 1.48f : 1f);
            stamina = Mathf.Clamp01(stamina + (running ? -.34f : .21f) * dt);
            Vector3 before = transform.position;
            transform.position = BasketballRules.ClampToCourt(before + direction * speed * dt);
            movement = (transform.position - before) / Mathf.Max(dt, .0001f);
            distanceTravelled += Vector3.Distance(before, transform.position);
        }

        public void Animate(float dt, bool hasBall, bool lobby)
        {
            actionCooldown = Mathf.Max(0, actionCooldown - dt);
            possessionAge = hasBall ? possessionAge + dt : 0;
            float speed = lobby ? .25f : movement.magnitude;
            gait += dt * (2.8f + speed * 2.1f);
            if (jump > 0 || jumpVelocity > 0)
            {
                // Same gravity as the ball, integrated over the average velocity across the step,
                // which is exact under constant acceleration so the apex does not drift with frame rate.
                float settled = jumpVelocity - BasketballRules.Gravity * dt;
                jump += (jumpVelocity + settled) * .5f * dt;
                jumpVelocity = settled;
                if (jump <= 0) { jump = 0; jumpVelocity = 0; }
            }
            float bob = Mathf.Abs(Mathf.Sin(gait)) * Mathf.Min(.11f, speed * .022f);
            visual.localPosition = visualOrigin + Vector3.up * (jump + bob);
            if (lobby) facing = 180f + Mathf.Sin(Time.unscaledTime * .65f + team) * 8f;
            else if (movement.sqrMagnitude > .2f) facing = Mathf.Atan2(movement.x, movement.z) * Mathf.Rad2Deg;
            float roll = lobby ? Mathf.Sin(gait) * 1.1f : -movement.x * 1.2f;
            visual.localRotation = Quaternion.Slerp(visual.localRotation, Quaternion.Euler(speed * 1.1f, facing, roll) * visualBase, dt * 10f);
        }

        public void FaceHoop() => facing = team == 0 ? 90f : -90f;

        /// <summary>Takes off with the speed that reaches <paramref name="apex"/> metres. No double jump.</summary>
        public void Jump(float apex = .45f)
        {
            if (jump > .02f) return;
            jumpVelocity = Mathf.Sqrt(2f * BasketballRules.Gravity * apex);
        }

        public void ResetMotion()
        {
            movement = Vector3.zero;
            stamina = 1;
            jump = jumpVelocity = 0;
            actionCooldown = possessionAge = reach = 0;
        }
    }
}
