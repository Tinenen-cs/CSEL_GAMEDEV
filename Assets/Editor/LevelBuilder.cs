using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Builds the whole platformer level (player, animations, obstacles, camera, music).
// Runs automatically once if Assets/Scenes/Level1.unity does not exist, or via Tools > Build Platformer Level.
[InitializeOnLoad]
public static class LevelBuilder
{
    const string ScenePath = "Assets/Scenes/Level1.unity";
    const string PlayerSprites = "Assets/Sprites/Player/";
    const string EnvSheet = "Assets/Nature_pixel_art_assets/textures/nature_environment_01.png";
    const string PropSheet = "Assets/Nature_pixel_art_assets/textures/Nature_props_01.png";
    const string AnimFolder = "Assets/Animations/";

    static int groundLayer;
    static Sprite[] envSprites, propSprites;
    static PhysicsMaterial2D noFriction;

    static LevelBuilder()
    {
        if (!File.Exists(ScenePath))
            EditorApplication.delayCall += Build;
    }

    [MenuItem("Tools/Build Platformer Level")]
    public static void Build()
    {
        foreach (string old in new[] { ScenePath, AnimFolder + "Player.controller", AnimFolder + "idle.anim", AnimFolder + "run.anim", AnimFolder + "jump.anim", "Assets/Sprites/PlayerNoFriction.physicsMaterial2D" })
            AssetDatabase.DeleteAsset(old);
        SetInputHandlerToBoth();
        groundLayer = EnsureLayer("Ground");
        ImportSprites();
        RuntimeAnimatorController controller = BuildAnimator();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject lightGo = new GameObject("Global Light 2D");
        Light2D light = lightGo.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;

        GameObject player = BuildPlayer(controller, new Vector2(-2f, 1.5f));
        BuildCamera(player);
        BuildMusic();
        BuildCourse();

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("LevelBuilder: built " + ScenePath);
    }

    // ---------- project setup ----------

    static void SetInputHandlerToBoth()
    {
        Object settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0];
        SerializedObject so = new SerializedObject(settings);
        SerializedProperty prop = so.FindProperty("activeInputHandler");
        if (prop != null && prop.intValue != 2)
        {
            prop.intValue = 2; // 0 = old, 1 = new, 2 = both
            so.ApplyModifiedProperties();
            Debug.LogWarning("LevelBuilder: Active Input Handling set to Both. Restart Unity for it to take effect.");
        }
    }

    static int EnsureLayer(string name)
    {
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        for (int i = 6; i < layers.arraySize; i++)
            if (layers.GetArrayElementAtIndex(i).stringValue == name) return i;
        for (int i = 6; i < layers.arraySize; i++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(layer.stringValue))
            {
                layer.stringValue = name;
                tagManager.ApplyModifiedProperties();
                return i;
            }
        }
        throw new System.Exception("No free layer slot for " + name);
    }

    static Sprite ImportSprite(string path, float ppu, bool fullRect)
    {
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = ppu;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = fullRect ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void ImportSprites()
    {
        foreach (string dir in new[] { "IDLE", "RUN", "JUMP" })
            foreach (string file in Directory.GetFiles(PlayerSprites + dir, "*.png"))
                ImportSprite(file.Replace('\\', '/'), 128f, false);

        envSprites = AssetDatabase.LoadAllAssetsAtPath(EnvSheet).OfType<Sprite>().ToArray();
        propSprites = AssetDatabase.LoadAllAssetsAtPath(PropSheet).OfType<Sprite>().ToArray();

        noFriction = new PhysicsMaterial2D("PlayerNoFriction") { friction = 0f, bounciness = 0f };
        AssetDatabase.CreateAsset(noFriction, "Assets/Sprites/PlayerNoFriction.physicsMaterial2D");
    }

    static Sprite Env(int i) { return envSprites.First(sp => sp.name == "nature_environment_01_" + i); }
    static Sprite Prop(int i) { return propSprites.First(sp => sp.name == "Nature_props_01_" + i); }

    // ---------- animation ----------

    static AnimationClip MakeClip(string folder, string prefix, int frames, float fps, bool loop)
    {
        AnimationClip clip = new AnimationClip { frameRate = fps };
        EditorCurveBinding binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[frames];
        for (int i = 0; i < frames; i++)
        {
            keys[i].time = i / fps;
            keys[i].value = AssetDatabase.LoadAssetAtPath<Sprite>(PlayerSprites + folder + "/" + prefix + (i + 1) + ".png");
        }
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AssetDatabase.CreateAsset(clip, AnimFolder + prefix.ToLower() + ".anim");
        return clip;
    }

    static RuntimeAnimatorController BuildAnimator()
    {
        AnimationClip idle = MakeClip("IDLE", "IDLE", 6, 8f, true);
        AnimationClip run = MakeClip("RUN", "RUN", 4, 10f, true);
        AnimationClip jump = MakeClip("JUMP", "JUMP", 6, 12f, false);

        AnimatorController ac = AnimatorController.CreateAnimatorControllerAtPath(AnimFolder + "Player.controller");
        ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ac.AddParameter("jump", AnimatorControllerParameterType.Bool);

        AnimatorStateMachine sm = ac.layers[0].stateMachine;
        AnimatorState idleState = sm.AddState("Idle");
        idleState.motion = idle;
        AnimatorState runState = sm.AddState("Run");
        runState.motion = run;
        AnimatorState jumpState = sm.AddState("Jump");
        jumpState.motion = jump;
        sm.defaultState = idleState;

        AnimatorStateTransition t = idleState.AddTransition(runState);
        t.hasExitTime = false; t.duration = 0f;
        t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

        t = runState.AddTransition(idleState);
        t.hasExitTime = false; t.duration = 0f;
        t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        t = sm.AddAnyStateTransition(jumpState);
        t.hasExitTime = false; t.duration = 0f; t.canTransitionToSelf = false;
        t.AddCondition(AnimatorConditionMode.If, 0f, "jump");

        t = jumpState.AddTransition(idleState);
        t.hasExitTime = true; t.exitTime = 1f; t.duration = 0f;

        return ac;
    }

    // ---------- player / camera / music ----------

    static GameObject BuildPlayer(RuntimeAnimatorController controller, Vector2 spawn)
    {
        GameObject player = new GameObject("Player");
        player.tag = "Player";
        player.transform.position = spawn;

        SpriteRenderer sr = player.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlayerSprites + "IDLE/IDLE1.png");
        sr.sortingOrder = 10;

        Rigidbody2D rb = player.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        CapsuleCollider2D col = player.AddComponent<CapsuleCollider2D>();
        col.size = new Vector2(0.9f, 1.55f);
        col.offset = new Vector2(0f, 0.07f);
        col.sharedMaterial = noFriction;

        Transform groundCheck = new GameObject("GroundCheck").transform;
        groundCheck.SetParent(player.transform, false);
        groundCheck.localPosition = new Vector3(0f, -0.72f, 0f);
        Transform ceilingCheck = new GameObject("CeilingCheck").transform;
        ceilingCheck.SetParent(player.transform, false);
        ceilingCheck.localPosition = new Vector3(0f, 0.85f, 0f);

        CharacterController2D cc = player.AddComponent<CharacterController2D>();
        SerializedObject so = new SerializedObject(cc);
        so.FindProperty("m_JumpForce").floatValue = 650f;
        so.FindProperty("m_AirControl").boolValue = true;
        so.FindProperty("m_WhatIsGround").intValue = 1 << groundLayer;
        so.FindProperty("m_GroundCheck").objectReferenceValue = groundCheck;
        so.FindProperty("m_CeilingCheck").objectReferenceValue = ceilingCheck;
        so.ApplyModifiedPropertiesWithoutUndo();

        Animator animator = player.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;

        playermovement pm = player.AddComponent<playermovement>();
        pm.anime = animator;
        pm.controller = cc;
        pm.runSpeed = 40f;

        return player;
    }

    static void BuildCamera(GameObject player)
    {
        GameObject camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(player.transform.position.x, player.transform.position.y + 1f, -10f);
        Camera cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.55f, 0.78f, 0.95f);
        camGo.AddComponent<AudioListener>();

        CameraFollow follow = camGo.AddComponent<CameraFollow>();
        follow.followObject = player;
        follow.followOffset = new Vector2(8.5f, 4.5f);
        follow.speed = 3f;

        // Background stage (sky tiles from the Nature pack) follows the camera.
        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(camGo.transform, false);
        bg.transform.localPosition = new Vector3(0f, 0f, 20f);
        bg.transform.localScale = new Vector3(4.6f, 4.6f, 1f);
        int[,] sky = { { 55, 56, 57, 58, 59 }, { 77, 78, 79, 80, 81 }, { 99, 100, 101, 102, 103 } };
        for (int row = 0; row < 3; row++)
            for (int col = 0; col < 5; col++)
            {
                GameObject tile = new GameObject("Sky");
                tile.transform.SetParent(bg.transform, false);
                tile.transform.localPosition = new Vector3(col - 2f, 1f - row, 0f);
                SpriteRenderer sr = tile.AddComponent<SpriteRenderer>();
                sr.sprite = Env(sky[row, col]);
                sr.sortingOrder = -100;
            }
    }

    static void BuildMusic()
    {
        GameObject music = new GameObject("Background Music");
        AudioSource source = music.AddComponent<AudioSource>();
        source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/BGM.mp3");
        source.loop = true;
        source.playOnAwake = true;
        source.volume = 0.6f;
        music.AddComponent<MusicPlayer>();
    }

    // ---------- level pieces ----------

    static Transform level;

    static GameObject Piece(string name, Sprite sprite, Vector2 position, Vector2 scale, int order = 0)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(level, false);
        go.transform.position = position;
        go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return go;
    }

    static void AddTile(Transform parent, Sprite sprite, Vector2 position, int order = 0)
    {
        GameObject tile = new GameObject(sprite.name);
        tile.transform.SetParent(parent, false);
        tile.transform.position = position;
        SpriteRenderer sr = tile.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
    }

    // Static grass ground from xStart to xEnd whose top surface is at topY, built from 1x1 Nature tiles.
    static void Ground(float xStart, float xEnd, float topY)
    {
        GameObject go = new GameObject("Ground");
        go.transform.SetParent(level, false);
        go.transform.position = new Vector2((xStart + xEnd) / 2f, topY - 1f);
        go.layer = groundLayer;
        go.AddComponent<BoxCollider2D>().size = new Vector2(xEnd - xStart, 2f);
        int tiles = Mathf.RoundToInt(xEnd - xStart);
        for (int i = 0; i < tiles; i++)
        {
            AddTile(go.transform, Env(i % 2 == 0 ? 22 : 23), new Vector2(xStart + i + 0.5f, topY - 0.5f));
            AddTile(go.transform, Env(i % 2 == 0 ? 97 : 98), new Vector2(xStart + i + 0.5f, topY - 1.5f));
        }
    }

    static void Spikes(float x, float topY, int width = 1)
    {
        GameObject go = new GameObject("Spikes (trap)");
        go.transform.SetParent(level, false);
        go.transform.position = new Vector2(x, topY + 0.5f);
        for (int i = 0; i < width; i++)
            AddTile(go.transform, Env(113), new Vector2(x - (width - 1) / 2f + i, topY + 0.5f), 1);
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(width - 0.2f, 0.35f);
        col.offset = new Vector2(0f, -0.3f);
        go.AddComponent<trap>();
    }

    // Pushable stone block.
    static void Crate(float x, float y)
    {
        GameObject go = Piece("Stone Block", Prop(15), new Vector2(x, y), new Vector2(1.1f, 1.5f));
        go.layer = groundLayer;
        go.AddComponent<BoxCollider2D>();
        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.mass = 1.5f;
    }

    static void Seesaw(float x, float y, float width)
    {
        GameObject go = Piece("Seesaw", Prop(14), new Vector2(x, y - 0.17f), new Vector2(width, 1f));
        go.layer = groundLayer;
        go.AddComponent<BoxCollider2D>();
        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.mass = 2f;
        rb.angularDamping = 0.5f;
        HingeJoint2D hinge = go.AddComponent<HingeJoint2D>();
        hinge.useLimits = true;
        hinge.limits = new JointAngleLimits2D { min = -22f, max = 22f };

        Piece("Seesaw Post", Prop(16), new Vector2(x, y - 2.5f), new Vector2(1f, 4f), -1);
    }

    static void FallingPlatform(float x, float topY, float width = 2.5f)
    {
        GameObject go = Piece("Falling Platform", Prop(13), new Vector2(x, topY - 0.25f), new Vector2(width, 0.75f));
        go.layer = groundLayer;
        go.GetComponent<SpriteRenderer>().color = new Color(1f, 0.85f, 0.6f);
        go.AddComponent<BoxCollider2D>();
        go.AddComponent<Rigidbody2D>();
        go.AddComponent<FallingPlatform>();
    }

    // Swinging rock hanging from a hinge. Rock bottom clears the floor by ~0.8 units.
    static void Pendulum(float x, float floorY, float length, float startAngle)
    {
        Vector2 pivot = new Vector2(x, floorY + length + 1.3f);
        GameObject root = new GameObject("Pendulum");
        root.transform.SetParent(level, false);
        root.transform.position = pivot;
        root.transform.rotation = Quaternion.Euler(0f, 0f, startAngle);

        Rigidbody2D rb = root.AddComponent<Rigidbody2D>();
        rb.useAutoMass = false;
        rb.mass = 5f;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
        HingeJoint2D hinge = root.AddComponent<HingeJoint2D>();
        hinge.anchor = Vector2.zero;
        root.AddComponent<Pendulum>();

        GameObject rod = new GameObject("Rod");
        rod.transform.SetParent(root.transform, false);
        rod.transform.localPosition = new Vector3(0f, -length / 2f, 0f);
        rod.transform.localScale = new Vector3(0.4f, length, 1f);
        rod.AddComponent<SpriteRenderer>().sprite = Prop(16);

        GameObject ball = new GameObject("Swinging Rock (trap)");
        ball.transform.SetParent(root.transform, false);
        ball.transform.localPosition = new Vector3(0f, -length, 0f);
        ball.transform.localScale = new Vector3(1.4f, 1.4f, 1f);
        SpriteRenderer ballSr = ball.AddComponent<SpriteRenderer>();
        ballSr.sprite = Prop(23);
        ballSr.sortingOrder = 2;
        ball.AddComponent<CircleCollider2D>().radius = 0.32f;
        ball.AddComponent<trap>();

        Piece("Pendulum Mount", Prop(8), pivot, Vector2.one, 1);
    }

    // Rope bridge of hinged planks between two anchor points at height y.
    static void Bridge(float xStart, float xEnd, float y, int planks)
    {
        float w = (xEnd - xStart) / planks;
        Rigidbody2D prev = null;
        for (int i = 0; i < planks; i++)
        {
            GameObject go = Piece("Bridge Plank", Prop(14), new Vector2(xStart + w * (i + 0.5f), y - 0.17f), new Vector2(w - 0.05f, 1f));
            go.layer = groundLayer;
            go.AddComponent<BoxCollider2D>();
            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.mass = 0.6f;
            // Joint anchors are in local space; the sprite is 1 unit wide before scaling.
            HingeJoint2D hinge = go.AddComponent<HingeJoint2D>();
            hinge.anchor = new Vector2(-0.5f, 0f);
            hinge.connectedBody = prev; // null = attached to the world
            if (i == planks - 1)
            {
                HingeJoint2D end = go.AddComponent<HingeJoint2D>();
                end.anchor = new Vector2(0.5f, 0f);
            }
            prev = rb;
        }
    }

    static void Finish(float x, float topY)
    {
        GameObject flag = Piece("Finish Sign", Prop(39), new Vector2(x, topY + 1.15f), new Vector2(2f, 2f));
        BoxCollider2D col = flag.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(1f, 3f);
        flag.AddComponent<FinishLine>();
    }

    // Non-colliding scenery behind the player.
    static void Decor(int prop, float x, float topY, float scale = 1.5f)
    {
        Sprite sp = Prop(prop);
        Piece("Decor", sp, new Vector2(x, topY + sp.bounds.extents.y * scale), new Vector2(scale, scale), -5);
    }

    static void BuildCourse()
    {
        level = new GameObject("Level").transform;

        // Fall zone: trap.cs reloads the scene, putting the player back at the start line.
        GameObject fall = new GameObject("Fall Zone (trap)");
        fall.transform.SetParent(level, false);
        fall.transform.position = new Vector2(150f, -15f);
        fall.AddComponent<BoxCollider2D>().size = new Vector2(400f, 4f);
        fall.AddComponent<trap>();

        // Start line and a wall behind it.
        Ground(-6f, 14f, 0f);
        GameObject wall = new GameObject("Wall");
        wall.transform.SetParent(level, false);
        wall.transform.position = new Vector2(-6.5f, 4f);
        wall.AddComponent<BoxCollider2D>().size = new Vector2(1f, 9f);
        for (int i = 0; i < 9; i++) AddTile(wall.transform, Env(83), new Vector2(-6.5f, i));

        // Scenery.
        Decor(27, -3f, 0f); Decor(10, 2f, 0f); Decor(1, 11f, 0f);
        Decor(19, 21f, 0f); Decor(28, 48f, 1f); Decor(11, 82f, 1.5f); Decor(29, 94f, 4f);
        Decor(21, 125f, 4f); Decor(0, 151f, 4f); Decor(17, 182f, 3f); Decor(27, 212f, 6f);
        Decor(22, 237f, 6f); Decor(28, 288f, 5f); Decor(10, 292f, 5f);
        Crate(6f, 0.5f); Crate(7.1f, 0.5f); Crate(6.55f, 1.5f);

        // Section 1: gaps and first spikes.
        Ground(18f, 24f, 0f);
        Ground(27f, 33f, 1.5f);
        Spikes(30f, 1.5f);

        // Section 2: seesaw, then a pendulum.
        Seesaw(39f, 1.5f, 7f);
        Ground(45f, 57f, 1f);
        Pendulum(51f, 1f, 4f, 70f);

        // Section 3: falling platforms.
        FallingPlatform(61f, 1f);
        FallingPlatform(65.5f, 2f);
        FallingPlatform(70f, 1f);
        FallingPlatform(74.5f, 2f);

        // Section 4: crates to climb, rope bridge.
        Ground(79f, 90f, 1.5f);
        Crate(84f, 2f); Crate(85.1f, 2f); Crate(84.55f, 3f);
        Ground(91f, 97f, 4f);
        Bridge(97f, 115f, 4f, 9);

        // Section 5: spikes and pendulum, double seesaw.
        Ground(115f, 127f, 4f);
        Spikes(118.5f, 4f);
        Pendulum(123f, 4f, 4f, -70f);
        Seesaw(133f, 4f, 7f);
        Seesaw(142f, 4f, 7f);
        Ground(149f, 153f, 4f);

        // Section 6: long falling platform run.
        FallingPlatform(157f, 4.5f);
        FallingPlatform(161.5f, 5.5f);
        FallingPlatform(166f, 4.5f);
        FallingPlatform(170.5f, 3.5f);
        FallingPlatform(175f, 4.5f);

        // Section 7: stairs with spikes.
        Ground(179f, 185f, 3f);
        Ground(188f, 192f, 4.5f);
        Ground(195f, 199f, 6f);
        Spikes(197f, 6f);
        Ground(202f, 206f, 7.5f);

        // Section 8: pendulum gauntlet.
        Ground(210f, 240f, 6f);
        Pendulum(216f, 6f, 4f, 70f);
        Pendulum(224f, 6f, 4f, -60f);
        Spikes(228f, 6f);
        Pendulum(233f, 6f, 4f, 50f);

        // Section 9: bridge and final falling platforms.
        Bridge(240f, 258f, 6f, 9);
        Ground(258f, 262f, 6f);
        FallingPlatform(266f, 6f);
        FallingPlatform(270.5f, 7f);
        FallingPlatform(275f, 6f);
        FallingPlatform(279.5f, 5f);

        // Finish line.
        Ground(284f, 300f, 5f);
        Finish(296f, 5f);
    }
}
