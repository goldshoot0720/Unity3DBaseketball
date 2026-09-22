using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MiaCourt
{
    public static class CourtBuilder
    {
        public static readonly Color Mint = new Color(.40f, .91f, .78f);
        public static readonly Color Coral = new Color(1f, .43f, .25f);
        static Material white, steel, wood, orange;
        static readonly Dictionary<string, Material> propMaterials = new Dictionary<string, Material>();

        /// <summary>
        /// Instantiates a converted scene prop, or returns null so the caller keeps its procedural
        /// stand-in. The props are decorative only: every one stands outside the ball containment.
        /// </summary>
        static GameObject Prop(MiaBasketballGame game, string id, Transform parent, Vector3 position, float yaw, string name)
        {
            GameObject model = game.assets == null ? null : game.assets.PropFor(id);
            if (model == null) return null;
            var instance = Object.Instantiate(model, parent);
            instance.name = name;
            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            if (!propMaterials.TryGetValue(id, out Material material))
            {
                material = Material(id, Color.white, .3f);
                Texture2D albedo = game.assets.PropTextureFor(id);
                if (albedo != null) material.mainTexture = albedo;
                Texture2D normal = game.assets.PropNormalFor(id);
                if (normal != null)
                {
                    material.SetTexture("_BumpMap", normal);
                    material.EnableKeyword("_NORMALMAP");
                }
                propMaterials[id] = material;
            }
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterial = material;
            return instance;
        }

        public static Material Material(string name, Color color, float smoothness = .25f)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var m = new Material(shader) { name = name, color = color };
            if (shader != null && shader.name.Contains("Lit")) m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        public static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 scale, Material material, bool collider = true)
        {
            var o = GameObject.CreatePrimitive(PrimitiveType.Cube);
            o.name = name;
            o.transform.SetParent(parent, false);
            o.transform.localPosition = pos;
            o.transform.localScale = scale;
            o.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) Object.Destroy(o.GetComponent<Collider>());
            return o;
        }

        public static GameObject Line(string name, Transform parent, IList<Vector3> points, float width, Material material, bool loop = false)
        {
            var o = new GameObject(name);
            o.transform.SetParent(parent, false);
            var line = o.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++) line.SetPosition(i, points[i]);
            line.loop = loop;
            line.widthMultiplier = width;
            line.numCornerVertices = 3;
            line.numCapVertices = 3;
            line.sharedMaterial = material;
            line.shadowCastingMode = ShadowCastingMode.Off;
            return o;
        }

        public static GameObject Ring(string name, Transform parent, Vector3 center, float radius, float width, Color color, bool collider)
        {
            var material = Material(name, color, .35f);
            var points = new Vector3[64];
            for (int i = 0; i < points.Length; i++)
            {
                float a = i * Mathf.PI * 2 / points.Length;
                points[i] = center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius;
            }
            var ring = Line(name, parent, points, width, material, true);
            if (collider)
            {
                for (int i = 0; i < 32; i++)
                {
                    Vector3 a = points[i * 2];
                    Vector3 b = points[(i * 2 + 2) % points.Length];
                    var segment = new GameObject("Rim collision");
                    segment.transform.SetParent(ring.transform, false);
                    segment.transform.localPosition = (a + b) * .5f;
                    segment.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
                    var capsule = segment.AddComponent<CapsuleCollider>();
                    capsule.radius = width * .48f;
                    capsule.height = Vector3.Distance(a, b) + width;
                    capsule.sharedMaterial = BounceMaterial(.65f);
                }
            }
            return ring;
        }

        public static PhysicsMaterial BounceMaterial(float bounce)
        {
            return new PhysicsMaterial("Basketball surface") { bounciness = bounce, dynamicFriction = .4f,
                staticFriction = .4f, bounceCombine = PhysicsMaterialCombine.Maximum };
        }

        public static void Build(MiaBasketballGame game)
        {
            var root = new GameObject("Taipei sunset court").transform;
            propMaterials.Clear();
            white = Material("Warm court paint", new Color(.90f, .87f, .73f), .18f);
            steel = Material("Graphite steel", new Color(.075f, .12f, .13f), .45f);
            wood = Material("Sun warmed concrete", new Color(.29f, .31f, .28f), .12f);
            orange = Material("Vermilion rim", new Color(.87f, .22f, .055f), .55f);
            Material green = new Material(game.assets.courtShader) { name = "Weathered jade court", color = new Color(.24f, .41f, .37f) };
            Material red = new Material(game.assets.courtShader) { name = "Clay key", color = new Color(.56f, .29f, .235f) };
            Material surround = new Material(game.assets.courtShader) { name = "Court apron", color = new Color(.20f, .28f, .26f) };

            Box("Court apron", root, new Vector3(0, -.18f, 0), new Vector3(52, .3f, 36), surround);
            Box("Playing surface", root, new Vector3(0, -.012f, 0), new Vector3(25, .025f, 12.8f), green);
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Painted key", root, new Vector3(side * 9.45f, .006f, 0), new Vector3(6.1f, .014f, 4.7f), red, false);
                PaintRect(root, side * 9.45f, 0, 6.1f, 4.7f);
                Ring("Free throw circle", root, new Vector3(side * 6.4f, .03f, 0), 1.8f, .065f, white.color, false);
                var arc = new List<Vector3>();
                for (int i = 0; i <= 96; i++)
                {
                    float angle = Mathf.Lerp(-Mathf.PI * .5f, Mathf.PI * .5f, i / 96f);
                    arc.Add(new Vector3(side * (BasketballRules.HoopX - Mathf.Cos(angle) * BasketballRules.ThreePointDistance), .031f,
                        Mathf.Sin(angle) * BasketballRules.ThreePointDistance));
                }
                // End the arc at the sideline; corners remain inside the playable court.
                arc.RemoveAll(p => Mathf.Abs(p.z) > 5.75f);
                if (arc.Count > 0)
                {
                    arc.Insert(0, new Vector3(side * 12.45f, .031f, arc[0].z));
                    arc.Add(new Vector3(side * 12.45f, .031f, arc[arc.Count - 1].z));
                }
                Line("Three point line", root, arc, .07f, white);
                BuildHoop(root, side);
            }
            PaintRect(root, 0, 0, 25, 12.8f);
            Line("Half court", root, new[] { new Vector3(0, .03f, -6.4f), new Vector3(0, .03f, 6.4f) }, .075f, white);
            Disc(root, Vector3.up * .019f, 1.8f, red);
            Ring("Center circle", root, Vector3.up * .033f, 1.8f, .075f, white.color, false);

            BuildFence(root, game);
            BuildBench(root, game, -6.5f);
            BuildBench(root, game, 6.5f);
            for (int side = -1; side <= 1; side += 2)
            {
                if (Prop(game, "CourtFloodlight", root, new Vector3(side * 15, 0, 7.7f), 0, "Floodlight pole") != null) continue;
                Box("Light pole", root, new Vector3(side * 15, 4.4f, 7.7f), new Vector3(.15f, 8.8f, .15f), steel);
                Box("Floodlight", root, new Vector3(side * 15, 8.8f, 7.5f), new Vector3(1.1f, .26f, .4f), white, false);
            }
            // Taipei street dressing: the kerbside bin and two scooters parked behind the fence.
            Prop(game, "CourtTrashBin", root, new Vector3(12.2f, 0, 7.6f), 0, "Trash bin");
            Prop(game, "CourtScooter", root, new Vector3(-11.4f, 0, 9.5f), 0, "Parked scooter");
            Prop(game, "CourtScooter", root, new Vector3(-9.1f, 0, 9.5f), 7, "Parked scooter");
            var sun = new GameObject("Golden hour sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(32, -42, 0);
            sun.color = new Color(1f, .82f, .62f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .65f;
            sun.shadowBias = .025f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.57f, .64f, .72f);
            RenderSettings.ambientEquatorColor = new Color(.46f, .49f, .48f);
            RenderSettings.ambientGroundColor = new Color(.27f, .26f, .23f);
            RenderSettings.fog = false;
            // Shadow quality and MSAA are configured on the URP asset.

            Camera cam = new GameObject("Court camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.nearClipPlane = .1f;
            cam.farClipPlane = 180;
            cam.fieldOfView = 46;
            // The supplied photograph is the sky. URP draws its skybox after the opaques, and the
            // backdrop quad writes no depth, so leaving the default Skybox flag lets the skybox paint
            // straight over the photograph.
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.50f, .62f, .72f);
            cam.allowHDR = true;
            cam.gameObject.AddComponent<AudioListener>();
            game.courtCamera = cam;
            game.cameraRig = cam.gameObject.AddComponent<CourtCamera>();
            game.cameraRig.Initialize(game);

            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Basketball";
            ball.transform.localScale = Vector3.one * BasketballRules.BallRadius * 2;
            ball.GetComponent<Renderer>().sharedMaterial = Material("Pebbled orange leather", new Color(.91f, .36f, .065f), .22f);
            var ballCollider = ball.GetComponent<Collider>();
            if (ballCollider == null) ballCollider = ball.AddComponent<SphereCollider>();
            ballCollider.sharedMaterial = BounceMaterial(.70f);
            var body = ball.AddComponent<Rigidbody>();
            body.mass = .62f;
            // BasketballRules.LaunchVelocity solves a drag-free arc, and the on-screen aim guide draws
            // that same arc. Any linear damping makes a perfect release land short of the line the
            // player is shown, so the ball must fly exactly as the solver predicts.
            body.linearDamping = 0f;
            body.angularDamping = .25f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            GameObject ballSkin = Prop(game, "CourtBall", ball.transform, Vector3.zero, 0, "Pebbled leather skin");
            if (ballSkin != null)
            {
                // The primitive stays as the collider. The model is already BallRadius * 2 across, so it
                // has to undo the scale the sphere carries.
                ball.GetComponent<Renderer>().enabled = false;
                ballSkin.transform.localScale = Vector3.one / (BasketballRules.BallRadius * 2);
            }
            else
            {
                for (int i = 0; i < 3; i++)
                {
                    var seam = Ring("Ball seam", ball.transform, Vector3.zero, .501f, .022f, new Color(.13f, .075f, .04f), false);
                    seam.transform.localRotation = Quaternion.Euler(i == 0 ? 0 : 90, i == 2 ? 90 : 0, 0);
                }
            }
            var trail = ball.AddComponent<TrailRenderer>();
            trail.time = .20f;
            trail.startWidth = .14f;
            trail.endWidth = 0;
            trail.material = new Material(game.assets.trailShader);
            trail.startColor = new Color(1f, .74f, .36f, .35f);
            trail.endColor = new Color(1f, .74f, .36f, 0);
            trail.emitting = false;
            ball.AddComponent<BallContactAudio>().game = game;
            game.ballBody = body;
            game.ballTrail = trail;
        }

        static void PaintRect(Transform root, float x, float z, float width, float height)
        {
            Line("Court marking", root, new[] { new Vector3(x-width/2,.029f,z-height/2),new Vector3(x+width/2,.029f,z-height/2),
                new Vector3(x+width/2,.029f,z+height/2),new Vector3(x-width/2,.029f,z+height/2)}, .068f, white, true);
        }

        static void Disc(Transform root, Vector3 pos, float radius, Material material)
        {
            var o = new GameObject("Center paint");
            o.transform.SetParent(root, false);
            o.transform.localPosition = pos;
            var vertices = new Vector3[66];
            var tris = new int[64 * 3];
            for (int i = 0; i <= 64; i++)
            {
                float a = i * Mathf.PI * 2 / 64;
                vertices[i + 1] = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius;
                if (i < 64) { tris[i * 3] = 0; tris[i * 3 + 1] = i + 2; tris[i * 3 + 2] = i + 1; }
            }
            Mesh mesh = new Mesh { vertices = vertices, triangles = tris };
            mesh.RecalculateNormals();
            o.AddComponent<MeshFilter>().sharedMesh = mesh;
            o.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        static void BuildHoop(Transform root, int side)
        {
            var h = new GameObject(side < 0 ? "Left hoop - CPU attacks" : "Right hoop - player attacks").transform;
            h.SetParent(root, false);
            h.localPosition = new Vector3(side * BasketballRules.HoopX, 0, 0);
            Box("Padded post", h, new Vector3(side * 2.0f, 1.45f, 0), new Vector3(.5f, 2.9f, .7f), steel);
            Box("Steel post", h, new Vector3(side * 2.0f, 3.0f, 0), new Vector3(.17f, 2.8f, .2f), steel);
            Box("Support arm", h, new Vector3(side * 1.43f, 3.50f, 0), new Vector3(1.4f, .15f, .2f), steel);
            Material glass = Material("Tinted backboard", new Color(.72f, .86f, .84f, .32f), .7f);
            glass.SetFloat("_Surface", 1);
            glass.SetFloat("_Blend", 0);
            glass.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            glass.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            glass.SetInt("_SrcBlendAlpha", (int)BlendMode.One);
            glass.SetInt("_DstBlendAlpha", (int)BlendMode.OneMinusSrcAlpha);
            glass.SetInt("_ZWrite", 0);
            glass.SetOverrideTag("RenderType", "Transparent");
            glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            glass.SetShaderPassEnabled("ShadowCaster", false);
            glass.SetShaderPassEnabled("DepthOnly", false);
            glass.renderQueue = 3000;
            Box("Glass backboard", h, new Vector3(side * .85f, 3.65f, 0), new Vector3(.10f, 1.5f, 2.5f), glass);
            foreach (float z in new[] { -1.25f, 1.25f })
                Box("Board frame", h, new Vector3(side*.85f,3.65f,z),new Vector3(.13f,1.6f,.08f),white,false);
            foreach (float y in new[] { 2.9f, 4.4f })
                Box("Board frame", h, new Vector3(side*.85f,y,0),new Vector3(.13f,.08f,2.5f),white,false);
            Line("Backboard target", h, new[] { new Vector3(side*.78f,3.13f,-.46f),new Vector3(side*.78f,3.13f,.46f),
                new Vector3(side*.78f,3.8f,.46f),new Vector3(side*.78f,3.8f,-.46f)}, .045f, white, true);
            Box("Rim mount", h, new Vector3(side * .69f, 3.05f, 0), new Vector3(.35f, .12f, .25f), orange);
            Ring("Rim", h, Vector3.up * BasketballRules.HoopHeight, BasketballRules.RimRadius, .075f, orange.color, true);
            Material net = Material("Cream net rope", new Color(.94f,.90f,.77f));
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 2 / 16;
                var rope = new Vector3[7];
                for (int j = 0; j <= 6; j++)
                {
                    float angle = a + (j % 2 == 0 ? 0 : Mathf.PI / 16);
                    float radius = Mathf.Lerp(.6f, .32f, j / 6f);
                    rope[j] = new Vector3(Mathf.Cos(angle)*radius,3.04f-j*.13f,Mathf.Sin(angle)*radius);
                }
                Line("Woven net", h, rope, .018f, net);
            }
            for (int j = 1; j <= 6; j++) Ring("Net loop", h, Vector3.up * (3.04f-j*.13f), Mathf.Lerp(.6f,.32f,j/6f), .013f, net.color, false);
        }

        static void BuildFence(Transform root, MiaBasketballGame game)
        {
            Box("Perimeter wall", root, new Vector3(0,.3f,8.2f), new Vector3(33,.6f,.30f),wood);
            // Twelve 2.73 m panels stand on the kerb and cover the sideline the wire mesh used to span.
            bool panelled = false;
            for (int i = 0; i < 12; i++)
                panelled |= Prop(game, "CourtFence", root, new Vector3((i - 5.5f) * 2.728f, .6f, 8.2f), 0, "Chain link panel") != null;
            if (!panelled) BuildWireFence(root);
            // Invisible sideline containment lets rebounds remain recoverable.
            foreach (int sign in new[] { -1, 1 })
            {
                var wall = Box("Ball containment",root,new Vector3(sign*13.5f,2,0),new Vector3(.2f,4,15),steel);
                wall.GetComponent<Renderer>().enabled = false;
                wall = Box("Ball containment",root,new Vector3(0,2,sign*7.2f),new Vector3(27,4,.2f),steel);
                wall.GetComponent<Renderer>().enabled = false;
            }
        }

        static void BuildWireFence(Transform root)
        {
            for (int x = -16; x <= 16; x += 4)
                Box("Fence post",root,new Vector3(x,1.55f,8.2f),new Vector3(.08f,2.6f,.08f),steel,false);
            foreach (float y in new[] { .65f, 2.8f })
                Box("Fence rail",root,new Vector3(0,y,8.2f),new Vector3(32,.055f,.055f),steel,false);
            for (float x = -18; x < 18; x += .48f)
            {
                for (int dir = -1; dir <= 1; dir += 2)
                {
                    float startX = Mathf.Clamp(x,-16,16), endX = Mathf.Clamp(x + dir*2.1f,-16,16);
                    if (Mathf.Abs(startX-endX) < .01f) continue;
                    Line("Chain link mesh",root,new[]{new Vector3(startX,.68f,8.2f),new Vector3(endX,2.75f,8.2f)},.010f,steel);
                }
            }
        }

        static void BuildBench(Transform root, MiaBasketballGame game, float x)
        {
            // The model carries its backrest at local +X, so -90 degrees turns the seat toward the court.
            if (Prop(game, "CourtBench", root, new Vector3(x, 0, 7.5f), -90, "Courtside bench") != null) return;
            Box("Concrete bench",root,new Vector3(x,.56f,7.5f),new Vector3(3.2f,.16f,.7f),wood);
            foreach (float offset in new[] { -1.2f,1.2f })
                Box("Bench leg",root,new Vector3(x+offset,.25f,7.5f),new Vector3(.24f,.5f,.6f),wood);
        }
    }
}
