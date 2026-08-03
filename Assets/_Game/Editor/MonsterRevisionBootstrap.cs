#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TheBestMonkeyGame.Monsters;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace TheBestMonkeyGame.Editor
{
    public static class MonsterRevisionBootstrap
    {
        public const string TiptoePrefabPath = "Assets/_Game/Prefabs/Monsters/Tiptoe.prefab";
        public const string StatuePrefabPath = "Assets/_Game/Prefabs/Monsters/Statue.prefab";
        public const string NavMeshDataPath = "Assets/_Game/Navigation/MainMapNavMesh.asset";
        public const float TiptoeTargetHeight = 1.7f;
        public const float StatueTargetHeight = 1.9f;
        public const int SpawnPointCount = 8;

        private const string TiptoeSourcePath = "Assets/ThirdParty/Monsters/Tiptoe/tiptoe.glb";
        private const string StatueSourcePath = "Assets/ThirdParty/Monsters/Statue/statue.glb";
        private const string PlaceholderAudioPath = "Assets/_Game/Audio/Monsters/placeholder_monster_noise.wav";
        private const string BlackMaterialPath = "Assets/_Game/Materials/JumpscareBlack.mat";
        private const string FadeMaterialPath = "Assets/_Game/Materials/JumpscareFade.mat";
        private const int LocomotionLayer = 8;
        private const int PlayerLayer = 9;

        [MenuItem("Tools/The Best Monkey Game/Build Monster Revision")]
        public static void BuildMonsterRevision()
        {
            try
            {
                RevisionBootstrap.BuildRevision();
                EnsureFolders();
                Material black = EnsureMaterial(BlackMaterialPath, "Unlit/Color", Color.black);
                Material fade = EnsureMaterial(FadeMaterialPath, "Sprites/Default", new Color(0f, 0f, 0f, 0f));
                ConfigurePlayerPrefab(fade);

                MonsterBuildResult tiptoe = BuildMonster(
                    "Tiptoe", TiptoeSourcePath, TiptoePrefabPath, TiptoeTargetHeight, true);
                MonsterBuildResult statue = BuildMonster(
                    "Statue", StatueSourcePath, StatuePrefabPath, StatueTargetHeight, false);
                BuildScene(tiptoe.prefab, statue.prefab, black);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log(
                    $"MONSTER_REVISION_BUILD_SUCCESS floorOffset={VRFloorHeightCalibration.DefaultVerticalOffset:F2} " +
                    $"tiptoeHeight={tiptoe.height:F3} tiptoeClip={tiptoe.clipName} " +
                    $"statueHeight={statue.height:F3} statueClip={statue.clipName} spawns={SpawnPointCount}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        private static void EnsureFolders()
        {
            string[] folders =
            {
                "Assets/_Game/Prefabs/Monsters",
                "Assets/_Game/Animation/Monsters",
                "Assets/_Game/Audio/Monsters",
                "Assets/_Game/Navigation"
            };
            foreach (string folder in folders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
                    string name = Path.GetFileName(folder);
                    AssetDatabase.CreateFolder(parent, name);
                }
            }
        }

        private static void ConfigurePlayerPrefab(Material fadeMaterial)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(RevisionBootstrap.PlayerPrefabPath);
            try
            {
                Transform tracking = root.transform.Find("XR Origin");
                Transform head = tracking.Find("Main Camera");
                Transform locomotionRoot = root.transform.Find("GorillaLocomotion");
                Transform leftHand = locomotionRoot.Find("Left Hand Sphere");
                Transform rightHand = locomotionRoot.Find("Right Hand Sphere");

                Transform oldOverlay = head.Find("Development Fade Overlay");
                if (oldOverlay != null) UnityEngine.Object.DestroyImmediate(oldOverlay.gameObject);
                GameObject overlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
                overlay.name = "Development Fade Overlay";
                overlay.layer = PlayerLayer;
                overlay.transform.SetParent(head, false);
                overlay.transform.localPosition = new Vector3(0f, 0f, 0.08f);
                overlay.transform.localRotation = Quaternion.identity;
                overlay.transform.localScale = new Vector3(0.32f, 0.24f, 1f);
                UnityEngine.Object.DestroyImmediate(overlay.GetComponent<Collider>());
                Renderer overlayRenderer = overlay.GetComponent<Renderer>();
                overlayRenderer.sharedMaterial = fadeMaterial;
                overlayRenderer.enabled = false;
                overlayRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                overlayRenderer.receiveShadows = false;

                PlayerDeathController death = root.GetComponent<PlayerDeathController>();
                if (death == null) death = root.AddComponent<PlayerDeathController>();
                death.Configure(
                    root.GetComponent<GorillaLocomotion.Player>(),
                    root.GetComponent<PlayerRespawn>(),
                    root.GetComponent<Rigidbody>(),
                    head,
                    root.GetComponentsInChildren<XRTrackedPose>(true),
                    new[] { leftHand.GetComponent<Renderer>(), rightHand.GetComponent<Renderer>() },
                    overlayRenderer);
                PrefabUtility.SaveAsPrefabAsset(root, RevisionBootstrap.PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static MonsterBuildResult BuildMonster(
            string monsterName,
            string sourcePath,
            string prefabPath,
            float targetHeight,
            bool tiptoe)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null) throw new InvalidOperationException($"Could not load animated GLB: {sourcePath}");
            AnimationClip sourceClip = AssetDatabase.LoadAllAssetsAtPath(sourcePath)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase) && clip.length > 0.05f)
                .OrderByDescending(clip => clip.length)
                .FirstOrDefault();
            if (sourceClip == null) throw new InvalidOperationException($"{monsterName} GLB contains no imported animation clip.");

            string clipPath = $"Assets/_Game/Animation/Monsters/{monsterName}Locomotion.anim";
            string controllerPath = $"Assets/_Game/Animation/Monsters/{monsterName}.controller";
            AssetDatabase.DeleteAsset(clipPath);
            AssetDatabase.DeleteAsset(controllerPath);
            AnimationClip loopClip = new AnimationClip();
            EditorUtility.CopySerialized(sourceClip, loopClip);
            loopClip.name = $"{monsterName}Locomotion";
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(loopClip);
            settings.loopTime = true;
            settings.loopBlend = true;
            AnimationUtility.SetAnimationClipSettings(loopClip, settings);
            AssetDatabase.CreateAsset(loopClip, clipPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(controllerPath, loopClip);

            GameObject root = new GameObject(monsterName);
            root.transform.localScale = Vector3.one;
            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            MonsterNavigation navigation = root.AddComponent<MonsterNavigation>();
            navigation.Configure(tiptoe ? 5f : 2.1f, tiptoe ? 35f : 12f, tiptoe ? 720f : 300f, tiptoe ? 0.18f : 0.3f);

            GameObject visualRootObject = new GameObject("VisualRoot");
            visualRootObject.transform.SetParent(root.transform, false);
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            model.name = $"Animated{monsterName}Model";
            model.transform.SetParent(visualRootObject.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            Bounds initialBounds = CalculateRendererBounds(model);
            float uniformScale = targetHeight / initialBounds.size.y;
            model.transform.localScale = Vector3.one * uniformScale;
            Physics.SyncTransforms();
            Bounds scaledBounds = CalculateRendererBounds(model);
            model.transform.position += Vector3.up * -scaledBounds.min.y;
            Physics.SyncTransforms();
            float finalHeight = CalculateRendererBounds(model).size.y;
            if (Mathf.Abs(finalHeight - targetHeight) > 0.02f)
                throw new InvalidOperationException($"{monsterName} normalized height {finalHeight:F3} did not reach target {targetHeight:F2}.");

            Animator animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            MonsterAnimationController animation = root.AddComponent<MonsterAnimationController>();
            animation.Configure(animator);

            GameObject eyeObject = new GameObject("Perception Eye");
            eyeObject.transform.SetParent(root.transform, false);
            eyeObject.transform.localPosition = new Vector3(0f, targetHeight * 0.7f, 0.12f);
            MonsterPerception perception = root.AddComponent<MonsterPerception>();
            perception.Configure(eyeObject.transform, 1 << LocomotionLayer, tiptoe ? 30f : 15f, tiptoe ? 120f : 100f, tiptoe ? 0.15f : 0.12f);

            GameObject audioObject = new GameObject("Audio");
            audioObject.transform.SetParent(root.transform, false);
            AudioSource movementAudio = audioObject.AddComponent<AudioSource>();
            AudioSource oneShotAudio = audioObject.AddComponent<AudioSource>();
            MonsterAudioController audio = audioObject.AddComponent<MonsterAudioController>();
            audio.Configure(movementAudio, oneShotAudio, AssetDatabase.LoadAssetAtPath<AudioClip>(PlaceholderAudioPath));

            GameObject killObject = new GameObject("KillTrigger");
            killObject.transform.SetParent(root.transform, false);
            killObject.transform.localPosition = new Vector3(0f, targetHeight * 0.5f, 0.16f);
            CapsuleCollider killCollider = killObject.AddComponent<CapsuleCollider>();
            killCollider.isTrigger = true;
            killCollider.direction = 1;
            killCollider.radius = 0.34f;
            killCollider.height = Mathf.Max(0.7f, targetHeight * 0.75f);

            MonsterJumpscareController jumpscare = root.AddComponent<MonsterJumpscareController>();
            jumpscare.Configure(visualRootObject.transform, audio);
            MonsterBrain brain;
            if (tiptoe)
            {
                TiptoeBrain tiptoeBrain = root.AddComponent<TiptoeBrain>();
                tiptoeBrain.ConfigureTiptoe(5f, 11.5f, 2f, 6.5f, 17.5f);
                brain = tiptoeBrain;
            }
            else
            {
                StatueBrain statueBrain = root.AddComponent<StatueBrain>();
                statueBrain.ConfigureStatue(35f, 15f, 25f, 2f);
                brain = statueBrain;
            }
            brain.ConfigureShared(navigation, perception, animation, audio, jumpscare, 5f);
            MonsterKillTrigger killTrigger = killObject.AddComponent<MonsterKillTrigger>();
            killTrigger.Configure(brain);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return new MonsterBuildResult(prefab, finalHeight, sourceClip.name);
        }

        private static void BuildScene(GameObject tiptoePrefab, GameObject statuePrefab, Material blackMaterial)
        {
            Scene scene = EditorSceneManager.OpenScene(RevisionBootstrap.MainScenePath, OpenSceneMode.Single);
            GameObject oldSystems = GameObject.Find("MonsterSystems");
            if (oldSystems != null) UnityEngine.Object.DestroyImmediate(oldSystems);

            GameObject systems = new GameObject("MonsterSystems");
            GameObject surfaceObject = new GameObject("MonsterNavigationSurface");
            surfaceObject.transform.SetParent(systems.transform, false);
            NavMeshSurface surface = surfaceObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = 1 << LocomotionLayer;
            surface.overrideTileSize = true;
            surface.tileSize = 128;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.12f;
            AssetDatabase.DeleteAsset(NavMeshDataPath);
            surface.BuildNavMesh();
            if (surface.navMeshData == null) throw new InvalidOperationException("AI Navigation did not produce NavMesh data.");
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshDataPath);

            Transform playerSpawn = GameObject.Find("PlayerSpawn").transform;
            Bounds mapBounds = CalculateRendererBounds(GameObject.Find("GiggleFartsMap"));
            List<Vector3> spawnPositions = FindSpawnPositions(mapBounds, playerSpawn.position, SpawnPointCount);
            GameObject spawnRoot = new GameObject("MonsterSpawnPoints");
            spawnRoot.transform.SetParent(systems.transform, false);
            List<MonsterSpawnPoint> spawnPoints = new List<MonsterSpawnPoint>();
            for (int index = 0; index < spawnPositions.Count; index++)
            {
                GameObject point = new GameObject($"MonsterSpawnPoint_{index + 1:00}");
                point.transform.SetParent(spawnRoot.transform, false);
                point.transform.position = spawnPositions[index];
                spawnPoints.Add(point.AddComponent<MonsterSpawnPoint>());
            }

            GameObject tiptoe = (GameObject)PrefabUtility.InstantiatePrefab(tiptoePrefab, scene);
            tiptoe.transform.position = spawnPositions[0];
            GameObject statue = (GameObject)PrefabUtility.InstantiatePrefab(statuePrefab, scene);
            statue.transform.position = spawnPositions[Mathf.Min(4, spawnPositions.Count - 1)];

            CreateJumpscareRoom(systems.transform, mapBounds, blackMaterial);
            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, RevisionBootstrap.MainScenePath);
        }

        private static List<Vector3> FindSpawnPositions(Bounds bounds, Vector3 playerPosition, int count)
        {
            List<Vector3> candidates = new List<Vector3>();
            NavMeshPath path = new NavMeshPath();
            float rayY = bounds.max.y + 10f;
            for (int x = 0; x < 21; x++)
            {
                for (int z = 0; z < 21; z++)
                {
                    float px = Mathf.Lerp(bounds.min.x + 2f, bounds.max.x - 2f, x / 20f);
                    float pz = Mathf.Lerp(bounds.min.z + 2f, bounds.max.z - 2f, z / 20f);
                    RaycastHit[] hits = Physics.RaycastAll(new Vector3(px, rayY, pz), Vector3.down, bounds.size.y + 20f, 1 << LocomotionLayer, QueryTriggerInteraction.Ignore);
                    foreach (RaycastHit hit in hits.OrderBy(item => item.point.y))
                    {
                        if (hit.normal.y < 0.9f || !NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, 2f, NavMesh.AllAreas)) continue;
                        if (Vector3.Distance(navHit.position, playerPosition) < 12f) continue;
                        if (!NavMesh.CalculatePath(playerPosition, navHit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                        if (candidates.All(existing => Vector3.Distance(existing, navHit.position) > 2.5f)) candidates.Add(navHit.position);
                        break;
                    }
                }
            }
            if (candidates.Count < count) throw new InvalidOperationException($"Only {candidates.Count} reachable monster spawn candidates were found.");

            List<Vector3> selected = new List<Vector3>();
            selected.Add(candidates.OrderByDescending(p => Vector3.Distance(p, playerPosition)).First());
            while (selected.Count < count)
            {
                Vector3 next = candidates
                    .Where(candidate => selected.All(existing => Vector3.Distance(existing, candidate) >= 5f))
                    .OrderByDescending(candidate => selected.Min(existing => Vector3.Distance(existing, candidate)))
                    .FirstOrDefault();
                if (next == Vector3.zero) throw new InvalidOperationException("Could not distribute monster spawn points across reachable NavMesh areas.");
                selected.Add(next);
            }
            return selected;
        }

        private static void CreateJumpscareRoom(Transform parent, Bounds mapBounds, Material blackMaterial)
        {
            GameObject room = new GameObject("JumpscareRoom");
            room.transform.SetParent(parent, false);
            room.transform.position = new Vector3(mapBounds.max.x + 200f, -500f, mapBounds.max.z + 200f);
            Vector3[] positions =
            {
                new Vector3(0f, -0.1f, 0f), new Vector3(0f, 3.1f, 0f),
                new Vector3(-3.1f, 1.5f, 0f), new Vector3(3.1f, 1.5f, 0f),
                new Vector3(0f, 1.5f, -3.1f), new Vector3(0f, 1.5f, 3.1f)
            };
            Vector3[] scales =
            {
                new Vector3(6f, 0.2f, 6f), new Vector3(6f, 0.2f, 6f),
                new Vector3(0.2f, 3f, 6f), new Vector3(0.2f, 3f, 6f),
                new Vector3(6f, 3f, 0.2f), new Vector3(6f, 3f, 0.2f)
            };
            GameObject blackRoom = new GameObject("BlackRoom");
            blackRoom.transform.SetParent(room.transform, false);
            for (int i = 0; i < positions.Length; i++)
            {
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = $"BlackWall_{i + 1}";
                wall.transform.SetParent(blackRoom.transform, false);
                wall.transform.localPosition = positions[i];
                wall.transform.localScale = scales[i];
                wall.GetComponent<Renderer>().sharedMaterial = blackMaterial;
                UnityEngine.Object.DestroyImmediate(wall.GetComponent<Collider>());
            }

            GameObject playerAnchor = new GameObject("PlayerJumpscareAnchor");
            playerAnchor.transform.SetParent(room.transform, false);
            playerAnchor.transform.localPosition = Vector3.zero;
            GameObject monsterAnchor = new GameObject("MonsterJumpscareAnchor");
            monsterAnchor.transform.SetParent(room.transform, false);
            monsterAnchor.transform.localPosition = new Vector3(0f, 0f, 1.25f);
            monsterAnchor.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            GameObject lightObject = new GameObject("JumpscareLight");
            lightObject.transform.SetParent(room.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 1.6f, 0.2f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.intensity = 1.1f;
            light.range = 3f;
            light.shadows = LightShadows.None;
            light.color = new Color(0.7f, 0.75f, 0.85f);

            AudioSource centered = room.AddComponent<AudioSource>();
            JumpscareRoomController controller = room.AddComponent<JumpscareRoomController>();
            controller.Configure(playerAnchor.transform, monsterAnchor.transform, centered);
        }

        private static Material EnsureMaterial(string path, string shaderName, Color color)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find(shaderName) ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            if (material == null)
            {
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path), color = color };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
                material.color = color;
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        private static Bounds CalculateRendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException($"{root.name} has no renderers.");
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }

        private readonly struct MonsterBuildResult
        {
            public readonly GameObject prefab;
            public readonly float height;
            public readonly string clipName;
            public MonsterBuildResult(GameObject builtPrefab, float measuredHeight, string animationClipName)
            {
                prefab = builtPrefab;
                height = measuredHeight;
                clipName = animationClipName;
            }
        }
    }
}
#endif
