using System.Collections.Generic;
using UnityEngine;

namespace MiaCourt
{
    /// <summary>The gestures the rigged models carry no clip for, so they are posed by hand.</summary>
    public enum CatAction { None, Shoot, Steal, Block }

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
        Transform upperArm, forearm, hand;
        Transform offUpperArm, offForearm, offHand;
        Transform chest, head;
        Transform rightThigh, rightShin, leftThigh, leftShin;
        Transform[] posedBones;
        Quaternion[] boneRest;
        float dribblePhase;
        public bool DribbleImpact { get; private set; }
        public CatAction Action { get; private set; }
        float actionAge, actionLength;
        float gatherAge;
        public bool Gathering { get; set; }
        public bool IsBlocking => Action == CatAction.Block;
        public bool HasArmRig => upperArm != null && forearm != null && hand != null && offUpperArm != null && offForearm != null && offHand != null;
        public Vector3 RightPalm => hand != null ? hand.position : transform.position;
        public Vector3 LeftPalm => offHand != null ? offHand.position : transform.position;
        Vector3 actionDirection;
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
            // The FBX imports as Humanoid so an avatar exists for future retargeted clips, which
            // means the instance arrives with an Animator. Nothing drives it and every pose below
            // writes bone transforms directly, so the component is dropped rather than left to
            // compete for the same skeleton.
            foreach (Animator idle in model.GetComponentsInChildren<Animator>()) Destroy(idle);
            // The rigged FBX imports under a plain root, so this is normally identity; the axis
            // correction now sits on the skinned child and is baked into the bind poses. Animate
            // still composes with whatever the importer left here rather than overwriting it.
            visualBase = visual.localRotation;
            CollectBones(model);
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return;
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

        /// <summary>Finds the limbs the procedural poses drive and records the pose to rebuild from.</summary>
        void CollectBones(GameObject model)
        {
            upperArm = forearm = hand = offUpperArm = offForearm = offHand = null;
            chest = head = rightThigh = rightShin = leftThigh = leftShin = null;
            List<Transform> found = new List<Transform>();
            foreach (Transform bone in model.GetComponentsInChildren<Transform>())
            {
                switch (StripRigPrefix(bone.name))
                {
                    case "RightArm": upperArm = bone; break;
                    case "RightForeArm": forearm = bone; break;
                    case "RightHand": hand = bone; break;
                    case "LeftArm": offUpperArm = bone; break;
                    case "LeftForeArm": offForearm = bone; break;
                    case "LeftHand": offHand = bone; break;
                    // Spine1 is the chest on a Mixamo rig; Spine is the fallback for anything else.
                    case "Spine": if (chest == null) chest = bone; break;
                    case "Spine1": chest = bone; break;
                    case "Head": head = bone; break;
                    case "RightUpLeg": rightThigh = bone; break;
                    case "RightLeg": rightShin = bone; break;
                    case "LeftUpLeg": leftThigh = bone; break;
                    case "LeftLeg": leftShin = bone; break;
                    default: continue;
                }
                found.Add(bone);
            }
            posedBones = found.ToArray();
            boneRest = new Quaternion[posedBones.Length];
            for (int i = 0; i < posedBones.Length; i++) boneRest[i] = posedBones[i].localRotation;
        }

        /// <summary>"mixamorig:RightArm", "mixamorig1:RightArm" and "mixamorigRightArm" all mean RightArm.</summary>
        static string StripRigPrefix(string boneName)
        {
            int separator = boneName.LastIndexOf(':');
            if (separator < 0) separator = boneName.LastIndexOf('_');
            if (separator >= 0 && boneName.Substring(0, separator).StartsWith("mixamorig"))
                return boneName.Substring(separator + 1);
            return boneName.StartsWith("mixamorig") ? boneName.Substring("mixamorig".Length).TrimStart('0', '1', '2', '3', '4', '5', '6', '7', '8', '9') : boneName;
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

        /// <summary>Starts a gesture. It runs itself out over <paramref name="seconds"/> inside Animate.</summary>
        public void BeginAction(CatAction next, float seconds)
        {
            Action = next;
            actionAge = 0;
            actionLength = Mathf.Max(.05f, seconds);
        }

        public void Animate(float dt, bool hasBall, bool lobby)
        {
            // Start from the imported pose so procedural rotations never accumulate.
            if (posedBones != null)
                for (int i = 0; i < posedBones.Length; i++) posedBones[i].localRotation = boneRest[i];
            actionCooldown = Mathf.Max(0, actionCooldown - dt);
            possessionAge = hasBall ? possessionAge + dt : 0;
            if (Action != CatAction.None)
            {
                actionAge += dt;
                if (actionAge >= actionLength) { Action = CatAction.None; actionAge = 0; }
            }
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
            else if (Gathering || Action == CatAction.Shoot) FaceHoop();
            else if (Action != CatAction.None) facing = Mathf.Atan2(actionDirection.x, actionDirection.z) * Mathf.Rad2Deg;
            else if (movement.sqrMagnitude > .2f) facing = Mathf.Atan2(movement.x, movement.z) * Mathf.Rad2Deg;
            float roll = lobby ? Mathf.Sin(gait) * 1.1f : -movement.x * 1.2f;
            visual.localRotation = Quaternion.Slerp(visual.localRotation, Quaternion.Euler(speed * 1.1f, facing, roll) * visualBase, dt * 10f);
            // The body is posed only after the root has moved, because every limb target below is
            // solved in world space against the shoulder and hip the root has just placed.
            PoseBody(speed, hasBall, lobby);
        }

        /// <summary>Stride, torso counter-rotation and whichever gesture is live.</summary>
        void PoseBody(float speed, bool hasBall, bool lobby)
        {
            Vector3 forward = Facing;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            float swing = Mathf.Sin(gait);
            float stride = Mathf.Clamp(speed * 5.6f, lobby ? 3.5f : 2.4f, 34f);
            float airborne = Mathf.Clamp01(jump * 2.6f);
            float grounded = 1f - airborne;
            // Loading up for a shot sinks into the knees; the dip reads as a gather rather than a pause.
            float load = Mathf.Clamp01(gatherAge / .45f) * grounded;
            float tuck = airborne * 30f;
            // A thigh swung negatively about the character's right axis travels forward.
            Swing(rightThigh, right, -swing * stride * grounded - tuck - load * 19f);
            Swing(leftThigh, right, swing * stride * grounded - tuck * .6f - load * 19f);
            Swing(rightShin, right, Mathf.Max(0, swing) * stride * 1.6f * grounded + tuck * 1.4f + load * 34f);
            Swing(leftShin, right, Mathf.Max(0, -swing) * stride * 1.6f * grounded + tuck * .8f + load * 34f);
            Swing(chest, Vector3.up, -swing * stride * .30f);
            Swing(head, Vector3.up, swing * stride * .12f);
            if (Action != CatAction.None) { PoseAction(forward, right); return; }
            // The off arm swings against the stride. The shooting arm only joins in when it is free:
            // with the ball it is already solved onto the dribble.
            Swing(offUpperArm, right, -swing * stride * 1.15f);
            Swing(offForearm, right, Mathf.Abs(swing) * stride * .55f);
            if (!hasBall)
            {
                Swing(upperArm, right, swing * stride * 1.15f);
                Swing(forearm, right, Mathf.Abs(swing) * stride * .55f);
            }
        }

        void PoseAction(Vector3 forward, Vector3 right)
        {
            float t = Mathf.Clamp01(actionAge / actionLength);
            Vector3 root = transform.position + Vector3.up * jump;
            switch (Action)
            {
                case CatAction.Shoot:
                {
                    // Up through the set point, snap the wrist over, then let the follow-through drop.
                    float rise = Mathf.Clamp01(t / .30f);
                    float relax = t < .72f ? 0 : (t - .72f) / .28f;
                    Vector3 set = root + Vector3.up * 1.78f + forward * .28f + right * .18f;
                    Vector3 top = root + Vector3.up * (2.66f - relax * .62f) + forward * (.32f + rise * .28f) + right * .22f;
                    PoseArm(upperArm, forearm, hand, Vector3.Lerp(set, top, rise), right * .6f + forward * .35f + Vector3.down * .5f);
                    // The guide hand rides up with the ball and peels away at release.
                    Vector3 guide = root + Vector3.up * (1.76f + rise * .46f) + forward * .20f - right * (.28f + rise * .26f);
                    PoseArm(offUpperArm, offForearm, offHand, guide, -right * .6f + Vector3.down * .5f);
                    Swing(chest, right, -9f * rise * (1f - relax));
                    break;
                }
                case CatAction.Block:
                {
                    // Both hands go straight up and stay there while the jump carries them.
                    float rise = Mathf.Sin(Mathf.Clamp01(t / .40f) * Mathf.PI * .5f);
                    float drop = t < .70f ? 0 : (t - .70f) / .30f;
                    float height = Mathf.Lerp(1.55f, 3.05f, rise) - drop * .95f;
                    PoseArm(upperArm, forearm, hand, root + Vector3.up * height + forward * .28f + right * .27f,
                        right * .8f + Vector3.down * .3f);
                    PoseArm(offUpperArm, offForearm, offHand, root + Vector3.up * (height - .07f) + forward * .24f - right * .27f,
                        -right * .8f + Vector3.down * .3f);
                    Swing(chest, right, -6f * rise);
                    break;
                }
                case CatAction.Steal:
                {
                    // One rake across the body: wide and low, in toward the ball, then back.
                    float sweep = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / .62f));
                    float retreat = t < .62f ? 0 : (t - .62f) / .38f;
                    Vector3 palm = root
                        + Vector3.up * (1.30f - sweep * .20f + retreat * .34f)
                        + forward * (.50f + sweep * .60f - retreat * .60f)
                        + right * Mathf.Lerp(.88f, -.62f, sweep);
                    PoseArm(upperArm, forearm, hand, palm, forward * .4f + Vector3.down * .5f);
                    Swing(chest, Vector3.up, -24f * sweep * (1f - retreat));
                    Swing(offUpperArm, right, 28f * sweep);
                    break;
                }
            }
        }

        /// <summary>A shared cycle drives ball travel, the shooting-side arm and floor contact.</summary>
        public Vector3 Dribble(float dt, bool gathering)
        {
            DribbleImpact = false;
            Gathering = gathering;
            gatherAge = gathering ? gatherAge + dt : 0;
            float oldPhase = dribblePhase;
            if (gathering) dribblePhase = 0;
            else
            {
                float period = Mathf.Lerp(.64f, .43f, Mathf.Clamp01(movement.magnitude / 7f));
                float nextPhase = dribblePhase + dt / period;
                DribbleImpact = Mathf.Floor(nextPhase + .5f) > Mathf.Floor(oldPhase + .5f);
                dribblePhase = Mathf.Repeat(nextPhase, 1f);
            }
            Vector3 forward = Facing;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            if (gathering)
            {
                // The gather lifts the ball from the hip to the set point above the shoulder, held
                // in both hands, so the wind-up is visible for the whole time the bar is running.
                float load = Mathf.SmoothStep(0, 1, Mathf.Clamp01(gatherAge / .45f));
                Vector3 ball = transform.position + Vector3.up * (Mathf.Lerp(1.15f, 1.86f, load) + jump)
                    + forward * Mathf.Lerp(.42f, .30f, load) + right * .04f;
                if (Action == CatAction.None)
                {
                    PoseArm(upperArm, forearm, hand, ball + right * .14f - Vector3.up * .04f,
                        right * .7f - forward * .3f + Vector3.down * .6f);
                    PoseArm(offUpperArm, offForearm, offHand, ball - right * .17f,
                        -right * .7f - forward * .3f + Vector3.down * .6f);
                }
                return ball;
            }
            // A parabolic flight hits the floor halfway through each cycle, then rebounds.
            float flight = Mathf.Abs(2f * dribblePhase - 1f);
            float height = .25f + 1.05f * (2f * flight - flight * flight);
            Vector3 bounce = transform.position + right * .55f + forward * .36f + Vector3.up * (height + jump);
            // Follow the ball near the top, releasing it on the way down instead of
            // stretching the arm to the floor. Solve in world space for every imported rig.
            if (Action == CatAction.None)
            {
                Vector3 palm = bounce + Vector3.up * .24f;
                palm.y = Mathf.Max(palm.y, transform.position.y + jump + 1.02f);
                PoseArm(upperArm, forearm, hand, palm, right * .7f - forward * .4f + Vector3.down);
                // The off arm shields the ball, rising as the ball comes back up.
                Vector3 shield = transform.position + Vector3.up * (1.28f + jump + (1f - flight) * .16f)
                    - right * .42f + forward * .40f;
                PoseArm(offUpperArm, offForearm, offHand, shield, -right * .7f + Vector3.down * .5f);
            }
            return bounce;
        }

        /// <summary>The rendered heading, so limbs and ball stay on the same side through turns.</summary>
        Vector3 Facing
        {
            get
            {
                Quaternion heading = visual.localRotation * Quaternion.Inverse(visualBase);
                Vector3 forward = Vector3.ProjectOnPlane(heading * Vector3.forward, Vector3.up);
                return forward.sqrMagnitude > .000001f ? forward.normalized : Vector3.forward;
            }
        }

        /// <summary>Rotates a bone about a world axis, on top of whatever the pose already has.</summary>
        static void Swing(Transform bone, Vector3 axis, float degrees)
        {
            if (bone == null || Mathf.Abs(degrees) < .01f) return;
            bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
        }

        /// <summary>Two-bone IK in world space, so it holds for any imported rig and any scale.</summary>
        void PoseArm(Transform upper, Transform lower, Transform tip, Vector3 target, Vector3 bendHint)
        {
            if (upper == null || lower == null || tip == null) return;
            Vector3 shoulder = upper.position;
            float upperLength = Vector3.Distance(shoulder, lower.position);
            float lowerLength = Vector3.Distance(lower.position, tip.position);
            if (upperLength < .001f || lowerLength < .001f) return;
            Vector3 delta = target - shoulder;
            float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(upperLength - lowerLength) + .001f,
                upperLength + lowerLength - .001f);
            Vector3 axis = delta.sqrMagnitude > .000001f ? delta.normalized : Vector3.down;
            Vector3 bend = Vector3.ProjectOnPlane(bendHint, axis).normalized;
            if (bend.sqrMagnitude < .01f) bend = Vector3.Cross(axis, Vector3.forward).normalized;
            float along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) / (2f * distance);
            float outward = Mathf.Sqrt(Mathf.Max(0, upperLength * upperLength - along * along));
            Vector3 elbow = shoulder + axis * along + bend * outward;
            upper.rotation = Quaternion.FromToRotation(lower.position - shoulder, elbow - shoulder) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(tip.position - lower.position,
                shoulder + axis * distance - lower.position) * lower.rotation;
        }

        public void Shoot()
        {
            Gathering = false;
            gatherAge = 0;
            BeginAction(CatAction.Shoot, .8f);
            // Charging already showed the gather; release begins at the extended set point.
            actionAge = .24f;
            FaceHoop();
            Jump(.55f);
            Animate(0, false, false);
        }

        public void Defend(bool block, Vector3 target)
        {
            actionDirection = Vector3.ProjectOnPlane(target - transform.position, Vector3.up).normalized;
            BeginAction(block ? CatAction.Block : CatAction.Steal, block ? .7f : .5f);
            if (block) Jump(.62f);
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
            dribblePhase = 0;
            DribbleImpact = false;
            movement = Vector3.zero;
            stamina = 1;
            jump = jumpVelocity = 0;
            actionCooldown = possessionAge = reach = 0;
            Action = CatAction.None;
            Gathering = false;
            actionAge = gatherAge = 0;
        }
    }
}
