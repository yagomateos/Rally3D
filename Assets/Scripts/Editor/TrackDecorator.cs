using System.Collections.Generic;
using Rally.Procedural;
using Rally.Systems;
using Rally.Track;
using Rally.Track.Generation;
using UnityEditor;
using UnityEngine;

namespace Rally.EditorTools
{
    /// <summary>Places road meshes and all roadside dressing along the stage.</summary>
    public class TrackDecorator
    {
        private readonly StageDefinition def;
        private readonly StageRoute route;
        private readonly TerrainSculptor sculptor;
        private readonly AssetLibrary lib;
        private readonly Terrain terrain;
        private readonly System.Random rng;
        private readonly List<Bounds> occupied = new List<Bounds>();

        public Transform Root { get; }

        public TrackDecorator(StageDefinition def, StageRoute route, TerrainSculptor sculptor, AssetLibrary lib, Terrain terrain)
        {
            this.def = def;
            this.route = route;
            this.sculptor = sculptor;
            this.lib = lib;
            this.terrain = terrain;
            rng = new System.Random(def.seed);
            Root = new GameObject("Stage Dressing").transform;
        }

        // ------------------------------------------------------------------ utilities

        private float R() => (float)rng.NextDouble();
        private float R(float a, float b) => a + (b - a) * R();

        private Vector2 Right(int i)
        {
            Vector2 t = route.Tangent(i);
            return new Vector2(t.y, -t.x);
        }

        private Vector3 Tangent3(int i)
        {
            Vector2 t = route.Tangent(i);
            return new Vector3(t.x, 0f, t.y);
        }

        private float GroundHeight(Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;

        /// <summary>Point beside the road at a lateral offset, snapped to the terrain.</summary>
        private Vector3 Beside(int i, float offset)
        {
            i = Mathf.Clamp(i, 0, route.Count - 1);
            Vector2 p = route.Planar[i] + Right(i) * offset;
            var world = new Vector3(p.x, 0f, p.y);
            world.y = GroundHeight(world);
            return world;
        }

        private float Curvature(int i, int window = 6)
        {
            int a = Mathf.Max(0, i - window), b = Mathf.Min(route.Count - 1, i + window);
            Vector2 ta = route.Tangent(a), tb = route.Tangent(b);
            float angle = Vector2.SignedAngle(tb, ta) * Mathf.Deg2Rad; // + = right turn
            return angle / ((b - a) * route.Spacing);
        }

        public bool IsBlocked(Vector3 p)
        {
            foreach (var b in occupied)
                if (b.Contains(new Vector3(p.x, b.center.y, p.z))) return true;
            return false;
        }

        private void Reserve(Vector3 center, float radius) =>
            occupied.Add(new Bounds(center, new Vector3(radius * 2f, 1000f, radius * 2f)));

        private GameObject Place(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent, float scale = 1f)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = Vector3.one * scale;
            // Props that can be knocked flying must not be static-batched (their mesh would stay put when they move).
            go.isStatic = go.GetComponent<Rally.Track.Knockable>() == null;
            return go;
        }

        private Transform Group(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(Root, false);
            return t;
        }

        // ------------------------------------------------------------------ road

        public void BuildRoads()
        {
            var parent = Group("Road");
            foreach (var chunk in RoadMeshBuilder.SplitIntoChunks(route))
            {
                Mesh mesh = PropFactory.SaveMesh(RoadMeshBuilder.Build(route, chunk, Vector3.zero));
                var go = PropFactory.MeshObject(mesh.name, mesh, RoadMaterial(chunk.surface));
                go.transform.SetParent(parent, false);
                go.isStatic = true;
                var mr = go.GetComponent<MeshRenderer>();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
                go.AddComponent<SurfaceZone>().Surface = chunk.surface;
            }
        }

        private Material RoadMaterial(SurfaceType surface)
        {
            switch (surface)
            {
                case SurfaceType.Gravel: return lib.roadGravel;
                case SurfaceType.Mud: return lib.roadMud;
                case SurfaceType.Asphalt: return lib.roadAsphalt;
                default: return lib.roadDirt;
            }
        }

        public void BuildPuddles()
        {
            var mb = new MeshBuilder();
            for (int i = 20; i < route.Count - 20; i += 3)
            {
                SurfaceType s = route.Surfaces[i];
                float chance = s == SurfaceType.Mud ? 0.5f : s == SurfaceType.Dirt ? 0.05f : s == SurfaceType.Gravel ? 0.03f : 0.02f;
                if (R() > chance) continue;

                float half = route.Widths[i] * 0.5f;
                float lateral = (R() < 0.5f ? -1f : 1f) * half * R(0.2f, 0.6f);
                float f = lateral / half;
                float crown = (1f - f * f) * 0.05f;
                Vector2 c2 = route.Planar[i] + Right(i) * lateral;
                Vector3 c = new Vector3(c2.x, route.Heights[i] + crown + 0.012f, c2.y);
                Vector3 t = Tangent3(i);
                Vector3 r = new Vector3(t.z, 0f, -t.x);
                float len = R(1.2f, s == SurfaceType.Mud ? 4.5f : 2.6f), wid = R(0.8f, 1.8f);
                mb.QuadUV(0, c - t * len - r * wid, c - t * len + r * wid, c + t * len + r * wid, c + t * len - r * wid);
            }
            Mesh mesh = PropFactory.SaveMesh(mb.ToMesh("M_Puddles"));
            var go = PropFactory.MeshObject("Puddles", mesh, lib.puddle);
            go.transform.SetParent(Group("Wet"), false);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ start / finish & checkpoints

        public Checkpoint[] BuildCheckpoints(TrackPath path, Transform parent)
        {
            var list = new List<Checkpoint>();
            int count = Mathf.Max(2, def.checkpointCount);
            float start = path.StartDistance, finish = path.FinishDistance;
            for (int k = 0; k < count; k++)
            {
                bool isFinish = k == count - 1;
                float d = isFinish ? finish : start + (finish - start) * (k + 1) / count;
                int i = path.IndexAt(d);
                Vector3 pos = path.PositionAt(d);
                Vector3 tan = path.TangentAt(d);
                var go = new GameObject(isFinish ? "Finish" : $"Checkpoint_{k + 1:00}");
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(pos + Vector3.up * 3f, Quaternion.LookRotation(new Vector3(tan.x, 0f, tan.z)));
                go.layer = 2; // Ignore Raycast
                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(route.Widths[i] + 14f, 9f, 4f);
                var cp = go.AddComponent<Checkpoint>();
                cp.Configure(k, d, isFinish);
                list.Add(cp);

                if (!isFinish) CheckpointMarkers(i, go.transform);
            }
            return list.ToArray();
        }

        private void CheckpointMarkers(int i, Transform parent)
        {
            float half = route.Widths[i] * 0.5f;
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 p = Beside(i, side * (half + 1.8f));
                var pole = Place(lib.markerPole, p, Quaternion.LookRotation(-Tangent3(i)), Root, 2.4f);
                pole.name = "CheckpointPole";
                pole.transform.SetParent(parent, true);
                foreach (var r in pole.GetComponentsInChildren<MeshRenderer>())
                    r.sharedMaterials = new[] { lib.whitePaint, lib.clothing[5] };
            }
        }

        public void BuildGantry(float distance, string label)
        {
            int i = Mathf.Clamp(Mathf.RoundToInt(distance / route.Spacing), 0, route.Count - 1);
            float half = route.Widths[i] * 0.5f + 1.5f;
            Vector3 center = route.Point(i);
            Vector3 t = Tangent3(i);
            Vector3 r = new Vector3(t.z, 0f, -t.x);
            var parent = Group($"Gantry_{label}");
            const float height = 5.2f;

            var mb = new MeshBuilder();
            foreach (float side in new[] { -1f, 1f })
                mb.Box(0, new Vector3(side * half, height * 0.5f - 0.5f, 0f), new Vector3(0.35f, height + 1f, 0.35f));
            mb.Box(0, new Vector3(0f, height + 0.2f, 0f), new Vector3(half * 2f + 0.6f, 1.2f, 0.3f));
            float w = half * 2f + 0.4f;
            // Checkered strips above and below the text, on the side facing approaching cars (-z).
            foreach (float y in new[] { height - 0.35f, height + 0.5f })
                mb.QuadUV(1, new Vector3(-w * 0.5f, y, -0.16f), new Vector3(w * 0.5f, y, -0.16f),
                    new Vector3(w * 0.5f, y + 0.25f, -0.16f), new Vector3(-w * 0.5f, y + 0.25f, -0.16f));
            Mesh mesh = PropFactory.SaveMesh(mb.ToMesh($"M_Gantry_{label}"));
            var go = PropFactory.MeshObject("Arch", mesh, lib.darkMetal, lib.checker);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, Quaternion.LookRotation(t));
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            foreach (float side in new[] { -1f, 1f })
            {
                var col = go.AddComponent<BoxCollider>();
                col.center = new Vector3(side * half, height * 0.5f, 0f);
                col.size = new Vector3(0.4f, height, 0.4f);
            }
            Object.DestroyImmediate(go.GetComponent<MeshCollider>());

            // Banner text facing approaching cars (front = -tangent side).
            AddText(parent, label, center + Vector3.up * (height + 0.2f) - t * 0.17f, Quaternion.LookRotation(t), 0.14f, Color.white);
            // Line on the road.
            var line = new MeshBuilder();
            float lh = route.Heights[i] + 0.06f;
            Vector3 c = new Vector3(center.x, lh, center.z);
            line.QuadUV(0, c - r * (half - 1.2f) - t * 0.5f, c + r * (half - 1.2f) - t * 0.5f, c + r * (half - 1.2f) + t * 0.5f, c - r * (half - 1.2f) + t * 0.5f);
            var lineGo = PropFactory.MeshObject("Line", PropFactory.SaveMesh(line.ToMesh($"M_Line_{label}")), lib.checker);
            lineGo.transform.SetParent(parent, false);
            lineGo.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Reserve(center, half + 4f);
            // Spectator area and tyre stacks around the gantry.
            for (int k = 0; k < 4; k++)
            {
                float side = k % 2 == 0 ? -1f : 1f;
                Place(lib.tyreStack, Beside(i + (k < 2 ? -3 : 3), side * (half + 1f)), Quaternion.identity, parent);
            }
            SpectatorZone(Mathf.Max(0, i - 12), i + 12, 1f, parent);
            SpectatorZone(Mathf.Max(0, i - 12), i + 12, -1f, parent);
        }

        public void BuildStageBoard(float distance)
        {
            int i = Mathf.Clamp(Mathf.RoundToInt(distance / route.Spacing), 0, route.Count - 1);
            float half = route.Widths[i] * 0.5f;
            Vector3 pos = Beside(i, half + 4f);
            Vector3 t = Tangent3(i);
            var parent = Group("StageBoard");
            var mb = new MeshBuilder();
            mb.Box(0, new Vector3(-1.4f, 1.2f, 0f), new Vector3(0.12f, 2.4f, 0.12f));
            mb.Box(0, new Vector3(1.4f, 1.2f, 0f), new Vector3(0.12f, 2.4f, 0.12f));
            mb.Box(1, new Vector3(0f, 2.2f, 0f), new Vector3(3.2f, 1.5f, 0.08f));
            var go = PropFactory.MeshObject("Board", PropFactory.SaveMesh(mb.ToMesh("M_StageBoard")), lib.wood, lib.darkMetal);
            go.transform.SetParent(parent, false);
            Quaternion facing = Quaternion.LookRotation(t) * Quaternion.Euler(0f, 200f, 0f);
            go.transform.SetPositionAndRotation(pos, facing);
            Vector3 front = facing * Vector3.forward;
            AddText(go.transform, def.stageNumber, pos + Vector3.up * 2.55f + front * 0.05f, facing * Quaternion.Euler(0f, 180f, 0f), 0.09f, UIColor);
            AddText(go.transform, def.stageName, pos + Vector3.up * 1.95f + front * 0.05f, facing * Quaternion.Euler(0f, 180f, 0f), 0.045f, Color.white);
        }

        private static readonly Color UIColor = new Color(1f, 0.42f, 0.1f);

        private static void AddText(Transform parent, string text, Vector3 position, Quaternion rotation, float size, Color color)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("Text_" + text);
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(position, rotation);
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = font;
            tm.fontSize = 96;
            tm.characterSize = size;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;
            tm.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        // ------------------------------------------------------------------ corners

        /// <summary>Chevron boards on the outside of tight corners, hay bales at hairpins.</summary>
        public void DressCorners()
        {
            var parent = Group("Corners");
            int i = 10;
            while (i < route.Count - 10)
            {
                float k = Curvature(i);
                if (Mathf.Abs(k) < 1f / 75f) { i++; continue; }

                // Walk to the end of this corner.
                int start = i;
                float sign = Mathf.Sign(k);
                float peak = 0f;
                int apex = i;
                while (i < route.Count - 10 && Mathf.Abs(Curvature(i)) > 1f / 110f && Mathf.Sign(Curvature(i)) == sign)
                {
                    if (Mathf.Abs(Curvature(i)) > peak) { peak = Mathf.Abs(Curvature(i)); apex = i; }
                    i++;
                }
                int end = i;
                bool hairpin = peak > 1f / 30f;
                float outside = -sign; // right turn (+) → outside is the left side

                // Chevrons around the apex, facing approaching drivers.
                int boards = hairpin ? 4 : 2;
                for (int b = 0; b < boards; b++)
                {
                    int idx = Mathf.Clamp(apex + (b - boards / 2) * (hairpin ? 3 : 5), start, end);
                    float half = route.Widths[idx] * 0.5f;
                    Vector3 pos = Beside(idx, outside * (half + 2.6f));
                    Place(sign < 0f ? lib.chevronSignLeft : lib.chevronSign, pos, Quaternion.LookRotation(-Tangent3(idx)), parent);
                    Reserve(pos, 2f);
                }

                if (hairpin)
                {
                    for (int b = start; b <= end; b += 3)
                    {
                        float half = route.Widths[b] * 0.5f;
                        Vector3 pos = Beside(b, outside * (half + 4.2f));
                        Place(lib.hayBale, pos, Quaternion.LookRotation(Tangent3(b)), parent);
                        Reserve(pos, 2f);
                    }
                    SpectatorZone(start, end, outside, parent, 9f);
                }
                i = end + 5;
            }
        }

        // ------------------------------------------------------------------ roadside

        public void PlaceMarkerPoles()
        {
            var parent = Group("MarkerPoles");
            for (int i = 25; i < route.Count - 5; i += 18)
            {
                if (route.Surfaces[i] == SurfaceType.Asphalt) continue;
                float half = route.Widths[i] * 0.5f;
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 p = Beside(i, side * (half + 1.9f));
                    if (IsBlocked(p)) continue;
                    Place(lib.markerPole, p, Quaternion.LookRotation(-Tangent3(i)), parent);
                }
            }
        }

        public void BuildJumpZones()
        {
            var parent = Group("Jumps");
            foreach (var jump in route.Jumps)
            {
                int i = Mathf.RoundToInt(jump.x / route.Spacing);
                SpectatorZone(i - 6, i + 14, 1f, parent, 8f);
                SpectatorZone(i - 6, i + 14, -1f, parent, 8f);
            }
        }

        /// <summary>Tape line with spectators behind it.</summary>
        private void SpectatorZone(int from, int to, float side, Transform parent, float distance = 7f)
        {
            from = Mathf.Clamp(from, 0, route.Count - 1);
            to = Mathf.Clamp(to, 0, route.Count - 1);
            var tape = new MeshBuilder();
            Vector3? previous = null;
            for (int i = from; i <= to; i += 2)
            {
                float half = route.Widths[i] * 0.5f;
                Vector3 post = Beside(i, side * (half + distance));
                Place(lib.tapePost, post, Quaternion.identity, parent);
                if (previous.HasValue)
                {
                    foreach (float h in new[] { 0.95f, 0.55f })
                    {
                        Vector3 a = previous.Value + Vector3.up * h, b = post + Vector3.up * h;
                        tape.Quad(0, a, b, b + Vector3.up * 0.07f, a + Vector3.up * 0.07f, 1.5f);
                    }
                }
                previous = post;

                // Spectators in small groups.
                if (R() < 0.75f)
                {
                    int count = rng.Next(1, 4);
                    for (int c = 0; c < count; c++)
                    {
                        Vector3 p = Beside(i + rng.Next(-1, 2), side * (half + distance + R(1.2f, 5f)));
                        Vector3 look = route.Point(i) - p;
                        look.y = 0f;
                        var spectator = Place(lib.spectators[rng.Next(lib.spectators.Count)], p,
                            Quaternion.LookRotation(look) * Quaternion.Euler(0f, R(-25f, 25f), 0f), parent, R(0.92f, 1.08f));
                        spectator.name = "Spectator";
                    }
                }
                Reserve(post, distance * 0.5f);
            }
            if (tape.VertexCount == 0) return;
            var go = PropFactory.MeshObject("Tape", PropFactory.SaveMesh(tape.ToMesh($"M_Tape_{from}_{(side > 0 ? "R" : "L")}")), lib.tape);
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public void BuildGuardRails()
        {
            var parent = Group("GuardRails");
            var rail = new MeshBuilder();
            var posts = new MeshBuilder();
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3? prevTop = null;
                for (int i = 0; i < route.Count; i++)
                {
                    bool village = route.Roadside[i] == RoadsideStyle.Village;
                    bool gap = (i / 25) % 4 == 3; // driveways
                    bool coastalRail = !village && CoastalRail(i, side);
                    if (!coastalRail && (!village || gap)) { prevTop = null; continue; }

                    float half = route.Widths[i] * 0.5f;
                    Vector3 p = Beside(i, side * (half + 1.3f));
                    p.y = Mathf.Max(p.y, route.Heights[i] - 0.2f);
                    Vector3 top = p + Vector3.up * 0.72f;
                    if (i % 2 == 0)
                        posts.Box(0, p + Vector3.up * 0.4f, new Vector3(0.1f, 0.8f, 0.1f));
                    if (prevTop.HasValue)
                    {
                        Vector3 a = prevTop.Value, b = top;
                        Vector3 outward = new Vector3(Right(i).x, 0f, Right(i).y) * side * 0.04f;
                        // Two-faced W-beam approximation.
                        rail.Quad(0, a - Vector3.up * 0.18f - outward, b - Vector3.up * 0.18f - outward, b + Vector3.up * 0.12f - outward, a + Vector3.up * 0.12f - outward, 0.5f);
                        rail.Quad(0, b - Vector3.up * 0.18f + outward, a - Vector3.up * 0.18f + outward, a + Vector3.up * 0.12f + outward, b + Vector3.up * 0.12f + outward, 0.5f);
                    }
                    prevTop = top;
                    Reserve(p, 1.5f);
                }
            }
            if (rail.VertexCount == 0) return;
            Mesh railMesh = PropFactory.SaveMesh(rail.ToMesh("M_GuardRail"));
            var railGo = PropFactory.MeshObject("Rail", railMesh, lib.metal);
            railGo.transform.SetParent(parent, false);
            railGo.AddComponent<MeshCollider>().sharedMesh = railMesh;
            var postGo = PropFactory.MeshObject("Posts", PropFactory.SaveMesh(posts.ToMesh("M_GuardRailPosts")), lib.darkMetal);
            postGo.transform.SetParent(parent, false);
        }

        /// <summary>
        /// Coastal stage: rails on the sea side of the road and on the outside of every real corner, but not at
        /// the start and finish areas (gantries, spectators).
        /// </summary>
        private bool CoastalRail(int i, float side)
        {
            if (!StageTheme.Coast) return false;
            float along = i * route.Spacing;
            if (along < def.startLineDistance + 60f || along > route.Length - def.finishLineOffset - 40f) return false;
            Vector2 outward = Right(i) * side;
            bool seaSide = Vector2.Dot(outward, TerrainSculptor.SeaDirection) > 0.25f;
            float k = Curvature(i, 10);
            bool outsideOfCorner = Mathf.Abs(k) > 1f / 400f && Mathf.Sign(k) == -side;
            return seaSide || outsideOfCorner;
        }

        /// <summary>Coastal stage: street lights every 60 m on the inland side, lamp arm over the road.</summary>
        public void PlaceStreetLights()
        {
            if (lib.streetLight == null) return;
            var parent = Group("StreetLights");
            for (int i = 20; i < route.Count - 20; i += 30)
            {
                float side = Vector2.Dot(Right(i), TerrainSculptor.SeaDirection) > 0f ? -1f : 1f; // inland
                float half = route.Widths[i] * 0.5f;
                Vector3 p = Beside(i, side * (half + 2.4f));
                if (IsBlocked(p)) continue;
                Vector3 toRoad = new Vector3(-Right(i).x, 0f, -Right(i).y) * side;
                Place(lib.streetLight, p + Vector3.down * 0.1f, Quaternion.LookRotation(toRoad), parent).name = "StreetLight";
                Reserve(p, 1.5f);
            }
        }

        /// <summary>
        /// Coastal stage: a curve warning sign about 90 m before every corner tighter than ~235 m radius, on the right
        /// of the road, facing approaching drivers, arrow pointing the way the road bends.
        /// </summary>
        public int PlaceCurveSigns()
        {
            if (lib.curveSign == null) return 0;
            var parent = Group("CurveSigns");
            int placed = 0;
            int i = 60;
            while (i < route.Count - 10)
            {
                float k = Curvature(i);
                if (Mathf.Abs(k) < 1f / 235f) { i++; continue; }
                float sign = Mathf.Sign(k);
                int signAt = Mathf.Max(5, i - 45);
                float half = route.Widths[signAt] * 0.5f;
                Vector3 p = Beside(signAt, half + 2f);
                if (!IsBlocked(p))
                {
                    Place(sign < 0f ? lib.curveSignLeft : lib.curveSign, p, Quaternion.LookRotation(-Tangent3(signAt)), parent).name = "CurveSign";
                    Reserve(p, 1.5f);
                    placed++;
                }
                // Skip to the end of this corner.
                while (i < route.Count - 10 && Mathf.Abs(Curvature(i)) > 1f / 300f && Mathf.Sign(Curvature(i)) == sign) i++;
                i += 20;
            }
            return placed;
        }

        public void BuildVillage()
        {
            var parent = Group("Village");
            int placed = 0;
            for (int i = 0; i < route.Count; i += 11)
            {
                if (route.Roadside[i] != RoadsideStyle.Village) continue;
                float side = placed % 2 == 0 ? 1f : -1f;
                float half = route.Widths[i] * 0.5f;
                Vector3 p = Beside(i, side * (half + R(13f, 18f)));
                if (IsBlocked(p)) continue;
                Vector3 toRoad = route.Point(i) - p;
                toRoad.y = 0f;
                var house = Place(lib.houses[rng.Next(lib.houses.Count)], p + Vector3.down * 0.3f, Quaternion.LookRotation(toRoad), parent);
                house.name = "House";
                Reserve(p, 9f);
                placed++;

                // Fence in front of the garden.
                Vector3 f = Beside(i, side * (half + 6.5f));
                Place(lib.fenceSegment, f, Quaternion.LookRotation(Tangent3(i)), parent);
            }
        }

        public void ScatterRocksAndProps()
        {
            var parent = Group("Scatter");
            bool desert = StageTheme.Desert; // rocky desert: more rocks and boulders, no farm fences
            for (int i = 30; i < route.Count - 10; i += 4)
            {
                float half = route.Widths[i] * 0.5f;
                RoadsideStyle style = route.Roadside[i];

                if (R() < (desert ? 0.6f : 0.35f))
                {
                    float side = R() < 0.5f ? -1f : 1f;
                    Vector3 p = Beside(i, side * (half + R(4f, 16f)));
                    if (!IsBlocked(p) && sculptor.RouteDistance(p.x, p.z) > half + 3f)
                    {
                        var rock = Place(lib.rocks[rng.Next(lib.rocks.Count)], p + Vector3.down * R(0.2f, 0.5f),
                            Quaternion.Euler(R(-12f, 12f), R(0f, 360f), R(-12f, 12f)), parent, R(0.35f, 1.1f));
                        rock.name = "Rock";
                    }
                }

                if (style == RoadsideStyle.Forest && R() < 0.02f)
                {
                    float side = R() < 0.5f ? -1f : 1f;
                    Vector3 p = Beside(i, side * (half + R(9f, 14f)));
                    if (!IsBlocked(p))
                    {
                        Place(lib.logPile, p, Quaternion.LookRotation(Tangent3(i)) * Quaternion.Euler(0f, R(-10f, 10f), 0f), parent);
                        Reserve(p, 4f);
                    }
                }

                if (style == RoadsideStyle.Open && i % 60 == 0 && !desert)
                {
                    float side = R() < 0.5f ? -1f : 1f;
                    for (int k = 0; k < 8; k++)
                    {
                        int idx = Mathf.Min(route.Count - 1, i + k * 2);
                        Vector3 p = Beside(idx, side * (route.Widths[idx] * 0.5f + 12f));
                        if (IsBlocked(p)) break;
                        Place(lib.fenceSegment, p, Quaternion.LookRotation(Tangent3(idx)), parent);
                    }
                }
            }

            // Large boulders further out.
            for (int n = 0; n < (desert ? 300 : 160); n++)
            {
                int i = rng.Next(route.Count);
                float side = R() < 0.5f ? -1f : 1f;
                Vector3 p = Beside(i, side * (route.Widths[i] * 0.5f + R(22f, 90f)));
                if (IsBlocked(p) || sculptor.RouteDistance(p.x, p.z) < 18f) continue;
                Place(lib.rocks[rng.Next(lib.rocks.Count)], p + Vector3.down * 0.6f,
                    Quaternion.Euler(R(-15f, 15f), R(0f, 360f), R(-15f, 15f)), parent, R(1.5f, 3.8f)).name = "Boulder";
            }
        }
    }
}
