using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Unity.Pipeline.Commands;

public static class BasicMeleeCombatBuilder
{
    private const string InputActionsPath = "Assets/Settings/InputSystem_Actions.inputactions";
    private const string EnemyName = "EnemyDummy";
    private const string AttackPointName = "AttackPoint";
    private const string SquareSpritePath = "Assets/Generated/Milestone1Square.png";

    [CliCommand("build_basic_melee_combat", "Build the scoped Basic Melee Combat prototype for the active Player.")]
    public static string Build()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            throw new InvalidOperationException("Player GameObject was not found in the active scene.");
        }

        InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        if (inputActions == null)
        {
            throw new InvalidOperationException($"Input action asset was not found at {InputActionsPath}.");
        }

        MVHumanWomanPlayerBuilder.Build();

        Transform attackPoint = GetOrCreateAttackPoint(player);
        PlayerCombat combat = player.GetComponent<PlayerCombat>() ?? player.AddComponent<PlayerCombat>();
        combat.Configure(inputActions, attackPoint, 1 << 0);

        GameObject enemy = GetOrCreateEnemy();
        ConfigureEnemyVisual(enemy);
        EnemyDummy dummy = enemy.GetComponent<EnemyDummy>() ?? enemy.AddComponent<EnemyDummy>();
        dummy.Configure(3);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return "Built Basic Melee Combat with Player/Attack, AttackPoint, Attack animator state, and EnemyDummy.";
    }

    private static Transform GetOrCreateAttackPoint(GameObject player)
    {
        Transform attackPoint = player.transform.Find(AttackPointName);
        if (attackPoint == null)
        {
            GameObject pointObject = new(AttackPointName);
            attackPoint = pointObject.transform;
            attackPoint.SetParent(player.transform, false);
        }

        attackPoint.localPosition = new Vector3(0.72f, -0.05f, 0f);
        attackPoint.localRotation = Quaternion.identity;
        attackPoint.localScale = Vector3.one;
        return attackPoint;
    }

    private static GameObject GetOrCreateEnemy()
    {
        GameObject enemy = GameObject.Find(EnemyName);
        if (enemy == null)
        {
            enemy = new GameObject(EnemyName);
        }

        enemy.transform.position = new Vector3(-5.9f, -1.5f, 0f);
        enemy.transform.rotation = Quaternion.identity;
        enemy.transform.localScale = Vector3.one;
        enemy.layer = 0;
        return enemy;
    }

    private static void ConfigureEnemyVisual(GameObject enemy)
    {
        SpriteRenderer renderer = enemy.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = enemy.AddComponent<SpriteRenderer>();
        }
        renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SquareSpritePath);
        renderer.color = new Color(0.95f, 0.25f, 0.25f, 1f);
        renderer.sortingOrder = 9;

        BoxCollider2D collider = enemy.GetComponent<BoxCollider2D>();
        if (collider == null)
        {
            collider = enemy.AddComponent<BoxCollider2D>();
        }
        collider.size = Vector2.one;
        collider.offset = Vector2.zero;
        collider.isTrigger = false;
    }
}
