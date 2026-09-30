using System.Collections.Generic;
using System.IO;
using Rally.Procedural;
using Rally.Systems;
using UnityEditor;
using UnityEngine;

namespace Rally.EditorTools
{
    /// <summary>
    /// Body meshes of the five selectable cars, each one inspired by a classic rally car (invented names, no
    /// brands or logos). They share the physics car's axles, wheels and colliders, and the sub-mesh layout of
    /// <see cref="CarFactory"/> (0 paint, 1 trim, 2 glass, 3 headlights, 4 taillights, 5 accent, 6 stripe), so
    /// <see cref="CarCatalog"/> can swap them onto any car at runtime. Saved in Resources/CarModels.
    /// </summary>
    public static class CarModelFactory
    {
        public const string Folder = "Assets/Resources/" + CarCatalog.ModelFolder;

        private const float FrontAxle = 1.27f;
        private const float RearAxle = -1.25f;

        [MenuItem("Rally/Build Car Models")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(Folder);
            Save(Pleyades(), CarCatalog.ModelPleyades);
            Save(Lanza(), CarCatalog.ModelLanza);
            Save(Italica(), CarCatalog.ModelItalica);
            Save(Escolta(), CarCatalog.ModelEscolta);
            Save(Leon(), CarCatalog.ModelLeon);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Entry point for -executeMethod in batch mode.</summary>
        public static void BuildFromCommandLine()
        {
            try { BuildAll(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        /// <summary>
        /// Renders each catalogue car (its prefab with the model and colours the menu gives it) from the front
        /// three-quarter and the rear three-quarter into <paramref name="folder"/>, to check the shapes.
        /// Batch: -executeMethod Rally.EditorTools.CarModelFactory.RenderPreviewsFromCommandLine -previewDir DIR
        /// </summary>
        public static void RenderPreviews(string folder)
        {
            Directory.CreateDirectory(folder);
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 2.2f;
            sun.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.62f);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = Vector3.one * 3f;
            var groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            groundMat.SetColor("_BaseColor", new Color(0.45f, 0.42f, 0.38f));
            ground.GetComponent<Renderer>().sharedMaterial = groundMat;

            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.7f, 0.78f);
            camera.fieldOfView = 30f;
            var rt = new RenderTexture(960, 540, 24);
            camera.targetTexture = rt;
            camera.Render(); // the very first render comes out wrong (shaders still loading)

            for (int i = 0; i < CarCatalog.Cars.Length; i++)
            {
                var entry = CarCatalog.Cars[i];
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{CarFactory.PrefabFolder}/{entry.livery}.prefab");
                var car = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                foreach (var mb in car.GetComponents<MonoBehaviour>()) mb.enabled = false;
                car.GetComponent<Rigidbody>().isKinematic = true;
                car.transform.Find("Body").GetComponent<MeshFilter>().sharedMesh = CarCatalog.ModelMesh(entry.model);
                if (entry.repaint)
                {
                    foreach (var r in car.GetComponentsInChildren<Renderer>())
                    {
                        var mats = r.sharedMaterials;
                        for (int m = 0; m < mats.Length; m++)
                        {
                            if (mats[m] == null) continue;
                            bool paint = mats[m].name.StartsWith("Paint_"), accent = mats[m].name.StartsWith("Accent_");
                            if (!paint && !accent) continue;
                            mats[m] = new Material(mats[m]);
                            mats[m].SetColor("_BaseColor", paint ? entry.paint : entry.accent);
                        }
                        r.sharedMaterials = mats;
                    }
                    foreach (var t in car.GetComponentsInChildren<TextMesh>()) t.text = entry.number;
                }
                // Wheels sit at their resting height.
                foreach (Transform child in car.transform)
                    if (child.name.StartsWith("Visual_")) child.localPosition += Vector3.down * 0.08f;
                car.transform.position = new Vector3(0f, 0.02f, 0f);

                foreach (var (name, pos) in new[] { ("delante", new Vector3(4.6f, 2.0f, 6.4f)), ("detras", new Vector3(-4.8f, 2.2f, -6.2f)), ("lado", new Vector3(8.5f, 1.2f, 0f)) })
                {
                    camera.transform.position = pos;
                    camera.transform.LookAt(new Vector3(0f, 0.7f, 0f));
                    camera.Render();
                    camera.Render(); // the first render after a car appears can come out with the wrong colours
                    RenderTexture.active = rt;
                    var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;
                    File.WriteAllBytes($"{folder}/{i}_{entry.model}_{name}.png", tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                }
                Object.DestroyImmediate(car);
            }
        }

        public static void RenderPreviewsFromCommandLine()
        {
            try
            {
                string dir = "Temp/CarPreviews";
                var args = System.Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-previewDir") dir = args[i + 1];
                BuildAll();
                RenderPreviews(dir);
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        private static void Save(MeshBuilder mb, string model)
        {
            var mesh = mb.ToMesh(CarCatalog.ModelMeshName(model));
            string path = $"{Folder}/{mesh.name}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
        }

        // ------------------------------------------------------------------ shape tools

        /// <summary>Piecewise-linear curve along the car (z → value).</summary>
        private class Curve
        {
            private readonly float[] keys;
            public Curve(params float[] zv) { keys = zv; }
            public IEnumerable<float> Zs() { for (int i = 0; i < keys.Length; i += 2) yield return keys[i]; }
            public float this[float z]
            {
                get
                {
                    if (z <= keys[0]) return keys[1];
                    for (int i = 2; i < keys.Length; i += 2)
                        if (z <= keys[i]) return Mathf.Lerp(keys[i - 1], keys[i + 1], Mathf.InverseLerp(keys[i - 2], keys[i], z));
                    return keys[keys.Length - 1];
                }
            }
        }

        /// <summary>
        /// Side view and plan of the lower body: sill line, shoulder line and deck (bonnet / waistline / boot),
        /// with their half-widths. Wheel arches are cut out of the sill line and the fenders bulge by
        /// <see cref="flare"/> around each wheel.
        /// </summary>
        private class Shape
        {
            public Curve bottom, shoulder, deck, width, deckWidth;
            public float flare = 0.05f;
            public float archHeight = 0.76f;
        }

        private static float Arch(float z, float archHeight)
        {
            float y = 0f;
            foreach (float axle in new[] { FrontAxle, RearAxle })
            {
                float d = Mathf.Abs(z - axle);
                if (d < 0.34f) y = Mathf.Max(y, Mathf.Lerp(archHeight, 0.62f, d / 0.34f));
                else if (d < 0.46f) y = Mathf.Max(y, Mathf.Lerp(0.62f, 0.3f, (d - 0.34f) / 0.12f));
            }
            return y;
        }

        private static float Bulge(float z)
        {
            float b = 0f;
            foreach (float axle in new[] { FrontAxle, RearAxle })
            {
                float d = Mathf.Abs(z - axle);
                b = Mathf.Max(b, d < 0.5f ? 1f : 1f - Mathf.InverseLerp(0.5f, 0.64f, d));
            }
            return b;
        }

        /// <summary>Lofts the lower body through stations at every key of the curves plus the arch corners.</summary>
        private static void Body(MeshBuilder mb, Shape s)
        {
            var zs = new SortedSet<float>();
            foreach (var c in new[] { s.bottom, s.shoulder, s.deck, s.width, s.deckWidth })
                foreach (float z in c.Zs()) zs.Add(z);
            float front = s.bottom.Zs().Max(), rear = s.bottom.Zs().Min();
            foreach (float axle in new[] { FrontAxle, RearAxle })
                foreach (float d in new[] { -0.64f, -0.5f, -0.46f, -0.34f, -0.17f, 0f, 0.17f, 0.34f, 0.46f, 0.5f, 0.64f })
                    zs.Add(axle + d);
            var list = new List<float>();
            foreach (float z in zs) if (z >= rear - 1e-4f && z <= front + 1e-4f) list.Add(z);

            Vector3[] Station(float z)
            {
                float y0 = Mathf.Max(s.bottom[z], Arch(z, s.archHeight));
                float y1 = Mathf.Max(s.shoulder[z], y0 + 0.02f);
                float y2 = Mathf.Max(s.deck[z], y1 + 0.04f);
                float bulge = Bulge(z) * s.flare;
                float w = s.width[z] + bulge, w2 = s.deckWidth[z] + bulge * 0.5f;
                return new[] { new Vector3(w, y0, z), new Vector3(w, y1, z), new Vector3(w2, y2, z) };
            }

            for (int i = 0; i < list.Count - 1; i++)
            {
                var a = Station(list[i]);
                var b = Station(list[i + 1]);
                for (int k = 0; k < 2; k++)
                {
                    mb.Hull(0, new[]
                    {
                        Mirror(a[k]), a[k], b[k], Mirror(b[k]),
                        Mirror(a[k + 1]), a[k + 1], b[k + 1], Mirror(b[k + 1])
                    });
                }
            }
        }

        private static float Max(this IEnumerable<float> v) { float m = float.MinValue; foreach (float x in v) m = Mathf.Max(m, x); return m; }
        private static float Min(this IEnumerable<float> v) { float m = float.MaxValue; foreach (float x in v) m = Mathf.Min(m, x); return m; }

        private static Vector3 Mirror(Vector3 p) => new Vector3(-p.x, p.y, p.z);

        /// <summary>Greenhouse: base on the waistline, roof above; glass on all four sides.</summary>
        private static void Cabin(MeshBuilder mb, float baseY, float roofY, float baseRear, float baseFront,
            float roofRear, float roofFront, float baseHalf, float roofHalf, bool splitSide = true)
        {
            var c = new[]
            {
                new Vector3(-baseHalf, baseY, baseRear), new Vector3(baseHalf, baseY, baseRear),
                new Vector3(baseHalf, baseY, baseFront), new Vector3(-baseHalf, baseY, baseFront),
                new Vector3(-roofHalf, roofY, roofRear), new Vector3(roofHalf, roofY, roofRear),
                new Vector3(roofHalf, roofY, roofFront), new Vector3(-roofHalf, roofY, roofFront)
            };
            mb.Hull(0, c);
            Glass(mb, c[7], c[6], c[2], c[3], 0.07f);   // windscreen
            Glass(mb, c[5], c[4], c[0], c[1], 0.1f);    // rear window
            SideGlass(mb, c[4], c[7], c[3], c[0], splitSide);
            SideGlass(mb, c[6], c[5], c[1], c[2], splitSide);
        }

        private static void Glass(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float inset)
        {
            Vector3 center = (a + b + c + d) * 0.25f;
            Vector3 n = Vector3.Cross(d - a, b - a).normalized;
            Vector3 Shrink(Vector3 p) => Vector3.Lerp(p, center, inset) + n * 0.012f;
            mb.Quad(2, Shrink(a), Shrink(b), Shrink(c), Shrink(d));
        }

        // Corners: top-back, top-front, bottom-front, bottom-back (outside view).
        private static void SideGlass(MeshBuilder mb, Vector3 topA, Vector3 topB, Vector3 bottomB, Vector3 bottomA, bool split)
        {
            Vector3 lowA = Vector3.Lerp(bottomA, topA, 0.1f), lowB = Vector3.Lerp(bottomB, topB, 0.1f);
            if (!split) { Glass(mb, topA, topB, lowB, lowA, 0.06f); return; }
            Vector3 midTop = Vector3.Lerp(topA, topB, 0.45f), midLow = Vector3.Lerp(lowA, lowB, 0.5f);
            Vector3 midTop2 = Vector3.Lerp(topA, topB, 0.53f), midLow2 = Vector3.Lerp(lowA, lowB, 0.58f);
            Glass(mb, topA, midTop, midLow, lowA, 0.06f);
            Glass(mb, midTop2, topB, lowB, midLow2, 0.06f);
        }

        /// <summary>Box laid on a slope: rotated about X by <paramref name="pitch"/> degrees (nose down = positive).</summary>
        private static void SlopedBox(MeshBuilder mb, int sub, Vector3 center, Vector3 size, float pitch)
        {
            mb.Transform = Matrix4x4.TRS(center, Quaternion.Euler(pitch, 0f, 0f), Vector3.one);
            mb.Box(sub, Vector3.zero, size);
            mb.Transform = Matrix4x4.identity;
        }

        /// <summary>Round lamp facing forward (+Z) or backward.</summary>
        private static void RoundLamp(MeshBuilder mb, int sub, Vector3 center, float radius, bool forward = true)
        {
            mb.Transform = Matrix4x4.TRS(center, Quaternion.Euler(forward ? 90f : -90f, 0f, 0f), Vector3.one);
            mb.Frustum(sub, Vector3.zero, radius, radius, 0.04f, 12);
            mb.Transform = Matrix4x4.identity;
        }

        private static float SlopeDeg(Curve deck, float z0, float z1) =>
            Mathf.Atan2(deck[z0] - deck[z1], z1 - z0) * Mathf.Rad2Deg;

        private static void Mirrors(MeshBuilder mb, float z, float y, float x)
        {
            foreach (float side in new[] { -1f, 1f })
                mb.Box(1, new Vector3(side * x, y, z), new Vector3(0.14f, 0.09f, 0.15f));
        }

        private static void MudFlaps(MeshBuilder mb)
        {
            foreach (float z in new[] { FrontAxle, RearAxle })
                foreach (float side in new[] { -1f, 1f })
                    mb.Box(5, new Vector3(side * 0.78f, 0.34f, z - 0.5f), new Vector3(0.3f, 0.3f, 0.02f));
        }

        private static void Exhaust(MeshBuilder mb, float x, float z)
        {
            mb.Transform = Matrix4x4.TRS(new Vector3(x, 0.36f, z), Quaternion.Euler(-90f, 0f, 0f), Vector3.one);
            mb.Frustum(1, Vector3.zero, 0.05f, 0.05f, 0.12f, 10);
            mb.Transform = Matrix4x4.identity;
        }

        /// <summary>Stripe band along both sides, following the body surface at height <paramref name="y"/>.</summary>
        private static void SideBand(MeshBuilder mb, int sub, Shape s, float y, float height, float z0, float z1)
        {
            // Split into pieces so the band follows the fender bulges.
            const int pieces = 16;
            for (int i = 0; i < pieces; i++)
            {
                float za = Mathf.Lerp(z0, z1, i / (float)pieces), zb = Mathf.Lerp(z0, z1, (i + 1) / (float)pieces);
                float zm = (za + zb) * 0.5f;
                if (Arch(zm, s.archHeight) > y - height * 0.5f) continue; // no stripe across the wheel opening
                float w = WidthAt(s, zm, y) + 0.006f;
                foreach (float side in new[] { -1f, 1f })
                    mb.Box(sub, new Vector3(side * w, y, zm), new Vector3(0.012f, height, zb - za + 0.002f));
            }
        }

        private static float WidthAt(Shape s, float z, float y)
        {
            float bulge = Bulge(z) * s.flare;
            float w = s.width[z] + bulge, w2 = s.deckWidth[z] + bulge * 0.5f;
            float y1 = s.shoulder[z], y2 = s.deck[z];
            return y <= y1 ? w : Mathf.Lerp(w, w2, Mathf.InverseLerp(y1, y2, y));
        }

        /// <summary>Stripe laid along the bonnet or roof centre line, following <paramref name="top"/>.</summary>
        private static void TopStripe(MeshBuilder mb, int sub, Curve top, float x, float width, float z0, float z1, float lift = 0.008f)
        {
            const int pieces = 6;
            for (int i = 0; i < pieces; i++)
            {
                float za = Mathf.Lerp(z0, z1, i / (float)pieces), zb = Mathf.Lerp(z0, z1, (i + 1) / (float)pieces);
                float zm = (za + zb) * 0.5f;
                SlopedBox(mb, sub, new Vector3(x, top[zm] + lift, zm), new Vector3(width, 0.012f, zb - za + 0.01f), SlopeDeg(top, za, zb));
            }
        }

        // ------------------------------------------------------------------ the five cars

        /// <summary>PLÉYADES WRX: late-90s four-door WRC saloon. Rounded bonnet with a big scoop, tall boot wing,
        /// wide flared arches, round fog lamps; blue with yellow graphics in the classic livery.</summary>
        private static MeshBuilder Pleyades()
        {
            var mb = new MeshBuilder();
            var s = new Shape
            {
                bottom = new Curve(-2.1f, 0.44f, -1.94f, 0.3f, 1.96f, 0.3f, 2.12f, 0.4f),
                shoulder = new Curve(-2.1f, 0.62f, 1.9f, 0.64f, 2.12f, 0.56f),
                deck = new Curve(-2.1f, 0.86f, -1.96f, 0.94f, -1.2f, 0.95f, 0.9f, 0.91f, 1.5f, 0.83f, 2.0f, 0.7f, 2.12f, 0.6f),
                width = new Curve(-2.1f, 0.82f, -1.9f, 0.9f, 1.9f, 0.9f, 2.12f, 0.78f),
                deckWidth = new Curve(-2.1f, 0.72f, -1.9f, 0.82f, 1.85f, 0.8f, 2.12f, 0.66f),
                flare = 0.07f
            };
            Body(mb, s);
            Cabin(mb, 0.93f, 1.4f, -1.2f, 0.9f, -0.72f, -0.04f, 0.8f, 0.64f);

            // Bonnet scoop and roof vent.
            float hoodPitch = SlopeDeg(s.deck, 0.9f, 1.5f);
            SlopedBox(mb, 0, new Vector3(0f, s.deck[1.25f] + 0.04f, 1.25f), new Vector3(0.52f, 0.08f, 0.38f), hoodPitch);
            SlopedBox(mb, 1, new Vector3(0f, s.deck[1.43f] + 0.04f, 1.43f), new Vector3(0.44f, 0.06f, 0.02f), hoodPitch);
            mb.TaperedBox(0, new Vector3(0f, 1.43f, -0.3f), new Vector3(0.36f, 0.07f, 0.3f), new Vector2(0.8f, 0.6f), -0.04f);

            // Tall boot wing on two stands.
            foreach (float side in new[] { -1f, 1f })
                mb.Box(0, new Vector3(side * 0.6f, 1.07f, -1.86f), new Vector3(0.05f, 0.24f, 0.18f));
            mb.TaperedBox(0, new Vector3(0f, 1.2f, -1.92f), new Vector3(1.6f, 0.05f, 0.34f), new Vector2(1f, 0.85f), 0.02f);
            foreach (float side in new[] { -1f, 1f })
                mb.Box(0, new Vector3(side * 0.81f, 1.2f, -1.92f), new Vector3(0.02f, 0.16f, 0.38f));

            // Bumpers, grille, lights.
            mb.Box(1, new Vector3(0f, 0.38f, 2.1f), new Vector3(1.66f, 0.16f, 0.12f));
            mb.Box(1, new Vector3(0f, 0.3f, 2.14f), new Vector3(1.4f, 0.05f, 0.14f));
            mb.Box(1, new Vector3(0f, 0.55f, 2.1f), new Vector3(0.42f, 0.1f, 0.06f));
            mb.Box(1, new Vector3(0f, 0.42f, -2.09f), new Vector3(1.7f, 0.18f, 0.1f));
            foreach (float side in new[] { -1f, 1f })
            {
                SlopedBox(mb, 3, new Vector3(side * 0.56f, 0.64f, 2.05f), new Vector3(0.42f, 0.1f, 0.12f), 20f);
                RoundLamp(mb, 3, new Vector3(side * 0.56f, 0.4f, 2.16f), 0.07f);
                mb.Box(4, new Vector3(side * 0.6f, 0.84f, -2.03f), new Vector3(0.38f, 0.1f, 0.06f));
            }
            Mirrors(mb, 0.8f, 1.0f, 0.9f);
            MudFlaps(mb);
            Exhaust(mb, 0.5f, -2.14f);

            // Livery: yellow swoosh along the sides, white pinstripe, yellow bonnet bands.
            SideBand(mb, 5, s, 0.5f, 0.14f, -1.9f, 1.9f);
            SideBand(mb, 6, s, 0.6f, 0.03f, -1.9f, 1.9f);
            TopStripe(mb, 5, s.deck, 0f, 0.36f, 1.55f, 2.05f);
            TopStripe(mb, 6, new Curve(-1f, 1.4f, 1f, 1.4f), 0f, 0.3f, -0.68f, -0.08f);
            return mb;
        }

        /// <summary>LANZA EVO VI: angular four-door saloon with a deep front bumper full of intakes, bonnet vents
        /// and a two-plane boot wing; red, white and black.</summary>
        private static MeshBuilder Lanza()
        {
            var mb = new MeshBuilder();
            var s = new Shape
            {
                bottom = new Curve(-2.1f, 0.44f, -1.94f, 0.3f, 1.98f, 0.28f, 2.16f, 0.3f),
                shoulder = new Curve(-2.1f, 0.64f, 1.9f, 0.62f, 2.16f, 0.56f),
                deck = new Curve(-2.1f, 0.88f, -1.96f, 0.96f, -1.25f, 0.97f, 0.85f, 0.9f, 1.6f, 0.8f, 2.08f, 0.68f, 2.16f, 0.62f),
                width = new Curve(-2.1f, 0.84f, -1.92f, 0.9f, 1.95f, 0.9f, 2.16f, 0.84f),
                deckWidth = new Curve(-2.1f, 0.76f, -1.92f, 0.84f, 1.9f, 0.82f, 2.16f, 0.74f),
                flare = 0.05f,
                archHeight = 0.74f
            };
            Body(mb, s);
            Cabin(mb, 0.92f, 1.42f, -1.25f, 0.85f, -0.8f, -0.1f, 0.8f, 0.66f);

            // Bonnet vents (two louvres) and a small scoop.
            float hoodPitch = SlopeDeg(s.deck, 0.85f, 1.6f);
            foreach (float side in new[] { -1f, 1f })
                SlopedBox(mb, 1, new Vector3(side * 0.34f, s.deck[1.35f] + 0.006f, 1.35f), new Vector3(0.24f, 0.012f, 0.3f), hoodPitch);
            SlopedBox(mb, 0, new Vector3(0f, s.deck[1.05f] + 0.03f, 1.05f), new Vector3(0.32f, 0.06f, 0.24f), hoodPitch);

            // Two-plane boot wing.
            foreach (float side in new[] { -1f, 1f })
            {
                mb.Box(0, new Vector3(side * 0.66f, 1.1f, -1.86f), new Vector3(0.05f, 0.28f, 0.22f));
                mb.Box(0, new Vector3(side * 0.82f, 1.18f, -1.9f), new Vector3(0.02f, 0.2f, 0.42f));
            }
            mb.TaperedBox(0, new Vector3(0f, 1.26f, -1.92f), new Vector3(1.64f, 0.05f, 0.36f), new Vector2(1f, 0.85f), 0.02f);
            mb.TaperedBox(1, new Vector3(0f, 1.13f, -1.84f), new Vector3(1.6f, 0.04f, 0.22f), new Vector2(1f, 0.85f), 0.02f);

            // Deep front bumper: three intakes and big square fog lamps.
            mb.Box(0, new Vector3(0f, 0.4f, 2.12f), new Vector3(1.74f, 0.22f, 0.12f));
            mb.Box(1, new Vector3(0f, 0.42f, 2.18f), new Vector3(0.64f, 0.14f, 0.02f));
            foreach (float side in new[] { -1f, 1f })
            {
                mb.Box(1, new Vector3(side * 0.62f, 0.4f, 2.18f), new Vector3(0.22f, 0.1f, 0.02f));
                mb.Box(3, new Vector3(side * 0.42f, 0.4f, 2.18f), new Vector3(0.14f, 0.12f, 0.02f));
                mb.TaperedBox(3, new Vector3(side * 0.56f, 0.64f, 2.08f), new Vector3(0.44f, 0.1f, 0.1f), new Vector2(0.8f, 0.7f), -0.02f);
                mb.Box(4, new Vector3(side * 0.62f, 0.86f, -2.05f), new Vector3(0.36f, 0.12f, 0.06f));
            }
            mb.Box(1, new Vector3(0f, 0.6f, 2.14f), new Vector3(0.5f, 0.1f, 0.06f));       // grille
            mb.Box(1, new Vector3(0f, 0.27f, 2.18f), new Vector3(1.5f, 0.04f, 0.14f));     // splitter
            mb.Box(1, new Vector3(0f, 0.42f, -2.09f), new Vector3(1.74f, 0.2f, 0.1f));
            Mirrors(mb, 0.78f, 1.0f, 0.9f);
            MudFlaps(mb);
            Exhaust(mb, -0.52f, -2.14f);

            // Livery: black stripe, white lower band, white roof.
            SideBand(mb, 6, s, 0.62f, 0.05f, -1.95f, 1.98f);
            SideBand(mb, 5, s, 0.4f, 0.14f, -1.9f, 1.95f);
            TopStripe(mb, 5, new Curve(-1f, 1.42f, 1f, 1.42f), 0f, 1.2f, -0.76f, -0.14f);
            TopStripe(mb, 6, s.deck, 0.3f, 0.08f, 1.65f, 2.08f);
            TopStripe(mb, 6, s.deck, -0.3f, 0.08f, 1.65f, 2.08f);
            return mb;
        }

        /// <summary>ITÁLICA INTEGRAL: boxy five-door hatchback with upright glass, huge box arches, four
        /// rectangular headlamps and a roof spoiler; white with the blue/red racing bands.</summary>
        private static MeshBuilder Italica()
        {
            var mb = new MeshBuilder();
            var s = new Shape
            {
                bottom = new Curve(-1.86f, 0.36f, -1.74f, 0.3f, 1.92f, 0.3f, 2.04f, 0.34f),
                shoulder = new Curve(-1.86f, 0.64f, 2.04f, 0.62f),
                deck = new Curve(-1.86f, 0.9f, -1.8f, 0.94f, 0.9f, 0.93f, 1.3f, 0.87f, 1.98f, 0.8f, 2.04f, 0.74f),
                width = new Curve(-1.86f, 0.86f, -1.76f, 0.88f, 1.96f, 0.88f, 2.04f, 0.84f),
                deckWidth = new Curve(-1.86f, 0.82f, -1.76f, 0.85f, 1.96f, 0.84f, 2.04f, 0.8f),
                flare = 0.1f,
                archHeight = 0.78f
            };
            Body(mb, s);
            Cabin(mb, 0.93f, 1.38f, -1.8f, 0.9f, -1.64f, 0.08f, 0.83f, 0.73f);

            // Boxy arches: a flat-topped lip over each wheel.
            foreach (float z in new[] { FrontAxle, RearAxle })
                foreach (float side in new[] { -1f, 1f })
                    mb.Box(0, new Vector3(side * 0.94f, 0.8f, z), new Vector3(0.1f, 0.06f, 1.04f));

            // Roof spoiler above the hatch, bonnet louvres.
            mb.TaperedBox(0, new Vector3(0f, 1.4f, -1.68f), new Vector3(1.34f, 0.05f, 0.24f), new Vector2(1f, 0.8f), -0.03f);
            float hoodPitch = SlopeDeg(s.deck, 1.3f, 1.98f);
            foreach (float side in new[] { -1f, 1f })
                SlopedBox(mb, 1, new Vector3(side * 0.42f, s.deck[1.55f] + 0.006f, 1.55f), new Vector3(0.22f, 0.012f, 0.26f), hoodPitch);

            // Four rectangular headlamps with the grille between them, wide tail lamps.
            mb.Box(1, new Vector3(0f, 0.64f, 2.04f), new Vector3(1.6f, 0.16f, 0.04f));
            foreach (float side in new[] { -1f, 1f })
            {
                mb.Box(3, new Vector3(side * 0.66f, 0.64f, 2.06f), new Vector3(0.26f, 0.12f, 0.03f));
                mb.Box(3, new Vector3(side * 0.38f, 0.64f, 2.06f), new Vector3(0.22f, 0.12f, 0.03f));
                mb.Box(4, new Vector3(side * 0.56f, 0.84f, -1.87f), new Vector3(0.46f, 0.12f, 0.04f));
                RoundLamp(mb, 3, new Vector3(side * 0.52f, 0.38f, 2.12f), 0.07f);
            }
            mb.Box(1, new Vector3(0f, 0.38f, 2.08f), new Vector3(1.8f, 0.18f, 0.12f));
            mb.Box(1, new Vector3(0f, 0.28f, 2.13f), new Vector3(1.5f, 0.04f, 0.12f));
            mb.Box(1, new Vector3(0f, 0.42f, -1.88f), new Vector3(1.78f, 0.2f, 0.1f));
            mb.Box(1, new Vector3(0f, 0.72f, -1.87f), new Vector3(0.36f, 0.12f, 0.02f)); // plate recess
            Mirrors(mb, 0.82f, 1.0f, 0.92f);
            MudFlaps(mb);
            Exhaust(mb, 0.5f, -1.92f);

            // Livery: navy and red bands along the sides, over the bonnet and the roof.
            SideBand(mb, 6, s, 0.52f, 0.1f, -1.8f, 2.0f);
            SideBand(mb, 5, s, 0.44f, 0.04f, -1.8f, 2.0f);
            foreach (var (x, sub, w) in new[] { (-0.16f, 6, 0.12f), (0f, 5, 0.06f), (0.16f, 6, 0.12f) })
            {
                TopStripe(mb, sub, s.deck, x, w, 0.95f, 2.0f);
                TopStripe(mb, sub, new Curve(-1f, 1.38f, 1f, 1.38f), x, w, -1.6f, 0.04f);
            }
            return mb;
        }

        /// <summary>ESCOLTA MK1: small early-70s two-door saloon, long flat bonnet, round headlamps with the
        /// "dog-bone" grille, bolt-on arches and four round spot lamps on the bumper.</summary>
        private static MeshBuilder Escolta()
        {
            var mb = new MeshBuilder();
            var s = new Shape
            {
                bottom = new Curve(-1.96f, 0.4f, -1.82f, 0.32f, 1.86f, 0.32f, 1.98f, 0.4f),
                shoulder = new Curve(-1.96f, 0.64f, 1.98f, 0.62f),
                deck = new Curve(-1.96f, 0.84f, -1.86f, 0.89f, -1.05f, 0.91f, 0.72f, 0.89f, 1.3f, 0.86f, 1.9f, 0.8f, 1.98f, 0.72f),
                width = new Curve(-1.96f, 0.8f, -1.84f, 0.84f, 1.9f, 0.84f, 1.98f, 0.8f),
                deckWidth = new Curve(-1.96f, 0.72f, -1.84f, 0.76f, 1.9f, 0.76f, 1.98f, 0.72f),
                flare = 0.1f,
                archHeight = 0.74f
            };
            Body(mb, s);
            Cabin(mb, 0.9f, 1.34f, -1.05f, 0.72f, -0.62f, 0.02f, 0.74f, 0.6f, splitSide: false);

            // Chrome-style bumpers (trim), dog-bone grille, round headlamps, four round spot lamps.
            mb.Box(1, new Vector3(0f, 0.44f, 2.0f), new Vector3(1.66f, 0.07f, 0.08f));
            mb.Box(1, new Vector3(0f, 0.46f, -1.98f), new Vector3(1.66f, 0.07f, 0.08f));
            mb.Box(1, new Vector3(0f, 0.62f, 1.99f), new Vector3(0.9f, 0.1f, 0.04f));
            foreach (float side in new[] { -1f, 1f })
            {
                mb.Box(1, new Vector3(side * 0.56f, 0.62f, 1.99f), new Vector3(0.28f, 0.2f, 0.04f));
                RoundLamp(mb, 3, new Vector3(side * 0.56f, 0.62f, 2.0f), 0.085f);
                mb.Box(4, new Vector3(side * 0.66f, 0.76f, -1.96f), new Vector3(0.14f, 0.16f, 0.04f));
                foreach (float x in new[] { 0.18f, 0.5f })
                {
                    mb.Box(1, new Vector3(side * x, 0.56f, 2.04f), new Vector3(0.04f, 0.14f, 0.04f));
                    RoundLamp(mb, 3, new Vector3(side * x, 0.64f, 2.08f), 0.075f);
                }
            }
            // Small boot lip, no wing.
            mb.Box(0, new Vector3(0f, 0.92f, -1.9f), new Vector3(1.4f, 0.03f, 0.08f));
            Mirrors(mb, 0.62f, 0.96f, 0.84f);
            MudFlaps(mb);
            Exhaust(mb, 0.46f, -2.0f);

            // Livery: blue side stripe and a bonnet stripe.
            SideBand(mb, 5, s, 0.58f, 0.08f, -1.85f, 1.9f);
            SideBand(mb, 6, s, 0.5f, 0.02f, -1.85f, 1.9f);
            TopStripe(mb, 5, s.deck, 0f, 0.3f, 0.78f, 1.92f);
            TopStripe(mb, 5, new Curve(-1f, 1.34f, 1f, 1.34f), 0f, 0.3f, -0.58f, -0.02f);
            return mb;
        }

        /// <summary>LEÓN T16: short mid-engined Group B hatch. Big arches, air intakes behind the doors, roof
        /// spoiler and a wide rear with the engine cover; white with yellow and black bands.</summary>
        private static MeshBuilder Leon()
        {
            var mb = new MeshBuilder();
            var s = new Shape
            {
                bottom = new Curve(-1.92f, 0.38f, -1.8f, 0.3f, 1.72f, 0.3f, 1.84f, 0.36f),
                shoulder = new Curve(-1.92f, 0.64f, 1.84f, 0.6f),
                deck = new Curve(-1.92f, 0.9f, -1.84f, 0.95f, 0.8f, 0.91f, 1.3f, 0.84f, 1.78f, 0.72f, 1.84f, 0.66f),
                width = new Curve(-1.92f, 0.86f, -1.8f, 0.9f, 1.76f, 0.9f, 1.84f, 0.84f),
                deckWidth = new Curve(-1.92f, 0.8f, -1.8f, 0.84f, 1.76f, 0.82f, 1.84f, 0.76f),
                flare = 0.08f
            };
            Body(mb, s);
            Cabin(mb, 0.93f, 1.38f, -1.66f, 0.8f, -1.46f, 0.02f, 0.8f, 0.68f);

            // Side intakes for the mid engine, roof spoiler, engine-cover vents.
            foreach (float side in new[] { -1f, 1f })
            {
                mb.TaperedBox(1, new Vector3(side * WidthAt(s, -0.9f, 0.76f), 0.76f, -0.9f), new Vector3(0.05f, 0.2f, 0.42f), new Vector2(1f, 0.7f), -0.06f);
                mb.Box(4, new Vector3(side * 0.6f, 0.82f, -1.93f), new Vector3(0.4f, 0.14f, 0.04f));
                SlopedBox(mb, 3, new Vector3(side * 0.56f, 0.63f, 1.8f), new Vector3(0.4f, 0.09f, 0.1f), 25f);
                RoundLamp(mb, 3, new Vector3(side * 0.5f, 0.4f, 1.9f), 0.07f);
            }
            mb.TaperedBox(0, new Vector3(0f, 1.4f, -1.5f), new Vector3(1.3f, 0.05f, 0.24f), new Vector2(1f, 0.8f), -0.03f);
            for (int i = 0; i < 3; i++)
                mb.Box(1, new Vector3(0f, 0.955f, -1.76f + i * 0.05f), new Vector3(1.1f, 0.012f, 0.025f));

            mb.Box(1, new Vector3(0f, 0.38f, 1.84f), new Vector3(1.7f, 0.18f, 0.12f));
            mb.Box(1, new Vector3(0f, 0.28f, 1.88f), new Vector3(1.4f, 0.04f, 0.14f));
            mb.Box(1, new Vector3(0f, 0.55f, 1.84f), new Vector3(0.7f, 0.06f, 0.04f)); // grille slot
            mb.Box(1, new Vector3(0f, 0.44f, -1.94f), new Vector3(1.8f, 0.2f, 0.08f));
            Mirrors(mb, 0.72f, 1.0f, 0.9f);
            MudFlaps(mb);
            Exhaust(mb, -0.44f, -1.98f);
            Exhaust(mb, 0.44f, -1.98f);

            // Livery: yellow and black bands along the sides and over the bonnet and roof.
            SideBand(mb, 5, s, 0.52f, 0.1f, -1.8f, 1.8f);
            SideBand(mb, 6, s, 0.6f, 0.03f, -1.8f, 1.8f);
            SideBand(mb, 6, s, 0.44f, 0.03f, -1.8f, 1.8f);
            TopStripe(mb, 5, s.deck, 0f, 0.24f, 0.85f, 1.8f);
            TopStripe(mb, 6, s.deck, 0.16f, 0.04f, 0.85f, 1.8f);
            TopStripe(mb, 6, s.deck, -0.16f, 0.04f, 0.85f, 1.8f);
            TopStripe(mb, 5, new Curve(-1f, 1.38f, 1f, 1.38f), 0f, 0.24f, -1.42f, -0.02f);
            return mb;
        }
    }
}
