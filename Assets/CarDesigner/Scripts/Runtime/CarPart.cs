using UnityEngine;

namespace CarDesigner
{
    public enum PartCategory { Body, Wheel, Door, Mirror, Glass, Interior }

    /// <summary>
    /// A selectable component of the car. Poses are always rebuilt from the captured rest pose,
    /// so edits never accumulate floating-point drift. All positions live in the rig's local space.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class CarPart : MonoBehaviour
    {
        [SerializeField] string displayName;
        [SerializeField] PartCategory category;

        public DesignEdit Edit = DesignEdit.Identity;

        Vector3 restPosition;
        Quaternion restRotation;
        Vector3 restScale;
        Bounds restBounds;
        Vector3 explodeOffset;
        Vector3 hinge;
        float doorSign;
        CarPart hingeOwner;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public PartCategory Category => category;
        public MeshFilter MeshFilter { get; private set; }
        public MeshRenderer Renderer { get; private set; }
        public Bounds RestBounds => restBounds;
        public Bounds WorldBounds => Renderer.bounds;
        public Vector3 Pivot => restBounds.center;

        public void Configure(string label, PartCategory partCategory)
        {
            displayName = label;
            category = partCategory;
        }

        public void Capture()
        {
            MeshFilter = GetComponent<MeshFilter>();
            Renderer = GetComponent<MeshRenderer>();
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
            restScale = transform.localScale;
            restBounds = BoundsInParent();
        }

        /// <summary>Derives explode direction and door hinge from the rest pose and the car's design frame.</summary>
        public void Prepare(Bounds car, Quaternion frame)
        {
            Vector3 forward = frame * Vector3.forward;
            Vector3 right = frame * Vector3.right;
            Vector3 up = frame * Vector3.up;
            Vector3 fromCentre = Pivot - car.center;
            float side = Mathf.Sign(Vector3.Dot(fromCentre, right));

            explodeOffset = category switch
            {
                PartCategory.Body => Vector3.zero,
                PartCategory.Wheel => right * (side * 0.55f) + forward * (Vector3.Dot(fromCentre, forward) * 0.12f),
                PartCategory.Door => right * (side * 0.75f),
                PartCategory.Mirror => right * (side * 0.95f) + up * 0.15f,
                PartCategory.Glass => up * 0.55f + right * (Vector3.Dot(fromCentre, right) * 0.6f),
                PartCategory.Interior => up * 1.1f + forward * (Vector3.Dot(fromCentre, forward) * 0.25f),
                _ => Vector3.zero
            };

            if (category == PartCategory.Door)
            {
                float halfLength = Vector3.Dot(restBounds.extents, Abs(forward));
                hinge = Pivot + forward * halfLength;
                // Positive rotation about +Y swings the trailing edge towards -right, so left doors use +angle.
                doorSign = -side;
            }
        }

        public void AttachToDoor(CarPart door) => hingeOwner = door;

        public void ApplyPose(Quaternion frame, float explodeT, float doorDegrees)
        {
            float s = Edit.scale;
            Quaternion r = frame * Edit.Rotation * Quaternion.Inverse(frame);
            Vector3 t = frame * (Edit.translationMm * 0.001f);

            // Composite T·R·S applied about the component's own centre: p' = c + t + R·(s·(p − c)).
            Vector3 position = Pivot + t + r * ((restPosition - Pivot) * s);
            Quaternion rotation = r * restRotation;

            CarPart door = category == PartCategory.Door ? this : hingeOwner;
            if (door != null && doorDegrees > 0f)
            {
                Quaternion swing = Quaternion.AngleAxis(doorDegrees * door.doorSign, frame * Vector3.up);
                position = door.hinge + swing * (position - door.hinge);
                rotation = swing * rotation;
            }

            transform.SetLocalPositionAndRotation(position + explodeOffset * explodeT, rotation);
            transform.localScale = restScale * s;
        }

        Bounds BoundsInParent()
        {
            Bounds mesh = MeshFilter.sharedMesh.bounds;
            Matrix4x4 toParent = Matrix4x4.TRS(transform.localPosition, transform.localRotation, transform.localScale);
            Vector3 c = mesh.center, e = mesh.extents;
            var bounds = new Bounds(toParent.MultiplyPoint3x4(c), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? -e.x : e.x,
                    (i & 2) == 0 ? -e.y : e.y,
                    (i & 4) == 0 ? -e.z : e.z);
                bounds.Encapsulate(toParent.MultiplyPoint3x4(c + corner));
            }
            return bounds;
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}
