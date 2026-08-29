using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Unity.Pipeline.Commands;

public static class Milestone1SceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Milestone1.unity";
    private const string SpritePath = "Assets/Generated/Milestone1Square.png";

    [CliCommand("build_milestone1", "Create the Milestone 1 2D prototype scene.")]
    public static string Build()
    {
        int groundLayer = EnsureLayer("Ground");
        Sprite squareSprite = EnsureSquareSprite();
        InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Settings/InputSystem_Actions.inputactions");

        if (inputActions == null)
        {
            throw new FileNotFoundException("Input System actions asset was not found.", "Assets/Settings/InputSystem_Actions.inputactions");
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject globalLightObject = new GameObject("Global Light 2D");
        Light2D globalLight = globalLightObject.AddComponent<Light2D>();
        globalLight.lightType = Light2D.LightType.Global;
        globalLight.intensity = 1f;

        GameObject background = CreateSprite("Background", new Vector3(0f, 2f, 5f), new Vector3(24f, 14f, 1f), new Color(0.025f, 0.04f, 0.09f), squareSprite, 0, false);
        background.GetComponent<SpriteRenderer>().sortingOrder = -10;

        GameObject ground = CreatePlatform("Ground", new Vector3(0f, -2.5f, 0f), new Vector3(20f, 1f, 1f), new Color(0.12f, 0.16f, 0.28f), squareSprite, groundLayer);
        ground.GetComponent<SpriteRenderer>().sortingOrder = 0;

        CreatePlatform("Platform_Left", new Vector3(-4.5f, -0.5f, 0f), new Vector3(3.5f, 0.45f, 1f), new Color(0.3f, 0.2f, 0.5f), squareSprite, groundLayer);
        CreatePlatform("Platform_Middle", new Vector3(0f, 1.1f, 0f), new Vector3(3.5f, 0.45f, 1f), new Color(0.3f, 0.2f, 0.5f), squareSprite, groundLayer);
        CreatePlatform("Platform_Right", new Vector3(4.5f, 2.7f, 0f), new Vector3(3.5f, 0.45f, 1f), new Color(0.3f, 0.2f, 0.5f), squareSprite, groundLayer);

        GameObject player = CreateSprite("Player", new Vector3(-7f, -1f, 0f), Vector3.one, new Color(0.15f, 0.85f, 1f), squareSprite, 0, true);
        Rigidbody2D body = player.AddComponent<Rigidbody2D>();
        body.gravityScale = 3f;
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        BoxCollider2D playerCollider = player.AddComponent<BoxCollider2D>();
        playerCollider.size = new Vector2(0.78f, 0.92f);

        GameObject groundCheckObject = new GameObject("GroundCheck");
        groundCheckObject.transform.SetParent(player.transform);
        groundCheckObject.transform.localPosition = new Vector3(0f, -0.5f, 0f);

        PlayerController controller = player.AddComponent<PlayerController>();
        controller.Configure(inputActions, groundCheckObject.transform, 1 << groundLayer);

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.backgroundColor = new Color(0.015f, 0.02f, 0.06f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        cameraObject.AddComponent<AudioListener>();
        CameraFollow2D follow = cameraObject.AddComponent<CameraFollow2D>();
        follow.Configure(player.transform);

        CharacterPrototypeBuilder.Build();
        EditorSceneManager.SaveScene(scene, ScenePath);
        SetSceneInBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return $"Built {ScenePath} with Player, ground, 3 platforms, Input System actions, and Camera Follow.";
    }

    private static GameObject CreatePlatform(string name, Vector3 position, Vector3 scale, Color color, Sprite sprite, int layer)
    {
        GameObject platform = CreateSprite(name, position, scale, color, sprite, layer, false);
        platform.AddComponent<BoxCollider2D>();
        return platform;
    }

    private static GameObject CreateSprite(string name, Vector3 position, Vector3 scale, Color color, Sprite sprite, int layer, bool addSortingLayer)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.position = position;
        gameObject.transform.localScale = scale;
        gameObject.layer = layer;

        SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        if (addSortingLayer)
        {
            renderer.sortingOrder = 10;
        }

        return gameObject;
    }

    private static Sprite EnsureSquareSprite()
    {
        string absoluteDirectory = Path.Combine(Application.dataPath, "Generated");
        Directory.CreateDirectory(absoluteDirectory);

        string absolutePath = Path.Combine(Application.dataPath, "Generated", "Milestone1Square.png");
        if (!File.Exists(absolutePath))
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 1f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
    }

    private static int EnsureLayer(string layerName)
    {
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");

        for (int index = 0; index < layers.arraySize; index++)
        {
            if (layers.GetArrayElementAtIndex(index).stringValue == layerName)
            {
                return index;
            }
        }

        for (int index = 8; index < layers.arraySize; index++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(index);
            if (string.IsNullOrEmpty(layer.stringValue))
            {
                layer.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                return index;
            }
        }

        throw new System.InvalidOperationException($"No free Unity layer was available for {layerName}.");
    }

    private static void SetSceneInBuildSettings(string scenePath)
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(scenePath, true)
        };
    }
}
