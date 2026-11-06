using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace CarDesigner
{
    /// <summary>
    /// Turntable camera for touch and mouse.
    /// Touch: one finger orbits, pinch zooms, two fingers pan, tap selects, double tap frames.
    /// Mouse: left drag orbits, right/middle drag pans, wheel zooms.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class OrbitCamera : MonoBehaviour
    {
        [SerializeField] Vector3 focus = new Vector3(0f, 0.6f, 0f);
        [SerializeField] float yaw = 215f;
        [SerializeField] float pitch = 14f;
        [SerializeField] float distance = 7.2f;
        [SerializeField] Vector2 pitchLimits = new Vector2(2f, 82f);
        [SerializeField] Vector2 distanceLimits = new Vector2(1.2f, 14f);
        [SerializeField] float panRadius = 4f;
        [SerializeField] float orbitDegreesPerInch = 150f;
        [SerializeField] float smoothing = 12f;
        [SerializeField] float autoRotateSpeed = 9f;

        const float TapMaxSeconds = 0.4f;
        const float DoubleTapSeconds = 0.32f;

        Camera cam;
        Vector3 homeFocus, focusGoal;
        float homeYaw, homePitch, homeDistance, yawGoal, pitchGoal, distanceGoal;

        bool pointerDown, dragging, pressStartedOnUi;
        Vector2 pressPosition, lastTapPosition;
        float pressTime, lastTapTime = -1f;
        int previousTouchCount;
        float previousPinch;
        Vector2 previousMidpoint;

        public event Action<Vector2> Tapped;
        public event Action<Vector2> DoubleTapped;

        public bool AutoRotate { get; set; }

        static float Dpi => Screen.dpi > 1f ? Screen.dpi : 160f;
        static float DragThreshold => 0.07f * Dpi;

        public void Configure(Vector3 newFocus, float newYaw, float newPitch, float newDistance)
        {
            focus = newFocus;
            yaw = newYaw;
            pitch = newPitch;
            distance = newDistance;
            ApplyTransform();
        }

        void Awake()
        {
            cam = GetComponent<Camera>();
            homeFocus = focusGoal = focus;
            homeYaw = yawGoal = yaw;
            homePitch = pitchGoal = pitch;
            homeDistance = distanceGoal = distance;
            ApplyTransform();
        }

        void OnEnable() => EnhancedTouchSupport.Enable();
        void OnDisable() => EnhancedTouchSupport.Disable();

        void LateUpdate()
        {
            if (Touch.activeTouches.Count > 0 || previousTouchCount > 0) HandleTouches();
            else HandleMouse();

            if (AutoRotate && !pointerDown) yawGoal += autoRotateSpeed * Time.unscaledDeltaTime;

            float k = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
            yaw = Mathf.Lerp(yaw, yawGoal, k);
            pitch = Mathf.Lerp(pitch, pitchGoal, k);
            distance = Mathf.Lerp(distance, distanceGoal, k);
            focus = Vector3.Lerp(focus, focusGoal, k);
            ApplyTransform();
        }

        /// <summary>
        /// Smoothly move the camera to look at <paramref name="point"/> from <paramref name="viewDistance"/>
        /// away. Optionally snap pitch to <paramref name="targetPitch"/> degrees for a clean framing angle.
        /// Pass null to leave the pitch unchanged.
        /// </summary>
        public void FocusOn(Vector3 point, float viewDistance, float? targetPitch = null)
        {
            // Use the actual world point; bypass the pan-radius clamp so framing always works.
            Vector3 clamped = point;
            clamped.y = Mathf.Clamp(clamped.y, 0.05f, 2.4f);
            focusGoal = clamped;
            distanceGoal = Mathf.Clamp(viewDistance, distanceLimits.x, distanceLimits.y);
            if (targetPitch.HasValue)
                pitchGoal = Mathf.Clamp(targetPitch.Value, pitchLimits.x, pitchLimits.y);
        }

        public void ResetView()
        {
            focusGoal = homeFocus;
            pitchGoal = homePitch;
            distanceGoal = homeDistance;
            yawGoal = homeYaw + Mathf.Round((yawGoal - homeYaw) / 360f) * 360f;
        }

        void ApplyTransform()
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
        }

        void HandleTouches()
        {
            var touches = Touch.activeTouches;
            int count = touches.Count;

            if (count == 1)
            {
                Touch touch = touches[0];
                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        BeginPress(touch.screenPosition);
                        break;
                    case TouchPhase.Moved:
                    case TouchPhase.Stationary:
                        UpdateDrag(touch.screenPosition, touch.delta, previousTouchCount == 1);
                        break;
                    case TouchPhase.Ended:
                        EndPress(touch.screenPosition);
                        break;
                    case TouchPhase.Canceled:
                        pointerDown = dragging = false;
                        break;
                }
            }
            else if (count >= 2)
            {
                dragging = true;
                Vector2 a = touches[0].screenPosition;
                Vector2 b = touches[1].screenPosition;
                float pinch = Vector2.Distance(a, b);
                Vector2 midpoint = (a + b) * 0.5f;

                if (previousTouchCount >= 2 && !pressStartedOnUi)
                {
                    distanceGoal = Mathf.Clamp(distanceGoal * previousPinch / Mathf.Max(pinch, 1f),
                        distanceLimits.x, distanceLimits.y);
                    Pan(midpoint - previousMidpoint);
                }

                previousPinch = pinch;
                previousMidpoint = midpoint;
                if (touches[0].phase == TouchPhase.Ended && touches[1].phase == TouchPhase.Ended)
                    pointerDown = dragging = false;
            }

            previousTouchCount = count;
        }

        void HandleMouse()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 position = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame) BeginPress(position);
            if (mouse.leftButton.isPressed) UpdateDrag(position, mouse.delta.ReadValue(), true);
            if (mouse.leftButton.wasReleasedThisFrame) EndPress(position);

            bool overUi = UiHit.IsOver(position);
            if ((mouse.rightButton.isPressed || mouse.middleButton.isPressed) && !overUi)
                Pan(mouse.delta.ReadValue());

            float scroll = mouse.scroll.ReadValue().y;
            if (!Mathf.Approximately(scroll, 0f) && !overUi)
            {
                float notches = Mathf.Abs(scroll) > 20f ? scroll / 120f : scroll;
                distanceGoal = Mathf.Clamp(distanceGoal * Mathf.Pow(0.88f, Mathf.Clamp(notches, -3f, 3f)),
                    distanceLimits.x, distanceLimits.y);
            }
        }

        void BeginPress(Vector2 position)
        {
            pointerDown = true;
            dragging = false;
            pressPosition = position;
            pressTime = Time.unscaledTime;
            pressStartedOnUi = UiHit.IsOver(position);
        }

        void UpdateDrag(Vector2 position, Vector2 delta, bool canOrbit)
        {
            if (!pointerDown || pressStartedOnUi) return;
            if (!dragging && Vector2.Distance(position, pressPosition) > DragThreshold) dragging = true;
            if (!dragging || !canOrbit) return;

            float degreesPerPixel = orbitDegreesPerInch / Dpi;
            yawGoal += delta.x * degreesPerPixel;
            pitchGoal = Mathf.Clamp(pitchGoal - delta.y * degreesPerPixel * 0.6f, pitchLimits.x, pitchLimits.y);
        }

        void EndPress(Vector2 position)
        {
            bool isTap = pointerDown && !dragging && !pressStartedOnUi && Time.unscaledTime - pressTime < TapMaxSeconds;
            pointerDown = dragging = false;
            if (!isTap) return;

            bool isDouble = Time.unscaledTime - lastTapTime < DoubleTapSeconds &&
                            Vector2.Distance(position, lastTapPosition) < DragThreshold * 4f;
            if (isDouble)
            {
                lastTapTime = -1f;
                DoubleTapped?.Invoke(position);
                return;
            }

            lastTapTime = Time.unscaledTime;
            lastTapPosition = position;
            Tapped?.Invoke(position);
        }

        void Pan(Vector2 screenDelta)
        {
            float metresPerPixel = 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Screen.height;
            Vector3 move = (transform.right * screenDelta.x + transform.up * screenDelta.y) * metresPerPixel;
            focusGoal = ClampFocus(focusGoal - move);
        }

        Vector3 ClampFocus(Vector3 point)
        {
            Vector3 offset = Vector3.ClampMagnitude(point - homeFocus, panRadius);
            Vector3 clamped = homeFocus + offset;
            clamped.y = Mathf.Clamp(clamped.y, 0.05f, 2.2f);
            return clamped;
        }
    }
}
