using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace NucleoInstavel.Editor
{
    public static class ProjectSetup
    {
        public const string Root = "Assets/_NucleoInstavel/";
        public const string ScenePath = Root + "Scenes/Game.unity";
        private const string ControlsPath = Root + "Input/GameControls.inputactions";
        private const string SettingsPath = Root + "Settings/GameSettings.asset";
        private const string FontPath =
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        private static Scene scene;
        private static TMP_FontAsset font;
        private static readonly Color Cyan = new Color(.35f, .95f, 1);

        // Input System 1.11.2 Set() does not invalidate this nonserialized cache.
        // Keep this workaround in the Editor; never replace persistent reference assets/GUIDs.
        private static readonly FieldInfo CachedReferenceAction =
            typeof(InputActionReference).GetField(
                "m_Action",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

        [MenuItem("Tools/Nucleo Instavel/Create or Repair Project")]
        public static void CreateOrRepair()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException(
                    "Saia do modo Play antes de reparar e salvar Game.unity."
                );

            EnsureDirectories();
            ValidateDependencies();
            // This phase creates/imports files only. Do not carry loaded asset handles across
            // Refresh, reimports or scene changes: Unity may unload/replace those instances.
            EnsureInputActions();
            EnsureSettingsAsset();
            ProceduralAssetGenerator.EnsureGeneratedAssets();
            EnsureLayer("Player");
            EnsureLayer("EnemyBullet");
            EnsurePrefabs();

            var current = SceneManager.GetActiveScene();
            bool resumePartial = !File.Exists(ScenePath) && IsPartialManagedScene(current);

            if (current.isDirty && current.path != ScenePath && !resumePartial)
                throw new InvalidOperationException(
                    "Salve sua cena atual antes de montar Game.unity; nenhuma cena foi descartada."
                );

            scene =
                current.path == ScenePath || resumePartial ? current
                : File.Exists(ScenePath) ? EditorSceneManager.OpenScene(ScenePath)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Give new/previously unsaved managed scenes a path before any fallible assembly step.
            // A subsequent invocation can then reuse the same scene after a failure or domain reload.
            if (scene.path != ScenePath && !EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException(
                    "Não foi possível salvar a cena gerenciada antes da montagem."
                );

            CreateOrRepairScene(SettingsPath, ControlsPath);
            ConfigurePhysicsAndPlayerSettings();
            EnsureBuildScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            ValidateProject();
            Debug.Log(
                "Núcleo Instável: montagem salva. Abra Game.unity e pressione Play. Se o Input System foi ativado agora, reinicie o Editor."
            );
        }

        private static void EnsureDirectories()
        {
            foreach (
                var p in new[]
                {
                    "Scenes",
                    "Prefabs",
                    "Settings",
                    "Input/References",
                    "Art/Sprites",
                    "Art/UI",
                    "Art/Materials",
                    "Audio/Music",
                    "Audio/Sfx",
                }
            )
                Directory.CreateDirectory(Root + p);

            Directory.CreateDirectory("Docs/Screenshots");
            AssetDatabase.Refresh();
        }

        private static void ValidateDependencies()
        {
            if (
                PackageInfo.FindForAssembly(typeof(InputAction).Assembly) == null
                || PackageInfo.FindForAssembly(typeof(TMP_Text).Assembly) == null
            )
                throw new InvalidOperationException(
                    "Aguarde resolver Input System e uGUI/TMP pelo Package Manager antes da montagem."
                );

            if (!AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath))
            {
                string package = PackageInfo
                    .FindForAssembly(typeof(TMP_Text).Assembly)
                    .resolvedPath;
                string essentials = Directory
                    .GetFiles(
                        package,
                        "TMP Essential Resources.unitypackage",
                        SearchOption.AllDirectories
                    )
                    .FirstOrDefault();

                if (essentials == null)
                    throw new InvalidOperationException(
                        "TMP Essential Resources ausente. Importe por Window > TextMeshPro > Import TMP Essential Resources e repita."
                    );

                AssetDatabase.ImportPackage(essentials, false);

                if (!AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath))
                    throw new InvalidOperationException(
                        "A importação TMP não terminou. Aguarde e execute novamente."
                    );
            }
        }

        private static InputActionAsset EnsureInputActions()
        {
            string path = ControlsPath;
            var existing = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);

            if (existing)
                return LoadInputActions(path);

            if (File.Exists(path))
                throw new InvalidOperationException(
                    "GameControls existe mas não foi importado; aguarde o Package Manager."
                );

            var asset = ScriptableObject.CreateInstance<InputActionAsset>();

            asset.name = "GameControls";

            var gameplay = new InputActionMap("Gameplay");

            asset.AddActionMap(gameplay);

            var move = gameplay.AddAction(
                "Move",
                InputActionType.Value,
                expectedControlLayout: "Vector2"
            );

            Composite(move, "<Keyboard>/w", "<Keyboard>/s", "<Keyboard>/a", "<Keyboard>/d");
            Composite(
                move,
                "<Keyboard>/upArrow",
                "<Keyboard>/downArrow",
                "<Keyboard>/leftArrow",
                "<Keyboard>/rightArrow"
            );
            move.AddBinding("<Gamepad>/leftStick");

            var focus = gameplay.AddAction("Focus", InputActionType.Button);

            focus.AddBinding("<Keyboard>/leftShift");
            focus.AddBinding("<Gamepad>/leftShoulder");

            var system = new InputActionMap("System");

            asset.AddActionMap(system);

            var pause = system.AddAction(
                "Pause",
                InputActionType.Button,
                interactions: "Press(behavior=0)"
            );

            foreach (string p in new[] { "<Keyboard>/p", "<Keyboard>/escape", "<Gamepad>/start" })
                pause.AddBinding(p);

            var ui = new InputActionMap("UI");

            asset.AddActionMap(ui);

            var navigate = ui.AddAction(
                "Navigate",
                InputActionType.PassThrough,
                expectedControlLayout: "Vector2"
            );

            Composite(
                navigate,
                "<Keyboard>/upArrow",
                "<Keyboard>/downArrow",
                "<Keyboard>/leftArrow",
                "<Keyboard>/rightArrow"
            );
            navigate.AddBinding("<Gamepad>/dpad");
            navigate.AddBinding("<Gamepad>/leftStick");

            var submit = ui.AddAction("Submit", InputActionType.Button);

            foreach (
                string p in new[]
                {
                    "<Keyboard>/enter",
                    "<Keyboard>/numpadEnter",
                    "<Gamepad>/buttonSouth",
                }
            )
                submit.AddBinding(p);

            var cancel = ui.AddAction("Cancel", InputActionType.Button);

            cancel.AddBinding("<Keyboard>/backspace");
            cancel.AddBinding("<Gamepad>/buttonEast");
            ui.AddAction(
                "Point",
                InputActionType.PassThrough,
                "<Mouse>/position",
                expectedControlLayout: "Vector2"
            );
            ui.AddAction(
                "Click",
                InputActionType.PassThrough,
                "<Mouse>/leftButton",
                expectedControlLayout: "Button"
            );
            ui.AddAction(
                "ScrollWheel",
                InputActionType.PassThrough,
                "<Mouse>/scroll",
                expectedControlLayout: "Vector2"
            );
            File.WriteAllText(path, asset.ToJson());
            Object.DestroyImmediate(asset);
            AssetDatabase.ImportAsset(
                path,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate
            );

            return LoadInputActions(path);
        }

        private static InputActionAsset LoadInputActions(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);

            if (!asset)
                throw new InvalidOperationException(
                    $"Entrada: não foi possível carregar o InputActionAsset em '{path}'. Confira o arquivo .inputactions e os erros de importação no Console; o arquivo não foi substituído."
                );

            ValidateActions(asset);

            return asset;
        }

        private static void EnsureSettingsAsset()
        {
            var settings = AssetDatabase.LoadAssetAtPath<GameSettings>(SettingsPath);

            if (!settings)
            {
                if (File.Exists(SettingsPath))
                    throw new InvalidOperationException(
                        $"Configuração: não foi possível importar '{SettingsPath}' como GameSettings; arquivo existente preservado."
                    );

                settings = ScriptableObject.CreateInstance<GameSettings>();

                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            if (!settings.TryValidate(out var error))
                throw new InvalidOperationException(error);

            AssetDatabase.SaveAssetIfDirty(settings);
        }

        private static GameSettings LoadSettings(string path)
        {
            var settings = AssetDatabase.LoadAssetAtPath<GameSettings>(path);

            if (!settings)
                throw new InvalidOperationException(
                    $"Configuração: não foi possível carregar GameSettings em '{path}'."
                );

            if (!settings.TryValidate(out var error))
                throw new InvalidOperationException(error);

            return settings;
        }

        private static void Composite(
            InputAction action,
            string up,
            string down,
            string left,
            string right
        ) =>
            action
                .AddCompositeBinding("2DVector")
                .With("Up", up)
                .With("Down", down)
                .With("Left", left)
                .With("Right", right);

        private static void ValidateActions(InputActionAsset asset)
        {
            if (ReferenceEquals(asset, null))
                throw new InvalidOperationException(
                    "Entrada: nenhum InputActionAsset foi recebido. Carregue o arquivo .inputactions antes de configurar a cena."
                );

            if (!asset)
                throw new InvalidOperationException(
                    "Entrada: a instância do InputActionAsset foi descarregada ou substituída. Recarregue o asset por caminho após importações e trocas de cena."
                );

            if (
                !EditorUtility.IsPersistent(asset)
                || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset))
            )
                throw new InvalidOperationException(
                    "Entrada: use o InputActionAsset persistente importado de GameControls.inputactions, não uma instância temporária."
                );

            foreach (
                string name in new[]
                {
                    "Gameplay/Move",
                    "Gameplay/Focus",
                    "System/Pause",
                    "UI/Navigate",
                    "UI/Submit",
                    "UI/Cancel",
                    "UI/Point",
                    "UI/Click",
                    "UI/ScrollWheel",
                }
            )
            {
                if (asset.FindAction(name) == null || asset.FindAction(name).bindings.Count == 0)
                    throw new InvalidOperationException(
                        $"Entrada editada/incompleta: falta {name}. Corrija preservando GameControls."
                    );

                var action = asset.FindAction(name);

                if (
                    action.actionMap == null
                    || action.actionMap.asset != asset
                    || !asset.Contains(action)
                )
                    throw new InvalidOperationException(
                        $"Entrada: {name} não pertence ao InputActionAsset importado {AssetDatabase.GetAssetPath(asset)}. Reimporte GameControls antes da montagem."
                    );
            }

            if (asset.FindAction("UI/Cancel").bindings.Any(b => b.path == "<Keyboard>/escape"))
                throw new InvalidOperationException(
                    "Remova Esc de UI/Cancel; ele já pertence a System/Pause."
                );
        }

        private static void EnsureLayer(string name)
        {
            if (LayerMask.NameToLayer(name) >= 0)
                return;

            var tags = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]
            );
            var layers = tags.FindProperty("layers");

            for (int i = 8; i < layers.arraySize; i++)
                if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                {
                    layers.GetArrayElementAtIndex(i).stringValue = name;

                    tags.ApplyModifiedPropertiesWithoutUndo();

                    return;
                }

            throw new InvalidOperationException($"Não há camada livre para {name}.");
        }

        private static Sprite Sprite(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(Root + "Art/Sprites/" + name + ".png");

        private static void EnsurePrefabs()
        {
            foreach (string name in new[] { "Player", "Enemy", "EnemyBullet" })
            {
                string path = Root + "Prefabs/" + name + ".prefab";

                if (File.Exists(path))
                {
                    var existing = PrefabUtility.LoadPrefabContents(path);

                    try
                    {
                        var visual = existing
                            .transform.Find("Visual")
                            ?.GetComponent<SpriteRenderer>();
                        string legacy =
                            name == "Player" ? "Player"
                            : name == "Enemy" ? "Core"
                            : "Bullet";
                        string upgraded =
                            name == "Player" ? "DroneHull"
                            : name == "Enemy" ? "ReactorCore"
                            : "ProjectileGlow";

                        if (
                            visual
                            && visual.sprite != UpgradeSprite(visual.sprite, legacy, upgraded)
                        )
                        {
                            visual.sprite = UpgradeSprite(visual.sprite, legacy, upgraded);

                            PrefabUtility.SaveAsPrefabAsset(existing, path);
                        }
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(existing);
                    }

                    continue;
                }

                var go = new GameObject(name);

                if (name == "Player")
                {
                    go.layer = LayerMask.NameToLayer("Player");

                    var rb = go.AddComponent<Rigidbody2D>();

                    rb.bodyType = RigidbodyType2D.Kinematic;
                    rb.gravityScale = 0;
                    rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                    go.AddComponent<CircleCollider2D>().radius = .14f;

                    go.AddComponent<PlayerMotor>();

                    var health = go.AddComponent<PlayerHealth>();
                    var feedback = go.AddComponent<PlayerFeedback>();
                    var visual = PrefabSprite(go, "Visual", Sprite("DroneHull"), .8f, 10);
                    var hitbox = PrefabSprite(go, "HitboxIndicator", Sprite("Hitbox"), .28f, 30);

                    Assign(feedback, "health", health);
                    Assign(feedback, "bodyVisual", visual);
                    Assign(feedback, "hitboxIndicator", hitbox);
                }
                else if (name == "Enemy")
                {
                    go.AddComponent<EnemyController>();
                    PrefabSprite(go, "Visual", Sprite("ReactorCore"), 1.6f, 10);

                    var primary = new GameObject("PrimaryEmitter");

                    primary.transform.SetParent(go.transform, false);

                    var a = primary.AddComponent<BulletEmitter>();

                    Assign(a, "origin", primary.transform);

                    var secondary = new GameObject("SecondaryEmitter");

                    secondary.transform.SetParent(go.transform, false);

                    var b = secondary.AddComponent<BulletEmitter>();

                    Assign(b, "origin", secondary.transform);
                    Assign(go.GetComponent<EnemyController>(), "primaryEmitter", a);
                    Assign(go.GetComponent<EnemyController>(), "secondaryEmitter", b);
                }
                else
                {
                    go.layer = LayerMask.NameToLayer("EnemyBullet");

                    var rb = go.AddComponent<Rigidbody2D>();

                    rb.gravityScale = 0;
                    rb.linearDamping = 0;
                    rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                    rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

                    var collider = go.AddComponent<CircleCollider2D>();

                    collider.radius = .06f;
                    collider.isTrigger = true;

                    go.AddComponent<EnemyBullet>();
                    PrefabSprite(go, "Visual", Sprite("ProjectileGlow"), .128f, 20);
                    go.SetActive(false);
                }

                PrefabUtility.SaveAsPrefabAsset(go, path);
                Object.DestroyImmediate(go);
            }
        }

        private static SpriteRenderer PrefabSprite(
            GameObject parent,
            string name,
            Sprite sprite,
            float size,
            int order
        )
        {
            var child = new GameObject(name);

            child.transform.SetParent(parent.transform, false);

            child.transform.localScale = Vector3.one * size;

            var renderer = child.AddComponent<SpriteRenderer>();

            renderer.sprite = sprite;
            renderer.sortingOrder = order;

            return renderer;
        }

        private static GameObject Node(string path, bool ui = false)
        {
            Transform parent = null;
            GameObject go = null;

            foreach (string part in path.Split('/'))
            {
                var found = parent
                    ? parent.Find(part)
                    : scene.GetRootGameObjects().FirstOrDefault(o => o.name == part)?.transform;

                go = found
                    ? found.gameObject
                    : new GameObject(part, ui ? new[] { typeof(RectTransform) } : Type.EmptyTypes);

                if (!found)
                {
                    if (parent)
                        go.transform.SetParent(parent, false);
                    else
                        SceneManager.MoveGameObjectToScene(go, scene);
                }

                parent = go.transform;
            }

            return go;
        }

        private static T Component<T>(string path, bool ui = false)
            where T : Component
        {
            var go = Node(path, ui);

            return go.TryGetComponent<T>(out var component) ? component : go.AddComponent<T>();
        }

        private static GameObject Instance(string name)
        {
            var found = scene.GetRootGameObjects().FirstOrDefault(o => o.name == name);

            return found
                ? found
                : (GameObject)
                    PrefabUtility.InstantiatePrefab(
                        AssetDatabase.LoadAssetAtPath<GameObject>(
                            Root + "Prefabs/" + name + ".prefab"
                        ),
                        scene
                    );
        }

        private static void Assign(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            var prop = serialized.FindProperty(field);

            if (prop == null)
                throw new InvalidOperationException(
                    $"Campo ausente: {target.GetType().Name}.{field}"
                );

            if (!prop.objectReferenceValue)
            {
                prop.objectReferenceValue = value;

                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            else if (prop.objectReferenceValue != value)
                Debug.LogWarning(
                    $"Referência editada preservada: {target.name}.{field}. Confira Validate Project.",
                    target
                );
        }

        private static void AssignUpgrade(
            Object target,
            string field,
            Object value,
            string previousPath
        )
        {
            var serialized = new SerializedObject(target);
            var prop = serialized.FindProperty(field);

            if (
                prop.objectReferenceValue
                && AssetDatabase.GetAssetPath(prop.objectReferenceValue) == previousPath
            )
            {
                prop.objectReferenceValue = value;

                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            else
                Assign(target, field, value);
        }

        private static void CreateOrRepairScene(
            string settingsPath,
            string controlsPath,
            string referenceDirectory = Root + "Input/References/"
        )
        {
            // Resolve fresh objects only after every import/refresh and scene switch. Paths
            // survive asset unloading; caller-owned UnityEngine.Object instances may not.
            var settings = LoadSettings(settingsPath);
            var controls = LoadInputActions(controlsPath);

            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            if (!font)
                throw new InvalidOperationException(
                    "Fonte TMP não carregada após a preparação dos assets. Importe TMP Essential Resources e repita."
                );

            bool hasCamera = NodeExists("Main Camera");
            var camera = Component<Camera>("Main Camera");

            camera.tag = "MainCamera";
            camera.orthographic = true;

            if (camera.transform.position == Vector3.zero)
                camera.transform.position = new Vector3(0, 0, -10);

            if (!hasCamera)
            {
                camera.backgroundColor = new Color(.018f, .03f, .06f);
                camera.clearFlags = CameraClearFlags.SolidColor;
            }

            Component<AudioListener>("Main Camera");

            var session = Component<GameSession>("Systems/GameSession");
            var input = Component<PlayerInputReader>("Systems/PlayerInputReader");

            Node("Systems").SetActive(true);
            session.gameObject.SetActive(true);

            session.enabled = true;

            input.gameObject.SetActive(true);

            input.enabled = true;

            var pool = Component<BulletPool>("Systems/BulletPool");
            var audio = Component<AudioController>("Systems/AudioController");
            var arena = Component<ArenaBounds>("Arena");
            var ps = Node("Arena/PlayerSpawn");

            if (ps.transform.position == Vector3.zero)
                ps.transform.position = new Vector3(0, -3);

            var es = Node("Arena/EnemySpawn");

            if (es.transform.position == Vector3.zero)
                es.transform.position = new Vector3(0, 3.8f);

            Assign(arena, "playerSpawn", ps.transform);
            Assign(arena, "enemySpawn", es.transform);
            Assign(arena, "worldCamera", camera);
            WorldSprite(
                "Arena/Background",
                "Background",
                Vector2.zero,
                new Vector2(19.6f, 11),
                Color.white,
                -20
            );

            var background = Component<SpriteRenderer>("Arena/Background");

            background.sprite = UpgradeSprite(background.sprite, "Background", "ChamberBackdrop");

            WorldSprite(
                "Arena/Borders/Left",
                "Panel",
                new Vector2(-8.05f, -1),
                new Vector2(.055f, 7.1f),
                Cyan * .6f,
                -5
            );
            WorldSprite(
                "Arena/Borders/Right",
                "Panel",
                new Vector2(8.05f, -1),
                new Vector2(.055f, 7.1f),
                Cyan * .6f,
                -5
            );
            WorldSprite(
                "Arena/Borders/Bottom",
                "Panel",
                new Vector2(0, -4.55f),
                new Vector2(16.15f, .055f),
                Cyan * .6f,
                -5
            );
            WorldSprite(
                "Arena/ContainmentStrip",
                "Panel",
                new Vector2(0, 2.6f),
                new Vector2(16.15f, .045f),
                new Color(.7f, .25f, .4f),
                -5
            );

            var player = Instance("Player");
            var motor = player.GetComponent<PlayerMotor>();
            var health = player.GetComponent<PlayerHealth>();
            var feedback = player.GetComponent<PlayerFeedback>();
            var enemy = Instance("Enemy").GetComponent<EnemyController>();

            if (player.transform.position == Vector3.zero)
                player.transform.position = ps.transform.position;

            if (enemy.transform.position == Vector3.zero)
                enemy.transform.position = es.transform.position;

            foreach (
                var visual in new[]
                {
                    player.transform.Find("Visual")?.GetComponent<SpriteRenderer>(),
                    enemy.transform.Find("Visual")?.GetComponent<SpriteRenderer>(),
                }
            )
                if (visual)
                    visual.sprite = UpgradeSprite(
                        visual.sprite,
                        visual.transform.parent == player.transform ? "Player" : "Core",
                        visual.transform.parent == player.transform ? "DroneHull" : "ReactorCore"
                    );

            var emitters = enemy.GetComponentsInChildren<BulletEmitter>(true);

            if (emitters.Length != 2)
                throw new InvalidOperationException(
                    "Enemy precisa de exatamente dois BulletEmitter; prefab existente preservado."
                );

            Assign(input, "controls", controls);
            Assign(motor, "input", input);
            Assign(motor, "settings", settings);
            Assign(motor, "arena", arena);
            Assign(health, "settings", settings);
            Assign(feedback, "input", input);
            Assign(feedback, "session", session);
            Assign(enemy, "player", motor);
            Assign(enemy, "arena", arena);

            foreach (var e in emitters)
            {
                Assign(e, "pool", pool);
                Assign(e, "settings", settings);
            }

            var bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Root + "Prefabs/EnemyBullet.prefab"
            );

            if (
                !bulletPrefab || !bulletPrefab.TryGetComponent<EnemyBullet>(out var bulletComponent)
            )
                throw new InvalidOperationException(
                    "EnemyBullet.prefab precisa conter EnemyBullet no objeto raiz; arquivo existente preservado."
                );

            Assign(pool, "prefab", bulletComponent);
            Assign(pool, "settings", settings);
            Assign(pool, "arena", arena);
            Assign(pool, "container", Node("Systems/ActiveBullets").transform);

            var eventSystem = CreateOrRepairEventSystem(controls, referenceDirectory);
            var ui = CreateOrRepairUI();
            var presentation = Component<ArenaPresentation>("Systems/ArenaPresentation");

            Assign(presentation, "session", session);
            Assign(
                presentation,
                "outerHalo",
                AccentSprite(
                    "Enemy/OuterHalo",
                    "ReactorHalo",
                    3.8f,
                    new Color(.2f, .85f, 1, .32f),
                    5
                )
            );
            Assign(
                presentation,
                "innerHalo",
                AccentSprite(
                    "Enemy/InnerHalo",
                    "ReactorHalo",
                    2.7f,
                    new Color(.2f, .85f, 1, .22f),
                    6
                )
            );
            Assign(
                presentation,
                "energyGlow",
                AccentSprite("Enemy/EnergyGlow", "EnergyGlow", 4.2f, new Color(.2f, .85f, 1), 4)
            );
            Assign(
                presentation,
                "playerBody",
                player.transform.Find("Visual").GetComponent<SpriteRenderer>()
            );
            Assign(
                presentation,
                "playerHitbox",
                player.transform.Find("HitboxIndicator").GetComponent<SpriteRenderer>()
            );
            Assign(
                presentation,
                "reactorBody",
                enemy.transform.Find("Visual").GetComponent<SpriteRenderer>()
            );
            Assign(
                presentation,
                "menuHalo",
                (RectTransform)Node("Canvas/MenuPanel/EmblemHalo", true).transform
            );
            Assign(
                presentation,
                "timeProgress",
                Component<Image>("Canvas/HUDPanel/TimeProgress", true)
            );
            Assign(ui, "session", session);
            Assign(ui, "health", health);
            Assign(ui, "audioController", audio);
            Assign(ui, "eventSystem", eventSystem);
            Assign(ui, "controls", controls);
            Assign(session, "settings", settings);
            Assign(session, "input", input);
            Assign(session, "player", motor);
            Assign(session, "health", health);
            Assign(session, "feedback", feedback);
            Assign(session, "enemy", enemy);
            Assign(session, "pool", pool);
            Assign(session, "arena", arena);
            Assign(session, "ui", ui);

            var sources = audio.GetComponents<AudioSource>();

            while (sources.Length < 2)
            {
                var source = audio.gameObject.AddComponent<AudioSource>();

                source.playOnAwake = false;
                source.spatialBlend = 0;
                source.volume = sources.Length == 0 ? .15f : .4f;
                sources = audio.GetComponents<AudioSource>();
            }

            if (sources.Length != 2)
                throw new InvalidOperationException(
                    "AudioController possui fontes extras; preserve/revise manualmente."
                );

            Assign(audio, "session", session);
            Assign(audio, "health", health);
            Assign(audio, "primaryEmitter", emitters[0]);
            Assign(audio, "secondaryEmitter", emitters[1]);
            Assign(audio, "musicSource", sources[0]);
            Assign(audio, "sfxSource", sources[1]);
            AssignUpgrade(
                audio,
                "music",
                AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "Audio/Music/ContainmentDrive.wav"),
                Root + "Audio/Music/Containment.wav"
            );
            Assign(
                audio,
                "menuMusic",
                AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "Audio/Music/MenuPulse.wav")
            );
            Assign(
                audio,
                "instabilityMusic",
                AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "Audio/Music/InstabilityDrive.wav")
            );
            Assign(
                audio,
                "overloadMusic",
                AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "Audio/Music/OverloadDrive.wav")
            );
            Assign(
                audio,
                "resultMusic",
                AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "Audio/Music/Afterglow.wav")
            );

            foreach (
                string name in new[]
                {
                    "UiClick",
                    "Volley",
                    "Hit",
                    "PhaseChange",
                    "Victory",
                    "Defeat",
                }
            )
                Assign(
                    audio,
                    char.ToLowerInvariant(name[0]) + name.Substring(1),
                    AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "Audio/Sfx/" + name + ".wav")
                );
        }

        private static EventSystem CreateOrRepairEventSystem(
            InputActionAsset controls,
            string referenceDirectory
        )
        {
            var go = Node("EventSystem");

            go.SetActive(false);

            try
            {
                var events = Component<EventSystem>("EventSystem");

                events.enabled = true;
                events.sendNavigationEvents = true;

                EditorUtility.SetDirty(events);

                var module = Component<InputSystemUIInputModule>("EventSystem");

                ConfigureUIInputModule(module, controls, referenceDirectory);

                return events;
            }
            finally
            {
                go.SetActive(true);
            }
        }

        private static void ConfigureUIInputModule(
            InputSystemUIInputModule module,
            InputActionAsset controls,
            string referenceDirectory = Root + "Input/References/"
        )
        {
            ValidateActions(controls);

            // Resolve persistent references before touching an existing module.
            var point = ActionReference(controls, "Point", referenceDirectory);
            var move = ActionReference(controls, "Navigate", referenceDirectory);
            var submit = ActionReference(controls, "Submit", referenceDirectory);
            var click = ActionReference(controls, "Click", referenceDirectory);
            var scroll = ActionReference(controls, "ScrollWheel", referenceDirectory);

            module.enabled = false;

            try
            {
                // actionsAsset's setter remaps OLD references through InputActionReference.Create.
                // Clear every slot first, including defaults from Reset/OnEnable or a partial run.
                module.point = null;
                module.move = null;
                module.submit = null;
                module.cancel = null;
                module.leftClick = null;
                module.rightClick = null;
                module.middleClick = null;
                module.scrollWheel = null;
                module.trackedDevicePosition = null;
                module.trackedDeviceOrientation = null;
                module.actionsAsset = controls;
                module.point = point;
                module.move = move;
                module.submit = submit;
                module.leftClick = click;
                module.scrollWheel = scroll;
                module.deselectOnBackgroundClick = false;

                EditorUtility.SetDirty(module);
                ValidateUIInputModule(module, controls, referenceDirectory);
            }
            finally
            {
                module.enabled = true;

                EditorUtility.SetDirty(module);
            }
        }

        private static InputActionReference ActionReference(
            InputActionAsset asset,
            string name,
            string referenceDirectory = Root + "Input/References/"
        )
        {
            ValidateActions(asset);

            var action = asset.FindAction("UI/" + name, true);
            string path = referenceDirectory + name + ".asset";
            var reference = AssetDatabase.LoadAssetAtPath<InputActionReference>(path);

            if (!reference)
            {
                if (File.Exists(path) || AssetDatabase.LoadMainAssetAtPath(path))
                    throw new InvalidOperationException(
                        $"Referência de UI inválida em {path}; arquivo existente preservado. Corrija o tipo/importação antes de repetir."
                    );

                reference = InputActionReference.Create(action);

                AssetDatabase.CreateAsset(reference, path);
            }
            else if (reference.asset != asset || reference.action != action)
            {
                // Repair the managed reference in place: preserve its GUID and scene links.
                reference.Set(action);
                EditorUtility.SetDirty(reference);
            }

            if (CachedReferenceAction == null)
                throw new InvalidOperationException(
                    "Input System: contrato do cache de InputActionReference mudou. Revise o reparo para a versão instalada antes de prosseguir."
                );

            CachedReferenceAction.SetValue(reference, null);

            return reference;
        }

        private static void ValidateUIInputModule(
            InputSystemUIInputModule module,
            InputActionAsset controls,
            string referenceDirectory = Root + "Input/References/"
        )
        {
            if (module.actionsAsset != controls)
                throw new InvalidOperationException(
                    "EventSystem: Actions Asset deve apontar para GameControls.inputactions."
                );

            ValidateUIReference(module.point, controls, "Point", referenceDirectory);
            ValidateUIReference(module.move, controls, "Navigate", referenceDirectory);
            ValidateUIReference(module.submit, controls, "Submit", referenceDirectory);
            ValidateUIReference(module.leftClick, controls, "Click", referenceDirectory);
            ValidateUIReference(module.scrollWheel, controls, "ScrollWheel", referenceDirectory);

            if (
                module.cancel
                || module.rightClick
                || module.middleClick
                || module.trackedDevicePosition
                || module.trackedDeviceOrientation
            )
                throw new InvalidOperationException(
                    "EventSystem: remova ações padrão extras; Cancel é tratado somente por GameUI."
                );
        }

        private static void ValidateUIReference(
            InputActionReference reference,
            InputActionAsset controls,
            string name,
            string referenceDirectory
        )
        {
            var action = controls.FindAction("UI/" + name, true);

            if (
                !reference
                || !EditorUtility.IsPersistent(reference)
                || reference.asset != controls
                || reference.action != action
                || action.actionMap == null
                || action.actionMap.asset != controls
                || !controls.Contains(action)
                || AssetDatabase.GetAssetPath(reference) != referenceDirectory + name + ".asset"
            )
                throw new InvalidOperationException(
                    $"EventSystem: UI/{name} precisa de uma referência persistente pertencente a GameControls.inputactions."
                );
        }

        private static bool IsPartialManagedScene(Scene candidate)
        {
            // Recognize the unsaved partial scene produced by the older generator, without
            // adopting unrelated dirty scenes or overwriting an existing Game.unity.
            if (
                !candidate.IsValid()
                || !candidate.isLoaded
                || !string.IsNullOrEmpty(candidate.path)
            )
                return false;

            var roots = candidate.GetRootGameObjects();

            foreach (string name in new[] { "Systems", "Arena", "Player", "Enemy" })
                if (roots.Count(r => r.name == name) != 1)
                    return false;

            var systems = roots.First(r => r.name == "Systems");
            var arena = roots.First(r => r.name == "Arena");
            var player = roots.First(r => r.name == "Player");
            var enemy = roots.First(r => r.name == "Enemy");

            if (
                !systems
                || !arena
                || !player
                || !enemy
                || !arena.GetComponent<ArenaBounds>()
                || !arena.transform.Find("PlayerSpawn")
                || !arena.transform.Find("EnemySpawn")
                || !player.GetComponent<PlayerMotor>()
                || !player.GetComponent<PlayerHealth>()
                || !enemy.GetComponent<EnemyController>()
            )
                return false;

            var sessionNode = systems.transform.Find("GameSession");
            var inputNode = systems.transform.Find("PlayerInputReader");
            var poolNode = systems.transform.Find("BulletPool");

            if (
                !sessionNode
                || !sessionNode.GetComponent<GameSession>()
                || !inputNode
                || !inputNode.GetComponent<PlayerInputReader>()
                || !poolNode
                || !poolNode.GetComponent<BulletPool>()
            )
                return false;

            var input = new SerializedObject(inputNode.GetComponent<PlayerInputReader>())
                .FindProperty("controls")
                .objectReferenceValue;
            var settings = new SerializedObject(player.GetComponent<PlayerMotor>())
                .FindProperty("settings")
                .objectReferenceValue;

            return AssetDatabase.GetAssetPath(input) == ControlsPath
                && AssetDatabase.GetAssetPath(settings) == SettingsPath;
        }

        private static void WorldSprite(
            string path,
            string sprite,
            Vector2 position,
            Vector2 size,
            Color color,
            int order
        )
        {
            bool exists = NodeExists(path);
            var renderer = Component<SpriteRenderer>(path);

            if (exists && renderer.sprite)
                return;

            renderer.sprite = Sprite(sprite);
            renderer.color = color;
            renderer.sortingOrder = order;
            renderer.transform.position = position;
            renderer.transform.localScale = new Vector3(size.x, size.y, 1);
        }

        private static Sprite UpgradeSprite(Sprite current, string legacy, string upgraded) =>
            !current
            || AssetDatabase.GetAssetPath(current) == Root + "Art/Sprites/" + legacy + ".png"
                ? Sprite(upgraded)
                : current;

        private static SpriteRenderer AccentSprite(
            string path,
            string sprite,
            float size,
            Color color,
            int order
        )
        {
            bool exists = NodeExists(path);
            var renderer = Component<SpriteRenderer>(path);

            if (!exists)
            {
                renderer.sprite = Sprite(sprite);
                renderer.color = color;
                renderer.sortingOrder = order;
                renderer.transform.localPosition = Vector3.zero;
                renderer.transform.localScale = new Vector3(size, size, 1);
            }

            return renderer;
        }

        private static bool NodeExists(string path)
        {
            string[] parts = path.Split('/');
            var root = scene.GetRootGameObjects().FirstOrDefault(o => o.name == parts[0]);

            return root
                && (
                    parts.Length == 1
                    || root.transform.Find(string.Join("/", parts.Skip(1))) != null
                );
        }

        private static GameUI CreateOrRepairUI()
        {
            bool exists = NodeExists("Canvas");
            var canvas = Component<Canvas>("Canvas", true);

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            canvas.gameObject.SetActive(true);

            canvas.enabled = true;

            var scaler = Component<CanvasScaler>("Canvas", true);

            if (!exists)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720);
                scaler.matchWidthOrHeight = .5f;
            }

            var raycaster = Component<GraphicRaycaster>("Canvas", true);

            raycaster.enabled = true;
            raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;

            var ui = Component<GameUI>("Canvas", true);

            ui.enabled = true;

            foreach (
                string name in new[]
                {
                    "Menu",
                    "Help",
                    "Credits",
                    "Pause",
                    "Result",
                    "ConfigurationError",
                }
            )
            {
                string path = "Canvas/" + name + "Panel";
                bool present = NodeExists(path);
                var image = Component<Image>(path, true);

                image.raycastTarget = false;

                if (!present)
                {
                    Stretch((RectTransform)image.transform);

                    image.color = new Color(.018f, .035f, .065f, .96f);
                }

                if (image.color == new Color(.018f, .035f, .065f, .96f))
                    image.color = new Color(.008f, .016f, .035f, name == "Menu" ? .58f : .86f);

                // Menu text must not share its backdrop with the arena's bright borders.
                if (name == "Menu")
                {
                    var color = image.color;

                    color.a = 1;
                    image.color = color;
                }

                Assign(
                    ui,
                    name == "ConfigurationError"
                        ? "errorPanel"
                        : char.ToLowerInvariant(name[0]) + name.Substring(1) + "Panel",
                    image.gameObject
                );
            }

            var hud = Node("Canvas/HUDPanel", true);

            Stretch((RectTransform)hud.transform);
            Assign(ui, "hudPanel", hud);

            // Reuse the old node when repairing existing scenes, but retire the frame:
            // its top/bottom edges intersected Eyebrow and Footer text rectangles.
            Decoration(
                "Canvas/MenuPanel/ConsoleFrame",
                "ConsoleFrame",
                new Vector2(0, -20),
                new Vector2(820, 500),
                Color.white
            ).enabled = false;

            Decoration(
                "Canvas/MenuPanel/AmbientGlow",
                "EnergyGlow",
                new Vector2(0, 180),
                new Vector2(1100, 600),
                new Color(.15f, .65f, .8f, .45f)
            );
            Decoration(
                "Canvas/MenuPanel/EmblemGlow",
                "EnergyGlow",
                new Vector2(0, 285),
                new Vector2(140, 140),
                Cyan
            );
            Decoration(
                "Canvas/MenuPanel/EmblemHalo",
                "ReactorHalo",
                new Vector2(0, 285),
                new Vector2(80, 80),
                Cyan
            );
            Decoration(
                "Canvas/MenuPanel/Emblem",
                "ReactorCore",
                new Vector2(0, 285),
                new Vector2(56, 56),
                Color.white
            );

            var hudFrame = Decoration(
                "Canvas/HUDPanel/TelemetryFrame",
                "Panel",
                Vector2.zero,
                Vector2.zero,
                new Color(.008f, .02f, .04f, .9f)
            );
            var hudRect = (RectTransform)hudFrame.transform;

            hudRect.anchorMin = new Vector2(0, 1);
            hudRect.anchorMax = Vector2.one;
            hudRect.offsetMin = new Vector2(16, -93);
            hudRect.offsetMax = new Vector2(-16, -8);

            var progress = Decoration(
                "Canvas/HUDPanel/TimeProgress",
                "Panel",
                Vector2.zero,
                Vector2.zero,
                Cyan
            );
            var progressRect = (RectTransform)progress.transform;

            progressRect.anchorMin = new Vector2(0, 1);
            progressRect.anchorMax = Vector2.one;
            progressRect.offsetMin = new Vector2(16, -97);
            progressRect.offsetMax = new Vector2(-16, -94);
            progress.type = Image.Type.Filled;
            progress.fillMethod = Image.FillMethod.Horizontal;
            progress.fillOrigin = 0;
            progress.fillAmount = 0;

            Text(
                "Canvas/MenuPanel/Eyebrow",
                "PROTOCOLO DE SOBREVIVÊNCIA // 01",
                18,
                new Vector2(0, 235),
                new Vector2(900, 40),
                Cyan
            );
            Text(
                "Canvas/MenuPanel/Title",
                "NÚCLEO INSTÁVEL",
                66,
                new Vector2(0, 155),
                new Vector2(1120, 100),
                Color.white
            );
            Text(
                "Canvas/MenuPanel/Description",
                "Atravesse as brechas. Preserve o drone. Sobreviva por 60 segundos.",
                23,
                new Vector2(0, 75),
                new Vector2(1080, 60),
                Color.white
            );
            Assign(
                ui,
                "playButton",
                Button("Canvas/MenuPanel/Play", "JOGAR", -30, ui.OnPlayClicked)
            );
            Assign(
                ui,
                "helpButton",
                Button("Canvas/MenuPanel/Help", "COMO JOGAR", -100, ui.OpenHelp)
            );
            Assign(
                ui,
                "creditsButton",
                Button("Canvas/MenuPanel/Credits", "CRÉDITOS", -170, ui.OpenCredits)
            );
            Text(
                "Canvas/MenuPanel/Footer",
                "WASD / SETAS   ·   SHIFT: PRECISÃO   ·   P: PAUSA\nControle: pressione um botão com o jogo em foco.",
                17,
                new Vector2(0, -285),
                new Vector2(1100, 70),
                Cyan
            );
            Text(
                "Canvas/MenuPanel/SystemStatus",
                "SISTEMA ARMADO   /   AMEAÇA ELEVADA",
                15,
                new Vector2(0, -235),
                new Vector2(720, 30),
                Cyan
            );
            Text(
                "Canvas/HelpPanel/Title",
                "COMO JOGAR",
                44,
                new Vector2(0, 225),
                new Vector2(1000, 80),
                Cyan
            );
            Text(
                "Canvas/HelpPanel/Body",
                "Sobreviva por 60 segundos. O drone não atira.\n\nWASD / setas ou analógico esquerdo: mover\nShift / LB: precisão (a hitbox não muda)\nP / Esc / Start: pausar\n\nO centro claro é sua hitbox. Você tem três vidas.\nApós um acerto, há um segundo de proteção.\n\nMenus: setas / direcional, Enter / A para confirmar.\nBackspace / B: voltar. No navegador, prefira P para pausar.\nPressione um botão do controle com o jogo em foco.\nSe o áudio não iniciar, clique na página e tente novamente.",
                23,
                new Vector2(0, 0),
                new Vector2(1100, 380),
                Color.white
            );
            Assign(
                ui,
                "helpBackButton",
                Button("Canvas/HelpPanel/Back", "VOLTAR", -260, ui.CloseHelp)
            );
            Text(
                "Canvas/CreditsPanel/Title",
                "CRÉDITOS",
                44,
                new Vector2(0, 180),
                new Vector2(1000, 80),
                Cyan
            );
            Text(
                "Canvas/CreditsPanel/Body",
                "NÚCLEO INSTÁVEL\n\nArte geométrica e áudio procedural.\n\nUnity · Input System · TextMesh Pro / Liberation Sans\nCréditos no README.",
                25,
                new Vector2(0, 0),
                new Vector2(1080, 300),
                Color.white
            );
            Assign(
                ui,
                "creditsBackButton",
                Button("Canvas/CreditsPanel/Back", "VOLTAR", -230, ui.CloseCredits)
            );
            Text(
                "Canvas/PausePanel/Title",
                "PAUSADO",
                52,
                new Vector2(0, 175),
                new Vector2(1000, 90),
                Cyan
            );
            Assign(
                ui,
                "resumeButton",
                Button("Canvas/PausePanel/Resume", "CONTINUAR", 30, ui.OnResumeClicked)
            );
            Button("Canvas/PausePanel/Restart", "REINICIAR", -45, ui.OnRetryClicked);
            Button("Canvas/PausePanel/Menu", "MENU", -120, ui.OnMenuClicked);
            Assign(
                ui,
                "resultTitle",
                Text(
                    "Canvas/ResultPanel/Title",
                    "CONTENÇÃO CONCLUÍDA",
                    46,
                    new Vector2(0, 215),
                    new Vector2(1150, 100),
                    Cyan
                )
            );
            Assign(
                ui,
                "resultText",
                Text(
                    "Canvas/ResultPanel/Body",
                    "",
                    28,
                    new Vector2(0, 35),
                    new Vector2(1000, 230),
                    Color.white
                )
            );
            Assign(
                ui,
                "retryButton",
                Button("Canvas/ResultPanel/Retry", "TENTAR NOVAMENTE", -145, ui.OnRetryClicked)
            );
            Button("Canvas/ResultPanel/Menu", "MENU", -220, ui.OnMenuClicked);
            Text(
                "Canvas/ConfigurationErrorPanel/Title",
                "CONFIGURAÇÃO INCOMPLETA",
                40,
                new Vector2(0, 170),
                new Vector2(1100, 100),
                Cyan
            );
            Assign(
                ui,
                "errorText",
                Text(
                    "Canvas/ConfigurationErrorPanel/Body",
                    "",
                    22,
                    Vector2.zero,
                    new Vector2(1050, 260),
                    Color.white
                )
            );
            Assign(
                ui,
                "errorBackButton",
                Button("Canvas/ConfigurationErrorPanel/Back", "VOLTAR", -240, ui.CloseError)
            );
            Assign(
                ui,
                "timeText",
                HudText("Time", "", new Vector2(-440, -27), new Vector2(360, 38))
            );
            Assign(
                ui,
                "scoreText",
                HudText("Score", "", new Vector2(-120, -27), new Vector2(250, 38))
            );
            Assign(
                ui,
                "healthText",
                HudText("Health", "", new Vector2(230, -27), new Vector2(360, 38))
            );
            Assign(
                ui,
                "phaseText",
                HudText("Phase", "", new Vector2(-390, -65), new Vector2(460, 32))
            );
            Assign(
                ui,
                "phaseNotice",
                HudText("Notice", "", new Vector2(0, -138), new Vector2(900, 50))
            );

            var pause = Button("Canvas/HUDPanel/Pause", "II  PAUSA", 0, ui.OnPauseClicked);
            var rect = (RectTransform)pause.transform;

            rect.anchorMin = rect.anchorMax = new Vector2(1, 1);
            rect.anchoredPosition = new Vector2(-100, -32);
            rect.sizeDelta = new Vector2(160, 46);

            // Explicit panel-local navigation; no path to another screen or hidden controls.
            foreach (
                string name in new[]
                {
                    "Menu",
                    "Help",
                    "Credits",
                    "Pause",
                    "Result",
                    "ConfigurationError",
                }
            )
            {
                var buttons = Node("Canvas/" + name + "Panel", true)
                    .GetComponentsInChildren<Button>(true);

                for (int i = 0; i < buttons.Length; i++)
                    buttons[i].navigation = new Navigation
                    {
                        mode = Navigation.Mode.Explicit,
                        selectOnUp = buttons[(i + buttons.Length - 1) % buttons.Length],
                        selectOnDown = buttons[(i + 1) % buttons.Length],
                    };
            }

            foreach (
                string name in new[]
                {
                    "Help",
                    "Credits",
                    "HUD",
                    "Pause",
                    "Result",
                    "ConfigurationError",
                }
            )
                Node("Canvas/" + name + "Panel", true).SetActive(false);

            Node("Canvas/MenuPanel", true).SetActive(true);

            return ui;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static Image Decoration(
            string path,
            string sprite,
            Vector2 position,
            Vector2 size,
            Color color
        )
        {
            bool exists = NodeExists(path);
            var image = Component<Image>(path, true);

            image.raycastTarget = false;

            if (!exists)
            {
                image.sprite = Sprite(sprite);
                image.color = color;

                var rect = (RectTransform)image.transform;

                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.anchoredPosition = position;
                rect.sizeDelta = size;

                image.transform.SetAsFirstSibling();
            }

            return image;
        }

        private static TMP_Text Text(
            string path,
            string content,
            int size,
            Vector2 position,
            Vector2 dimensions,
            Color color
        )
        {
            bool exists = NodeExists(path);
            var text = Component<TextMeshProUGUI>(path, true);

            if (!text.font)
                text.font = font;

            text.raycastTarget = false;

            if (!exists)
            {
                var rt = (RectTransform)text.transform;

                rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
                rt.anchoredPosition = position;
                rt.sizeDelta = dimensions;
                text.text = content;
                text.fontSize = size;
                text.color = color;
                text.alignment = TextAlignmentOptions.Center;
                text.raycastTarget = false;
            }

            return text;
        }

        private static TMP_Text HudText(string name, string content, Vector2 position, Vector2 size)
        {
            bool exists = NodeExists("Canvas/HUDPanel/" + name);
            var text = Text("Canvas/HUDPanel/" + name, content, 20, position, size, Cyan);

            if (!exists)
            {
                var rt = (RectTransform)text.transform;

                rt.anchorMin = rt.anchorMax = new Vector2(.5f, 1);
                rt.anchoredPosition = position;
            }

            return text;
        }

        private static Button Button(string path, string label, float y, UnityAction action)
        {
            bool exists = NodeExists(path);
            var image = Component<Image>(path, true);
            var button = Component<Button>(path, true);

            if (!exists)
            {
                var rt = (RectTransform)image.transform;

                rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
                rt.anchoredPosition = new Vector2(0, y);
                rt.sizeDelta = new Vector2(420, 56);
                image.color = Color.white;
                button.targetGraphic = image;

                var colors = button.colors;

                colors.normalColor = new Color(.07f, .15f, .21f);
                colors.highlightedColor = new Color(.15f, .42f, .5f);
                colors.selectedColor = new Color(.15f, .42f, .5f);
                colors.pressedColor = new Color(.2f, .65f, .7f);
                button.colors = colors;
                image.sprite = Sprite("ButtonNormal");
                button.transition = Selectable.Transition.SpriteSwap;

                var selected = Sprite("ButtonSelected");

                button.spriteState = new SpriteState
                {
                    highlightedSprite = selected,
                    selectedSprite = selected,
                    pressedSprite = selected,
                    disabledSprite = Sprite("ButtonNormal"),
                };
            }

            Text(path + "/Label", label, 23, Vector2.zero, new Vector2(410, 50), Color.white);

            image.sprite = UpgradeSprite(image.sprite, "ButtonNormal", "ButtonFrame");

            var spriteState = button.spriteState;

            spriteState.highlightedSprite = UpgradeSprite(
                spriteState.highlightedSprite,
                "ButtonSelected",
                "ButtonFrameSelected"
            );
            spriteState.selectedSprite = UpgradeSprite(
                spriteState.selectedSprite,
                "ButtonSelected",
                "ButtonFrameSelected"
            );
            spriteState.pressedSprite = UpgradeSprite(
                spriteState.pressedSprite,
                "ButtonSelected",
                "ButtonFrameSelected"
            );
            spriteState.disabledSprite = UpgradeSprite(
                spriteState.disabledSprite,
                "ButtonNormal",
                "ButtonFrame"
            );
            button.spriteState = spriteState;

            button.gameObject.SetActive(true);

            button.enabled = true;
            button.interactable = true;
            image.enabled = true;
            image.raycastTarget = true;
            button.targetGraphic = image;

            if (button.onClick.GetPersistentEventCount() == 0)
                UnityEventTools.AddPersistentListener(button.onClick, action);
            else if (
                button.onClick.GetPersistentEventCount() != 1
                || button.onClick.GetPersistentMethodName(0) != action.Method.Name
            )
                throw new InvalidOperationException(
                    $"Callback editado preservado: {path}. Revise antes de prosseguir."
                );
            else
                // Re-register the managed callback, including its target and argument mode.
                // A matching method name alone does not imply a callable persistent event.
                UnityEventTools.RegisterPersistentListener(button.onClick, 0, action);

            button.onClick.SetPersistentListenerState(0, UnityEventCallState.RuntimeOnly);
            EditorUtility.SetDirty(button);
            EditorUtility.SetDirty(image);

            return button;
        }

        private static void ConfigurePhysicsAndPlayerSettings()
        {
            int player = LayerMask.NameToLayer("Player"),
                bullet = LayerMask.NameToLayer("EnemyBullet");

            for (int i = 0; i < 32; i++)
            {
                Physics2D.IgnoreLayerCollision(bullet, i, i != player);
                Physics2D.IgnoreLayerCollision(player, i, i != bullet);
            }

            EditorUtility.SetDirty(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/Physics2DSettings.asset")[0]
            );

            PlayerSettings.productName = "Núcleo Instável";
            PlayerSettings.companyName = "NucleoInstavel";
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;

            var ps = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]
            );
            var handler = ps.FindProperty("activeInputHandler");

            if (handler == null)
                handler = ps.FindProperty("m_ActiveInputHandler");

            if (handler == null)
                throw new InvalidOperationException(
                    "Defina Active Input Handling como Input System Package (New) em Player Settings e repita."
                );

            if (handler.intValue == 0)
            {
                handler.intValue = 1;

                ps.ApplyModifiedPropertiesWithoutUndo();
                Debug.LogWarning(
                    "Input System ativado: reinicie o Editor antes de jogar/testar/buildar."
                );
            }
        }

        private static void EnsureBuildScene()
        {
            var entries = EditorBuildSettings.scenes.ToList();
            int i = entries.FindIndex(s => s.path == ScenePath);

            if (i < 0)
                entries.Add(new EditorBuildSettingsScene(ScenePath, true));
            else
                entries[i].enabled = true;

            EditorBuildSettings.scenes = entries.ToArray();
        }

        [MenuItem("Tools/Nucleo Instavel/Diagnose UI Input (Play Mode)")]
        public static void DiagnoseUIInput()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.Log(
                    "Diagnóstico de entrada: entre no modo Play e reproduza a falha antes de executar este comando."
                );

                return;
            }

            var active = SceneManager.GetActiveScene();
            var all = active
                .GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true))
                .ToArray();
            var session = all.OfType<GameSession>().FirstOrDefault();
            var ui = all.OfType<GameUI>().FirstOrDefault();
            var events = all.OfType<EventSystem>().FirstOrDefault();
            var module = all.OfType<InputSystemUIInputModule>().FirstOrDefault();
            var lines = new List<string>
            {
                $"Diagnóstico UI — cena {active.path}; Editor pausado={EditorApplication.isPaused}; foco da aplicação={Application.isFocused}; timeScale={Time.timeScale}",
                $"Input System {PackageInfo.FindForAssembly(typeof(InputAction).Assembly)?.version}; updateMode={InputSystem.settings.updateMode}; editorInput={InputSystem.settings.editorInputBehaviorInPlayMode}; background={InputSystem.settings.backgroundBehavior}",
                session
                    ? $"GameSession ativa={session.isActiveAndEnabled}; inicializada={session.IsInitialized}; estado={session.State}"
                    : "GameSession ausente",
                events
                    ? $"EventSystems={all.OfType<EventSystem>().Count()}; ativo={events.isActiveAndEnabled}; current correto={EventSystem.current == events}; módulo atual={events.currentInputModule}; navegação={events.sendNavigationEvents}; selecionado={events.currentSelectedGameObject}"
                    : "EventSystem ausente",
                module
                    ? $"Módulo ativo={module.isActiveAndEnabled}; asset={AssetDatabase.GetAssetPath(module.actionsAsset)}"
                    : "InputSystemUIInputModule ausente",
            };

            if (ui)
            {
                lines.Add(ui.ValidateBindings(out var error) ? "Referências UI válidas" : error);

                var button =
                    new SerializedObject(ui).FindProperty("playButton").objectReferenceValue
                    as Button;

                if (button)
                {
                    lines.Add(
                        $"JOGAR ativo={button.isActiveAndEnabled}; interagível={button.IsInteractable()}; callbacks={button.onClick.GetPersistentEventCount()}"
                    );

                    for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                        lines.Add(
                            $"  callback {i}: {button.onClick.GetPersistentTarget(i)}.{button.onClick.GetPersistentMethodName(i)}; estado={button.onClick.GetPersistentListenerState(i)}"
                        );
                }
            }

            if (module && module.actionsAsset)
                foreach (string name in new[] { "Point", "Click", "Navigate", "Submit" })
                {
                    var action = module.actionsAsset.FindAction("UI/" + name);

                    lines.Add(
                        action == null
                            ? $"UI/{name} ausente"
                            : $"UI/{name}: habilitada={action.enabled}; fase={action.phase}; controles={string.Join(", ", action.controls.Select(c => c.path))}"
                    );
                }

            foreach (var device in InputSystem.devices)
                lines.Add(
                    $"Dispositivo {device.deviceId}: {device.path}; layout={device.layout}; habilitado={device.enabled}"
                );

            Debug.Log(string.Join("\n", lines), ui);
        }

        [MenuItem("Tools/Nucleo Instavel/Validate Project")]
        public static void ValidateProject()
        {
            if (!File.Exists(ScenePath))
                throw new InvalidOperationException(
                    "Execute Create or Repair Project para gerar Game.unity."
                );

            var current = SceneManager.GetActiveScene();

            if (current.path != ScenePath && current.isDirty)
                throw new InvalidOperationException("Salve sua cena antes de validar Game.unity.");

            scene = current.path == ScenePath ? current : EditorSceneManager.OpenScene(ScenePath);

            var all = scene
                .GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true))
                .ToArray();
            var session = all.OfType<GameSession>().Single();

            if (!session.ValidateSetup(out var error))
                throw new InvalidOperationException(error);

            if (!all.OfType<ArenaPresentation>().Single().ValidateSetup(out error))
                throw new InvalidOperationException(error);

            foreach (var behaviour in all)
            {
                if (!behaviour)
                    throw new InvalidOperationException("Há script ausente na cena.");

                if (behaviour.GetType().Namespace != "NucleoInstavel")
                    continue;

                var iterator = new SerializedObject(behaviour).GetIterator();

                while (iterator.NextVisible(true))
                    if (
                        iterator.propertyType == SerializedPropertyType.ObjectReference
                        && !iterator.objectReferenceValue
                    )
                        throw new InvalidOperationException(
                            $"Referência ausente: {behaviour.name}.{iterator.propertyPath}"
                        );
            }

            if (
                all.OfType<EventSystem>().Count() != 1
                || all.OfType<InputSystemUIInputModule>().Count() != 1
                || all.OfType<BaseInputModule>().Count() != 1
                || all.OfType<BulletEmitter>().Count() != 2
            )
                throw new InvalidOperationException(
                    "Exija um EventSystem, um módulo novo de UI e dois emissores."
                );

            if (
                scene
                    .GetRootGameObjects()
                    .Sum(r => r.GetComponentsInChildren<AudioListener>(true).Length) != 1
            )
                throw new InvalidOperationException("Exija uma única AudioListener.");

            var controls = LoadInputActions(ControlsPath);

            ValidateUIInputModule(all.OfType<InputSystemUIInputModule>().Single(), controls);

            foreach (var button in all.OfType<Button>())
                if (
                    !button.enabled
                    || !button.interactable
                    || !button.targetGraphic
                    || !button.targetGraphic.raycastTarget
                    || button.onClick.GetPersistentEventCount() != 1
                    || button.onClick.GetPersistentTarget(0) != all.OfType<GameUI>().Single()
                    || button.onClick.GetPersistentListenerState(0)
                        != UnityEventCallState.RuntimeOnly
                )
                    throw new InvalidOperationException(
                        $"Botão sem um callback válido: {button.name}"
                    );

            foreach (var text in all.OfType<TMP_Text>())
            {
                if (!text.font || !text.font.material || !text.font.material.shader)
                    throw new InvalidOperationException(
                        $"Texto sem fonte/material/shader: {text.name}"
                    );

                foreach (
                    char character in text.text
                        + "Contenção concluída Núcleo Instável Sobrecarga Precisão Fase - 0123456789[]+:/"
                )
                    if (!char.IsWhiteSpace(character) && !text.font.HasCharacter((int)character))
                        throw new InvalidOperationException(
                            $"Fonte {text.font.name}: falta o caractere '{character}'. Inclua-o no Font Asset Creator/TMP antes da build."
                        );
            }

            int playerLayer = LayerMask.NameToLayer("Player"),
                bulletLayer = LayerMask.NameToLayer("EnemyBullet");

            if (
                playerLayer < 0
                || bulletLayer < 0
                || Physics2D.GetIgnoreLayerCollision(playerLayer, bulletLayer)
                || !Physics2D.GetIgnoreLayerCollision(bulletLayer, bulletLayer)
            )
                throw new InvalidOperationException(
                    "Confira as camadas Player/EnemyBullet e a matriz de colisão 2D."
                );

            var bullet = AssetDatabase.LoadAssetAtPath<GameObject>(
                Root + "Prefabs/EnemyBullet.prefab"
            );

            if (
                !bullet
                || bullet.activeSelf
                || bullet.transform.localScale != Vector3.one
                || !bullet.GetComponent<CircleCollider2D>().isTrigger
                || bullet.GetComponent<Rigidbody2D>().bodyType != RigidbodyType2D.Dynamic
                || !Mathf.Approximately(bullet.GetComponent<CircleCollider2D>().radius, .06f)
            )
                throw new InvalidOperationException(
                    "Prefab EnemyBullet precisa estar inativo, com corpo Dynamic e trigger de raio .06."
                );

            Debug.Log(
                "Núcleo Instável: validação estrutural do Editor concluída. Isto não substitui testes PlayMode ou WebGL."
            );
        }
    }
}
