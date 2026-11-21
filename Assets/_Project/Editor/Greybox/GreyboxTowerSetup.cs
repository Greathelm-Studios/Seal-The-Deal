using System.Collections.Generic;
using CloseTheDeal.Glass;
using CloseTheDeal.Props;
using CloseTheDeal.Tower;
using FishNet.Object;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CloseTheDeal.Editor.Greybox
{
    /// <summary>Generates greybox floor template prefabs, spiral stair mesh, locked door prefab, and Tower object.</summary>
    public static class GreyboxTowerSetup
    {
        const string FloorFolder = "Assets/_Project/Floors";
        const string ProfileFolder = "Assets/_Project/Profiles";
        const string TowerProfilePath = ProfileFolder + "/DefaultTower.asset";
        const string FlightMeshPath = FloorFolder + "/SpiralFlight.asset";
        const string GlassMaterialPath = FloorFolder + "/GreyboxGlass.mat";
        const string DoorMaterialPath = FloorFolder + "/GreyboxDoor.mat";
        const string DoorPrefabPath = "Assets/_Project/Prefabs/Props/LockedDoor.prefab";
        const string GlassProfilePath = "Assets/_Project/Profiles/Glass/DefaultGlass.asset";
        const string TowerObjectName = "Tower";
        static readonly Vector3 TowerOrigin = new(80f, 0f, 0f);

        const float Slab = 0.2f;
        const float Wall = 0.2f;
        const float SillHeight = 0.6f; // Must match art kit SILL height.
        const float PillarWidth = 0.4f;
        const float PillarSpacing = 4f;
        const float DoorWidth = 1.2f;
        const float ParapetHeight = 1.2f;
        const float PaneThickness = 0.04f;
        const float PaneInset = 0.01f;

        // Spiral stairs: one full turn per floor around a solid column, in an open well near
        // the middle of the main floor. Angles run clockwise seen from above, 0° facing the
        // rival tower. Each flight starts at 0° on its own floor and arrives at 0° on the next.
        const float StairRadius = 2.6f;      // outer edge of the treads
        const float ColumnRadius = 0.8f;     // inner edge; 42° at the inner edge with 4.5 m floors, under the motor's 50° limit
        const float FlightThickness = 0.25f;
        const int FlightSegments = 48;
        const float CageGap = 0.15f;         // cage panels sit just outside the treads
        const int CagePanels = 12;
        const float CageOpeningFrom = 300f;  // no panels from here to 360°: where the flight from below arrives
        const float StairBackClearance = 1.2f; // walkway between the cage and the spine wall
        const float LockedDoorAngle = 30f;   // the door stands just past the arrival point

        // Service spine rooms across the width, left to right, as fractions of the width.
        static readonly (string Name, float Fraction)[] SpineRooms =
        {
            ("WC", 0.3f), ("STORE", 0.4f), ("UTIL", 0.3f)
        };

        const int SpawnsPerLobby = 4;
        const int GeneratedOffices = 3;
        // "Windows" is left out on purpose: merging freezes a mesh, and a pane must vanish when it breaks.
        static readonly string[] StillGroups = { "Shell", "Stairs", "Contents" };

        [MenuItem("Close the Deal/Greybox/Rebuild Floor Templates")]
        public static void RebuildFloorTemplates()
        {
            TowerProfile profile = EnsureProfile();
            GreyboxPropSetup.Ensure(false);
            BuildAllTemplates(profile, true);
            GreyboxSceneSetup.SetUpScene();
        }

        /// <summary>Rebuilds tool-owned floor templates only (does not open or save Greybox.unity).</summary>
        [MenuItem("Close the Deal/Greybox/Rebuild Floor Templates Only")]
        public static void RebuildFloorTemplatesOnly()
        {
            TowerProfile profile = EnsureProfile();
            GreyboxPropSetup.Ensure(false);
            BuildAllTemplates(profile, true);
        }

        /// <summary>Called by Set Up Scene: creates only what is missing.</summary>
        public static void Ensure()
        {
            TowerProfile profile = EnsureProfile();
            BuildAllTemplates(profile, false);
            EnsureTowerObject(profile);
        }

        static TowerProfile EnsureProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<TowerProfile>(TowerProfilePath);
            if (profile != null)
                return profile;

            if (!AssetDatabase.IsValidFolder(ProfileFolder))
                AssetDatabase.CreateFolder("Assets/_Project", "Profiles");

            profile = ScriptableObject.CreateInstance<TowerProfile>();
            AssetDatabase.CreateAsset(profile, TowerProfilePath);
            Debug.Log("[Tower] Created " + TowerProfilePath);
            return profile;
        }

        struct StairKit
        {
            public Mesh Flight;
            public Material Glass;
            public Material WindowGlass;
            public NetworkObject Door;
        }

        static void BuildAllTemplates(TowerProfile profile, bool overwrite)
        {
            if (!AssetDatabase.IsValidFolder(FloorFolder))
                AssetDatabase.CreateFolder("Assets/_Project", "Floors");

            GreyboxPropSetup.PropSet props = GreyboxPropSetup.Ensure(false);
            var kit = new StairKit
            {
                Flight = EnsureFlightMesh(profile, overwrite),
                Glass = EnsureGlassMaterial(),
                WindowGlass = WindowGlassMaterial(),
                Door = EnsureDoorPrefab(profile, overwrite)
            };

            var serialized = new SerializedObject(profile);
            AssignIfEmpty(serialized.FindProperty("Lobby"), BuildTemplate(profile, "Lobby", FloorKind.Lobby, 0, overwrite, props, kit));
            AssignIfEmpty(serialized.FindProperty("Checkpoint"), BuildTemplate(profile, "Checkpoint", FloorKind.Checkpoint, 0, overwrite, props, kit));
            AssignIfEmpty(serialized.FindProperty("Boardroom"), BuildTemplate(profile, "Boardroom", FloorKind.Boardroom, 0, overwrite, props, kit));
            AssignIfEmpty(serialized.FindProperty("Roof"), BuildTemplate(profile, "Roof", FloorKind.Roof, 0, overwrite, props, kit));

            // A designer's office list is never shortened or reordered; the generated offices
            // only fill it when it is empty, or fill slots that have been left empty.
            SerializedProperty offices = serialized.FindProperty("Offices");
            if (offices.arraySize == 0)
                offices.arraySize = GeneratedOffices;
            for (int i = 0; i < GeneratedOffices; i++)
            {
                FloorTemplate office = BuildTemplate(profile, $"Office{(char)('A' + i)}", FloorKind.Office, i, overwrite, props, kit);
                if (i < offices.arraySize)
                    AssignIfEmpty(offices.GetArrayElementAtIndex(i), office);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        static void AssignIfEmpty(SerializedProperty slot, FloorTemplate template)
        {
            if (template != null && slot.objectReferenceValue == null)
                slot.objectReferenceValue = template;
        }

        static FloorTemplate BuildTemplate(TowerProfile profile, string name, FloorKind kind, int variant, bool overwrite, GreyboxPropSetup.PropSet props, StairKit kit)
        {
            string path = $"{FloorFolder}/Floor_{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && (!overwrite || !GeneratedAssetGuard.MayOverwrite(path)))
                return existing.GetComponent<FloorTemplate>();

            var root = new GameObject("Floor_" + name);
            var template = root.AddComponent<FloorTemplate>();
            template.Kind = kind;

            BuildShell(root.transform, profile, kind);
            BuildWindowPanes(root.transform, profile, kind, kit.WindowGlass);
            BuildStairs(root.transform, profile, kind, kit);
            BuildContents(root.transform, profile, kind, variant, props);
            if (kind == FloorKind.Lobby)
                template.SpawnPoints = BuildSpawns(root.transform, profile);
            template.NeverMoves = StillParts(root.transform);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            GeneratedAssetGuard.MarkGenerated(prefab);
            Debug.Log("[Tower] Wrote " + path);
            return prefab.GetComponent<FloorTemplate>();
        }

        static Transform[] StillParts(Transform floor)
        {
            var groups = new List<Transform>();
            foreach (string name in StillGroups)
            {
                Transform group = floor.Find(name);
                if (group != null)
                    groups.Add(group);
            }

            return groups.ToArray();
        }

        static void BuildShell(Transform floor, TowerProfile p, FloorKind kind)
        {
            float w = p.Width;
            float depth = p.MainDepth + p.SpineDepth;
            float h = p.FloorHeight;
            float half = w * 0.5f;

            Transform shell = Group(floor, "Shell");

            // The lobby has nothing below it; every other floor has the stairwell open to the flight from below.
            if (kind == FloorKind.Lobby)
                Box(shell, "Slab main", new Vector3(0f, -Slab * 0.5f, -p.MainDepth * 0.5f), new Vector3(w, Slab, p.MainDepth));
            else
                BuildSlabWithStairwell(shell, p);
            Box(shell, "Slab spine", new Vector3(0f, -Slab * 0.5f, -p.MainDepth - p.SpineDepth * 0.5f), new Vector3(w, Slab, p.SpineDepth));

            float wallHeight = kind == FloorKind.Roof ? ParapetHeight : h;
            float wallY = wallHeight * 0.5f;
            Box(shell, "Wall back", new Vector3(0f, wallY, -depth + Wall * 0.5f), new Vector3(w, wallHeight, Wall));
            Box(shell, "Wall left", new Vector3(-half + Wall * 0.5f, wallY, -depth * 0.5f), new Vector3(Wall, wallHeight, depth));
            Box(shell, "Wall right", new Vector3(half - Wall * 0.5f, wallY, -depth * 0.5f), new Vector3(Wall, wallHeight, depth));

            if (kind == FloorKind.Roof)
            {
                Box(shell, "Parapet front", new Vector3(0f, wallY, -Wall * 0.5f), new Vector3(w, wallHeight, Wall));
            }
            else
            {
                BuildWindowWall(shell, p);
                BuildSpinePartitions(shell, p);
            }
        }

        static void BuildSlabWithStairwell(Transform shell, TowerProfile p)
        {
            float half = p.Width * 0.5f;
            float y = -Slab * 0.5f;
            Vector3 c = StairCentre(p);
            float r = StairRadius;
            float front = c.z + r;   // nearer the windows
            float back = c.z - r;

            Box(shell, "Slab front", new Vector3(0f, y, front * 0.5f), new Vector3(p.Width, Slab, -front));
            Box(shell, "Slab behind stairs", new Vector3(0f, y, (back - p.MainDepth) * 0.5f), new Vector3(p.Width, Slab, back + p.MainDepth));
            Box(shell, "Slab left of stairs", new Vector3((-half - r) * 0.5f, y, c.z), new Vector3(half - r, Slab, 2f * r));
            Box(shell, "Slab right of stairs", new Vector3((half + r) * 0.5f, y, c.z), new Vector3(half - r, Slab, 2f * r));

            for (int i = 0; i < 4; i++)
            {
                float angle = 45f + i * 90f;
                Vector3 centre = c + Direction(angle) * (r * 1.5f) + new Vector3(0f, y, 0f);
                Box(shell, "Slab stair corner", centre, new Vector3(r, Slab, r), Quaternion.Euler(0f, angle, 0f));
            }
        }

        static void BuildWindowWall(Transform shell, TowerProfile p)
        {
            float half = p.Width * 0.5f;
            Box(shell, "Sill", new Vector3(0f, SillHeight * 0.5f, -Wall * 0.5f), new Vector3(p.Width, SillHeight, Wall));

            float pillarHeight = p.FloorHeight - SillHeight;
            for (float x = -half; x <= half + 0.01f; x += PillarSpacing)
            {
                float clampedX = Mathf.Clamp(x, -half + PillarWidth * 0.5f, half - PillarWidth * 0.5f);
                Box(shell, "Pillar", new Vector3(clampedX, SillHeight + pillarHeight * 0.5f, -Wall * 0.5f), new Vector3(PillarWidth, pillarHeight, Wall));
            }
        }

        static void BuildWindowPanes(Transform floor, TowerProfile p, FloorKind kind, Material glass)
        {
            if (kind == FloorKind.Roof)
                return;
            if (glass == null)
            {
                Debug.LogWarning("[Tower] No window glass: " + GlassProfilePath + " lists no Glass Materials. Templates are built with open windows.");
                return;
            }

            Transform windows = Group(floor, "Windows");
            bool doorway = kind == FloorKind.Lobby || kind == FloorKind.Checkpoint;
            float half = p.Width * 0.5f;
            float height = p.FloorHeight - Slab - SillHeight;

            for (float x = -half; x < half - 0.01f; x += PillarSpacing)
            {
                float left = Mathf.Max(x, -half + PillarWidth * 0.5f) + PillarWidth * 0.5f;
                float right = Mathf.Min(x + PillarSpacing, half - PillarWidth * 0.5f) - PillarWidth * 0.5f;
                if (right - left < 0.2f || (doorway && left < 0f && right > 0f))
                    continue;

                GameObject pane = Box(windows, "Window glass", new Vector3((left + right) * 0.5f, SillHeight + height * 0.5f, -Wall * 0.5f), new Vector3(right - left - PaneInset * 2f, height - PaneInset, PaneThickness));
                pane.isStatic = false;
                pane.GetComponent<Renderer>().sharedMaterial = glass;
                pane.AddComponent<GlassPane>();
            }
        }

        static Material WindowGlassMaterial()
        {
            var profile = AssetDatabase.LoadAssetAtPath<GlassProfile>(GlassProfilePath);
            return profile != null && profile.GlassMaterials != null && profile.GlassMaterials.Length > 0 ? profile.GlassMaterials[0] : null;
        }

        static void BuildSpinePartitions(Transform shell, TowerProfile p)
        {
            float h = p.FloorHeight;
            float frontZ = -p.MainDepth + Wall * 0.5f;
            float backZ = -p.MainDepth - p.SpineDepth;

            for (int i = 0; i < SpineRooms.Length; i++)
            {
                RoomBounds room = RoomX(p, i);
                float doorCentre = room.Centre;

                float leftLength = doorCentre - DoorWidth * 0.5f - room.Left;
                float rightLength = room.Right - (doorCentre + DoorWidth * 0.5f);
                Box(shell, SpineRooms[i].Name + " front left", new Vector3(room.Left + leftLength * 0.5f, h * 0.5f, frontZ), new Vector3(leftLength, h, Wall));
                Box(shell, SpineRooms[i].Name + " front right", new Vector3(room.Right - rightLength * 0.5f, h * 0.5f, frontZ), new Vector3(rightLength, h, Wall));

                if (i > 0)
                    Box(shell, SpineRooms[i].Name + " divider", new Vector3(room.Left, h * 0.5f, (frontZ + backZ) * 0.5f), new Vector3(Wall, h, p.SpineDepth - Wall));
            }
        }

        static void BuildStairs(Transform floor, TowerProfile p, FloorKind kind, StairKit kit)
        {
            if (kind == FloorKind.Roof)
                return;

            Transform stairs = Group(floor, "Stairs");
            stairs.localPosition = StairCentre(p);

            Column(stairs, p.FloorHeight);
            var flight = new GameObject("Flight") { isStatic = true };
            flight.transform.SetParent(stairs, false);
            flight.AddComponent<MeshFilter>().sharedMesh = kit.Flight;
            flight.AddComponent<MeshRenderer>().sharedMaterial = GraphicsSettings.currentRenderPipeline != null
                ? GraphicsSettings.currentRenderPipeline.defaultMaterial
                : AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
            flight.AddComponent<MeshCollider>().sharedMesh = kit.Flight;

            if (kind == FloorKind.Lobby)
                return;

            BuildCage(stairs, p, kit.Glass);

            float doorY = p.FloorHeight * LockedDoorAngle / 360f - 0.02f;
            float doorRadius = (ColumnRadius + StairRadius) * 0.5f;
            var marker = new GameObject("Locked door marker");
            marker.transform.SetParent(stairs, false);
            marker.transform.localPosition = Direction(LockedDoorAngle) * doorRadius + new Vector3(0f, doorY, 0f);
            // The door's width runs along its local X; turn that to point out from the column.
            marker.transform.localRotation = Quaternion.Euler(0f, LockedDoorAngle - 90f, 0f);
            marker.AddComponent<PropMarker>().Prefab = kit.Door;
        }

        static void BuildCage(Transform stairs, TowerProfile p, Material glass)
        {
            float step = 360f / CagePanels;
            float radius = StairRadius + CageGap;
            float width = 2f * radius * Mathf.Tan(step * 0.5f * Mathf.Deg2Rad) + 0.05f;
            float height = p.FloorHeight - Slab;

            for (int i = 0; i < CagePanels; i++)
            {
                float angle = step * (i + 0.5f);
                if (angle > CageOpeningFrom)
                    continue;

                GameObject panel = Box(stairs, "Cage glass", Direction(angle) * radius + new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, 0.05f), Quaternion.Euler(0f, angle, 0f));
                panel.GetComponent<Renderer>().sharedMaterial = glass;
            }
        }

        static void Column(Transform stairs, float height)
        {
            GameObject column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            column.name = "Column";
            column.isStatic = true;
            column.transform.SetParent(stairs, false);
            column.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            column.transform.localScale = new Vector3(ColumnRadius * 2f, height * 0.5f, ColumnRadius * 2f);
        }

        static Vector3 StairCentre(TowerProfile p)
        {
            float outer = StairRadius + CageGap;
            float z = -p.MainDepth + outer + StairBackClearance;
            return new Vector3(0f, 0f, Mathf.Min(z, -outer - 1f));
        }

        static Vector3 Direction(float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        }

        static Mesh EnsureFlightMesh(TowerProfile p, bool overwrite)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(FlightMeshPath);
            if (mesh != null && (!overwrite || !GeneratedAssetGuard.MayOverwrite(FlightMeshPath)))
                return mesh;

            bool created = mesh == null;
            if (created)
                mesh = new Mesh { name = "SpiralFlight" };

            FillFlightMesh(mesh, p.FloorHeight);
            if (created)
                AssetDatabase.CreateAsset(mesh, FlightMeshPath);
            else
                EditorUtility.SetDirty(mesh);

            GeneratedAssetGuard.MarkGenerated(mesh);
            Debug.Log("[Tower] Wrote " + FlightMeshPath);
            return mesh;
        }

        static void FillFlightMesh(Mesh mesh, float rise)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            Vector3 Point(int i, float radius, bool top)
            {
                float t = i / (float)FlightSegments;
                Vector3 p = Direction(t * 360f) * radius;
                p.y = t * rise - (top ? 0f : FlightThickness);
                return p;
            }

            for (int i = 0; i < FlightSegments; i++)
            {
                Vector3 innerTop0 = Point(i, ColumnRadius, true), innerTop1 = Point(i + 1, ColumnRadius, true);
                Vector3 outerTop0 = Point(i, StairRadius, true), outerTop1 = Point(i + 1, StairRadius, true);
                Vector3 innerBottom0 = Point(i, ColumnRadius, false), innerBottom1 = Point(i + 1, ColumnRadius, false);
                Vector3 outerBottom0 = Point(i, StairRadius, false), outerBottom1 = Point(i + 1, StairRadius, false);
                Vector3 mid = Direction((i + 0.5f) / FlightSegments * 360f);

                Quad(vertices, triangles, innerTop0, outerTop0, outerTop1, innerTop1, Vector3.up);
                Quad(vertices, triangles, innerBottom0, outerBottom0, outerBottom1, innerBottom1, Vector3.down);
                Quad(vertices, triangles, outerTop0, outerBottom0, outerBottom1, outerTop1, mid);
                Quad(vertices, triangles, innerTop0, innerBottom0, innerBottom1, innerTop1, -mid);
            }

            // End caps, facing back along the turn at the start and forward at the end.
            Quad(vertices, triangles, Point(0, ColumnRadius, true), Point(0, StairRadius, true), Point(0, StairRadius, false), Point(0, ColumnRadius, false), -Vector3.right);
            Quad(vertices, triangles, Point(FlightSegments, ColumnRadius, true), Point(FlightSegments, StairRadius, true), Point(FlightSegments, StairRadius, false), Point(FlightSegments, ColumnRadius, false), Vector3.right);

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        static void Quad(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);

            // Unity's front faces wind clockwise seen from outside, and for that winding the cross product points outward.
            bool flip = Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f;
            if (flip)
                triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            else
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }

        static Material EnsureGlassMaterial()
        {
            var glass = AssetDatabase.LoadAssetAtPath<Material>(GlassMaterialPath);
            if (glass != null)
                return glass;

            glass = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "GreyboxGlass" };
            glass.SetFloat("_Surface", 1f);
            glass.SetFloat("_Blend", 0f);
            glass.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            glass.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            glass.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            glass.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            glass.SetFloat("_ZWrite", 0f);
            glass.SetOverrideTag("RenderType", "Transparent");
            glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            glass.renderQueue = (int)RenderQueue.Transparent;
            glass.SetColor("_BaseColor", new Color(0.65f, 0.85f, 0.95f, 0.2f));
            AssetDatabase.CreateAsset(glass, GlassMaterialPath);
            return glass;
        }

        static Material EnsureDoorMaterial()
        {
            var door = AssetDatabase.LoadAssetAtPath<Material>(DoorMaterialPath);
            if (door != null)
                return door;

            door = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "GreyboxDoor" };
            door.SetColor("_BaseColor", new Color(0.7f, 0.15f, 0.12f));
            AssetDatabase.CreateAsset(door, DoorMaterialPath);
            return door;
        }

        static NetworkObject EnsureDoorPrefab(TowerProfile p, bool overwrite)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefabPath);
            if (existing != null && (!overwrite || !GeneratedAssetGuard.MayOverwrite(DoorPrefabPath)))
                return existing.GetComponent<NetworkObject>();

            var size = new Vector3(StairRadius - ColumnRadius - 0.05f, p.FloorHeight - FlightThickness - 0.05f, 0.2f);
            var root = new GameObject("LockedDoor") { layer = GreyboxSceneSetup.PropLayer };

            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, size.y * 0.5f, 0f);
            collider.size = size;

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Body";
            visual.layer = GreyboxSceneSetup.PropLayer;
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = collider.center;
            visual.transform.localScale = size;
            visual.GetComponent<Renderer>().sharedMaterial = EnsureDoorMaterial();

            root.AddComponent<NetworkObject>();
            root.AddComponent<BreakableDoor>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, DoorPrefabPath);
            Object.DestroyImmediate(root);
            GeneratedAssetGuard.MarkGenerated(prefab);
            Debug.Log("[Tower] Wrote " + DoorPrefabPath);
            return prefab.GetComponent<NetworkObject>();
        }

        static void BuildContents(Transform floor, TowerProfile p, FloorKind kind, int variant, GreyboxPropSetup.PropSet props)
        {
            Transform contents = Group(floor, "Contents");
            float midZ = -p.MainDepth * 0.5f;

            switch (kind)
            {
                case FloorKind.Lobby:
                    Box(contents, "Reception desk", new Vector3(p.Width * 0.3f, 0.55f, -p.MainDepth * 0.65f), new Vector3(5f, 1.1f, 0.8f));
                    Marker(contents, "Chair", props.Chair, new Vector3(-p.Width * 0.4f, 0f, midZ), 90f);
                    Marker(contents, "Chair", props.Chair, new Vector3(p.Width * 0.4f, 0f, midZ), -90f);
                    break;
                case FloorKind.Checkpoint:
                    Box(contents, "Checkpoint marker", new Vector3(-p.Width * 0.3f, 1.5f, midZ), new Vector3(1f, 3f, 1f));
                    break;
                case FloorKind.Boardroom:
                    BuildBoardroom(contents, p, props);
                    break;
                case FloorKind.Roof:
                    break;
                default:
                    BuildOffice(contents, p, variant, props);
                    break;
            }
        }

        static void BuildBoardroom(Transform contents, TowerProfile p, GreyboxPropSetup.PropSet props)
        {
            float midZ = -p.MainDepth * 0.5f;
            float length = p.Width * 0.25f;
            float x = -p.Width * 0.28f;
            Box(contents, "Board table", new Vector3(x, 0.4f, midZ), new Vector3(length, 0.8f, 2.5f));
            for (int i = 0; i < 3; i++)
            {
                float chairX = x + (i - 1) * length / 3f;
                Marker(contents, "Chair", props.Chair, new Vector3(chairX, 0f, midZ + 2f), 180f);
                Marker(contents, "Chair", props.Chair, new Vector3(chairX, 0f, midZ - 2f));
            }
        }

        static void BuildOffice(Transform contents, TowerProfile p, int variant, GreyboxPropSetup.PropSet props)
        {
            float half = p.Width * 0.5f;
            float d = p.MainDepth;
            switch (variant)
            {
                case 0:
                    // One row of four desks, evenly spaced across the floor, in front of the stairwell.
                    for (int i = 0; i < 4; i++)
                    {
                        float x = -half + p.Width * (i + 1) / 5f;
                        float z = -d * 0.3f;
                        Marker(contents, "Desk", props.Desk, new Vector3(x, 0f, z));
                        Marker(contents, "Monitor", props.Monitor, new Vector3(x, 0.75f, z - 0.1f));
                        Marker(contents, "Chair", props.Chair, new Vector3(x, 0f, z + 1f), 180f);
                    }
                    break;
                case 1:
                    GlassRoom(contents, new Vector3(-p.Width * 0.3f, 0f, -d * 0.55f), props);
                    GlassRoom(contents, new Vector3(p.Width * 0.3f, 0f, -d * 0.55f), props);
                    break;
                default:
                    Marker(contents, "Cabinet", props.Cabinet, new Vector3(-half + 1.5f, 0f, -d + 2.5f), 90f);
                    Marker(contents, "Cabinet", props.Cabinet, new Vector3(half - 1.5f, 0f, -d + 2.5f), -90f);
                    Marker(contents, "Desk", props.Desk, new Vector3(-p.Width * 0.2f, 0f, -d * 0.3f));
                    Marker(contents, "Desk", props.Desk, new Vector3(p.Width * 0.2f, 0f, -d * 0.3f));
                    Marker(contents, "Chair", props.Chair, new Vector3(-p.Width * 0.2f, 0f, -d * 0.3f + 1f), 180f);
                    Marker(contents, "Chair", props.Chair, new Vector3(p.Width * 0.2f, 0f, -d * 0.3f + 1f), 180f);
                    Box(contents, "Sofa", new Vector3(-p.Width * 0.3f, 0.4f, -d * 0.6f), new Vector3(2.4f, 0.8f, 0.9f));
                    Marker(contents, "Copier", props.Copier, new Vector3(p.Width * 0.3f, 0f, -d + 1.5f));
                    break;
            }
        }

        static void GlassRoom(Transform contents, Vector3 centre, GreyboxPropSetup.PropSet props)
        {
            const float roomW = 4f, roomD = 3f, glassH = 2.5f;
            Box(contents, "Glass back", centre + new Vector3(0f, glassH * 0.5f, -roomD * 0.5f), new Vector3(roomW, glassH, 0.1f));
            Box(contents, "Glass left", centre + new Vector3(-roomW * 0.5f, glassH * 0.5f, 0f), new Vector3(0.1f, glassH, roomD));
            Box(contents, "Glass right", centre + new Vector3(roomW * 0.5f, glassH * 0.5f, 0f), new Vector3(0.1f, glassH, roomD));
            Box(contents, "Meeting table", centre + new Vector3(0f, 0.375f, 0f), new Vector3(2f, 0.75f, 1f));
            Marker(contents, "Chair", props.Chair, centre + new Vector3(-1.4f, 0f, 0f), 90f);
            Marker(contents, "Chair", props.Chair, centre + new Vector3(1.4f, 0f, 0f), -90f);
        }

        static void Marker(Transform parent, string name, NetworkObject prefab, Vector3 position, float yawDegrees = 0f)
        {
            var marker = new GameObject(name + " marker");
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = position;
            marker.transform.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);
            marker.AddComponent<PropMarker>().Prefab = prefab;
        }

        static Transform[] BuildSpawns(Transform floor, TowerProfile p)
        {
            Transform group = Group(floor, "Spawns");
            // One per player of a full team, in a row between the windows and the stairwell.
            var points = new Transform[SpawnsPerLobby];
            float z = -Mathf.Min(2.5f, p.MainDepth * 0.25f);
            for (int i = 0; i < points.Length; i++)
            {
                var point = new GameObject("Spawn " + (i + 1));
                point.transform.SetParent(group, false);
                point.transform.localPosition = new Vector3(p.Width * (-0.3f + i * 0.2f), 0.1f, z);
                point.transform.localRotation = Quaternion.identity; // facing +Z: the rival tower
                points[i] = point.transform;
            }

            return points;
        }

        static void EnsureTowerObject(TowerProfile profile)
        {
            TowerBuilder builder = Object.FindAnyObjectByType<TowerBuilder>();
            if (builder == null)
            {
                var go = new GameObject(TowerObjectName);
                go.transform.position = TowerOrigin;
                go.AddComponent<NetworkObject>();
                builder = go.AddComponent<TowerBuilder>();

                var serialized = new SerializedObject(builder);
                serialized.FindProperty("_profile").objectReferenceValue = profile;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[Tower] Placed the Tower object.");
            }

            if (builder.GetComponent<PropSpawner>() == null)
                builder.gameObject.AddComponent<PropSpawner>();
        }

        struct RoomBounds
        {
            public float Left;
            public float Right;
            public float Width => Right - Left;
            public float Centre => (Left + Right) * 0.5f;
        }

        static RoomBounds RoomX(TowerProfile p, int index)
        {
            float x = -p.Width * 0.5f;
            for (int i = 0; i < index; i++)
                x += p.Width * SpineRooms[i].Fraction;

            return new RoomBounds { Left = x, Right = x + p.Width * SpineRooms[index].Fraction };
        }

        static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static GameObject Box(Transform parent, string name, Vector3 centre, Vector3 size, Quaternion? rotation = null)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.isStatic = true;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = centre;
            box.transform.localRotation = rotation ?? Quaternion.identity;
            box.transform.localScale = size;
            return box;
        }
    }
}
