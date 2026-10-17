using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class ForestPopulator
{
    private const string FOREST_PARENT_NAME = "PurePolyNatureForest";
    private const string PURE_POLY_PREFABS_PATH = "Assets/Models/Pure Poly/Free Low Poly Nature Pack/Prefabs";

    [MenuItem("Tools/Populate with Pure Poly Nature Pack")]
    public static void PopulateWithPurePolyNaturePack()
    {
        var terrain = FindOrCreateTerrain();
        if (terrain == null || terrain.terrainData == null) return;

        ClearExistingForest();

        var forestParent = new GameObject(FOREST_PARENT_NAME);
        Undo.RegisterCreatedObjectUndo(forestParent, "Create Forest Container");

        Debug.Log("🌲 Starting population using **Free Low Poly Nature Pack** from Models/Pure Poly directory...");

        var rand = new System.Random(42);

        // Load categorized prefabs from the Pure Poly Nature Pack
        var treePrefabs = LoadPrefabsByKeywords(new[] { "Birch_Tree", "Tree_" });
        var detailPrefabs = LoadPrefabsByKeywords(new[] { "Grass", "Daffodil", "Hyacinth", "Sunflower", "Mushroom", "Cemetery_Pebbles", "Pebbles" });
        var uniquePrefabs = LoadPrefabsByKeywords(new[] { "Rock", "Moss_Grown", "Rock_Pile", "Forest_Mountain_Moss", "Bridge", "Fence", "Meadow", "Floor_Tile", "Lake_Ground" });

        // Configure terrain prototypes for efficient rendering (TreeInstances + Detail meshes)
        SetupTreePrototypes(terrain, treePrefabs);
        SetupDetailPrototypes(terrain, detailPrefabs);

        Vector3 terrainCenter = new Vector3(125f, 0f, 125f);
        float terrainSize = 250f;

        AddTreeInstances(terrain, rand, terrainCenter, terrainSize);
        AddDetailLayers(terrain, rand);
        PlaceUniqueObjects(forestParent, uniquePrefabs, rand, terrain, terrainCenter, terrainSize);

        terrain.Flush();
        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(terrain.terrainData);
        EditorUtility.SetDirty(forestParent);

        Debug.Log("✅ **Pure Poly Nature Forest** population complete from Models/Pure Poly!\n" +
                  "• TreePrototypes + ~200 high-performance TreeInstances (Birch Trees, PP_Tree variants)\n" +
                  "• DetailPrototypes + dense layers for grass, flowers (Daffodil/Hyacinth/Sunflower), mushrooms, pebbles\n" +
                  "• 40+ hand-placed unique objects (rocks, mossy piles, bridges, fences, meadow/floor tiles)\n" +
                  "Terrain uses 250x250 size matching the pack. Run Tools > Generate Low Poly Forest Terrain (Free Pack) to reset. Bake NavMesh next if using AI Navigation.");
    }

    private static Terrain FindOrCreateTerrain()
    {
        var terrainObj = GameObject.Find("Terrain");
        if (terrainObj == null)
        {
            Debug.LogError("Terrain not found. Run Tools > Generate Low Poly Forest Terrain (Free Pack) first.");
            return null;
        }
        return terrainObj.GetComponent<Terrain>();
    }

    private static void ClearExistingForest()
    {
        var oldParent = GameObject.Find(FOREST_PARENT_NAME);
        if (oldParent != null)
        {
            Undo.DestroyObjectImmediate(oldParent);
        }

        // Clean any remaining forest objects
        foreach (var obj in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (obj == null) continue;
            string name = obj.name;
            if (name.Contains("Forest") || name.Contains("PurePoly") || 
                name.StartsWith("PP_") || name.Contains("PP_Birch") || name.Contains("PP_Tree"))
            {
                if (!name.Contains("Terrain"))
                {
                    Undo.DestroyObjectImmediate(obj);
                }
            }
        }

        Debug.Log("🧹 Cleared previous Pure Poly forest objects.");
    }

    private static List<GameObject> LoadPrefabsByKeywords(string[] keywords)
    {
        var models = new List<GameObject>();
        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { PURE_POLY_PREFABS_PATH });

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                string prefabName = prefab.name;
                if (keywords.Any(k => prefabName.IndexOf(k, System.StringComparison.OrdinalIgnoreCase) >= 0) &&
                    !models.Any(m => m.name == prefabName))
                {
                    models.Add(prefab);
                }
            }
        }

        Debug.Log($"Loaded {models.Count} prefabs from Pure Poly matching keywords: {string.Join(", ", keywords)}");
        return models;
    }

    private static void SetupTreePrototypes(Terrain terrain, List<GameObject> treePrefabs)
    {
        if (treePrefabs.Count == 0)
        {
            Debug.LogWarning("No tree prefabs found in Pure Poly pack.");
            return;
        }

        var prototypes = new List<TreePrototype>();
        // Prefer Birch and main trees, limit to 4-6 for performance
        foreach (var prefab in treePrefabs.Take(6))
        {
            var tp = new TreePrototype
            {
                prefab = prefab,
                bendFactor = 0.3f
            };
            prototypes.Add(tp);
            Debug.Log($"Added TreePrototype: {prefab.name}");
        }

        terrain.terrainData.treePrototypes = prototypes.ToArray();
        Debug.Log($"Configured {prototypes.Count} TreePrototypes from Pure Poly Nature Pack.");
    }

    private static void SetupDetailPrototypes(Terrain terrain, List<GameObject> detailPrefabs)
    {
        if (detailPrefabs.Count == 0)
        {
            Debug.LogWarning("No detail prefabs found.");
            return;
        }

        var prototypes = new List<DetailPrototype>();
        foreach (var prefab in detailPrefabs.Take(8))
        {
            var dp = new DetailPrototype
            {
                prototype = prefab,
                renderMode = DetailRenderMode.VertexLit,
                healthyColor = new Color(0.8f, 1f, 0.7f),
                dryColor = new Color(0.6f, 0.7f, 0.4f),
                minWidth = 0.6f,
                maxWidth = 1.8f,
                minHeight = 0.6f,
                maxHeight = 1.8f,
                noiseSpread = 0.4f,
                density = 1f
            };
            prototypes.Add(dp);
            Debug.Log($"Added DetailPrototype: {prefab.name}");
        }

        terrain.terrainData.detailPrototypes = prototypes.ToArray();
        terrain.terrainData.SetDetailResolution(512, 16); // Ensure resolution
        Debug.Log($"Configured {prototypes.Count} DetailPrototypes (grass, flowers, mushrooms, pebbles) from Pure Poly pack.");
    }

    private static void AddTreeInstances(Terrain terrain, System.Random rand, Vector3 center, float size)
    {
        var tData = terrain.terrainData;
        if (tData.treePrototypes.Length == 0) return;

        var treeInstances = new List<TreeInstance>();
        int targetTreeCount = 220; // Balanced density for low-poly performance

        for (int i = 0; i < targetTreeCount * 3; i++) // Oversample to account for overlap culling
        {
            float normX = (float)rand.NextDouble();
            float normZ = (float)rand.NextDouble();

            Vector3 worldPos = new Vector3(normX * size, 100f, normZ * size);
            float height = terrain.SampleHeight(worldPos);
            worldPos.y = height + 0.2f;

            // Overlap check in world space
            bool tooClose = false;
            foreach (var existing in treeInstances)
            {
                Vector3 existingWorld = new Vector3(existing.position.x * size, 0, existing.position.z * size);
                if (Vector3.Distance(existingWorld, new Vector3(worldPos.x, 0, worldPos.z)) < 6f)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) continue;

            int prototypeIndex = rand.Next(tData.treePrototypes.Length);

            var instance = new TreeInstance
            {
                position = new Vector3(normX, height / tData.size.y, normZ),
                prototypeIndex = prototypeIndex,
                widthScale = 0.7f + (float)rand.NextDouble() * 1.1f,
                heightScale = 0.8f + (float)rand.NextDouble() * 1.3f,
                color = Color.white,
                lightmapColor = Color.white
            };

            treeInstances.Add(instance);
            if (treeInstances.Count >= targetTreeCount) break;
        }

        tData.SetTreeInstances(treeInstances.ToArray(), true);
        Debug.Log($"Added {treeInstances.Count} TreeInstances using Pure Poly trees.");
    }

    private static void AddDetailLayers(Terrain terrain, System.Random rand)
    {
        var tData = terrain.terrainData;
        if (tData.detailPrototypes.Length == 0) return;

        int layerCount = tData.detailPrototypes.Length;
        int detailWidth = tData.detailWidth;
        int detailHeight = tData.detailHeight;

        // Clear all layers first
        for (int l = 0; l < layerCount; l++)
        {
            tData.SetDetailLayer(0, 0, l, new int[detailWidth, detailHeight]);
        }

        for (int layer = 0; layer < layerCount; layer++)
        {
            int[,] detailLayer = new int[detailWidth, detailHeight];
            // Vary density per layer type (higher for grass, lower for flowers/mushrooms)
            int baseDensity = 8 - (layer / 2); // denser base layers
            float densityChance = Mathf.Clamp01(0.12f + layer * 0.03f);

            for (int x = 0; x < detailWidth; x++)
            {
                for (int y = 0; y < detailHeight; y++)
                {
                    if (rand.NextDouble() < densityChance)
                    {
                        detailLayer[x, y] = rand.Next(1, baseDensity);
                    }
                }
            }
            tData.SetDetailLayer(0, 0, layer, detailLayer);
        }

        Debug.Log($"Added dense detail layers using Pure Poly grass/flower/mushroom/pebble prefabs. Total layers: {layerCount}");
    }

    private static void PlaceUniqueObjects(GameObject parent, List<GameObject> uniquePrefabs, System.Random rand, 
                                          Terrain terrain, Vector3 center, float size)
    {
        if (uniquePrefabs.Count == 0) 
        {
            Debug.LogWarning("No unique prefabs available from Pure Poly.");
            return;
        }

        int targetCount = 45;
        List<Vector3> placedPositions = new List<Vector3>();
        int placed = 0;

        for (int i = 0; i < targetCount * 2 && placed < targetCount; i++)
        {
            var prefab = uniquePrefabs[rand.Next(uniquePrefabs.Count)];
            Vector3 pos = center + new Vector3(
                (float)(rand.NextDouble() - 0.5) * size * 0.9f,
                100f,
                (float)(rand.NextDouble() - 0.5) * size * 0.9f
            );

            float height = terrain.SampleHeight(pos);
            pos.y = height + 0.1f;

            // Avoid close placement
            if (placedPositions.Any(p => Vector3.Distance(p, pos) < 9f)) continue;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
            instance.transform.position = pos;
            instance.transform.rotation = Quaternion.Euler(0, rand.Next(0, 360), 0);

            // Type-specific adjustments for Pure Poly assets
            string pName = prefab.name.ToLowerInvariant();
            float scale = 1.0f;

            if (pName.Contains("bridge"))
            {
                scale = 1.2f;
                instance.transform.rotation = Quaternion.Euler(0, rand.Next(0, 360), 0);
            }
            else if (pName.Contains("fence"))
            {
                scale = 1.5f;
            }
            else if (pName.Contains("rock") || pName.Contains("moss") || pName.Contains("mountain"))
            {
                scale = 1.0f + (float)rand.NextDouble() * 2.0f;
                instance.transform.rotation = Quaternion.Euler(
                    rand.Next(-5, 6), rand.Next(0, 360), rand.Next(-8, 9));
            }
            else if (pName.Contains("meadow") || pName.Contains("floor") || pName.Contains("lake"))
            {
                // Ground tiles should sit flat
                scale = 2.5f;
                instance.transform.rotation = Quaternion.Euler(0, rand.Next(0, 360), 0);
                pos.y = height + 0.05f; // very close to ground
                instance.transform.position = pos;
            }
            else if (pName.Contains("tree"))
            {
                scale = 1.8f;
            }

            instance.transform.localScale = Vector3.one * scale;
            instance.name = $"PurePoly_{placed}_{prefab.name}";

            // Materials are usually correct in pack prefabs, but ensure URP compatibility
            FixMaterialsIfNeeded(instance);

            placedPositions.Add(pos);
            placed++;
        }

        Debug.Log($"Placed {placed} unique objects from Pure Poly Nature Pack (rocks, bridges, fences, tiles).");
    }

    private static void FixMaterialsIfNeeded(GameObject go)
    {
        if (go == null) return;

        // Prefer materials from the Pure Poly pack
        var standardMat = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Models/Pure Poly/Free Low Poly Nature Pack/Materials/PP_Standard_Material.mat");
        var groundMat = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Models/Pure Poly/Free Low Poly Nature Pack/Materials/PP_Ground.mat");

        if (standardMat == null)
        {
            standardMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Foliage.mat");
        }
        if (groundMat == null)
        {
            groundMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/GroundMaterial.mat");
        }

        foreach (var renderer in go.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer.sharedMaterial == null || renderer.sharedMaterial.name.Contains("Standard") || 
                renderer.sharedMaterial.shader.name.Contains("Legacy"))
            {
                string nameLower = (go.name + renderer.gameObject.name).ToLower();
                if (nameLower.Contains("grass") || nameLower.Contains("flower") || nameLower.Contains("mushroom") ||
                    nameLower.Contains("tree") || nameLower.Contains("bush"))
                {
                    renderer.sharedMaterial = standardMat;
                }
                else
                {
                    renderer.sharedMaterial = groundMat;
                }
            }
        }
    }

    [MenuItem("Tools/Clear Pure Poly Nature Forest")]
    public static void ClearPurePolyNatureForest()
    {
        ClearExistingForest();
        Debug.Log("🧹 Pure Poly Nature Forest objects cleared.");
    }
}
