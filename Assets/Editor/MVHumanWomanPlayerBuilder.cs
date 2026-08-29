using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Pipeline.Commands;

public static class MVHumanWomanPlayerBuilder
{
    private const string SpriteSheetPath = "Assets/Art/Player/mvHumanWoman/full/ninja.png";
    private const string GeneratedDirectory = "Assets/Generated/MVHumanWomanPlayer";
    private const string ControllerPath = GeneratedDirectory + "/MVHumanWomanPlayer.controller";
    private const int Columns = 10;
    private const int Rows = 10;
    private const int FrameWidth = 32;
    private const int FrameHeight = 64;
    private const float PixelsPerUnit = 64f;

    [CliCommand("integrate_mv_human_woman", "Use the MV Human Woman 32x64 sprite sheet for the active Player.")]
    public static string Build()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            throw new InvalidOperationException("Player GameObject was not found in the active scene.");
        }

        Sprite[] sprites = ConfigureSpriteSheet();
        AnimatorController controller = CreateAnimatorController(sprites);
        Transform visualRoot = ConfigurePlayerVisual(player, sprites[0], controller);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return $"Integrated {SpriteSheetPath} as {visualRoot.name} using {PixelsPerUnit} PPU and Idle/Walk/Jump/Fall clips.";
    }

    private static Sprite[] ConfigureSpriteSheet()
    {
        AssetDatabase.ImportAsset(SpriteSheetPath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(SpriteSheetPath) as TextureImporter;
        if (importer == null)
        {
            throw new InvalidOperationException($"Texture importer was not found for {SpriteSheetPath}.");
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.isReadable = false;

        SpriteMetaData[] metadata = new SpriteMetaData[Columns * Rows];
        int metadataIndex = 0;
        for (int row = 0; row < Rows; row++)
        {
            for (int column = 0; column < Columns; column++)
            {
                metadata[metadataIndex++] = new SpriteMetaData
                {
                    name = FrameName(row, column),
                    rect = new Rect(column * FrameWidth, (Rows - 1 - row) * FrameHeight, FrameWidth, FrameHeight),
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = new Vector2(0.5f, 0f),
                    border = Vector4.zero
                };
            }
        }
        importer.spritesheet = metadata;
        importer.SaveAndReimport();

        Dictionary<string, Sprite> spriteByName = new();
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(SpriteSheetPath))
        {
            if (asset is Sprite sprite)
            {
                spriteByName[sprite.name] = sprite;
            }
        }

        Sprite[] result = new Sprite[Columns * Rows];
        for (int row = 0; row < Rows; row++)
        {
            for (int column = 0; column < Columns; column++)
            {
                if (!spriteByName.TryGetValue(FrameName(row, column), out Sprite sprite))
                {
                    throw new InvalidOperationException($"Expected sliced sprite {FrameName(row, column)} was not imported.");
                }
                result[row * Columns + column] = sprite;
            }
        }
        return result;
    }

    private static Transform ConfigurePlayerVisual(GameObject player, Sprite idleSprite, AnimatorController controller)
    {
        Transform visualRoot = player.transform.Find("CharacterVisual") ?? player.transform.Find("VisualRoot");
        if (visualRoot == null)
        {
            GameObject visualObject = new GameObject("VisualRoot");
            visualRoot = visualObject.transform;
            visualRoot.SetParent(player.transform, false);
        }
        visualRoot.name = "VisualRoot";
        visualRoot.localPosition = new Vector3(0f, -0.46f, 0f);
        visualRoot.localRotation = Quaternion.identity;
        visualRoot.localScale = Vector3.one * 1.25f;

        SpriteRenderer renderer = visualRoot.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = visualRoot.gameObject.AddComponent<SpriteRenderer>();
        }
        renderer.sprite = idleSprite;
        renderer.flipX = false;
        renderer.sortingOrder = 10;
        renderer.maskInteraction = SpriteMaskInteraction.None;

        Transform marker = player.transform.Find("FacingMarker");
        if (marker != null)
        {
            UnityEngine.Object.DestroyImmediate(marker.gameObject, true);
        }

        Animator animator = player.GetComponent<Animator>();
        if (animator == null)
        {
            animator = player.AddComponent<Animator>();
        }
        animator.runtimeAnimatorController = controller;

        PlayerController controllerComponent = player.GetComponent<PlayerController>();
        if (controllerComponent == null)
        {
            throw new InvalidOperationException("PlayerController is missing from Player.");
        }
        controllerComponent.enabled = true;
        controllerComponent.ConfigureVisualRoot(visualRoot);
        return visualRoot;
    }

    private static AnimatorController CreateAnimatorController(Sprite[] sprites)
    {
        EnsureDirectory(GeneratedDirectory);
        string[] clipPaths =
        {
            GeneratedDirectory + "/Idle.anim",
            GeneratedDirectory + "/Run.anim",
            GeneratedDirectory + "/Jump.anim",
            GeneratedDirectory + "/Fall.anim"
        };

        AnimationClip existingIdle = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPaths[0]);
        AnimationClip existingRun = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPaths[1]);
        AnimationClip existingJump = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPaths[2]);
        AnimationClip existingFall = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPaths[3]);
        AnimatorController existingController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (existingController != null
            && HasExpectedClip(existingIdle, new[] { sprites[FrameIndex(0, 0)] }, true)
            && HasExpectedClip(existingRun, new[]
            {
                sprites[FrameIndex(0, 1)], sprites[FrameIndex(0, 2)], sprites[FrameIndex(0, 3)],
                sprites[FrameIndex(0, 4)], sprites[FrameIndex(0, 5)], sprites[FrameIndex(0, 6)]
            }, true)
            && HasExpectedClip(existingJump, new[]
            {
                sprites[FrameIndex(1, 7)], sprites[FrameIndex(1, 8)], sprites[FrameIndex(1, 9)]
            }, false)
            && HasExpectedClip(existingFall, new[] { sprites[FrameIndex(1, 9)] }, false)
            && HasExpectedController(existingController, existingIdle, existingRun, existingJump, existingFall))
        {
            return existingController;
        }

        DeleteAssetIfPresent(ControllerPath);
        foreach (string clipPath in clipPaths)
        {
            DeleteAssetIfPresent(clipPath);
        }

        AnimationClip idle = CreateClip("Idle", new[] { sprites[FrameIndex(0, 0)] }, 0.5f, true);
        AnimationClip run = CreateClip("Run", new[]
        {
            sprites[FrameIndex(0, 1)], sprites[FrameIndex(0, 2)], sprites[FrameIndex(0, 3)],
            sprites[FrameIndex(0, 4)], sprites[FrameIndex(0, 5)], sprites[FrameIndex(0, 6)]
        }, 0.1f, true);
        AnimationClip jump = CreateClip("Jump", new[]
        {
            sprites[FrameIndex(1, 7)], sprites[FrameIndex(1, 8)], sprites[FrameIndex(1, 9)]
        }, 0.12f, false);
        AnimationClip fall = CreateClip("Fall", new[] { sprites[FrameIndex(1, 9)] }, 0.1f, false);
        AssetDatabase.CreateAsset(idle, clipPaths[0]);
        AssetDatabase.CreateAsset(run, clipPaths[1]);
        AssetDatabase.CreateAsset(jump, clipPaths[2]);
        AssetDatabase.CreateAsset(fall, clipPaths[3]);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("VerticalVelocity", AnimatorControllerParameterType.Float);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState idleState = stateMachine.AddState("Idle");
        AnimatorState runState = stateMachine.AddState("Run");
        AnimatorState jumpState = stateMachine.AddState("Jump");
        AnimatorState fallState = stateMachine.AddState("Fall");
        idleState.motion = idle;
        runState.motion = run;
        jumpState.motion = jump;
        fallState.motion = fall;
        stateMachine.defaultState = idleState;

        AddTransition(idleState, runState, new AnimatorCondition { mode = AnimatorConditionMode.Greater, parameter = "Speed", threshold = 0.1f });
        AddTransition(idleState, jumpState,
            new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = "Grounded" },
            new AnimatorCondition { mode = AnimatorConditionMode.Greater, parameter = "VerticalVelocity", threshold = 0.1f });
        AddTransition(idleState, fallState,
            new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = "Grounded" },
            new AnimatorCondition { mode = AnimatorConditionMode.Less, parameter = "VerticalVelocity", threshold = 0.1f });
        AddTransition(runState, idleState, new AnimatorCondition { mode = AnimatorConditionMode.Less, parameter = "Speed", threshold = 0.1f });
        AddTransition(runState, jumpState,
            new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = "Grounded" },
            new AnimatorCondition { mode = AnimatorConditionMode.Greater, parameter = "VerticalVelocity", threshold = 0.1f });
        AddTransition(runState, fallState,
            new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = "Grounded" },
            new AnimatorCondition { mode = AnimatorConditionMode.Less, parameter = "VerticalVelocity", threshold = 0.1f });
        AddTransition(jumpState, fallState, new AnimatorCondition { mode = AnimatorConditionMode.Less, parameter = "VerticalVelocity", threshold = 0.1f });
        AddTransition(jumpState, idleState, new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = "Grounded" }, new AnimatorCondition { mode = AnimatorConditionMode.Less, parameter = "Speed", threshold = 0.1f });
        AddTransition(jumpState, runState, new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = "Grounded" }, new AnimatorCondition { mode = AnimatorConditionMode.Greater, parameter = "Speed", threshold = 0.1f });
        AddTransition(fallState, idleState, new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = "Grounded" }, new AnimatorCondition { mode = AnimatorConditionMode.Less, parameter = "Speed", threshold = 0.1f });
        AddTransition(fallState, runState, new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = "Grounded" }, new AnimatorCondition { mode = AnimatorConditionMode.Greater, parameter = "Speed", threshold = 0.1f });
        return controller;
    }

    private static bool HasExpectedClip(AnimationClip clip, Sprite[] expectedSprites, bool looping)
    {
        if (clip == null || clip.isLooping != looping)
        {
            return false;
        }

        EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
        if (bindings.Length != 1)
        {
            return false;
        }

        EditorCurveBinding binding = bindings[0];
        if (binding.path != "VisualRoot"
            || binding.type != typeof(SpriteRenderer)
            || binding.propertyName != "m_Sprite")
        {
            return false;
        }

        ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
        int expectedKeyCount = expectedSprites.Length + (looping ? 1 : 0);
        if (keys.Length != expectedKeyCount)
        {
            return false;
        }

        for (int index = 0; index < expectedSprites.Length; index++)
        {
            if (keys[index].value != expectedSprites[index])
            {
                return false;
            }
        }

        return !looping || keys[^1].value == expectedSprites[0];
    }

    private static bool HasExpectedController(AnimatorController controller, AnimationClip idle, AnimationClip run, AnimationClip jump, AnimationClip fall)
    {
        AnimatorControllerParameter grounded = Array.Find(controller.parameters, parameter => parameter.name == "Grounded");
        AnimatorControllerParameter speed = Array.Find(controller.parameters, parameter => parameter.name == "Speed");
        AnimatorControllerParameter verticalVelocity = Array.Find(controller.parameters, parameter => parameter.name == "VerticalVelocity");
        if (grounded == null || grounded.type != AnimatorControllerParameterType.Bool
            || speed == null || speed.type != AnimatorControllerParameterType.Float
            || verticalVelocity == null || verticalVelocity.type != AnimatorControllerParameterType.Float
            || controller.layers.Length == 0)
        {
            return false;
        }

        Dictionary<string, AnimationClip> expectedMotions = new()
        {
            ["Idle"] = idle,
            ["Run"] = run,
            ["Jump"] = jump,
            ["Fall"] = fall
        };
        ChildAnimatorState[] states = controller.layers[0].stateMachine.states;
        if (states.Length != expectedMotions.Count)
        {
            return false;
        }

        foreach (ChildAnimatorState childState in states)
        {
            if (!expectedMotions.TryGetValue(childState.state.name, out AnimationClip expectedMotion)
                || childState.state.motion != expectedMotion)
            {
                return false;
            }

            foreach (AnimatorStateTransition transition in childState.state.transitions)
            {
                foreach (AnimatorCondition condition in transition.conditions)
                {
                    if (condition.parameter == "Grounded"
                        && condition.mode != AnimatorConditionMode.If
                        && condition.mode != AnimatorConditionMode.IfNot)
                    {
                        return false;
                    }
                }
            }
        }

        return true;
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
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("VisualRoot", typeof(SpriteRenderer), "m_Sprite"), keys);
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = looping;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        return clip;
    }

    private static void AddTransition(AnimatorState from, AnimatorState to, params AnimatorCondition[] conditions)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.05f;
        transition.conditions = conditions;
    }

    private static string FrameName(int row, int column) => $"ninja_{row}_{column}";

    private static int FrameIndex(int row, int column) => row * Columns + column;

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
