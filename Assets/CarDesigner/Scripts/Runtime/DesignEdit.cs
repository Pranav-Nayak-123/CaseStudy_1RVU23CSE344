using System;
using UnityEngine;

namespace CarDesigner
{
    public enum Axis { X, Y, Z }

    /// <summary>
    /// A designer's edit expressed in the car's design frame
    /// (X = lateral / wheel axle, Y = vertical, Z = longitudinal / forward), translation in millimetres.
    /// The composite homogeneous matrix is M = T · R · S, i.e. scale first, then rotate, then translate.
    /// </summary>
    [Serializable]
    public struct DesignEdit
    {
        public Vector3 translationMm;
        public float rotationDeg;
        public Axis axis;
        public float scale;

        public static DesignEdit Identity => new DesignEdit { scale = 1f, axis = Axis.X };

        public static DesignEdit CaseStudy => new DesignEdit
        {
            translationMm = new Vector3(0f, 0f, 50f),
            rotationDeg = 45f,
            axis = Axis.X,
            scale = 1.5f
        };

        public static Vector3 AxisVector(Axis axis) => axis switch
        {
            Axis.X => Vector3.right,
            Axis.Y => Vector3.up,
            Axis.Z => Vector3.forward,
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null)
        };

        public Quaternion Rotation => Quaternion.AngleAxis(rotationDeg, AxisVector(axis));

        public Matrix4x4 Matrix => Matrix4x4.TRS(translationMm, Rotation, Vector3.one * scale);

        public bool IsIdentity =>
            translationMm == Vector3.zero && Mathf.Approximately(rotationDeg, 0f) && Mathf.Approximately(scale, 1f);
    }
}
