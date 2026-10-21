using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Veilbreak
{
    public sealed class VeilbreakBootstrap : MonoBehaviour
    {
        [SerializeField] int targetFrameRate = 60;
        PrimitiveFactory art;
        void Awake()
        {
            Application.targetFrameRate = targetFrameRate;
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            art = new PrimitiveFactory();
            Build();
        }
        void OnDestroy() => art?.Dispose();
        void Build()
        {
            var stone = art.Material("Warm limestone", new Color(.4f, .38f, .33f));
            var pale = art.Material("Cut stone edges", new Color(.58f, .57f, .51f));
            var dark = art.Material("Layered midnight armor", new Color(.045f, .065f, .09f));
            var metal = art.Material("Guardian bronze armor", new Color(.28f, .27f, .23f));
            var skin = art.Material("Skin placeholder", new Color(.68f, .44f, .32f));
            var energy = art.Material("Shared Veil energy", new Color(.05f, .9f, 1), true);
            var cyan = art.Material("Living route", new Color(.04f, .65f, .75f), true);
            var violet = art.Material("Broken route", new Color(.5f, .12f, .8f), true);
            var warning = art.Material("Attack warning", new Color(1, .24f, .05f), true);
            var world = art.Group("The Veil Arena - procedural blockout", transform, Vector3.zero);
            art.Shape("Shared courtyard foundation", world, PrimitiveType.Cube, new Vector3(0, -.5f, 0), new Vector3(32, 1, 32), stone, true);
            art.Shape("Combat dais", world, PrimitiveType.Cylinder, new Vector3(0, -.03f, 1), new Vector3(23, .05f, 23), pale, true);
            // Inlaid radial marks define the central fighting space without adding colliders.
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI / 12;
                var mark = art.Shape("Arena inlay", world, PrimitiveType.Cube, new Vector3(Mathf.Sin(angle) * 10.5f, .035f, 1 + Mathf.Cos(angle) * 10.5f), new Vector3(.12f, .025f, 1), energy);
                mark.localRotation = Quaternion.Euler(0, i * 15, 0);
            }
            for (int i = 0; i < 4; i++)
            {
                var side = art.Group("Courtyard perimeter", world, Vector3.zero); side.localRotation = Quaternion.Euler(0, i * 90, 0);
                art.Shape("Boundary stone", side, PrimitiveType.Cube, new Vector3(0, 1.1f, 16), new Vector3(33, 2.2f, .8f), stone, true);
            }
            for (int i = 0; i < 8; i++)
            {
                float angle = (i * 45 + 22.5f) * Mathf.Deg2Rad;
                var column = art.Group("Ruined column", world, new Vector3(Mathf.Sin(angle) * 13.8f, 0, Mathf.Cos(angle) * 13.8f));
                art.Shape("Plinth", column, PrimitiveType.Cube, new Vector3(0, .25f, 0), new Vector3(1.7f, .5f, 1.7f), pale, true);
                art.Shape("Shaft", column, PrimitiveType.Cylinder, new Vector3(0, 2.5f, 0), new Vector3(1, 2.3f, 1), stone, true);
                art.Shape("Capital", column, PrimitiveType.Cube, new Vector3(0, 4.9f, 0), new Vector3(1.6f, .5f, 1.6f), pale, true);
                var crystal = art.Shape("Veil crystal", column, PrimitiveType.Cube, new Vector3(0, 5.65f, 0), new Vector3(.45f, 1, .45f), energy);
                crystal.localRotation = Quaternion.Euler(0, 45, 18);
            }
            // North arch is the boss landmark; south arch marks the spawn.
            foreach (int z in new[] { -14, 14 })
            {
                art.Shape("Arch left", world, PrimitiveType.Cube, new Vector3(-3, 3, z), new Vector3(1, 6, 1.4f), pale, true);
                art.Shape("Arch right", world, PrimitiveType.Cube, new Vector3(3, 3, z), new Vector3(1, 6, 1.4f), pale, true);
                art.Shape("Arch lintel", world, PrimitiveType.Cube, new Vector3(0, 6.2f, z), new Vector3(7, 1, 1.4f), stone, true);
            }
            var reactive = new List<VeilReactiveObject>();
            AddReactive(world, "Living cover / route gate", new Vector3(-7, 1, 1), new Vector3(1.4f, 2, 5), cyan, VeilState.Living, reactive);
            AddReactive(world, "Broken cover / route gate", new Vector3(7, 1, 1), new Vector3(1.4f, 2, 5), violet, VeilState.Broken, reactive);
            AddReactive(world, "Living step platform", new Vector3(-10, .2f, -5), new Vector3(4, .4f, 4), cyan, VeilState.Living, reactive);
            AddReactive(world, "Broken step platform", new Vector3(10, .2f, 6), new Vector3(4, .4f, 4), violet, VeilState.Broken, reactive);
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
            var visual = art.Group("Warrior silhouette", playerRoot, Vector3.zero);
            art.Shape("Coat torso", visual, PrimitiveType.Capsule, new Vector3(0, 1, 0), new Vector3(.65f, .5f, .4f), dark);
            art.Shape("Head", visual, PrimitiveType.Sphere, new Vector3(0, 1.65f, 0), new Vector3(.4f, .45f, .4f), skin);
            art.Shape("Dark swept hair", visual, PrimitiveType.Sphere, new Vector3(0, 1.84f, -.04f), new Vector3(.48f, .28f, .46f), dark);
            for (int i = 0; i < 3; i++)
            {
                var hair = art.Shape("Hair tuft", visual, PrimitiveType.Cube, new Vector3(-.18f + i * .15f, 1.97f, -.08f), new Vector3(.12f, .24f, .16f), dark);
                hair.localRotation = Quaternion.Euler(-20, 0, -25);
            }
            art.Shape("Scarf", visual, PrimitiveType.Cube, new Vector3(0, 1.43f, -.12f), new Vector3(.65f, .22f, .48f), dark);
            art.Shape("Split coat left", visual, PrimitiveType.Cube, new Vector3(-.2f, .55f, -.18f), new Vector3(.27f, .7f, .15f), dark);
            art.Shape("Split coat right", visual, PrimitiveType.Cube, new Vector3(.2f, .55f, -.18f), new Vector3(.27f, .7f, .15f), dark);
            foreach (float x in new[] { -.2f, .2f })
                art.Shape("Armored leg", visual, PrimitiveType.Capsule, new Vector3(x, .36f, 0), new Vector3(.22f, .36f, .26f), dark);
            art.Shape("Shoulder energy core", visual, PrimitiveType.Sphere, new Vector3(-.4f, 1.35f, 0), Vector3.one * .3f, energy);
            var blade = art.Group("Blade swing", visual, new Vector3(.48f, 1, .05f));
            art.Shape("Veil blade", blade, PrimitiveType.Cube, new Vector3(0, 0, .65f), new Vector3(.1f, .13f, 1.5f), energy);
            var bossRoot = art.Group("Veil Guardian (placeholder)", transform, new Vector3(0, .1f, 5));
            var bossBody = bossRoot.gameObject.AddComponent<CharacterController>(); bossBody.height = 4.7f; bossBody.center = Vector3.up * 2.35f;
            bossBody.radius = 1.15f; bossBody.stepOffset = .45f;
            bossRoot.gameObject.AddComponent<HealthComponent>().Configure(540);
            var boss = bossRoot.gameObject.AddComponent<GuardianAI>();
            var bossVisual = art.Group("Mechanical guardian", bossRoot, Vector3.zero);
            art.Shape("Torso", bossVisual, PrimitiveType.Cube, new Vector3(0, 3, 0), new Vector3(2.5f, 1.9f, 1.3f), metal);
            art.Shape("Head", bossVisual, PrimitiveType.Cube, new Vector3(0, 4.4f, .1f), new Vector3(1.2f, .8f, 1), dark);
            art.Shape("Visor", bossVisual, PrimitiveType.Cube, new Vector3(0, 4.4f, .63f), new Vector3(.8f, .13f, .1f), energy);
            foreach (float x in new[] { -1f, 1f })
            {
                art.Shape("Heavy leg", bossVisual, PrimitiveType.Cube, new Vector3(x * .75f, 1.1f, 0), new Vector3(.85f, 2.2f, 1), metal);
                art.Shape("Shoulder", bossVisual, PrimitiveType.Sphere, new Vector3(x * 1.65f, 3.6f, 0), Vector3.one * 1.2f, metal);
                art.Shape("Arm", bossVisual, PrimitiveType.Cube, new Vector3(x * 1.8f, 2.5f, 0), new Vector3(.85f, 1.7f, .9f), dark);
                art.Shape("Fist", bossVisual, PrimitiveType.Cube, new Vector3(x * 1.8f, 1.6f, .1f), Vector3.one, metal);
            }
            boss.Core = art.Shape("Exposed core", bossVisual, PrimitiveType.Sphere, new Vector3(0, 3, .8f), Vector3.one, energy);
            boss.ArmorPlate = art.Shape("Intact core armor", bossVisual, PrimitiveType.Cube, new Vector3(0, 3, 1.02f), new Vector3(1.15f, 1.2f, .25f), metal);
            boss.SlamMarker = art.Shape("Ground slam warning disc", bossRoot, PrimitiveType.Cylinder, Vector3.zero, Vector3.one, warning);
            boss.StrikeMarker = art.Shape("Energy strike warning lane", bossRoot, PrimitiveType.Cube, Vector3.zero, Vector3.one, warning);
            boss.SlamMarker.gameObject.SetActive(false); boss.StrikeMarker.gameObject.SetActive(false);
            var cameraRoot = art.Group("Main Camera", transform, Vector3.zero); cameraRoot.tag = "MainCamera";
            var camera = cameraRoot.gameObject.AddComponent<Camera>(); camera.nearClipPlane = .1f; camera.farClipPlane = 90; camera.fieldOfView = 60;
            cameraRoot.gameObject.AddComponent<AudioListener>();
            var follow = cameraRoot.gameObject.AddComponent<ThirdPersonCameraController>();
            player.InputSource = input; player.Game = game; player.CameraTransform = cameraRoot; player.Visual = visual; player.SetSpawn();
            var combat = playerRoot.gameObject.AddComponent<PlayerCombat>(); combat.Player = player; combat.Guardian = boss; combat.BladePivot = blade;
            boss.Player = player; boss.Game = game; boss.Veil = veil; boss.Visual = bossVisual;
            veil.Player = player; veil.Validator = safety; veil.Objects = reactive.ToArray(); veil.Sun = sun; veil.EnergyMaterial = energy;
            follow.Target = playerRoot; follow.Guardian = bossRoot; follow.InputSource = input; follow.Game = game;
            game.Player = player; game.Guardian = boss; game.Combat = combat; game.Veil = veil; game.InputSource = input; game.FollowCamera = follow;
            veil.ResetState(); boss.Initialize(); follow.ResetView(); game.Initialize();
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)); eventSystem.transform.SetParent(transform);
            var hudRoot = new GameObject("Native gameplay HUD", typeof(RectTransform)); hudRoot.transform.SetParent(transform, false);
            var hud = hudRoot.AddComponent<RuntimeHUD>(); hud.Game = game; hud.InputSource = input; hud.Initialize();
            Debug.Log("VEILBREAK: Veil Arena ready. Procedural placeholder art; device performance not measured.");
        }
        void AddReactive(Transform parent, string name, Vector3 position, Vector3 size, Material material, VeilState state, List<VeilReactiveObject> objects)
        {
            var obj = art.Shape(name, parent, PrimitiveType.Cube, position, size, material, true);
            var reactive = obj.gameObject.AddComponent<VeilReactiveObject>(); reactive.ActiveState = state; objects.Add(reactive);
        }
    }
}
