using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Veilbreak
{
    public sealed class RuntimeHUD : MonoBehaviour
    {
        public ArenaGameManager Game { get; set; }
        public GameInput InputSource { get; set; }
        RectTransform safe;
        GameObject gameplay, modal, resume;
        Text healthLabel, bossLabel, realityLabel, shiftLabel, hint, heading, description, playLabel;
        Image healthFill, bossFill, flash;
        string message;
        float messageUntil, flashUntil, previousHealth;
        Font font;
        Sprite whiteSprite;
        static readonly Color Panel = new Color(.025f, .05f, .075f, .94f);
        static readonly Color Cyan = new Color(.08f, .9f, 1);
        public void Initialize()
        {
            whiteSprite = Sprite.Create(Texture2D.whiteTexture, new UnityEngine.Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), Vector2.one * .5f);
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = 1;
            gameObject.AddComponent<GraphicRaycaster>();
            safe = Rect("Safe Area", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            safe.gameObject.AddComponent<SafeAreaPanel>();
            gameplay = Rect("Gameplay", safe, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            RectTransform g = (RectTransform)gameplay.transform;
            var look = Image("Camera drag", g, new Vector2(.42f, 0), Vector2.one, Vector2.zero, Vector2.zero, Color.clear, true);
            look.raycastTarget = Application.isMobilePlatform; // Do not swallow desktop LMB attacks on empty screen.
            var lookArea = look.gameObject.AddComponent<TouchLookArea>();
            lookArea.InputSource = InputSource; InputSource.LookArea = lookArea;
            var playerBar = Bar(g, new Vector2(0, 1), new Vector2(190, -55), new Vector2(300, 24), Cyan, out healthFill);
            healthLabel = Label("Player health", playerBar, "", 19, new Vector2(.5f, .5f), new Vector2(0, 30), new Vector2(300, 30));
            var bossBar = Bar(g, new Vector2(.5f, 1), new Vector2(0, -55), new Vector2(460, 22), new Color(1, .28f, .37f), out bossFill);
            bossLabel = Label("Guardian health", bossBar, "VEIL GUARDIAN", 21, new Vector2(.5f, .5f), new Vector2(0, 30), new Vector2(500, 30));
            realityLabel = Label("Reality", g, "LIVING VEIL", 22, new Vector2(1, 1), new Vector2(-200, -45), new Vector2(260, 45));
            Button(g, "II", new Vector2(1, 1), new Vector2(-53, -48), new Vector2(66, 60), Game.TogglePause);
            var joyImage = Image("Movement joystick", g, Vector2.zero, Vector2.zero, new Vector2(45, 40), new Vector2(245, 240), new Color(.07f, .14f, .2f, .65f), true);
            var joy = joyImage.gameObject.AddComponent<VirtualJoystick>(); InputSource.Joystick = joy;
            var knob = Image("Thumb", joyImage.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-28, -28), new Vector2(28, 28), new Color(.55f, .72f, .8f, .8f), false);
            joy.Knob = knob.rectTransform;
            Action(g, "ATTACK", new Vector2(-95, 110), new Vector2(145, 135), InputSource.Attack, out _);
            Action(g, "DODGE", new Vector2(-263, 95), new Vector2(135, 110), InputSource.Dodge, out _);
            Action(g, "SHIFT", new Vector2(-190, 265), new Vector2(150, 130), InputSource.Shift, out shiftLabel);
            hint = Label("Combat guidance", g, "", 23, new Vector2(.5f, 1), new Vector2(0, -110), new Vector2(950, 55));
            flash = Image("Damage feedback", safe, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear, false);
            modal = Image("Menu overlay", safe, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(.015f, .03f, .05f, .94f), true).gameObject;
            heading = Label("Heading", modal.transform, "VEILBREAK", 55, new Vector2(.5f, .5f), new Vector2(0, 220), new Vector2(1000, 90));
            description = Label("Description", modal.transform, "", 24, new Vector2(.5f, .5f), new Vector2(0, 115), new Vector2(1100, 110));
            resume = Button(modal.transform, "RESUME", new Vector2(.5f, .5f), new Vector2(0, 15), new Vector2(390, 65), Game.TogglePause).gameObject;
            var play = Button(modal.transform, "START ENCOUNTER", new Vector2(.5f, .5f), new Vector2(0, -65), new Vector2(390, 65), Game.Restart);
            playLabel = play.GetComponentInChildren<Text>();
            Button(modal.transform, "RETURN TO TITLE", new Vector2(.5f, .5f), new Vector2(0, -145), new Vector2(390, 60), Game.ReturnToTitle);
            Label("Sensitivity label", modal.transform, "CAMERA SENSITIVITY", 19, new Vector2(.5f, .5f), new Vector2(-175, -230), new Vector2(270, 40));
            var sliderBack = Image("Sensitivity", modal.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-20, -241), new Vector2(300, -219), new Color(.15f, .25f, .3f), true);
            var slider = sliderBack.gameObject.AddComponent<Slider>();
            var handle = Image("Handle", sliderBack.transform, new Vector2(.5f, 0), new Vector2(.5f, 1), new Vector2(-14, -8), new Vector2(14, 8), Cyan, true);
            slider.handleRect = handle.rectTransform; slider.targetGraphic = handle; slider.minValue = .03f; slider.maxValue = .5f;
            slider.value = Game.FollowCamera.Sensitivity; slider.onValueChanged.AddListener(v => Game.FollowCamera.Sensitivity = v);
            Label("Studio", modal.transform, "AVENOX GAMES  •  Change Reality. Control the Fight.", 20, new Vector2(.5f, 0), new Vector2(0, 40), new Vector2(1000, 45));
            Game.Changed += OnState; Game.Veil.Rejected += Message; Game.Veil.Changed += OnShift;
            Game.Player.Health.Changed += OnHealth; previousHealth = Game.Player.Health.Current;
            OnState(Game.State);
        }
        void OnDestroy()
        {
            if (whiteSprite) Destroy(whiteSprite);
            if (!Game) return;
            Game.Changed -= OnState; Game.Veil.Rejected -= Message; Game.Veil.Changed -= OnShift;
            Game.Player.Health.Changed -= OnHealth;
        }
        void OnShift(VeilState state) { Message(state == VeilState.Broken ? "BROKEN VEIL — core exposed: deal double damage" : "LIVING VEIL — guardian armor protected"); }
        void OnHealth()
        {
            if (Game.Player.Health.Current < previousHealth) flashUntil = Time.unscaledTime + .25f;
            previousHealth = Game.Player.Health.Current;
        }
        void Message(string value)
        {
            message = value; messageUntil = Time.unscaledTime + 2.6f;
            hint.text = Game.Guardian.State == GuardianAI.Phase.Telegraph ? Game.Guardian.AttackName : value;
        }
        void OnState(ArenaState state)
        {
            gameplay.SetActive(state == ArenaState.Playing); modal.SetActive(state != ArenaState.Playing);
            resume.SetActive(state == ArenaState.Paused);
            heading.text = state == ArenaState.Title ? "VEILBREAK" : state == ArenaState.Paused ? "PAUSED" : state == ArenaState.Victory ? "VICTORY" : "ENCOUNTER FAILED";
            description.text = state == ArenaState.Title ? "THE VEIL ARENA\nMove, dodge, and shift into the Broken Veil to expose the guardian's core." :
                state == ArenaState.Victory ? "The Veil Guardian is defeated." : state == ArenaState.Defeat ? "The guardian remains. Watch its warning, then dodge." : "Take a breath. The encounter is frozen.";
            playLabel.text = state == ArenaState.Title ? "START ENCOUNTER" : state == ArenaState.Paused ? "RESTART ENCOUNTER" : state == ArenaState.Victory ? "REPLAY ENCOUNTER" : "RETRY";
            if (state == ArenaState.Playing) Message("Move with the left stick / WASD. Attack nearby. SHIFT / Q exposes the core.");
            flashUntil = 0;
        }
        void Update()
        {
            if (!Game || !healthFill) return;
            healthFill.fillAmount = Game.Player.Health.Current / Game.Player.Health.Maximum;
            healthLabel.text = $"HEALTH  {Game.Player.Health.Current:0} / {Game.Player.Health.Maximum:0}";
            bossFill.fillAmount = Game.Guardian.Health.Current / Game.Guardian.Health.Maximum;
            bool broken = Game.Veil.Current == VeilState.Broken;
            realityLabel.text = broken ? "BROKEN VEIL" : "LIVING VEIL";
            realityLabel.color = broken ? new Color(.8f, .4f, 1) : Cyan;
            shiftLabel.text = Game.Veil.CooldownRemaining > 0 ? $"SHIFT\n{Game.Veil.CooldownRemaining:0.0}s" : "SHIFT\nREADY";
            // A 2.6 s tutorial/shift message must not hide a 1.3 s attack warning.
            hint.text = Game.Guardian.State == GuardianAI.Phase.Telegraph ? Game.Guardian.AttackName :
                Time.unscaledTime <= messageUntil ? message :
                ""; // No persistent AI/status commentary over the playfield. Warnings/rejections are transient.
            flash.color = new Color(1, .06f, .1f, Mathf.Clamp01((flashUntil - Time.unscaledTime) * 4) * .3f);
        }
        RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 lo, Vector2 hi)
        {
            var go = new GameObject(name, typeof(RectTransform)); var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false); rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = lo; rt.offsetMax = hi; return rt;
        }
        Image Image(string name, Transform parent, Vector2 min, Vector2 max, Vector2 lo, Vector2 hi, Color color, bool raycast)
        { var rt = Rect(name, parent, min, max, lo, hi); var img = rt.gameObject.AddComponent<Image>(); img.sprite = whiteSprite; img.color = color; img.raycastTarget = raycast; return img; }
        Text Label(string name, Transform parent, string text, int size, Vector2 anchor, Vector2 position, Vector2 dimensions)
        {
            var rt = Rect(name, parent, anchor, anchor, position - dimensions / 2, position + dimensions / 2);
            var label = rt.gameObject.AddComponent<Text>(); label.font = font; label.text = text; label.fontSize = size;
            label.alignment = TextAnchor.MiddleCenter; label.color = Color.white; label.raycastTarget = false; return label;
        }
        RectTransform Bar(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color, out Image fill)
        {
            var bg = Image("Health bar", parent, anchor, anchor, pos - size / 2, pos + size / 2, new Color(.16f, .19f, .23f), false);
            fill = Image("Fill", bg.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, color, false);
            fill.type = UnityEngine.UI.Image.Type.Filled; fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal; return bg.rectTransform;
        }
        UnityEngine.UI.Button Button(Transform parent, string title, Vector2 anchor, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction callback)
        {
            var image = Image(title, parent, anchor, anchor, pos - size / 2, pos + size / 2, new Color(.08f, .24f, .29f, .95f), true);
            var button = image.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image; button.onClick.AddListener(callback);
            Label("Label", image.transform, title, 24, new Vector2(.5f, .5f), Vector2.zero, size); return button;
        }
        void Action(Transform parent, string title, Vector2 pos, Vector2 size, System.Action callback, out Text label)
        {
            var image = Image(title, parent, new Vector2(1, 0), new Vector2(1, 0), pos - size / 2, pos + size / 2, Panel, true);
            image.gameObject.AddComponent<ActionButton>().Pressed = callback;
            var outline = image.gameObject.AddComponent<Outline>(); outline.effectColor = Cyan; outline.effectDistance = new Vector2(2, -2);
            label = Label("Label", image.transform, title, 25, new Vector2(.5f, .5f), Vector2.zero, size);
        }
    }
}
