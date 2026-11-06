using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CarDesigner.UI
{
    /// <summary>
    /// Landscape studio interface: component list (left), transform inspector with the live
    /// composite matrix (right), finishes and view actions (bottom). Built in code at startup.
    /// </summary>
    public class DesignerUI : MonoBehaviour
    {
        static readonly PartCategory[] CategoryOrder =
        {
            PartCategory.Body, PartCategory.Door, PartCategory.Wheel,
            PartCategory.Mirror, PartCategory.Glass, PartCategory.Interior
        };

        static readonly string[] AxisNames = { "X  axle", "Y  vertical", "Z  forward" };

        [SerializeField] CarSwitcher switcher;
        [SerializeField] PartSelector selector;
        [SerializeField] OrbitCamera orbit;

        CarRig rig => switcher != null ? switcher.ActiveRig : null;
        CarPaint paint => switcher != null ? switcher.ActivePaint : null;

        readonly Dictionary<CarPart, Button> partButtons = new Dictionary<CarPart, Button>();
        readonly Slider[] translation = new Slider[3];
        readonly Text[] translationValues = new Text[3];
        readonly Button[] axisButtons = new Button[3];
        readonly Text[] matrixCells = new Text[16];
        readonly List<Button> swatches = new List<Button>();

        RectTransform safeRoot;
        Rect appliedSafeArea;
        GameObject componentPanel, inspectorPanel;
        CanvasGroup inspectorControls;
        Text partTitle, partHint, statsText, finishName;
        Slider rotation, scale;
        Text rotationValue, scaleValue;
        Button designMode, showroomMode, doorsButton, explodeButton;
        readonly List<Button> carButtons = new List<Button>();
        Text dimsText;
        CarRig boundRig;
        CarPaint boundPaint;
        CarPart current;
        bool suppressCallbacks;
        float smoothedFrameTime = 1f / 60f, statsTimer;

        static CultureInfo Invariant => CultureInfo.InvariantCulture;

        void Awake()
        {
            EnsureEventSystem();
            BuildCanvas();
            BuildTopBar();
            BuildComponentPanel();
            BuildInspector();
            BuildBottomBar();
        }

        void OnEnable()
        {
            selector.SelectionChanged += OnSelectionChanged;
            if (switcher != null) switcher.SlotChanged += OnCarSwitched;
            BindCar();
        }

        void OnDisable()
        {
            selector.SelectionChanged -= OnSelectionChanged;
            if (switcher != null) switcher.SlotChanged -= OnCarSwitched;
            UnbindCar();
        }

        void BindCar()
        {
            boundRig = rig;
            boundPaint = paint;
            if (boundRig != null) boundRig.PartEdited += OnPartEdited;
            if (boundPaint != null) boundPaint.FinishChanged += OnFinishChanged;
        }

        void UnbindCar()
        {
            if (boundRig != null) boundRig.PartEdited -= OnPartEdited;
            if (boundPaint != null) boundPaint.FinishChanged -= OnFinishChanged;
            boundRig = null;
            boundPaint = null;
        }

        void Start()
        {
            SetShowroom(false);
            OnSelectionChanged(selector.Selected);
            if (paint != null) OnFinishChanged(paint.Current);
        }

        void Update()
        {
            ApplySafeArea();
            UpdateStats();
        }

        // ───────────────────────────── construction ─────────────────────────────

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        void BuildCanvas()
        {
            var canvasGo = new GameObject("Designer Canvas", typeof(RectTransform)) { layer = 5 };
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            canvasGo.AddComponent<GraphicRaycaster>();
            safeRoot = UiKit.Rect("Safe Area", canvasGo.transform).Fill();
        }

        void BuildTopBar()
        {
            RectTransform bar = UiKit.Rect("Top Bar", safeRoot)
                .Anchor(new Vector2(0f, 1f), Vector2.one, new Vector2(20f, -92f), new Vector2(-20f, -16f));
            UiKit.Row(bar, 18f, new RectOffset(4, 4, 0, 0));

            RectTransform brand = UiKit.Rect("Brand", bar);
            UiKit.Column(brand, 2f, new RectOffset(0, 0, 6, 6));
            UiKit.Size(brand, 520f);
            Text title = UiKit.Label(brand, "ATELIER <color=#E3A23B>01</color>", 30, Palette.Text, style: FontStyle.Bold);
            title.supportRichText = true;
            UiKit.Size(title, height: 36f);
            dimsText = UiKit.Label(brand, "", 16, Palette.Muted);
            UiKit.Size(dimsText, height: 22f);
            RefreshCarInfo();

            if (switcher != null && switcher.Count > 1)
            {
                for (int i = 0; i < switcher.Count; i++)
                {
                    int index = i;
                    Button car = UiKit.Button(bar, switcher.SlotName(i).ToUpperInvariant(), () => switcher.SwitchTo(index), 16);
                    UiKit.Size(car, 170f, 52f);
                    carButtons.Add(car);
                }
                RefreshCarButtons();
            }

            Spacer(bar);

            Image dot = UiKit.Surface("Live", bar, Palette.Live);
            dot.raycastTarget = false;
            UiKit.Size(dot, 10f, 10f).minWidth = 10f;
            Text session = UiKit.Label(bar, "LIVE REVIEW  ·  BENGALURU  /  DETROIT", 16, Palette.Muted);
            UiKit.Size(session, 380f);

            Spacer(bar);

            statsText = UiKit.Label(bar, "", 16, Palette.Muted, TextAnchor.MiddleRight);
            UiKit.Size(statsText, 230f);

            designMode = UiKit.Button(bar, "DESIGN", () => SetShowroom(false), 18);
            UiKit.Size(designMode, 150f, 52f);
            showroomMode = UiKit.Button(bar, "SHOWROOM", () => SetShowroom(true), 18);
            UiKit.Size(showroomMode, 170f, 52f);

            var barLayout = bar.GetComponent<HorizontalLayoutGroup>();
            barLayout.childForceExpandHeight = false;
            barLayout.childAlignment = TextAnchor.MiddleLeft;
        }

        void BuildComponentPanel()
        {
            Image panel = UiKit.Surface("Components", safeRoot, Palette.Panel);
            panel.rectTransform.Anchor(Vector2.zero, new Vector2(0f, 1f), new Vector2(20f, 132f), new Vector2(340f, -108f));
            componentPanel = panel.gameObject;
            UiKit.Column(panel, 10f, new RectOffset(18, 18, 18, 18));

            Caption(panel.transform, "COMPONENTS");
            UiKit.Divider(panel.transform);

            RectTransform viewport = UiKit.Rect("Viewport", panel.transform);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image hitArea = viewport.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            UiKit.Size(viewport).flexibleHeight = 1f;

            RectTransform content = UiKit.Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            UiKit.Column(content, 6f, new RectOffset(0, 0, 0, 8));
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            if (rig == null) return;

            foreach (PartCategory category in CategoryOrder)
            {
                bool headerAdded = false;
                foreach (CarPart part in rig.Parts)
                {
                    if (part.Category != category) continue;
                    if (!headerAdded)
                    {
                        Text header = UiKit.Label(content, CategoryLabel(category), 14, Palette.Muted, TextAnchor.LowerLeft, FontStyle.Bold);
                        UiKit.Size(header, height: 30f);
                        headerAdded = true;
                    }

                    CarPart target = part;
                    Button button = UiKit.Button(content, part.DisplayName, () => SelectFromList(target), 19);
                    Text label = button.GetComponentInChildren<Text>();
                    label.alignment = TextAnchor.MiddleLeft;
                    label.fontStyle = FontStyle.Normal;
                    label.rectTransform.offsetMin = new Vector2(16f, 0f);
                    UiKit.Size(button, height: 48f);
                    partButtons[part] = button;
                }
            }
        }

        void BuildInspector()
        {
            Image panel = UiKit.Surface("Inspector", safeRoot, Palette.Panel);
            panel.rectTransform.Anchor(new Vector2(1f, 0f), Vector2.one, new Vector2(-500f, 132f), new Vector2(-20f, -108f));
            inspectorPanel = panel.gameObject;
            UiKit.Column(panel, 8f, new RectOffset(22, 22, 20, 20));

            partTitle = UiKit.Label(panel.transform, "", 28, Palette.Text, style: FontStyle.Bold);
            UiKit.Size(partTitle, height: 36f);
            partHint = UiKit.Label(panel.transform, "", 16, Palette.Muted);
            UiKit.Size(partHint, height: 40f);
            UiKit.Divider(panel.transform);

            RectTransform controls = UiKit.Rect("Controls", panel.transform);
            UiKit.Column(controls, 8f, new RectOffset(0, 0, 4, 0));
            UiKit.Size(controls).flexibleHeight = 1f;
            inspectorControls = controls.gameObject.AddComponent<CanvasGroup>();

            Caption(controls, "TRANSLATE  ·  mm");
            for (int i = 0; i < 3; i++)
            {
                int axis = i;
                translation[i] = SliderRow(controls, "XYZ"[i].ToString(), -300f, 300f, true,
                    v => SetTranslation(axis, v), out translationValues[i]);
            }

            Caption(controls, "ROTATE  ·  degrees about");
            RectTransform axisRow = UiKit.Rect("Axis", controls);
            UiKit.Row(axisRow, 8f, new RectOffset(0, 0, 0, 0)).childForceExpandWidth = true;
            UiKit.Size(axisRow, height: 40f);
            for (int i = 0; i < 3; i++)
            {
                var axis = (Axis)i;
                axisButtons[i] = UiKit.Button(axisRow, AxisNames[i], () => SetAxis(axis), 16);
                UiKit.Size(axisButtons[i], height: 40f, flexibleWidth: 1f);
            }
            rotation = SliderRow(controls, "θ", -180f, 180f, true, SetRotation, out rotationValue);

            Caption(controls, "SCALE  ·  uniform");
            scale = SliderRow(controls, "s", 0.25f, 2.5f, false, SetScale, out scaleValue);

            RectTransform actions = UiKit.Rect("Actions", controls);
            UiKit.Row(actions, 10f, new RectOffset(0, 0, 6, 0));
            UiKit.Size(actions, height: 58f);
            Button preset = UiKit.Button(actions, "CASE PRESET  S1.5 · R45° · T+50", ApplyCasePreset, 17);
            UiKit.SetActive(preset, true);
            UiKit.Size(preset, flexibleWidth: 1f);
            Button reset = UiKit.Button(actions, "RESET", ResetSelected, 17);
            UiKit.Size(reset, 120f);

            UiKit.Divider(controls);
            Caption(controls, "COMPOSITE   M = T · R · S   (homogeneous, mm)");

            Image matrix = UiKit.Surface("Matrix", controls, Palette.Raised);
            matrix.raycastTarget = false;
            UiKit.Size(matrix, height: 4f * 30f + 3f * 4f + 16f);
            var grid = matrix.gameObject.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(14, 14, 8, 8);
            grid.cellSize = new Vector2(98f, 30f);
            grid.spacing = new Vector2(4f, 4f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.childAlignment = TextAnchor.MiddleCenter;
            for (int i = 0; i < 16; i++)
            {
                int row = i / 4, column = i % 4;
                Color tone = row == 3 ? Palette.Muted : column == 3 ? Palette.Amber : Palette.Text;
                matrixCells[i] = UiKit.Label(matrix.transform, "", 19, tone, TextAnchor.MiddleRight);
            }
        }

        void BuildBottomBar()
        {
            Image bar = UiKit.Surface("Bottom Bar", safeRoot, Palette.Panel);
            bar.rectTransform.Anchor(Vector2.zero, new Vector2(1f, 0f), new Vector2(20f, 16f), new Vector2(-20f, 116f));
            HorizontalLayoutGroup row = UiKit.Row(bar, 10f, new RectOffset(22, 18, 16, 16));
            row.childForceExpandHeight = false;
            row.childAlignment = TextAnchor.MiddleLeft;

            Text caption = UiKit.Label(bar.transform, "FINISH", 14, Palette.Muted, style: FontStyle.Bold);
            UiKit.Size(caption, 72f);

            var finishes = paint != null ? paint.Finishes : System.Array.Empty<CarPaint.Finish>() as IReadOnlyList<CarPaint.Finish>;
            for (int i = 0; i < finishes.Count; i++)
            {
                int index = i;
                Image swatch = UiKit.Surface("Swatch", bar.transform, finishes[i].color);
                var button = swatch.gameObject.AddComponent<Button>();
                button.targetGraphic = swatch;
                button.onClick.AddListener(() => paint.Apply(index));
                var outline = swatch.gameObject.AddComponent<Outline>();
                outline.effectColor = Palette.Amber;
                outline.effectDistance = new Vector2(4f, -4f);
                outline.enabled = false;
                UiKit.Size(swatch, 60f, 60f);
                swatches.Add(button);
            }

            finishName = UiKit.Label(bar.transform, "", 17, Palette.Text);
            UiKit.Size(finishName, 170f).minWidth = 120f;

            Spacer(bar.transform);

            doorsButton = UiKit.Button(bar.transform, "OPEN DOORS", ToggleDoors, 17);
            UiKit.Size(doorsButton, 180f, 60f);
            explodeButton = UiKit.Button(bar.transform, "EXPLODED VIEW", ToggleExplode, 17);
            UiKit.Size(explodeButton, 200f, 60f);
            Button frame = UiKit.Button(bar.transform, "FRAME", FrameView, 17);
            UiKit.Size(frame, 120f, 60f);
            Button resetAll = UiKit.Button(bar.transform, "RESET ALL", ResetAll, 17);
            UiKit.Size(resetAll, 150f, 60f);
        }

        // ───────────────────────────── behaviour ─────────────────────────────

        void OnSelectionChanged(CarPart part)
        {
            current = part;
            foreach (KeyValuePair<CarPart, Button> entry in partButtons) UiKit.SetActive(entry.Value, entry.Key == part);

            bool hasPart = part != null;
            inspectorControls.interactable = hasPart;
            inspectorControls.alpha = hasPart ? 1f : 0.35f;
            partTitle.text = hasPart ? part.DisplayName.ToUpperInvariant() : "NO COMPONENT";
            partHint.text = hasPart
                ? CategoryLabel(part.Category) + "  ·  pivot at component centre  ·  double-tap to frame"
                : "Tap a component on the car, or pick one from the list.";
            RefreshControls();
        }

        void OnPartEdited(CarPart part)
        {
            if (part == current) RefreshControls();
        }

        void OnFinishChanged(int index)
        {
            for (int i = 0; i < swatches.Count; i++) swatches[i].GetComponent<Outline>().enabled = i == index;
            if (paint != null) finishName.text = index >= 0 ? paint.Finishes[index].name.ToUpperInvariant() : "";
        }

        void OnCarSwitched(int _)
        {
            UnbindCar();
            BindCar();
            selector.Select(null);
            orbit.ResetView();

            bool visible = componentPanel.activeSelf;
            Destroy(componentPanel);
            partButtons.Clear();
            BuildComponentPanel();
            componentPanel.SetActive(visible);

            UiKit.SetActive(doorsButton, rig != null && rig.DoorsOpen);
            UiKit.SetActive(explodeButton, rig != null && rig.Exploded);
            RefreshCarInfo();
            RefreshCarButtons();
            if (paint != null) OnFinishChanged(paint.Current);
        }

        void RefreshCarInfo()
        {
            if (rig == null)
            {
                dimsText.text = "NO CAR ASSIGNED";
                return;
            }
            Vector3 d = rig.DimensionsMetres;
            dimsText.text = string.Format(Invariant, "{0}  ·  1:1 SCALE  ·  L {1:0.00} m   W {2:0.00} m   H {3:0.00} m",
                switcher.ActiveName.ToUpperInvariant(), d.z, d.x, d.y);
        }

        void RefreshCarButtons()
        {
            for (int i = 0; i < carButtons.Count; i++) UiKit.SetActive(carButtons[i], i == switcher.ActiveIndex);
        }

        void RefreshControls()
        {
            DesignEdit edit = current != null ? current.Edit : DesignEdit.Identity;
            suppressCallbacks = true;

            for (int i = 0; i < 3; i++)
            {
                translation[i].SetValueWithoutNotify(edit.translationMm[i]);
                translationValues[i].text = edit.translationMm[i].ToString("+0;-0;0", Invariant);
                UiKit.SetActive(axisButtons[i], (int)edit.axis == i);
            }
            rotation.SetValueWithoutNotify(edit.rotationDeg);
            rotationValue.text = edit.rotationDeg.ToString("+0;-0;0", Invariant) + "°";
            scale.SetValueWithoutNotify(edit.scale);
            scaleValue.text = edit.scale.ToString("0.00", Invariant) + "×";

            Matrix4x4 m = edit.Matrix;
            for (int i = 0; i < 16; i++)
            {
                int row = i / 4, column = i % 4;
                float value = m[row, column];
                string format = column == 3 && row < 3 ? "0.0" : "0.000";
                if (row == 3) format = "0";
                if (Mathf.Abs(value) < 0.0005f) value = 0f;
                matrixCells[i].text = value.ToString(format, Invariant);
            }

            suppressCallbacks = false;
        }

        void Modify(System.Func<DesignEdit, DesignEdit> change)
        {
            if (suppressCallbacks || current == null || rig == null) return;
            rig.SetEdit(current, change(current.Edit));
        }

        void SetTranslation(int axis, float value) => Modify(e =>
        {
            Vector3 t = e.translationMm;
            t[axis] = value;
            e.translationMm = t;
            return e;
        });

        void SetRotation(float value) => Modify(e => { e.rotationDeg = value; return e; });

        void SetScale(float value) => Modify(e => { e.scale = Mathf.Round(value * 100f) / 100f; return e; });

        void SetAxis(Axis axis) => Modify(e => { e.axis = axis; return e; });

        void SelectFromList(CarPart part)
        {
            selector.Select(part);
            selector.Frame(part);
        }

        void ApplyCasePreset()
        {
            if (rig == null) return;
            CarPart wheel = current != null && current.Category == PartCategory.Wheel ? current : rig.Find("Tire_FL");
            if (wheel == null) wheel = FirstWheel();
            if (wheel == null) return;

            selector.Select(wheel);
            rig.SetEdit(wheel, DesignEdit.CaseStudy);
            selector.Frame(wheel);
        }

        CarPart FirstWheel()
        {
            if (rig == null) return null;
            foreach (CarPart part in rig.Parts)
                if (part.Category == PartCategory.Wheel) return part;
            return null;
        }

        void ResetSelected()
        {
            if (current != null && rig != null) rig.ResetPart(current);
        }

        void ToggleDoors()
        {
            if (rig == null) return;
            rig.DoorsOpen = !rig.DoorsOpen;
            UiKit.SetActive(doorsButton, rig.DoorsOpen);
        }

        void ToggleExplode()
        {
            if (rig == null) return;
            rig.Exploded = !rig.Exploded;
            UiKit.SetActive(explodeButton, rig.Exploded);
        }

        void FrameView()
        {
            if (selector.Selected != null) selector.Frame(selector.Selected);
            else orbit.ResetView();
        }

        void ResetAll()
        {
            if (rig != null) rig.ResetAll();
            selector.Select(null);
            orbit.ResetView();
            UiKit.SetActive(doorsButton, false);
            UiKit.SetActive(explodeButton, false);
        }

        void SetShowroom(bool showroom)
        {
            componentPanel.SetActive(!showroom);
            inspectorPanel.SetActive(!showroom);
            orbit.AutoRotate = showroom;
            UiKit.SetActive(designMode, !showroom);
            UiKit.SetActive(showroomMode, showroom);
            if (showroom)
            {
                selector.Select(null);
                orbit.ResetView();
            }
        }

        void ApplySafeArea()
        {
            Rect safe = Screen.safeArea;
            if (safe == appliedSafeArea || Screen.width == 0 || Screen.height == 0) return;
            appliedSafeArea = safe;
            var size = new Vector2(Screen.width, Screen.height);
            safeRoot.anchorMin = safe.position / size;
            safeRoot.anchorMax = (safe.position + safe.size) / size;
        }

        void UpdateStats()
        {
            smoothedFrameTime = Mathf.Lerp(smoothedFrameTime, Time.unscaledDeltaTime, 0.08f);
            statsTimer -= Time.unscaledDeltaTime;
            if (statsTimer > 0f) return;
            statsTimer = 0.25f;
            statsText.text = string.Format(Invariant, "{0:0} FPS  ·  {1:0.0}K TRIS",
                1f / Mathf.Max(smoothedFrameTime, 0.0001f), rig != null ? rig.TriangleCount / 1000f : 0f);
        }

        // ───────────────────────────── helpers ─────────────────────────────

        Slider SliderRow(Transform parent, string axisLabel, float min, float max, bool whole,
            System.Action<float> onChanged, out Text valueLabel)
        {
            RectTransform row = UiKit.Rect("Row", parent);
            UiKit.Row(row, 12f, new RectOffset(0, 0, 0, 0));
            UiKit.Size(row, height: 40f);

            Text label = UiKit.Label(row, axisLabel, 18, Palette.Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Size(label, 30f);
            Slider slider = UiKit.Slider(row, min, max, whole, onChanged);
            UiKit.Size(slider, flexibleWidth: 1f);
            valueLabel = UiKit.Label(row, "", 18, Palette.Text, TextAnchor.MiddleRight);
            UiKit.Size(valueLabel, 76f);
            return slider;
        }

        static void Caption(Transform parent, string text)
        {
            Text caption = UiKit.Label(parent, text, 14, Palette.Muted, TextAnchor.LowerLeft, FontStyle.Bold);
            UiKit.Size(caption, height: 22f);
        }

        static void Spacer(Transform parent)
        {
            RectTransform spacer = UiKit.Rect("Spacer", parent);
            UiKit.Size(spacer, flexibleWidth: 1f);
        }

        static string CategoryLabel(PartCategory category) => category switch
        {
            PartCategory.Body => "BODY",
            PartCategory.Door => "DOORS",
            PartCategory.Wheel => "WHEELS",
            PartCategory.Mirror => "MIRRORS",
            PartCategory.Glass => "GLAZING",
            PartCategory.Interior => "CABIN",
            _ => throw new System.ArgumentOutOfRangeException(nameof(category), category, null)
        };
    }
}
