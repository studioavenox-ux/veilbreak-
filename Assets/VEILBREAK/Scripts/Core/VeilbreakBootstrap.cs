using UnityEngine;
using UnityEngine.EventSystems;

namespace Veilbreak
{
    [DisallowMultipleComponent]
    public sealed class VeilbreakBootstrap : MonoBehaviour
    {
        [SerializeField] int targetFrameRate = 60;
        PrimitiveFactory art;
        void Awake()
        {
            Application.targetFrameRate = targetFrameRate;
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            art = new PrimitiveFactory();
            try { Build(); }
            catch (System.Exception error)
            {
                Debug.LogException(error, this);
                // Disable partial construction so one setup failure does not produce per-frame null errors.
                gameObject.SetActive(false); Time.timeScale = 1;
            }
        }
        void OnDestroy() => art?.Dispose();
        void Build()
        {
            var sceneArt = new PrototypeSceneArt(art);
            var reactive = sceneArt.Courtyard(transform);
            var lightObject = new GameObject("Sun"); lightObject.transform.SetParent(transform); lightObject.transform.rotation = Quaternion.Euler(48, -30, 0);
            var sun = lightObject.AddComponent<Light>(); sun.type = LightType.Directional; sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun; RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = 28; RenderSettings.fogEndDistance = 65;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            var input = gameObject.AddComponent<GameInput>();
            var game = gameObject.AddComponent<ArenaGameManager>();
            var veil = gameObject.AddComponent<VeilStateManager>();
            var safety = gameObject.AddComponent<ShiftSafetyValidator>();
            var playerRoot = art.Group("Protagonist (placeholder)", transform, new Vector3(0, .15f, -9));
            var controller = playerRoot.gameObject.AddComponent<CharacterController>(); controller.height = 1.8f; controller.center = Vector3.up * .9f;
            controller.radius = .32f; controller.stepOffset = .45f; controller.skinWidth = .035f;
            playerRoot.gameObject.AddComponent<HealthComponent>().Configure(100);
            var player = playerRoot.gameObject.AddComponent<PlayerController>();
            var visual = sceneArt.Warrior(playerRoot, out Transform blade);
            var bossRoot = art.Group("Veil Guardian (placeholder)", transform, new Vector3(0, .1f, 5));
            bossRoot.localRotation = Quaternion.Euler(0,180,0);
            var bossBody = bossRoot.gameObject.AddComponent<CharacterController>(); bossBody.height = 4.7f; bossBody.center = Vector3.up * 2.35f;
            bossBody.radius = 1.15f; bossBody.stepOffset = .45f;
            bossRoot.gameObject.AddComponent<HealthComponent>().Configure(540);
            var boss = bossRoot.gameObject.AddComponent<GuardianAI>();
            var bossVisual = sceneArt.Guardian(bossRoot, out Transform core, out Transform armor);
            boss.Core = core; boss.ArmorPlate = armor;
            boss.SlamMarker = art.Shape("Ground slam warning disc", bossRoot, PrimitiveType.Cylinder, Vector3.zero, Vector3.one, sceneArt.Warning);
            boss.StrikeMarker = art.Shape("Energy strike warning lane", bossRoot, PrimitiveType.Cube, Vector3.zero, Vector3.one, sceneArt.Warning);
            boss.SlamMarker.gameObject.SetActive(false); boss.StrikeMarker.gameObject.SetActive(false);
            var cameraRoot = art.Group("Main Camera", transform, Vector3.zero); cameraRoot.tag = "MainCamera";
            var camera = cameraRoot.gameObject.AddComponent<Camera>(); camera.nearClipPlane = .1f; camera.farClipPlane = 90; camera.fieldOfView = 60; camera.clearFlags = CameraClearFlags.SolidColor;
            cameraRoot.gameObject.AddComponent<AudioListener>();
            var follow = cameraRoot.gameObject.AddComponent<ThirdPersonCameraController>();
            player.InputSource = input; player.Game = game; player.CameraTransform = cameraRoot; player.Visual = visual; player.SetSpawn();
            var combat = playerRoot.gameObject.AddComponent<PlayerCombat>(); combat.Player = player; combat.Guardian = boss; combat.BladePivot = blade;
            boss.Player = player; boss.Game = game; boss.Veil = veil; boss.Visual = bossVisual;
            veil.GuardianBody = bossBody; veil.Player = player; veil.Validator = safety; veil.Objects = reactive.ToArray(); veil.Sun = sun; veil.EnergyMaterial = sceneArt.Energy;
            follow.Target = playerRoot; follow.Guardian = bossRoot; follow.InputSource = input; follow.Game = game;
            game.Player = player; game.Guardian = boss; game.Combat = combat; game.Veil = veil; game.InputSource = input; game.FollowCamera = follow;
            veil.ResetState(); boss.Initialize(); follow.ResetView(); game.Initialize();
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)); eventSystem.transform.SetParent(transform);
            var hudRoot = new GameObject("Native gameplay HUD", typeof(RectTransform)); hudRoot.transform.SetParent(transform, false);
            var hud = hudRoot.AddComponent<RuntimeHUD>(); hud.Game = game; hud.InputSource = input; hud.Initialize();
            gameObject.AddComponent<EncounterAudio>().Initialize(game);
            Debug.Log("VEILBREAK: Veil Arena ready. Procedural placeholder art; device performance not measured.");
        }
    }
}
