using System.IO;
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
    const string LevelSprites = "Assets/Sprites/Level/";
    const string AnimFolder = "Assets/Animations/";

    static int groundLayer;
    static Sprite groundSprite, crateSprite, spikeSprite, ballSprite, whiteSprite, plankSprite, flagSprite, bgSprite;
    static PhysicsMaterial2D noFriction;

    static LevelBuilder()
    {
        if (!File.Exists(ScenePath))
            EditorApplication.delayCall += Build;
    }

    [MenuItem("Tools/Build Platformer Level")]
    public static void Build()
    {
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

        groundSprite = ImportSprite(LevelSprites + "ground.png", 64f, true);
        crateSprite = ImportSprite(LevelSprites + "crate.png", 64f, true);
        spikeSprite = ImportSprite(LevelSprites + "spikes.png", 64f, true);
        ballSprite = ImportSprite(LevelSprites + "spikeball.png", 64f, true);
        whiteSprite = ImportSprite(LevelSprites + "white.png", 16f, true);
        plankSprite = ImportSprite(LevelSprites + "plank.png", 32f, true);
        flagSprite = ImportSprite(LevelSprites + "flag.png", 32f, true);
        bgSprite = ImportSprite(LevelSprites + "background.png", 100f, true);

        noFriction = new PhysicsMaterial2D("PlayerNoFriction") { friction = 0f, bounciness = 0f };
        AssetDatabase.CreateAsset(noFriction, "Assets/Sprites/PlayerNoFriction.physicsMaterial2D");
    }

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

        // Background stage follows the camera.
        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(camGo.transform, false);
        bg.transform.localPosition = new Vector3(0f, 0f, 20f);
        bg.transform.localScale = new Vector3(1.4f, 1.4f, 1f);
        SpriteRenderer sr = bg.AddComponent<SpriteRenderer>();
        sr.sprite = bgSprite;
        sr.sortingOrder = -100;
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

    static GameObject Tiled(string name, Sprite sprite, Vector2 center, Vector2 size, int order = 0)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(level, false);
        go.transform.position = center;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = size;
        sr.sortingOrder = order;
        return go;
    }

    // Static ground from xStart to xEnd whose top surface is at topY.
    static void Ground(float xStart, float xEnd, float topY)
    {
        GameObject go = Tiled("Ground", groundSprite, new Vector2((xStart + xEnd) / 2f, topY - 0.5f), new Vector2(xEnd - xStart, 1f));
        go.layer = groundLayer;
        go.AddComponent<BoxCollider2D>().size = new Vector2(xEnd - xStart, 1f);
    }

    static void Spikes(float x, float topY, int width = 1)
    {
        GameObject go = Tiled("Spikes (trap)", spikeSprite, new Vector2(x, topY + 0.25f), new Vector2(width, 0.5f), 1);
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(width - 0.2f, 0.4f);
        go.AddComponent<trap>();
    }

    static void Crate(float x, float y)
    {
        GameObject go = Tiled("Crate", crateSprite, new Vector2(x, y), Vector2.one);
        go.layer = groundLayer;
        go.AddComponent<BoxCollider2D>().size = Vector2.one;
        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.mass = 1.5f;
    }

    static void Seesaw(float x, float y, float width)
    {
        GameObject go = Tiled("Seesaw", plankSprite, new Vector2(x, y - 0.25f), new Vector2(width, 0.5f));
        go.layer = groundLayer;
        go.AddComponent<BoxCollider2D>().size = new Vector2(width, 0.5f);
        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.mass = 2f;
        rb.angularDamping = 0.5f;
        HingeJoint2D hinge = go.AddComponent<HingeJoint2D>();
        hinge.useLimits = true;
        hinge.limits = new JointAngleLimits2D { min = -22f, max = 22f };

        Tiled("Seesaw Post", whiteSprite, new Vector2(x, y - 2.5f), new Vector2(0.3f, 4f), -1).GetComponent<SpriteRenderer>().color = new Color(0.35f, 0.25f, 0.2f);
    }

    static void FallingPlatform(float x, float topY, float width = 2.5f)
    {
        GameObject go = Tiled("Falling Platform", plankSprite, new Vector2(x, topY - 0.25f), new Vector2(width, 0.5f));
        go.layer = groundLayer;
        go.GetComponent<SpriteRenderer>().color = new Color(1f, 0.75f, 0.6f);
        go.AddComponent<BoxCollider2D>().size = new Vector2(width, 0.5f);
        go.AddComponent<Rigidbody2D>();
        go.AddComponent<FallingPlatform>();
    }

    // Swinging spiked ball hanging from a hinge. Ball bottom clears the floor by ~0.8 units.
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
        SpriteRenderer rodSr = rod.AddComponent<SpriteRenderer>();
        rodSr.sprite = whiteSprite;
        rodSr.drawMode = SpriteDrawMode.Tiled;
        rodSr.size = new Vector2(0.12f, length);
        rodSr.color = new Color(0.3f, 0.3f, 0.3f);

        GameObject ball = new GameObject("Spike Ball (trap)");
        ball.transform.SetParent(root.transform, false);
        ball.transform.localPosition = new Vector3(0f, -length, 0f);
        ball.AddComponent<SpriteRenderer>().sprite = ballSprite;
        ball.GetComponent<SpriteRenderer>().sortingOrder = 2;
        ball.AddComponent<CircleCollider2D>().radius = 0.45f;
        ball.AddComponent<trap>();

        Tiled("Pendulum Mount", whiteSprite, pivot, new Vector2(0.5f, 0.3f), 1).GetComponent<SpriteRenderer>().color = Color.gray;
    }

    // Rope bridge of hinged planks between two anchor points at height y.
    static void Bridge(float xStart, float xEnd, float y, int planks)
    {
        float w = (xEnd - xStart) / planks;
        Rigidbody2D prev = null;
        for (int i = 0; i < planks; i++)
        {
            GameObject go = Tiled("Bridge Plank", plankSprite, new Vector2(xStart + w * (i + 0.5f), y - 0.2f), new Vector2(w - 0.05f, 0.4f));
            go.layer = groundLayer;
            go.AddComponent<BoxCollider2D>().size = new Vector2(w - 0.05f, 0.4f);
            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.mass = 0.6f;
            HingeJoint2D hinge = go.AddComponent<HingeJoint2D>();
            hinge.anchor = new Vector2(-w / 2f, 0f);
            hinge.connectedBody = prev; // null = attached to the world
            if (i == planks - 1)
            {
                HingeJoint2D end = go.AddComponent<HingeJoint2D>();
                end.anchor = new Vector2(w / 2f, 0f);
            }
            prev = rb;
        }
    }

    static void Finish(float x, float topY)
    {
        GameObject flag = new GameObject("Finish Flag");
        flag.transform.SetParent(level, false);
        flag.transform.position = new Vector2(x, topY + 2f);
        flag.AddComponent<SpriteRenderer>().sprite = flagSprite;
        BoxCollider2D col = flag.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(1.5f, 4f);
        flag.AddComponent<FinishLine>();
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
        GameObject wall = Tiled("Wall", groundSprite, new Vector2(-6.5f, 4f), new Vector2(1f, 9f));
        wall.AddComponent<BoxCollider2D>().size = new Vector2(1f, 9f);
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
