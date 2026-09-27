using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

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
    const string NatureScenePath = "Assets/Nature_pixel_art_assets/Scenes/Nature_assets.unity";

    static int groundLayer, hazardLayer;
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
        hazardLayer = EnsureLayer("Hazard");
        Physics2D.IgnoreLayerCollision(hazardLayer, groundLayer, true); // swinging rocks pass through scenery
        ImportSprites();
        RuntimeAnimatorController controller = BuildAnimator();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject lightGo = new GameObject("Global Light 2D");
        Light2D light = lightGo.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;

        Vector2 spawn = BuildCourse();
        GameObject player = BuildPlayer(controller, spawn);
        BuildCamera(player);
        BuildMusic();

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
                ImportSprite(file.Replace('\\', '/'), 224f, false);

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
        col.size = new Vector2(0.5f, 0.88f);
        col.offset = new Vector2(0f, 0.04f);
        col.sharedMaterial = noFriction;

        Transform groundCheck = new GameObject("GroundCheck").transform;
        groundCheck.SetParent(player.transform, false);
        groundCheck.localPosition = new Vector3(0f, -0.41f, 0f);
        Transform ceilingCheck = new GameObject("CeilingCheck").transform;
        ceilingCheck.SetParent(player.transform, false);
        ceilingCheck.localPosition = new Vector3(0f, 0.49f, 0f);

        CharacterController2D cc = player.AddComponent<CharacterController2D>();
        SerializedObject so = new SerializedObject(cc);
        so.FindProperty("m_JumpForce").floatValue = 540f;
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
        pm.runSpeed = 25f;

        return player;
    }

    static void BuildCamera(GameObject player)
    {
        GameObject camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(player.transform.position.x, player.transform.position.y + 1f, -10f);
        Camera cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = cameraSize;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.55f, 0.78f, 0.95f);
        camGo.AddComponent<AudioListener>();

        CameraFollow follow = camGo.AddComponent<CameraFollow>();
        follow.followObject = player;
        follow.followOffset = new Vector2(cameraSize * 16f / 9f - 1.5f, cameraSize - 1f);
        follow.speed = 3f;

        BuildSky(cam);
    }

    // The Sky layer of the Nature_assets scene, pinned behind the camera so it covers the whole course.
    static void BuildSky(Camera cam)
    {
        Scene nature = EditorSceneManager.OpenScene(NatureScenePath, OpenSceneMode.Additive);
        GameObject sourceGrid = nature.GetRootGameObjects().First(g => g.GetComponent<Grid>() != null);
        GameObject grid = Object.Instantiate(sourceGrid);
        grid.name = "Sky (Nature_assets scene)";
        SceneManager.MoveGameObjectToScene(grid, cam.gameObject.scene);
        EditorSceneManager.CloseScene(nature, true);

        foreach (Transform child in grid.transform.Cast<Transform>().ToArray())
            if (child.name != "Sky") Object.DestroyImmediate(child.gameObject);

        Tilemap sky = grid.GetComponentInChildren<Tilemap>();
        sky.CompressBounds();
        Bounds bounds = sky.localBounds;
        sky.GetComponent<TilemapRenderer>().sortingOrder = -100;

        float viewH = cam.orthographicSize * 2f;
        float viewW = viewH * 16f / 9f;
        float scale = Mathf.Max(viewW / bounds.size.x, viewH / bounds.size.y) * 1.1f;
        grid.transform.SetParent(cam.transform, false);
        grid.transform.localScale = new Vector3(scale, scale, 1f);
        grid.transform.localPosition = new Vector3(-bounds.center.x * scale, -bounds.center.y * scale, 20f);
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

    static void Spikes(float x, float topY)
    {
        GameObject go = new GameObject("Spikes (trap)");
        go.transform.SetParent(level, false);
        go.transform.position = new Vector2(x, topY + 0.5f);
        AddTile(go.transform, Env(113), go.transform.position, 1);
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.8f, 0.35f);
        col.offset = new Vector2(0f, -0.3f);
        go.AddComponent<trap>();
    }

    // Invisible trap collider over spike tiles that are already painted in the Nature scene.
    static void SpikeTrap(float xStart, float xEnd, float topY)
    {
        GameObject go = new GameObject("Scene Spikes (trap)");
        go.transform.SetParent(level, false);
        go.transform.position = new Vector2((xStart + xEnd) / 2f, topY + 0.2f);
        go.AddComponent<BoxCollider2D>().size = new Vector2(xEnd - xStart - 0.2f, 0.4f);
        go.AddComponent<trap>();
    }

    // Pushable stone block.
    static void Crate(float x, float floorY)
    {
        GameObject go = Piece("Stone Block", Prop(15), new Vector2(x, floorY + 0.34f), new Vector2(0.7f, 1f));
        go.layer = groundLayer;
        go.AddComponent<BoxCollider2D>();
        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.mass = 1f;
    }

    static void Seesaw(float x, float y, float width)
    {
        GameObject go = Piece("Seesaw", Prop(14), new Vector2(x, y - 0.17f), new Vector2(width, 1f));
        go.layer = groundLayer;
        go.AddComponent<BoxCollider2D>();
        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.mass = 1f;
        rb.angularDamping = 0.5f;
        HingeJoint2D hinge = go.AddComponent<HingeJoint2D>();
        hinge.useLimits = true;
        hinge.limits = new JointAngleLimits2D { min = -20f, max = 20f };

        Piece("Seesaw Post", Prop(16), new Vector2(x, y - 1.6f), new Vector2(0.6f, 2.6f), -1);
    }

    static void FallingPlatform(float x, float topY, float width = 1.5f)
    {
        GameObject go = Piece("Falling Platform", Prop(13), new Vector2(x, topY - 0.25f), new Vector2(width, 0.75f));
        go.layer = groundLayer;
        go.GetComponent<SpriteRenderer>().color = new Color(1f, 0.85f, 0.6f);
        go.AddComponent<BoxCollider2D>();
        go.AddComponent<Rigidbody2D>();
        go.AddComponent<FallingPlatform>();
    }

    // Swinging rock on a hinge. The rock skims the floor at player height.
    static void Pendulum(float x, float floorY, float startAngle)
    {
        const float length = 1.6f;
        Vector2 pivot = new Vector2(x, floorY + length + 0.75f);
        GameObject root = new GameObject("Pendulum");
        root.transform.SetParent(level, false);
        root.transform.position = pivot;
        root.transform.rotation = Quaternion.Euler(0f, 0f, startAngle);
        root.layer = hazardLayer;

        Rigidbody2D rb = root.AddComponent<Rigidbody2D>();
        rb.useAutoMass = false;
        rb.mass = 3f;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
        HingeJoint2D hinge = root.AddComponent<HingeJoint2D>();
        hinge.anchor = Vector2.zero;
        root.AddComponent<Pendulum>();

        GameObject rod = new GameObject("Rod");
        rod.transform.SetParent(root.transform, false);
        rod.transform.localPosition = new Vector3(0f, -length / 2f, 0f);
        rod.transform.localScale = new Vector3(0.3f, length, 1f);
        rod.AddComponent<SpriteRenderer>().sprite = Prop(16);

        GameObject ball = new GameObject("Swinging Rock (trap)");
        ball.transform.SetParent(root.transform, false);
        ball.transform.localPosition = new Vector3(0f, -length, 0f);
        ball.layer = hazardLayer;
        SpriteRenderer ballSr = ball.AddComponent<SpriteRenderer>();
        ballSr.sprite = Prop(23);
        ballSr.sortingOrder = 6;
        ball.AddComponent<CircleCollider2D>().radius = 0.3f;
        ball.AddComponent<trap>();

        Piece("Pendulum Mount", Prop(8), pivot, new Vector2(0.6f, 0.6f), 6);
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
            rb.mass = 0.4f;
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
        GameObject flag = Piece("Finish Sign", Prop(39), new Vector2(x, topY + 0.58f), Vector2.one, 5);
        BoxCollider2D col = flag.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(1f, 2f);
        flag.AddComponent<FinishLine>();
    }

    static float cameraSize = 3f;
    const int Islands = 10;
    const float GapWidth = 4f;

    // Layout of the Nature_assets scene in its own coordinates (1 tile = 1 unit):
    //   x -9..-8 cliff top y=1 (start), -8..-6 ledge y=0 over a cave, -6..-3 floor y=-1,
    //   -3..2 slope up to y=0, 2..9 cave floor y=-1 (spike tiles at x 7..9), 9..15 ground y=0,
    //   15..20 cave floor y=-1, 20..24 slope up to y=0 and a wall at x 23..24 (removed on each copy).
    const float SceneLeft = -9f, SceneRight = 24f;

    // The course is the Nature_assets scene repeated as islands, with physics obstacles
    // on each island and in the gaps between them.
    static Vector2 BuildCourse()
    {
        level = new GameObject("Level").transform;
        Material spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");

        Scene nature = EditorSceneManager.OpenScene(NatureScenePath, OpenSceneMode.Additive);
        GameObject sourceGrid = nature.GetRootGameObjects().First(g => g.GetComponent<Grid>() != null);
        GameObject sourceProps = nature.GetRootGameObjects().FirstOrDefault(g => g.name == "Props");
        Camera natureCam = nature.GetRootGameObjects().Select(g => g.GetComponentInChildren<Camera>()).FirstOrDefault(c => c != null);
        if (natureCam != null) cameraSize = natureCam.orthographicSize;

        float step = SceneRight - SceneLeft + GapWidth;
        for (int k = 0; k < Islands; k++)
        {
            float ox = k * step;
            Vector3 offset = new Vector3(ox, 0f, 0f);

            GameObject grid = Object.Instantiate(sourceGrid);
            grid.name = "Nature_assets Island " + (k + 1);
            SceneManager.MoveGameObjectToScene(grid, level.gameObject.scene);
            grid.transform.SetParent(level, false);
            grid.transform.position = sourceGrid.transform.position + offset;
            foreach (Tilemap tilemap in grid.GetComponentsInChildren<Tilemap>())
            {
                if (tilemap.name == "Sky") continue;
                // Open the right-hand wall so the player can leave the island.
                if (k < Islands - 1)
                    for (int y = 0; y <= 2; y++) tilemap.SetTile(new Vector3Int(23, y, 0), null);
                tilemap.gameObject.layer = groundLayer;
                Rigidbody2D body = tilemap.gameObject.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Static;
                TilemapCollider2D tileCollider = tilemap.gameObject.AddComponent<TilemapCollider2D>();
                tileCollider.compositeOperation = Collider2D.CompositeOperation.Merge;
                tilemap.gameObject.AddComponent<CompositeCollider2D>();
            }

            if (sourceProps != null)
            {
                GameObject props = Object.Instantiate(sourceProps);
                props.name = "Props " + (k + 1);
                SceneManager.MoveGameObjectToScene(props, level.gameObject.scene);
                props.transform.SetParent(level, false);
                props.transform.position = sourceProps.transform.position + offset;
                foreach (var col in props.GetComponentsInChildren<Collider2D>(true)) Object.DestroyImmediate(col);
                foreach (var sr in props.GetComponentsInChildren<SpriteRenderer>(true))
                    if (sr.sharedMaterial == null && spriteMaterial != null) sr.sharedMaterial = spriteMaterial;
            }

            // Obstacles on the island.
            SpikeTrap(ox + 7f, ox + 9f, -1f);
            if (k % 2 == 0) { Crate(ox - 5f, -1f); Crate(ox - 4.2f, -1f); }
            if (k % 2 == 1) Pendulum(ox + 11.5f, 0f, k % 4 == 1 ? 65f : -65f);
            if (k >= 1) Spikes(ox + 17.5f, -1f);
            if (k >= 3) Pendulum(ox + 0.5f, 0f, k % 2 == 0 ? 60f : -60f);
            if (k >= 5) Spikes(ox + 4.5f, -1f);

            // Gap to the next island, crossed on a physics obstacle.
            if (k < Islands - 1)
            {
                float gapStart = ox + SceneRight;
                float gapEnd = gapStart + GapWidth;
                switch (k % 3)
                {
                    case 0:
                        FallingPlatform(gapStart + GapWidth / 2f, 0f);
                        break;
                    case 1:
                        Seesaw(gapStart + GapWidth / 2f, 0f, GapWidth - 0.8f);
                        break;
                    default:
                        Bridge(gapStart, gapEnd, 0f, 4);
                        break;
                }
            }
        }
        EditorSceneManager.CloseScene(nature, true);

        // Invisible wall behind the start line.
        GameObject wall = new GameObject("Start Wall");
        wall.transform.SetParent(level, false);
        wall.transform.position = new Vector2(SceneLeft - 0.5f, 5f);
        wall.AddComponent<BoxCollider2D>().size = new Vector2(1f, 20f);

        // Fall zone: trap.cs reloads the scene, putting the player back at the start line.
        float courseEnd = (Islands - 1) * step + SceneRight;
        GameObject fall = new GameObject("Fall Zone (trap)");
        fall.transform.SetParent(level, false);
        fall.transform.position = new Vector2((SceneLeft + courseEnd) / 2f, -9f);
        fall.AddComponent<BoxCollider2D>().size = new Vector2(courseEnd - SceneLeft + 40f, 4f);
        fall.AddComponent<trap>();

        Finish((Islands - 1) * step + 22.4f, 0f);

        return new Vector2(SceneLeft + 0.6f, 1.6f);
    }
}
