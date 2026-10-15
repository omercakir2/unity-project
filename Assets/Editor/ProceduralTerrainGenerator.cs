using UnityEngine;
using UnityEditor;

public class ProceduralTerrainGenerator
{
    [MenuItem("Tools/Generate Low Poly Forest Terrain (Free Pack)")]
    public static void GenerateLowPolyForestTerrain()
    {
        GameObject terrainObj = GameObject.Find("Terrain");
        if (terrainObj == null)
        {
            terrainObj = Terrain.CreateTerrainGameObject(null);
            terrainObj.name = "Terrain";
            Debug.Log("Created new Terrain GameObject for the forest.");
        }

        Terrain terrain = terrainObj.GetComponent<Terrain>();
        TerrainData tData = terrain.terrainData;

        if (tData == null)
        {
            tData = new TerrainData();
            AssetDatabase.CreateAsset(tData, "Assets/New Forest Terrain.asset");
            terrain.terrainData = tData;
        }

        // 250x250x250 as explicitly requested - matches Free Low Poly Nature Forest pack's 250x250 terrain map
        tData.size = new Vector3(250f, 250f, 250f);
        tData.heightmapResolution = 257;
        tData.baseMapResolution = 512;
        tData.SetDetailResolution(512, 16);

        GenerateNaturalHeightmap(tData);

        // Simple material setup using existing GroundMaterial for meadow/lake/rock look from the pack
        if (terrain.materialTemplate == null)
        {
            Material groundMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/GroundMaterial.mat");
            if (groundMat != null)
                terrain.materialTemplate = groundMat;
        }

        terrain.Flush();

        // Position main objects for good view of the new forest
        GameObject player = GameObject.Find("Player");
        if (player != null) player.transform.position = new Vector3(65f, 55f, 55f);

        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.transform.position = new Vector3(20f, 85f, 25f);
            mainCam.transform.LookAt(new Vector3(130f, 35f, 130f));
        }

        Debug.Log("✅ Fully rewritten: 250x250x250 low-poly forest terrain generated for **Free Low Poly Nature Forest** pack.\n" +
                  "• Heightmap with natural meadows, hills, ridges and mountain areas (v3.0 style).\n" +
                  "• Ready for the pack's Terrain Tiles (Meadow, Lake), Trees (Birch, Fir), Flowers, Grass, Rocks, Mushrooms, Pebbles, Bridges and Fences.\n" +
                  "Next step: Run Tools > Populate with Free Low Poly Nature Forest.");

        EditorUtility.SetDirty(tData);
        AssetDatabase.SaveAssets();
    }

    private static void GenerateNaturalHeightmap(TerrainData tData)
    {
        int resolution = tData.heightmapResolution;
        float[,] heights = new float[resolution, resolution];

        for (int x = 0; x < resolution; x++)
        {
            for (int z = 0; z < resolution; z++)
            {
                float worldX = x * 0.016f;
                float worldZ = z * 0.016f;

                float baseHeight = Mathf.PerlinNoise(worldX * 0.8f, worldZ * 0.8f) * 0.5f;
                float detail1 = Mathf.PerlinNoise(worldX * 3.5f, worldZ * 3.5f) * 0.25f;
                float detail2 = Mathf.PerlinNoise(worldX * 7.2f, worldZ * 7.2f) * 0.15f;
                float ridge = Mathf.Pow(Mathf.PerlinNoise(worldX * 1.1f, worldZ * 1.1f), 2.5f) * 0.2f;

                float finalHeight = baseHeight + detail1 + detail2 + ridge;
                finalHeight = Mathf.Clamp01(finalHeight * 0.75f);
                finalHeight = Mathf.Pow(finalHeight, 1.75f); // Creates flatter forest floor with dramatic peaks

                heights[x, z] = finalHeight;
            }
        }

        tData.SetHeights(0, 0, heights);
        Debug.Log("Natural heightmap (250x250) written with varied forest-friendly topography.");
    }
}
