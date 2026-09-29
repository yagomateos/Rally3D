using System.IO;
using Rally.AI;
using Rally.Audio;
using Rally.Car;
using Rally.Procedural;
using Rally.Systems;
using Rally.VFX;
using UnityEditor;
using UnityEngine;

namespace Rally.EditorTools
{
    /// <summary>Builds the rally car model (procedural) and assembles physics / gameplay prefabs.</summary>
    public static class CarFactory
    {
        public const string PrefabFolder = "Assets/Prefabs/Cars";

        public struct Livery
        {
            public string name;
            public string driver;
            public Color paint;
            public Color accent;
            public Color stripe;
            public string number;
        }

        private const float WheelRadius = 0.34f;
        private const float WheelWidth = 0.24f;
        private const float TrackHalf = 0.79f;
        private const float FrontAxle = 1.27f;
        private const float RearAxle = -1.25f;
        private const float WheelMountHeight = 0.42f;

        private static Material trim, glass, headlight, taillight, rim, tyre, chrome;
        private static Mesh wheelMesh;

        public static void CreateSharedMaterials()
        {
            var (tyreTex, tyreN) = ProceduralTextures.Tyre();
            trim = MaterialFactory.Opaque("Car_Trim", new Color(0.05f, 0.05f, 0.055f), null, null, 0.35f, 0f, 1f, null, "Cars");
            glass = MaterialFactory.Transparent("Car_Glass", new Color(0.04f, 0.05f, 0.06f, 0.82f), 0.96f, null, "Cars");
            headlight = MaterialFactory.Emissive("Car_Headlight", new Color(0.95f, 0.95f, 0.9f), new Color(2.2f, 2.1f, 1.9f));
            taillight = MaterialFactory.Emissive("Car_Taillight", new Color(0.6f, 0.03f, 0.02f), new Color(1.4f, 0.05f, 0.02f));
            rim = MaterialFactory.Opaque("Car_Rim", new Color(0.75f, 0.76f, 0.78f), null, null, 0.75f, 0.9f, 1f, null, "Cars");
            chrome = MaterialFactory.Opaque("Car_Chrome", new Color(0.8f, 0.8f, 0.82f), null, null, 0.9f, 1f, 1f, null, "Cars");
            tyre = MaterialFactory.Opaque("Car_Tyre", new Color(0.8f, 0.8f, 0.8f), tyreTex, tyreN, 0.3f, 0f, 1f, null, "Cars");
            wheelMesh = BuildWheel();
        }

        // ------------------------------------------------------------------ visual model

        private static Mesh BuildBody(string name)
        {
            // Sub-meshes: 0 paint, 1 trim, 2 glass, 3 headlights, 4 taillights, 5 accent, 6 stripe
            var mb = new MeshBuilder();

            // Lower body (sills to shoulder line).
            mb.Hull(0, new[]
            {
                new Vector3(-0.9f, 0.3f, -1.92f), new Vector3(0.9f, 0.3f, -1.92f), new Vector3(0.9f, 0.3f, 1.25f), new Vector3(-0.9f, 0.3f, 1.25f),
                new Vector3(-0.91f, 0.64f, -1.98f), new Vector3(0.91f, 0.64f, -1.98f), new Vector3(0.91f, 0.64f, 1.25f), new Vector3(-0.91f, 0.64f, 1.25f)
            });
            // Upper body with tumblehome, up to the window line.
            mb.Hull(0, new[]
            {
                new Vector3(-0.91f, 0.64f, -1.98f), new Vector3(0.91f, 0.64f, -1.98f), new Vector3(0.91f, 0.64f, 1.25f), new Vector3(-0.91f, 0.64f, 1.25f),
                new Vector3(-0.84f, 0.88f, -1.86f), new Vector3(0.84f, 0.88f, -1.86f), new Vector3(0.84f, 0.88f, 1.0f), new Vector3(-0.84f, 0.88f, 1.0f)
            });
            // Nose and sloping bonnet.
            mb.Hull(0, new[]
            {
                new Vector3(-0.9f, 0.3f, 1.25f), new Vector3(0.9f, 0.3f, 1.25f), new Vector3(0.82f, 0.32f, 2.06f), new Vector3(-0.82f, 0.32f, 2.06f),
                new Vector3(-0.84f, 0.88f, 1.0f), new Vector3(0.84f, 0.88f, 1.0f), new Vector3(0.74f, 0.64f, 2.02f), new Vector3(-0.74f, 0.64f, 2.02f)
            });

            // Hatchback cabin: raked windscreen, sloping rear hatch.
            var cabin = new[]
            {
                new Vector3(-0.8f, 0.88f, -1.62f), new Vector3(0.8f, 0.88f, -1.62f), new Vector3(0.8f, 0.88f, 0.95f), new Vector3(-0.8f, 0.88f, 0.95f),
                new Vector3(-0.66f, 1.4f, -1.22f), new Vector3(0.66f, 1.4f, -1.22f), new Vector3(0.66f, 1.4f, -0.02f), new Vector3(-0.66f, 1.4f, -0.02f)
            };
            mb.Hull(0, cabin);
            GlassPanel(mb, cabin[7], cabin[6], cabin[2], cabin[3], 0.07f);                  // windscreen
            GlassPanel(mb, cabin[5], cabin[4], cabin[0], cabin[1], 0.12f);                  // rear hatch glass
            SideWindows(mb, cabin[4], cabin[7], cabin[3], cabin[0]);                        // left
            SideWindows(mb, cabin[6], cabin[5], cabin[1], cabin[2]);                        // right

            // Roof vent and roof stripe.
            mb.TaperedBox(5, new Vector3(0f, 1.44f, -0.25f), new Vector3(0.42f, 0.1f, 0.4f), new Vector2(0.8f, 0.6f), -0.05f);
            mb.Box(6, new Vector3(0f, 1.405f, -0.62f), new Vector3(0.36f, 0.012f, 1.15f));

            // Bumpers, skirts and arches.
            mb.Box(1, new Vector3(0f, 0.36f, 2.05f), new Vector3(1.72f, 0.2f, 0.16f));
            mb.Box(1, new Vector3(0f, 0.38f, -2.0f), new Vector3(1.76f, 0.24f, 0.14f));
            mb.Box(1, new Vector3(0f, 0.3f, 2.14f), new Vector3(1.5f, 0.06f, 0.12f)); // splitter
            mb.Box(1, new Vector3(-0.92f, 0.36f, 0f), new Vector3(0.06f, 0.14f, 1.6f));
            mb.Box(1, new Vector3(0.92f, 0.36f, 0f), new Vector3(0.06f, 0.14f, 1.6f));
            foreach (float z in new[] { FrontAxle, RearAxle })
            foreach (float side in new[] { -1f, 1f })
            {
                // Box flare over the wheel: flush with the body at the top, bulging out at the bottom.
                float inner = 0.86f, outer = 0.97f;
                mb.Hull(0, new[]
                {
                    new Vector3(side < 0 ? -outer : inner, 0.46f, z - 0.5f), new Vector3(side < 0 ? -inner : outer, 0.46f, z - 0.5f),
                    new Vector3(side < 0 ? -inner : outer, 0.46f, z + 0.5f), new Vector3(side < 0 ? -outer : inner, 0.46f, z + 0.5f),
                    new Vector3(side < 0 ? -0.92f : inner, 0.8f, z - 0.42f), new Vector3(side < 0 ? -inner : 0.92f, 0.8f, z - 0.42f),
                    new Vector3(side < 0 ? -inner : 0.92f, 0.8f, z + 0.42f), new Vector3(side < 0 ? -0.92f : inner, 0.8f, z + 0.42f)
                });
                // Mud flap behind each wheel.
                mb.Box(5, new Vector3(side * 0.78f, 0.36f, z - 0.48f), new Vector3(0.3f, 0.34f, 0.02f));
            }

            // Lights.
            foreach (float side in new[] { -1f, 1f })
            {
                mb.Box(3, new Vector3(side * 0.6f, 0.6f, 2.05f), new Vector3(0.36f, 0.11f, 0.05f));
                mb.Box(4, new Vector3(side * 0.66f, 0.78f, -1.9f), new Vector3(0.32f, 0.1f, 0.05f));
                mb.Box(1, new Vector3(side * 0.9f, 1.0f, 0.78f), new Vector3(0.14f, 0.09f, 0.16f)); // mirrors
            }
            // Rally light pod on the bonnet.
            mb.Box(1, new Vector3(0f, 0.72f, 1.9f), new Vector3(1.1f, 0.1f, 0.12f));
            for (int i = 0; i < 4; i++)
                mb.Box(3, new Vector3(-0.42f + i * 0.28f, 0.72f, 1.97f), new Vector3(0.18f, 0.12f, 0.03f));

            // Rear wing.
            mb.Box(1, new Vector3(-0.5f, 1.3f, -1.42f), new Vector3(0.05f, 0.26f, 0.2f));
            mb.Box(1, new Vector3(0.5f, 1.3f, -1.42f), new Vector3(0.05f, 0.26f, 0.2f));
            mb.TaperedBox(5, new Vector3(0f, 1.45f, -1.5f), new Vector3(1.6f, 0.05f, 0.36f), new Vector2(1f, 0.85f), 0.02f);
            mb.Box(5, new Vector3(-0.81f, 1.45f, -1.5f), new Vector3(0.02f, 0.16f, 0.4f));
            mb.Box(5, new Vector3(0.81f, 1.45f, -1.5f), new Vector3(0.02f, 0.16f, 0.4f));

            // Livery: side stripes and bonnet stripes.
            foreach (float side in new[] { -1f, 1f })
            {
                mb.Box(6, new Vector3(side * 0.912f, 0.5f, -0.25f), new Vector3(0.012f, 0.12f, 3.2f));
                mb.Box(5, new Vector3(side * 0.914f, 0.61f, -0.3f), new Vector3(0.012f, 0.05f, 2.9f));
                mb.Box(1, new Vector3(side * 0.3f, 0.38f, -2.08f), new Vector3(0.09f, 0.09f, 0.1f)); // exhausts
            }
            // Bonnet stripes follow the bonnet slope.
            mb.Transform = Matrix4x4.TRS(new Vector3(0f, 0.775f, 1.5f), Quaternion.Euler(13.4f, 0f, 0f), Vector3.one);
            mb.Box(6, new Vector3(-0.18f, 0f, 0f), new Vector3(0.14f, 0.012f, 1.0f));
            mb.Box(6, new Vector3(0.18f, 0f, 0f), new Vector3(0.14f, 0.012f, 1.0f));
            mb.Transform = Matrix4x4.identity;

            return PropFactory.SaveMesh(mb.ToMesh(name));
        }

        private static void GlassPanel(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float inset)
        {
            Vector3 center = (a + b + c + d) * 0.25f;
            Vector3 n = Vector3.Cross(d - a, b - a).normalized;
            Vector3 Shrink(Vector3 p) => Vector3.Lerp(p, center, inset) + n * 0.012f;
            mb.Quad(2, Shrink(a), Shrink(b), Shrink(c), Shrink(d));
        }

        /// <summary>Side glass split by a B-pillar. Corners: top-back, top-front, bottom-front, bottom-back (outside view).</summary>
        private static void SideWindows(MeshBuilder mb, Vector3 topA, Vector3 topB, Vector3 bottomB, Vector3 bottomA)
        {
            Vector3 lowA = Vector3.Lerp(bottomA, topA, 0.12f), lowB = Vector3.Lerp(bottomB, topB, 0.12f);
            Vector3 midTop = Vector3.Lerp(topA, topB, 0.48f), midLow = Vector3.Lerp(lowA, lowB, 0.52f);
            Vector3 midTop2 = Vector3.Lerp(topA, topB, 0.56f), midLow2 = Vector3.Lerp(lowA, lowB, 0.6f);
            GlassPanel(mb, topA, midTop, midLow, lowA, 0.06f);
            GlassPanel(mb, midTop2, topB, lowB, midLow2, 0.06f);
        }

        private static Mesh BuildWheel()
        {
            // Built along +Y, then rotated so the axle is the X axis with the rim facing +X.
            var mb = new MeshBuilder { Transform = Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, -90f)) };
            float half = WheelWidth * 0.5f;
            mb.Frustum(0, new Vector3(0f, -half, 0f), WheelRadius, WheelRadius, WheelWidth, 24, false, false);
            mb.Frustum(0, new Vector3(0f, half, 0f), WheelRadius, WheelRadius * 0.86f, 0.03f, 24, false, false);
            mb.Frustum(0, new Vector3(0f, -half - 0.03f, 0f), WheelRadius * 0.86f, WheelRadius, 0.03f, 24, false, false);
            mb.Disc(1, new Vector3(0f, half + 0.02f, 0f), WheelRadius * 0.72f, 20, true);
            mb.Disc(0, new Vector3(0f, -half - 0.02f, 0f), WheelRadius * 0.86f, 20, false);
            // Spokes and hub.
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 c = new Vector3(0f, half + 0.035f, 0f) + dir * WheelRadius * 0.38f;
                mb.Transform = Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, -90f)) *
                               Matrix4x4.TRS(c, Quaternion.LookRotation(dir, Vector3.up), Vector3.one);
                mb.Box(2, Vector3.zero, new Vector3(0.07f, 0.03f, WheelRadius * 0.62f));
            }
            mb.Transform = Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, -90f));
            mb.Frustum(2, new Vector3(0f, half + 0.02f, 0f), 0.07f, 0.05f, 0.04f, 10);
            return PropFactory.SaveMesh(mb.ToMesh("M_Wheel"));
        }

        // ------------------------------------------------------------------ prefab assembly

        public static GameObject BuildCarPrefab(Livery livery, bool player, CarTuning tuning, SurfaceDatabase surfaces,
            Material dust, Material debris)
        {
            Directory.CreateDirectory(PrefabFolder);
            var paint = MaterialFactory.Paint($"Paint_{livery.name}", livery.paint);
            var accent = MaterialFactory.Paint($"Accent_{livery.name}", livery.accent);
            var stripe = MaterialFactory.Opaque($"Stripe_{livery.name}", livery.stripe, null, null, 0.6f, 0.1f, 1f, null, "Cars");

            int vehicleLayer = LayerMask.NameToLayer(ProjectSetup.VehicleLayer);
            var root = new GameObject(livery.name);

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = tuning.mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var lower = root.AddComponent<BoxCollider>();
            lower.center = new Vector3(0f, 0.58f, 0.02f);
            lower.size = new Vector3(1.78f, 0.52f, 4.1f);
            var upper = root.AddComponent<BoxCollider>();
            upper.center = new Vector3(0f, 1.12f, -0.55f);
            upper.size = new Vector3(1.5f, 0.52f, 2.2f);

            // Visual body (leans independently of physics).
            var bodyGo = PropFactory.MeshObject("Body", BuildBody($"M_CarBody_{livery.name}"),
                paint, trim, glass, headlight, taillight, accent, stripe);
            bodyGo.transform.SetParent(root.transform, false);
            var bodyMotion = bodyGo.AddComponent<CarBodyMotion>();
            AddNumberPlates(bodyGo.transform, livery.number);

            // Wheels.
            var wheelsRoot = new GameObject("Wheels").transform;
            wheelsRoot.SetParent(root.transform, false);
            var carWheels = new CarWheel[4];
            int index = 0;
            foreach (bool front in new[] { true, false })
            foreach (bool left in new[] { true, false })
            {
                string id = (front ? "F" : "R") + (left ? "L" : "R");
                var colliderGo = new GameObject("Wheel_" + id);
                colliderGo.transform.SetParent(wheelsRoot, false);
                colliderGo.transform.localPosition = new Vector3(left ? -TrackHalf : TrackHalf, WheelMountHeight, front ? FrontAxle : RearAxle);
                var wc = colliderGo.AddComponent<WheelCollider>();
                wc.radius = tuning.wheelRadius;
                wc.suspensionDistance = tuning.suspensionDistance;

                var visualPivot = new GameObject("Visual_" + id).transform;
                visualPivot.SetParent(root.transform, false);
                visualPivot.localPosition = colliderGo.transform.localPosition;
                var mesh = PropFactory.MeshObject("Mesh", wheelMesh, tyre, rim, chrome);
                mesh.transform.SetParent(visualPivot, false);
                if (left) mesh.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

                var carWheel = colliderGo.AddComponent<CarWheel>();
                carWheel.Configure(wc, visualPivot, front, left);
                carWheels[index++] = carWheel;
            }

            var controller = root.AddComponent<CarController>();
            controller.Configure(tuning, surfaces, carWheels, FrontAxle - RearAxle);
            bodyMotion.SetCar(controller);

            var participant = root.AddComponent<RaceParticipant>();
            participant.Configure(livery.driver, player, livery.accent);

            var ai = root.AddComponent<AIDriver>();
            if (player)
            {
                root.AddComponent<PlayerCarInput>();
                ai.enabled = false;
                root.AddComponent<CarFeedback>();
            }

            var dustFx = root.AddComponent<CarDustEffects>();
            SetSerialized(dustFx, "dustMaterial", dust);
            SetSerialized(dustFx, "debrisMaterial", debris);
            root.AddComponent<CarSkidMarks>();
            var audio = root.AddComponent<CarAudio>();
            audio.SetIsPlayer(player);

            SetLayerRecursive(root, vehicleLayer);
            return PropFactory.SavePrefab(root, PrefabFolder);
        }

        private static void AddNumberPlates(Transform body, string number)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            foreach (float side in new[] { -1f, 1f })
            {
                var plate = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(plate.GetComponent<Collider>());
                plate.name = "NumberPlate";
                plate.transform.SetParent(body, false);
                plate.transform.localPosition = new Vector3(side * 0.918f, 0.52f, 0.25f);
                plate.transform.localRotation = Quaternion.Euler(0f, side * -90f, 0f);
                plate.transform.localScale = new Vector3(0.62f, 0.42f, 1f);
                plate.GetComponent<MeshRenderer>().sharedMaterial =
                    AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFactory.Folder}/Props/WhitePaint.mat");

                var text = new GameObject("Number");
                text.transform.SetParent(body, false);
                text.transform.localPosition = new Vector3(side * 0.924f, 0.52f, 0.25f);
                text.transform.localRotation = Quaternion.Euler(0f, side * -90f, 0f);
                var tm = text.AddComponent<TextMesh>();
                tm.text = number;
                tm.font = font;
                tm.fontSize = 64;
                tm.characterSize = 0.075f;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.fontStyle = FontStyle.Bold;
                tm.color = new Color(0.08f, 0.08f, 0.1f);
                text.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
        }

        public static void SetSerialized(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursive(child.gameObject, layer);
        }
    }
}
