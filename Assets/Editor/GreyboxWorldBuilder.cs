using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering.Universal;
using Unity.Pipeline.Commands;

public static class GreyboxWorldBuilder
{
    private const string ScenePath = "Assets/Scenes/M3_Greybox.unity";
    private const string BaselineScenePath = "Assets/Scenes/Milestone1.unity";
    private const string GeneratedDirectory = "Assets/Generated/M3Greybox";
    private const string TileTexturePath = GeneratedDirectory + "/GreyboxTiles.png";
    private const string GroundTilePath = GeneratedDirectory + "/GroundTile.asset";
    private const string OneWayTilePath = GeneratedDirectory + "/OneWayPlatformTile.asset";
    private const string BackgroundTilePath = GeneratedDirectory + "/BackgroundTile.asset";
    private const string InputActionsPath = "Assets/Settings/InputSystem_Actions.inputactions";

    private static readonly Vector3 PlayerSpawn = new(-7f, -3.5f, 0f);

    [CliCommand("build_m3_greybox_world", "Build or safely refresh the M3 greybox world scene.")]
    public static string Build()
    {
        RequireBaselineDependencies();
        bool sceneWasCreated = !File.Exists(ToAbsolutePath(ScenePath));
        if (sceneWasCreated)
        {
            if (!AssetDatabase.CopyAsset(BaselineScenePath, ScenePath))
            {
                throw new InvalidOperationException($"Could not create {ScenePath} from {BaselineScenePath}.");
            }

            AssetDatabase.Refresh();
        }

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject player = GameObject.Find("Player");
        GameObject cameraObject = GameObject.Find("Main Camera");
        if (player == null || cameraObject == null)
        {
            throw new InvalidOperationException("The M3 scene must contain the baseline Player and Main Camera.");
        }

        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer < 0)
        {
            throw new InvalidOperationException("Required Ground layer was not found.");
        }

        TileAssets tiles = EnsureTileAssets();
        WorldObjects world = EnsureWorldHierarchy(groundLayer, tiles);
        BuildGeometry(world, tiles);
        EnsurePlayerSpawn(world.spawnPoints.transform, player, sceneWasCreated);
        EnsureEnemies(world.enemies.transform, tiles.squareSprite, sceneWasCreated);
        ConfigureCamera(cameraObject, player.transform, world.rooms.transform);
        AddSceneToBuildSettings();

        ValidateScene(scene, player, cameraObject, world);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return $"Built {ScenePath}; scene {(sceneWasCreated ? "created" : "reused")}, geometry and references validated.";
    }

    private static void RequireBaselineDependencies()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BaselineScenePath) == null)
        {
            throw new FileNotFoundException("Baseline scene was not found.", BaselineScenePath);
        }

        if (AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath) == null)
        {
            throw new FileNotFoundException("Input System actions asset was not found.", InputActionsPath);
        }
    }

    private static WorldObjects EnsureWorldHierarchy(int groundLayer, TileAssets tiles)
    {
        GameObject world = EnsureRoot("World");
        GameObject gridObject = EnsureChild(world.transform, "Grid");
        Grid grid = gridObject.GetComponent<Grid>();
        if (grid == null)
        {
            grid = gridObject.AddComponent<Grid>();
        }

        if (grid == null)
        {
            throw new InvalidOperationException("Could not attach the required Grid component to World/Grid.");
        }

        grid.cellSize = Vector3.one;
        grid.cellLayout = GridLayout.CellLayout.Rectangle;
        gridObject.transform.localPosition = Vector3.zero;
        gridObject.transform.localRotation = Quaternion.identity;
        gridObject.transform.localScale = Vector3.one;

        GameObject backgroundObject = EnsureChild(gridObject.transform, "Background");
        GameObject groundObject = EnsureChild(gridObject.transform, "Ground");
        GameObject oneWayObject = EnsureChild(gridObject.transform, "OneWayPlatform");
        Tilemap background = EnsureTilemap(backgroundObject, 0);
        Tilemap ground = EnsureTilemap(groundObject, groundLayer);
        Tilemap oneWay = EnsureTilemap(oneWayObject, groundLayer);
        EnsureGroundCollision(groundObject, false);
        EnsureGroundCollision(oneWayObject, true);
        SetRenderer(backgroundObject, -20);
        SetRenderer(groundObject, 0);
        SetRenderer(oneWayObject, 1);

        GameObject rooms = EnsureChild(world.transform, "Rooms");
        GameObject spawnPoints = EnsureChild(world.transform, "SpawnPoints");
        GameObject enemies = EnsureChild(world.transform, "Enemies");
        EnsureChild(world.transform, "ManualOverrides");

        return new WorldObjects(world, gridObject, backgroundObject, groundObject, oneWayObject,
            background, ground, oneWay, rooms, spawnPoints, enemies);
    }

    private static Tilemap EnsureTilemap(GameObject gameObject, int layer)
    {
        gameObject.layer = layer;
        gameObject.transform.localPosition = Vector3.zero;
        gameObject.transform.localRotation = Quaternion.identity;
        gameObject.transform.localScale = Vector3.one;
        if (gameObject.GetComponent<TilemapRenderer>() == null)
        {
            gameObject.AddComponent<TilemapRenderer>();
        }
        return gameObject.GetComponent<Tilemap>() ?? gameObject.AddComponent<Tilemap>();
    }

    private static void EnsureGroundCollision(GameObject gameObject, bool oneWay)
    {
        TilemapCollider2D tilemapCollider = gameObject.GetComponent<TilemapCollider2D>();
        bool createdTilemapCollider = tilemapCollider == null;
        if (createdTilemapCollider)
        {
            tilemapCollider = gameObject.AddComponent<TilemapCollider2D>();
        }

        CompositeCollider2D composite = gameObject.GetComponent<CompositeCollider2D>();
        bool createdComposite = composite == null;
        if (createdComposite)
        {
            composite = gameObject.AddComponent<CompositeCollider2D>();
        }

        Rigidbody2D body = gameObject.GetComponent<Rigidbody2D>();
        bool createdBody = body == null;
        if (createdBody)
        {
            body = gameObject.AddComponent<Rigidbody2D>();
        }

        if (createdTilemapCollider)
        {
            tilemapCollider.compositeOperation = Collider2D.CompositeOperation.Merge;
        }

        if (createdComposite)
        {
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            composite.generationType = CompositeCollider2D.GenerationType.Synchronous;
        }

        body.bodyType = RigidbodyType2D.Static;
        body.simulated = true;

        if (oneWay)
        {
            PlatformEffector2D effector = gameObject.GetComponent<PlatformEffector2D>();
            bool createdEffector = effector == null;
            if (createdEffector)
            {
                effector = gameObject.AddComponent<PlatformEffector2D>();
            }

            if (createdEffector)
            {
                effector.useOneWay = true;
                effector.useOneWayGrouping = true;
                effector.surfaceArc = 180f;
            }

            if (createdComposite)
            {
                composite.usedByEffector = true;
            }
        }
    }

    private static void SetRenderer(GameObject gameObject, int sortingOrder)
    {
        TilemapRenderer renderer = gameObject.GetComponent<TilemapRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = sortingOrder;
        }
    }

    private static void BuildGeometry(WorldObjects world, TileAssets tiles)
    {
        PopulateIfEmpty(world.background, tilemap =>
        {
            FillRect(tilemap, -12, 44, -8, 32, tiles.backgroundTile);
        });

        PopulateIfEmpty(world.ground, tilemap =>
        {
            FillSurface(tilemap, -10, 9, -4, 1, tiles.groundTile);
            FillSurface(tilemap, 10, 29, -5, 1, tiles.groundTile);
            FillSurface(tilemap, 10, 29, 7, 1, tiles.groundTile);
            FillSurface(tilemap, 30, 41, 3, 1, tiles.groundTile);
            FillSurface(tilemap, 12, 29, 26, 1, tiles.groundTile);
            FillSurface(tilemap, 6, 9, 3, 1, tiles.groundTile);
            FillRect(tilemap, -10, -9, -4, 4, tiles.groundTile);
            FillRect(tilemap, 29, 29, -5, 5, tiles.groundTile);
            FillRect(tilemap, 41, 41, 3, 29, tiles.groundTile);
        });

        PopulateIfEmpty(world.oneWay, tilemap =>
        {
            FillSurface(tilemap, 22, 28, -3, 1, tiles.oneWayTile);
            FillSurface(tilemap, 25, 28, -1, 1, tiles.oneWayTile);
            FillSurface(tilemap, 22, 25, 1, 1, tiles.oneWayTile);
            FillSurface(tilemap, 19, 22, 3, 1, tiles.oneWayTile);
            FillSurface(tilemap, 17, 20, 9, 1, tiles.oneWayTile);
            FillSurface(tilemap, 22, 25, 11, 1, tiles.oneWayTile);
            FillSurface(tilemap, 17, 20, 13, 1, tiles.oneWayTile);
            FillSurface(tilemap, 22, 26, 15, 1, tiles.oneWayTile);
            FillSurface(tilemap, 27, 30, 15, 1, tiles.oneWayTile);
            FillSurface(tilemap, 32, 35, 5, 1, tiles.oneWayTile);
            FillSurface(tilemap, 37, 40, 7, 1, tiles.oneWayTile);
            FillSurface(tilemap, 32, 35, 9, 1, tiles.oneWayTile);
            FillSurface(tilemap, 37, 40, 11, 1, tiles.oneWayTile);
            FillSurface(tilemap, 32, 35, 13, 1, tiles.oneWayTile);
            FillSurface(tilemap, 37, 40, 15, 1, tiles.oneWayTile);
            FillSurface(tilemap, 32, 35, 17, 1, tiles.oneWayTile);
            FillSurface(tilemap, 37, 40, 19, 1, tiles.oneWayTile);
            FillSurface(tilemap, 32, 35, 21, 1, tiles.oneWayTile);
            FillSurface(tilemap, 37, 40, 23, 1, tiles.oneWayTile);
            FillSurface(tilemap, 32, 35, 25, 1, tiles.oneWayTile);
            FillSurface(tilemap, 37, 40, 27, 1, tiles.oneWayTile);
            FillSurface(tilemap, 26, 29, 28, 1, tiles.oneWayTile);
            FillSurface(tilemap, 20, 24, 24, 1, tiles.oneWayTile);
            FillSurface(tilemap, 15, 19, 22, 1, tiles.oneWayTile);
            FillSurface(tilemap, 12, 15, 20, 1, tiles.oneWayTile);
            FillSurface(tilemap, 8, 11, 17, 1, tiles.oneWayTile);
            FillSurface(tilemap, 8, 11, 13, 1, tiles.oneWayTile);
            FillSurface(tilemap, 8, 11, 9, 1, tiles.oneWayTile);
        });
    }

    private static void PopulateIfEmpty(Tilemap tilemap, Action<Tilemap> populate)
    {
        if (tilemap == null || tilemap.GetUsedTilesCount() > 0)
        {
            return;
        }

        populate(tilemap);
    }

    private static void FillSurface(Tilemap tilemap, int xMin, int xMax, int topY, int depth, TileBase tile)
    {
        FillRect(tilemap, xMin, xMax, topY - depth, topY - 1, tile);
    }

    private static void FillRect(Tilemap tilemap, int xMin, int xMax, int yMin, int yMax, TileBase tile)
    {
        for (int x = xMin; x <= xMax; x++)
        {
            for (int y = yMin; y <= yMax; y++)
            {
                tilemap.SetTile(new Vector3Int(x, y, 0), tile);
            }
        }
    }

    private static void EnsurePlayerSpawn(Transform spawnRoot, GameObject player, bool sceneWasCreated)
    {
        Transform spawn = spawnRoot.Find("PlayerSpawn");
        if (spawn == null)
        {
            GameObject spawnObject = new("PlayerSpawn");
            spawn = spawnObject.transform;
            spawn.SetParent(spawnRoot, false);
            spawn.position = PlayerSpawn;
        }

        if (sceneWasCreated)
        {
            player.transform.position = spawn.position;
        }
    }

    private static void EnsureEnemies(Transform enemyRoot, Sprite squareSprite, bool sceneWasCreated)
    {
        GameObject existing = GameObject.Find("EnemyDummy");
        if (existing != null && existing.transform.parent == null)
        {
            if (enemyRoot.Find("EnemyDummy_R02_A") != null)
            {
                throw new InvalidOperationException("M3 contains both a baseline EnemyDummy root and an EnemyDummy_R02_A; resolve the conflict before rerunning.");
            }

            existing.name = "EnemyDummy_R02_A";
            existing.transform.SetParent(enemyRoot, true);
            existing.transform.position = new Vector3(17f, -4.5f, 0f);
        }

        EnsureEnemy(enemyRoot, "EnemyDummy_R02_A", new Vector3(17f, -4.5f, 0f), squareSprite, sceneWasCreated);
        EnsureEnemy(enemyRoot, "EnemyDummy_R02_B", new Vector3(25f, -4.5f, 0f), squareSprite, sceneWasCreated);
        EnsureEnemy(enemyRoot, "EnemyDummy_R03", new Vector3(24f, 14f, 0f), squareSprite, sceneWasCreated);
        EnsureEnemy(enemyRoot, "EnemyDummy_R05", new Vector3(18f, 25f, 0f), squareSprite, sceneWasCreated);
    }

    private static void EnsureEnemy(Transform enemyRoot, string name, Vector3 position, Sprite squareSprite, bool sceneWasCreated)
    {
        Transform existing = enemyRoot.Find(name);
        if (existing == null)
        {
            GameObject enemy = new(name);
            enemy.transform.SetParent(enemyRoot, false);
            enemy.transform.position = position;
            enemy.layer = 0;
            SpriteRenderer renderer = enemy.AddComponent<SpriteRenderer>();
            renderer.sprite = squareSprite;
            renderer.color = new Color(0.95f, 0.25f, 0.25f, 1f);
            renderer.sortingOrder = 9;
            BoxCollider2D collider = enemy.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            collider.isTrigger = false;
            EnemyDummy dummy = enemy.AddComponent<EnemyDummy>();
            dummy.Configure(3);
            return;
        }

        if (sceneWasCreated && name == "EnemyDummy_R02_A")
        {
            existing.position = position;
        }
    }

    private static void ConfigureCamera(GameObject cameraObject, Transform player, Transform roomsRoot)
    {
        Camera camera = cameraObject.GetComponent<Camera>();
        if (camera == null)
        {
            throw new InvalidOperationException("Main Camera is missing its Camera component.");
        }

        CameraBounds2D bounds = cameraObject.GetComponent<CameraBounds2D>() ?? cameraObject.AddComponent<CameraBounds2D>();
        CameraFollow2D follow = cameraObject.GetComponent<CameraFollow2D>();
        if (follow == null)
        {
            throw new InvalidOperationException("Main Camera is missing CameraFollow2D.");
        }

        List<CameraRoomZone2D> zones = new();
        zones.Add(EnsureRoomZone(roomsRoot, "R01_SpawnMovement", new Vector3(0f, 0f, 0f), new Vector2(20f, 10f), 0));
        zones.Add(EnsureRoomZone(roomsRoot, "R02_Combat", new Vector3(20f, 0f, 0f), new Vector2(20f, 12f), 0));
        zones.Add(EnsureRoomZone(roomsRoot, "R03_Platforming", new Vector3(20f, 13f, 0f), new Vector2(20f, 14f), 0));
        zones.Add(EnsureRoomZone(roomsRoot, "R04_Vertical", new Vector3(36f, 16f, 0f), new Vector2(18f, 28f), 0));
        zones.Add(EnsureRoomZone(roomsRoot, "R05_ReturnShortcut", new Vector3(19f, 25f, 0f), new Vector2(22f, 10f), 0));
        zones.Add(EnsureRoomZone(roomsRoot, "R05_ReturnShaft", new Vector3(10f, 12.5f, 0f), new Vector2(18f, 15f), 1));

        bounds.Configure(player, zones);
        follow.ConfigureBounds(bounds);
    }

    private static CameraRoomZone2D EnsureRoomZone(Transform roomsRoot, string name, Vector3 position, Vector2 size, int priority)
    {
        Transform existing = roomsRoot.Find(name);
        GameObject room = existing == null ? new GameObject(name) : existing.gameObject;
        if (existing == null)
        {
            room.transform.SetParent(roomsRoot, false);
            room.transform.position = position;
        }

        CameraRoomZone2D zone = room.GetComponent<CameraRoomZone2D>();
        bool created = zone == null;
        if (created)
        {
            zone = room.AddComponent<CameraRoomZone2D>();
        }

        if (created)
        {
            zone.Configure(size, priority);
        }

        return zone;
    }

    private static TileAssets EnsureTileAssets()
    {
        Directory.CreateDirectory(ToAbsolutePath(GeneratedDirectory));
        EnsureGreyboxTexture();
        Sprite[] sprites = LoadTileSprites();
        if (sprites.Length < 3)
        {
            throw new InvalidOperationException("GreyboxTiles.png did not import three sprites.");
        }

        Tile ground = EnsureTile(GroundTilePath, sprites[0], Tile.ColliderType.Grid);
        Tile oneWay = EnsureTile(OneWayTilePath, sprites[1], Tile.ColliderType.Grid);
        Tile background = EnsureTile(BackgroundTilePath, sprites[2], Tile.ColliderType.None);
        Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Generated/Milestone1Square.png");
        if (square == null)
        {
            throw new FileNotFoundException("Baseline square sprite was not found.", "Assets/Generated/Milestone1Square.png");
        }

        return new TileAssets(ground, oneWay, background, square);
    }

    private static void EnsureGreyboxTexture()
    {
        string absolutePath = ToAbsolutePath(TileTexturePath);
        if (!File.Exists(absolutePath))
        {
            Texture2D texture = new(48, 16, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[48 * 16];
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 48; x++)
                {
                    int tileIndex = x / 16;
                    pixels[y * 48 + x] = tileIndex switch
                    {
                        0 => new Color(0.14f, 0.18f, 0.32f, 1f),
                        1 => new Color(0.55f, 0.24f, 0.36f, 1f),
                        _ => new Color(0.035f, 0.055f, 0.12f, 1f)
                    };
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        AssetDatabase.ImportAsset(TileTexturePath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(TileTexturePath) as TextureImporter;
        if (importer == null)
        {
            throw new InvalidOperationException("Greybox tile texture importer was unavailable.");
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 16f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spritesheet = new[]
        {
            MakeSpriteMetaData("GroundTile", 0),
            MakeSpriteMetaData("OneWayPlatformTile", 16),
            MakeSpriteMetaData("BackgroundTile", 32)
        };
        importer.SaveAndReimport();
    }

    private static SpriteMetaData MakeSpriteMetaData(string name, int x)
    {
        return new SpriteMetaData
        {
            name = name,
            rect = new Rect(x, 0, 16, 16),
            alignment = (int)SpriteAlignment.Center,
            pivot = new Vector2(0.5f, 0.5f)
        };
    }

    private static Sprite[] LoadTileSprites()
    {
        List<Sprite> sprites = new();
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(TileTexturePath))
        {
            if (asset is Sprite sprite)
            {
                sprites.Add(sprite);
            }
        }

        sprites.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
        sprites.Sort((left, right) => TileSpriteIndex(left.name).CompareTo(TileSpriteIndex(right.name)));
        return sprites.ToArray();
    }

    private static int TileSpriteIndex(string name)
    {
        if (name == "GroundTile") return 0;
        if (name == "OneWayPlatformTile") return 1;
        return 2;
    }

    private static Tile EnsureTile(string path, Sprite sprite, Tile.ColliderType colliderType)
    {
        Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (tile != null)
        {
            return tile;
        }

        tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        tile.colliderType = colliderType;
        tile.color = Color.white;
        AssetDatabase.CreateAsset(tile, path);
        return tile;
    }

    private static GameObject EnsureRoot(string name)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null && existing.transform.parent == null)
        {
            return existing;
        }

        return new GameObject(name);
    }

    private static GameObject EnsureChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            return existing.gameObject;
        }

        GameObject child = new(name);
        child.transform.SetParent(parent, false);
        return child;
    }

    private static void AddSceneToBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new(EditorBuildSettings.scenes);
        foreach (EditorBuildSettingsScene scene in scenes)
        {
            if (scene.path == ScenePath)
            {
                return;
            }
        }

        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void ValidateScene(Scene scene, GameObject player, GameObject cameraObject, WorldObjects world)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            throw new InvalidOperationException("M3 scene is not loaded after save.");
        }

        if (world.ground.GetUsedTilesCount() == 0 || world.oneWay.GetUsedTilesCount() == 0)
        {
            throw new InvalidOperationException("M3 Tilemaps contain no geometry.");
        }

        if (world.ground.GetComponent<TilemapCollider2D>() == null || world.ground.GetComponent<CompositeCollider2D>() == null ||
            world.ground.GetComponent<Rigidbody2D>()?.bodyType != RigidbodyType2D.Static)
        {
            throw new InvalidOperationException("Ground collision setup is incomplete.");
        }

        if (world.oneWay.GetComponent<TilemapCollider2D>() == null || world.oneWay.GetComponent<CompositeCollider2D>() == null ||
            world.oneWay.GetComponent<PlatformEffector2D>() == null || world.oneWay.GetComponent<Rigidbody2D>()?.bodyType != RigidbodyType2D.Static)
        {
            throw new InvalidOperationException("OneWayPlatform collision setup is incomplete.");
        }

        Camera camera = cameraObject.GetComponent<Camera>();
        CameraFollow2D follow = cameraObject.GetComponent<CameraFollow2D>();
        CameraBounds2D bounds = cameraObject.GetComponent<CameraBounds2D>();
        if (camera == null || follow == null || bounds == null || player.GetComponent<PlayerController>() == null)
        {
            throw new InvalidOperationException("Player or camera references are incomplete.");
        }

        if (world.enemies.transform.childCount < 4)
        {
            throw new InvalidOperationException("Expected four deterministic enemy placements.");
        }
    }

    private static string ToAbsolutePath(string assetPath)
    {
        return Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }

    private sealed class WorldObjects
    {
        public readonly GameObject world;
        public readonly GameObject gridObject;
        public readonly GameObject backgroundObject;
        public readonly GameObject groundObject;
        public readonly GameObject oneWayObject;
        public readonly Tilemap background;
        public readonly Tilemap ground;
        public readonly Tilemap oneWay;
        public readonly GameObject rooms;
        public readonly GameObject spawnPoints;
        public readonly GameObject enemies;

        public WorldObjects(GameObject world, GameObject gridObject, GameObject backgroundObject, GameObject groundObject,
            GameObject oneWayObject, Tilemap background, Tilemap ground, Tilemap oneWay, GameObject rooms,
            GameObject spawnPoints, GameObject enemies)
        {
            this.world = world;
            this.gridObject = gridObject;
            this.backgroundObject = backgroundObject;
            this.groundObject = groundObject;
            this.oneWayObject = oneWayObject;
            this.background = background;
            this.ground = ground;
            this.oneWay = oneWay;
            this.rooms = rooms;
            this.spawnPoints = spawnPoints;
            this.enemies = enemies;
        }
    }

    private readonly struct TileAssets
    {
        public readonly Tile groundTile;
        public readonly Tile oneWayTile;
        public readonly Tile backgroundTile;
        public readonly Sprite squareSprite;

        public TileAssets(Tile groundTile, Tile oneWayTile, Tile backgroundTile, Sprite squareSprite)
        {
            this.groundTile = groundTile;
            this.oneWayTile = oneWayTile;
            this.backgroundTile = backgroundTile;
            this.squareSprite = squareSprite;
        }
    }
}
