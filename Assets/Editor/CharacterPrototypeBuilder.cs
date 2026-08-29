using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Pipeline.Commands;

public static class CharacterPrototypeBuilder
{
    private const string GeneratedDirectory = "Assets/Generated/CharacterPrototype";
    private const string SpriteDirectory = GeneratedDirectory + "/Sprites";
    private const string ControllerPath = GeneratedDirectory + "/PlayerCharacter.controller";
    private const int TextureWidth = 128;
    private const int TextureHeight = 192;
    private const float PixelsPerUnit = 96f;

    private static readonly Color Hair = new(0.025f, 0.018f, 0.035f, 1f);
    private static readonly Color HairHighlight = new(0.12f, 0.08f, 0.14f, 1f);
    private static readonly Color Red = new(0.78f, 0.035f, 0.08f, 1f);
    private static readonly Color RedHighlight = new(1f, 0.16f, 0.18f, 1f);
    private static readonly Color BlackCloth = new(0.055f, 0.045f, 0.09f, 1f);
    private static readonly Color Skin = new(1f, 0.68f, 0.54f, 1f);
    private static readonly Color SkinShadow = new(0.72f, 0.31f, 0.28f, 1f);
    private static readonly Color Gold = new(1f, 0.68f, 0.12f, 1f);
    private static readonly Color GoldShadow = new(0.55f, 0.25f, 0.04f, 1f);
    private static readonly Color Mask = new(0.96f, 0.88f, 0.72f, 1f);

    [CliCommand("build_character_prototype", "Build the original 2D heroine visual and Animator for the active Player.")]
    public static string Build()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            throw new InvalidOperationException("Player GameObject was not found in the active scene.");
        }

        EnsureDirectory(SpriteDirectory);
        Sprite idleA = CreateSprite("Idle_A", 0, 0, 0);
        Sprite idleB = CreateSprite("Idle_B", 1, 0, 0);
        Sprite runA = CreateSprite("Run_A", 0, -5, 0);
        Sprite runB = CreateSprite("Run_B", 0, 5, 0);
        Sprite jump = CreateSprite("Jump", 0, 0, 5);
        Sprite fall = CreateSprite("Fall", 0, 0, -3);
        Sprite facingArrow = CreateFacingArrowSprite();
        AnimatorController animatorController = CreateAnimatorController(idleA, idleB, runA, runB, jump, fall);

        RemoveChild(player.transform, "CharacterVisual");
        RemoveChild(player.transform, "FacingMarker");

        SpriteRenderer rootRenderer = player.GetComponent<SpriteRenderer>();
        if (rootRenderer != null)
        {
            UnityEngine.Object.DestroyImmediate(rootRenderer, true);
        }

        GameObject visualObject = new GameObject("CharacterVisual");
        visualObject.transform.SetParent(player.transform, false);
        visualObject.transform.localPosition = new Vector3(0f, 0.4f, 0f);

        SpriteRenderer visualRenderer = visualObject.AddComponent<SpriteRenderer>();
        visualRenderer.sprite = idleA;
        visualRenderer.sortingOrder = 10;
        visualRenderer.maskInteraction = SpriteMaskInteraction.None;

        Animator animator = player.GetComponent<Animator>();
        if (animator == null)
        {
            animator = player.AddComponent<Animator>();
        }
        animator.runtimeAnimatorController = animatorController;

        GameObject facingObject = new GameObject("FacingMarker");
        facingObject.transform.SetParent(player.transform, false);
        facingObject.transform.localPosition = new Vector3(0.66f, 0.05f, 0f);
        SpriteRenderer facingRenderer = facingObject.AddComponent<SpriteRenderer>();
        facingRenderer.sprite = facingArrow;
        facingRenderer.color = RedHighlight;
        facingRenderer.sortingOrder = 20;

        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller == null)
        {
            throw new InvalidOperationException("PlayerController is missing from Player.");
        }
        controller.ConfigureVisualRoot(visualObject.transform);
        controller.ConfigureFacingMarker(facingObject.transform);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return "Built original heroine placeholder with twin tails, horns, fox mask, bell, facing marker, and Idle/Run/Jump/Fall Animator states.";
    }

    [CliCommand("fix_player_animator_transitions", "Fix Bool Grounded conditions in the PlayerCharacter Animator Controller.")]
    public static string FixAnimatorTransitions()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            throw new InvalidOperationException("PlayerCharacter Animator Controller was not found.");
        }

        AnimatorControllerParameter groundedParameter = Array.Find(controller.parameters, parameter => parameter.name == "Grounded");
        if (groundedParameter == null || groundedParameter.type != AnimatorControllerParameterType.Bool)
        {
            throw new InvalidOperationException("PlayerCharacter Grounded parameter must be Bool.");
        }

        int fixedConditionCount = 0;
        foreach (ChildAnimatorState childState in controller.layers[0].stateMachine.states)
        {
            foreach (AnimatorStateTransition transition in childState.state.transitions)
            {
                AnimatorCondition[] conditions = transition.conditions;
                for (int index = 0; index < conditions.Length; index++)
                {
                    AnimatorCondition condition = conditions[index];
                    if (condition.parameter != "Grounded" || condition.mode == AnimatorConditionMode.If || condition.mode == AnimatorConditionMode.IfNot)
                    {
                        continue;
                    }

                    conditions[index] = new AnimatorCondition
                    {
                        mode = AnimatorConditionMode.IfNot,
                        parameter = "Grounded",
                        threshold = 0f
                    };
                    fixedConditionCount++;
                }
                transition.conditions = conditions;
            }
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return $"Verified Grounded is Bool and corrected {fixedConditionCount} incompatible condition(s) to If Not.";
    }

    private static void RemoveChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
        {
            UnityEngine.Object.DestroyImmediate(child.gameObject, true);
        }
    }

    private static Sprite CreateSprite(string name, int bob, int stride, int airborne)
    {
        string assetPath = SpriteDirectory + "/" + name + ".png";
        string absolutePath = Path.Combine(Application.dataPath, "Generated", "CharacterPrototype", "Sprites", name + ".png");
        Texture2D texture = new(TextureWidth, TextureHeight, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        Clear(texture);
        DrawCharacter(texture, bob, stride, airborne);
        File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    private static Sprite CreateFacingArrowSprite()
    {
        string assetPath = SpriteDirectory + "/FacingArrow.png";
        string absolutePath = Path.Combine(Application.dataPath, "Generated", "CharacterPrototype", "Sprites", "FacingArrow.png");
        Texture2D texture = new(32, 24, TextureFormat.RGBA32, false);
        Clear(texture);
        FillPolygon(texture, new[] { new Vector2Int(3, 12), new Vector2Int(22, 12), new Vector2Int(16, 19), new Vector2Int(29, 12), new Vector2Int(16, 5), new Vector2Int(22, 12) }, RedHighlight);
        FillRect(texture, 3, 9, 21, 15, RedHighlight);
        File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 96f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    private static AnimatorController CreateAnimatorController(Sprite idleA, Sprite idleB, Sprite runA, Sprite runB, Sprite jump, Sprite fall)
    {
        DeleteAssetIfPresent(ControllerPath);
        string[] clipPaths =
        {
            GeneratedDirectory + "/Idle.anim",
            GeneratedDirectory + "/Run.anim",
            GeneratedDirectory + "/Jump.anim",
            GeneratedDirectory + "/Fall.anim"
        };
        foreach (string clipPath in clipPaths)
        {
            DeleteAssetIfPresent(clipPath);
        }

        AnimationClip idleClip = CreateClip("Idle", new[] { idleA, idleB }, 0.36f, true);
        AnimationClip runClip = CreateClip("Run", new[] { runA, runB }, 0.12f, true);
        AnimationClip jumpClip = CreateClip("Jump", new[] { jump }, 0.1f, false);
        AnimationClip fallClip = CreateClip("Fall", new[] { fall }, 0.1f, false);
        AssetDatabase.CreateAsset(idleClip, clipPaths[0]);
        AssetDatabase.CreateAsset(runClip, clipPaths[1]);
        AssetDatabase.CreateAsset(jumpClip, clipPaths[2]);
        AssetDatabase.CreateAsset(fallClip, clipPaths[3]);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("VerticalVelocity", AnimatorControllerParameterType.Float);
        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState idleState = stateMachine.AddState("Idle");
        AnimatorState runState = stateMachine.AddState("Run");
        AnimatorState jumpState = stateMachine.AddState("Jump");
        AnimatorState fallState = stateMachine.AddState("Fall");
        idleState.motion = idleClip;
        runState.motion = runClip;
        jumpState.motion = jumpClip;
        fallState.motion = fallClip;
        stateMachine.defaultState = idleState;

        AddTransition(idleState, runState, false, new AnimatorCondition { mode = AnimatorConditionMode.Greater, parameter = "Speed", threshold = 0.1f });
        AddTransition(idleState, jumpState, false, new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = "Grounded", threshold = 0f }, new AnimatorCondition { mode = AnimatorConditionMode.Greater, parameter = "VerticalVelocity", threshold = 0.1f });
        AddTransition(idleState, fallState, false, new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = "Grounded", threshold = 0f }, new AnimatorCondition { mode = AnimatorConditionMode.Less, parameter = "VerticalVelocity", threshold = 0.1f });
        AddTransition(runState, idleState, false, new AnimatorCondition { mode = AnimatorConditionMode.Less, parameter = "Speed", threshold = 0.1f });
        AddTransition(runState, jumpState, false, new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = "Grounded", threshold = 0f }, new AnimatorCondition { mode = AnimatorConditionMode.Greater, parameter = "VerticalVelocity", threshold = 0.1f });
        AddTransition(runState, fallState, false, new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = "Grounded", threshold = 0f }, new AnimatorCondition { mode = AnimatorConditionMode.Less, parameter = "VerticalVelocity", threshold = 0.1f });
        AddTransition(jumpState, fallState, false, new AnimatorCondition { mode = AnimatorConditionMode.Less, parameter = "VerticalVelocity", threshold = 0.1f });
        AddTransition(jumpState, idleState, false, new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = "Grounded", threshold = 0f }, new AnimatorCondition { mode = AnimatorConditionMode.Less, parameter = "Speed", threshold = 0.1f });
        AddTransition(jumpState, runState, false, new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = "Grounded", threshold = 0f }, new AnimatorCondition { mode = AnimatorConditionMode.Greater, parameter = "Speed", threshold = 0.1f });
        AddTransition(fallState, idleState, false, new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = "Grounded", threshold = 0f }, new AnimatorCondition { mode = AnimatorConditionMode.Less, parameter = "Speed", threshold = 0.1f });
        AddTransition(fallState, runState, false, new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = "Grounded", threshold = 0f }, new AnimatorCondition { mode = AnimatorConditionMode.Greater, parameter = "Speed", threshold = 0.1f });
        return controller;
    }

    private static AnimationClip CreateClip(string name, Sprite[] sprites, float frameDuration, bool looping)
    {
        AnimationClip clip = new() { name = name, frameRate = 12f };
        ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[sprites.Length + (looping ? 1 : 0)];
        for (int index = 0; index < sprites.Length; index++)
        {
            keys[index] = new ObjectReferenceKeyframe { time = index * frameDuration, value = sprites[index] };
        }
        if (looping)
        {
            keys[^1] = new ObjectReferenceKeyframe { time = sprites.Length * frameDuration, value = sprites[0] };
        }
        EditorCurveBinding binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = looping;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        return clip;
    }

    private static void AddTransition(AnimatorState from, AnimatorState to, bool hasExitTime, params AnimatorCondition[] conditions)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = hasExitTime;
        transition.duration = 0.05f;
        transition.conditions = conditions;
    }

    private static void DrawCharacter(Texture2D texture, int bob, int stride, int airborne)
    {
        int y = bob + airborne;
        FillEllipse(texture, 28, 133 + y, 18, 43, Hair);
        FillEllipse(texture, 100, 133 + y, 18, 43, Hair);
        FillEllipse(texture, 26, 133 + y, 10, 32, HairHighlight);
        FillEllipse(texture, 102, 133 + y, 10, 32, HairHighlight);
        FillPolygon(texture, new[] { new Vector2Int(42, 166 + y), new Vector2Int(52, 184 + y), new Vector2Int(60, 166 + y) }, Red);
        FillPolygon(texture, new[] { new Vector2Int(68, 166 + y), new Vector2Int(76, 184 + y), new Vector2Int(86, 166 + y) }, Red);
        FillEllipse(texture, 28, 159 + y, 6, 6, RedHighlight);
        FillEllipse(texture, 100, 159 + y, 6, 6, RedHighlight);

        FillPolygon(texture, new[] { new Vector2Int(47, 167 + y), new Vector2Int(52, 188 + y), new Vector2Int(60, 171 + y) }, Red);
        FillPolygon(texture, new[] { new Vector2Int(81, 167 + y), new Vector2Int(76, 188 + y), new Vector2Int(68, 171 + y) }, Red);
        FillEllipse(texture, 64, 145 + y, 29, 33, SkinShadow);
        FillEllipse(texture, 64, 148 + y, 27, 31, Skin);
        FillEllipse(texture, 64, 166 + y, 29, 20, Hair);
        FillPolygon(texture, new[] { new Vector2Int(39, 161 + y), new Vector2Int(48, 176 + y), new Vector2Int(55, 158 + y), new Vector2Int(64, 174 + y), new Vector2Int(73, 158 + y), new Vector2Int(81, 176 + y), new Vector2Int(89, 161 + y), new Vector2Int(84, 182 + y), new Vector2Int(43, 182 + y) }, Hair);
        FillEllipse(texture, 53, 148 + y, 4, 3, Red);
        FillEllipse(texture, 75, 148 + y, 4, 3, Red);
        FillEllipse(texture, 54, 148 + y, 1, 1, Mask);
        FillEllipse(texture, 76, 148 + y, 1, 1, Mask);
        FillRect(texture, 61, 135 + y, 6, 3, RedHighlight);
        FillPolygon(texture, new[] { new Vector2Int(43, 174 + y), new Vector2Int(51, 190 + y), new Vector2Int(58, 174 + y) }, Red);
        FillPolygon(texture, new[] { new Vector2Int(85, 174 + y), new Vector2Int(77, 190 + y), new Vector2Int(70, 174 + y) }, Red);

        FillRect(texture, 54, 115 + y, 20, 14, SkinShadow);
        FillRect(texture, 51, 112 + y, 26, 9, BlackCloth);
        FillPolygon(texture, new[] { new Vector2Int(36, 113 + y), new Vector2Int(92, 113 + y), new Vector2Int(101, 69 + y), new Vector2Int(27, 69 + y) }, BlackCloth);
        FillPolygon(texture, new[] { new Vector2Int(38, 115 + y), new Vector2Int(63, 113 + y), new Vector2Int(58, 72 + y), new Vector2Int(27, 72 + y) }, Red);
        FillPolygon(texture, new[] { new Vector2Int(90, 115 + y), new Vector2Int(65, 113 + y), new Vector2Int(70, 72 + y), new Vector2Int(101, 72 + y) }, Red);
        FillPolygon(texture, new[] { new Vector2Int(42, 112 + y), new Vector2Int(64, 119 + y), new Vector2Int(86, 112 + y), new Vector2Int(96, 59 + y), new Vector2Int(32, 59 + y) }, BlackCloth);
        FillPolygon(texture, new[] { new Vector2Int(48, 112 + y), new Vector2Int(64, 118 + y), new Vector2Int(80, 112 + y), new Vector2Int(75, 72 + y), new Vector2Int(53, 72 + y) }, Red);
        FillRect(texture, 43, 81 + y, 42, 7, GoldShadow);
        FillRect(texture, 45, 82 + y, 38, 4, Gold);
        FillEllipse(texture, 64, 116 + y, 7, 7, GoldShadow);
        FillEllipse(texture, 64, 116 + y, 5, 5, Gold);
        FillRect(texture, 62, 114 + y, 4, 5, BlackCloth);
        DrawLine(texture, 64, 128 + y, 64, 87 + y, 2, RedHighlight);
        FillEllipse(texture, 64, 96 + y, 5, 5, Gold);
        FillEllipse(texture, 64, 96 + y, 2, 2, GoldShadow);

        FillPolygon(texture, new[] { new Vector2Int(29, 106 + y), new Vector2Int(16, 86 + y), new Vector2Int(25, 78 + y), new Vector2Int(42, 105 + y) }, Red);
        FillPolygon(texture, new[] { new Vector2Int(99, 106 + y), new Vector2Int(112, 86 + y), new Vector2Int(103, 78 + y), new Vector2Int(86, 105 + y) }, Red);
        FillPolygon(texture, new[] { new Vector2Int(18, 87 + y), new Vector2Int(26, 87 + y), new Vector2Int(31, 96 + y), new Vector2Int(25, 100 + y) }, Skin);
        FillPolygon(texture, new[] { new Vector2Int(110, 87 + y), new Vector2Int(102, 87 + y), new Vector2Int(97, 96 + y), new Vector2Int(103, 100 + y) }, Skin);

        int leftLeg = stride;
        int rightLeg = -stride;
        FillPolygon(texture, new[] { new Vector2Int(48, 62 + y), new Vector2Int(64, 62 + y), new Vector2Int(58 + leftLeg, 18 + y), new Vector2Int(43 + leftLeg, 18 + y) }, BlackCloth);
        FillPolygon(texture, new[] { new Vector2Int(64, 62 + y), new Vector2Int(80, 62 + y), new Vector2Int(85 + rightLeg, 18 + y), new Vector2Int(70 + rightLeg, 18 + y) }, BlackCloth);
        FillPolygon(texture, new[] { new Vector2Int(43 + leftLeg, 20 + y), new Vector2Int(58 + leftLeg, 20 + y), new Vector2Int(62 + leftLeg, 10 + y), new Vector2Int(44 + leftLeg, 10 + y) }, Red);
        FillPolygon(texture, new[] { new Vector2Int(70 + rightLeg, 20 + y), new Vector2Int(85 + rightLeg, 20 + y), new Vector2Int(88 + rightLeg, 10 + y), new Vector2Int(70 + rightLeg, 10 + y) }, Red);
        FillRect(texture, 48 + leftLeg, 7 + y, 17, 5, Gold);
        FillRect(texture, 68 + rightLeg, 7 + y, 17, 5, Gold);

        FillPolygon(texture, new[] { new Vector2Int(91, 78 + y), new Vector2Int(111, 74 + y), new Vector2Int(114, 58 + y), new Vector2Int(96, 62 + y) }, Mask);
        FillPolygon(texture, new[] { new Vector2Int(96, 64 + y), new Vector2Int(102, 55 + y), new Vector2Int(107, 64 + y) }, Mask);
        FillPolygon(texture, new[] { new Vector2Int(103, 68 + y), new Vector2Int(108, 72 + y), new Vector2Int(103, 76 + y) }, Red);
        FillRect(texture, 98, 68 + y, 4, 2, Red);
        FillRect(texture, 92, 76 + y, 3, 20, RedHighlight);
    }

    private static void Clear(Texture2D texture)
    {
        Color[] pixels = new Color[texture.width * texture.height];
        for (int index = 0; index < pixels.Length; index++)
        {
            pixels[index] = Color.clear;
        }
        texture.SetPixels(pixels);
    }

    private static void FillRect(Texture2D texture, int xMin, int yMin, int xMax, int yMax, Color color)
    {
        for (int x = Mathf.Max(0, xMin); x <= Mathf.Min(texture.width - 1, xMax); x++)
        {
            for (int y = Mathf.Max(0, yMin); y <= Mathf.Min(texture.height - 1, yMax); y++)
            {
                texture.SetPixel(x, y, color);
            }
        }
    }

    private static void FillEllipse(Texture2D texture, int centerX, int centerY, int radiusX, int radiusY, Color color)
    {
        for (int x = centerX - radiusX; x <= centerX + radiusX; x++)
        {
            for (int y = centerY - radiusY; y <= centerY + radiusY; y++)
            {
                float normalizedX = (x - centerX) / (float)radiusX;
                float normalizedY = (y - centerY) / (float)radiusY;
                if (normalizedX * normalizedX + normalizedY * normalizedY <= 1f)
                {
                    texture.SetPixel(x, y, color);
                }
            }
        }
    }

    private static void FillPolygon(Texture2D texture, Vector2Int[] points, Color color)
    {
        int minY = texture.height - 1;
        int maxY = 0;
        foreach (Vector2Int point in points)
        {
            minY = Mathf.Min(minY, point.y);
            maxY = Mathf.Max(maxY, point.y);
        }
        for (int y = Mathf.Max(0, minY); y <= Mathf.Min(texture.height - 1, maxY); y++)
        {
            List<int> intersections = new();
            for (int index = 0; index < points.Length; index++)
            {
                Vector2Int first = points[index];
                Vector2Int second = points[(index + 1) % points.Length];
                if (first.y == second.y)
                {
                    continue;
                }
                bool crossesScanline = (first.y <= y && second.y > y) || (second.y <= y && first.y > y);
                if (crossesScanline)
                {
                    float x = first.x + (y - first.y) * (second.x - first.x) / (float)(second.y - first.y);
                    intersections.Add(Mathf.RoundToInt(x));
                }
            }
            intersections.Sort();
            for (int index = 0; index + 1 < intersections.Count; index += 2)
            {
                FillRect(texture, intersections[index], y, intersections[index + 1], y, color);
            }
        }
    }

    private static void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, int thickness, Color color)
    {
        int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
        for (int step = 0; step <= steps; step++)
        {
            float t = steps == 0 ? 0f : step / (float)steps;
            int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
            int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
            FillEllipse(texture, x, y, thickness, thickness, color);
        }
    }

    private static void DeleteAssetIfPresent(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
        {
            AssetDatabase.DeleteAsset(path);
        }
    }

    private static void EnsureDirectory(string assetDirectory)
    {
        string absoluteDirectory = Path.Combine(Application.dataPath, assetDirectory.Substring("Assets/".Length));
        Directory.CreateDirectory(absoluteDirectory);
    }
}
