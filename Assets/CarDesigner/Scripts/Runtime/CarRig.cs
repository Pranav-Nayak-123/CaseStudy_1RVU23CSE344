using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarDesigner
{
    /// <summary>
    /// Owns every <see cref="CarPart"/> (all direct children) and the car's design frame.
    /// Drives designer edits plus the door and exploded-view animations.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public class CarRig : MonoBehaviour
    {
        [SerializeField] Vector3 forwardAxis = Vector3.forward;
        [SerializeField] float doorOpenAngle = 62f;
        [SerializeField] float animationSpeed = 2.8f;

        CarPart[] parts = Array.Empty<CarPart>();
        float explode, explodeGoal, doors, doorsGoal;

        public event Action<CarPart> PartEdited;

        public IReadOnlyList<CarPart> Parts => parts;
        public Quaternion DesignFrame { get; private set; } = Quaternion.identity;
        public Bounds RestBounds { get; private set; }
        public Vector3 DimensionsMetres { get; private set; }
        public int TriangleCount { get; private set; }

        public bool Exploded
        {
            get => explodeGoal > 0.5f;
            set => explodeGoal = value ? 1f : 0f;
        }

        public bool DoorsOpen
        {
            get => doorsGoal > 0.5f;
            set => doorsGoal = value ? 1f : 0f;
        }

        public void SetForwardAxis(Vector3 forward) => forwardAxis = forward.normalized;

        void Awake()
        {
            parts = GetComponentsInChildren<CarPart>(true);
            DesignFrame = Quaternion.LookRotation(forwardAxis.normalized, Vector3.up);

            var bounds = new Bounds();
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i].Capture();
                if (i == 0) bounds = parts[i].RestBounds;
                else bounds.Encapsulate(parts[i].RestBounds);
                TriangleCount += CountTriangles(parts[i].MeshFilter.sharedMesh);
            }
            RestBounds = bounds;

            Vector3 size = Quaternion.Inverse(DesignFrame) * bounds.size;
            DimensionsMetres = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));

            foreach (CarPart part in parts) part.Prepare(bounds, DesignFrame);
            AttachDoorFollowers(bounds);
        }

        void Update()
        {
            float step = animationSpeed * Time.unscaledDeltaTime;
            float e = Mathf.MoveTowards(explode, explodeGoal, step);
            float d = Mathf.MoveTowards(doors, doorsGoal, step);
            if (Mathf.Approximately(e, explode) && Mathf.Approximately(d, doors)) return;

            explode = e;
            doors = d;
            ApplyAll();
        }

        public void SetEdit(CarPart part, DesignEdit edit)
        {
            part.Edit = edit;
            Apply(part);
            PartEdited?.Invoke(part);
        }

        public void ResetPart(CarPart part) => SetEdit(part, DesignEdit.Identity);

        public void ResetAll()
        {
            foreach (CarPart part in parts) SetEdit(part, DesignEdit.Identity);
            Exploded = false;
            DoorsOpen = false;
        }

        public CarPart Find(string partName) => Array.Find(parts, p => p.name == partName);

        void ApplyAll()
        {
            foreach (CarPart part in parts) Apply(part);
        }

        void Apply(CarPart part) =>
            part.ApplyPose(DesignFrame, Smooth(explode), Smooth(doors) * doorOpenAngle);

        /// <summary>Side glass and mirrors are mounted on the doors, so they swing with the door on their side.</summary>
        void AttachDoorFollowers(Bounds car)
        {
            Vector3 right = DesignFrame * Vector3.right;
            var absRight = new Vector3(Mathf.Abs(right.x), Mathf.Abs(right.y), Mathf.Abs(right.z));
            float halfWidth = Vector3.Dot(car.extents, absRight);
            foreach (CarPart part in parts)
            {
                if (part.Category != PartCategory.Mirror && part.Category != PartCategory.Glass) continue;
                float side = Vector3.Dot(part.Pivot - car.center, right);
                if (Mathf.Abs(side) < halfWidth * 0.35f) continue;

                foreach (CarPart door in parts)
                {
                    if (door.Category != PartCategory.Door) continue;
                    if (Mathf.Sign(Vector3.Dot(door.Pivot - car.center, right)) != Mathf.Sign(side)) continue;
                    part.AttachToDoor(door);
                    break;
                }
            }
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);

        static int CountTriangles(Mesh mesh)
        {
            if (mesh == null) return 0;
            int total = 0;
            for (int i = 0; i < mesh.subMeshCount; i++) total += (int)mesh.GetIndexCount(i) / 3;
            return total;
        }
    }
}
