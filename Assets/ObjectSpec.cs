using System.Collections.Generic;
using UnityEngine;

public enum ShapeType { Cube, Sphere, Cylinder }
public enum ColorType { Red, Green, Blue }
public enum SizeType { Small, Medium, Large }

/// <summary>
/// Describes every property the user can change on the object.
/// Used for both the editable object and the target object.
/// </summary>
[System.Serializable]
public struct ObjectSpec
{
    public ShapeType shape;
    public ColorType color;
    public SizeType size;
    public Vector3 eulerDegrees;

    public ObjectSpec(ShapeType shape, ColorType color, SizeType size, Vector3 eulerDegrees)
    {
        this.shape = shape;
        this.color = color;
        this.size = size;
        this.eulerDegrees = eulerDegrees;
    }

    public Quaternion Rotation => Quaternion.Euler(eulerDegrees);

    public Color UnityColor
    {
        get
        {
            switch (color)
            {
                case ColorType.Red: return new Color(0.90f, 0.15f, 0.15f);
                case ColorType.Green: return new Color(0.15f, 0.75f, 0.25f);
                default: return new Color(0.15f, 0.35f, 0.95f);
            }
        }
    }

    /// <summary>Edge length / diameter / height in meters.</summary>
    public float SizeMeters
    {
        get
        {
            switch (size)
            {
                case SizeType.Small: return 0.15f;
                case SizeType.Medium: return 0.25f;
                default: return 0.40f;
            }
        }
    }

    /// <summary>
    /// Scale to apply to the Unity primitive mesh. The built-in cylinder is 2 units
    /// tall, so its Y scale is halved to make it as tall as it is wide.
    /// </summary>
    public Vector3 VisualScale
    {
        get
        {
            float s = SizeMeters;
            return shape == ShapeType.Cylinder ? new Vector3(s, s * 0.5f, s) : Vector3.one * s;
        }
    }
}

public struct MatchResult
{
    public bool shape;
    public bool color;
    public bool size;
    public bool rotation;

    public bool IsCorrect => shape && color && size && rotation;

    public int MismatchCount
    {
        get
        {
            int n = 0;
            if (!shape) n++;
            if (!color) n++;
            if (!size) n++;
            if (!rotation) n++;
            return n;
        }
    }
}

/// <summary>
/// Compares the user's object with the target. Validation is intentionally forgiving:
///  - Rotation has an angular tolerance.
///  - Rotation is symmetry-aware: a sphere ignores rotation, a cylinder only cares
///    about its axis (and accepts it flipped), and a cube accepts any of its 24
///    equivalent orientations.
/// </summary>
public static class MatchValidator
{
    static readonly List<Quaternion> CubeSymmetries = BuildCubeSymmetries();

    static List<Quaternion> BuildCubeSymmetries()
    {
        var list = new List<Quaternion>();
        for (int x = 0; x < 4; x++)
        for (int y = 0; y < 4; y++)
        for (int z = 0; z < 4; z++)
        {
            Quaternion q = Quaternion.Euler(90f * x, 90f * y, 90f * z);
            bool duplicate = false;
            foreach (Quaternion existing in list)
            {
                if (Quaternion.Angle(existing, q) < 1f) { duplicate = true; break; }
            }
            if (!duplicate) list.Add(q); // ends up with 24 unique orientations
        }
        return list;
    }

    public static MatchResult Evaluate(ObjectSpec player, ObjectSpec target, float rotationToleranceDegrees)
    {
        return new MatchResult
        {
            shape = player.shape == target.shape,
            color = player.color == target.color,
            size = player.size == target.size,
            rotation = RotationMatches(player, target, rotationToleranceDegrees)
        };
    }

    static bool RotationMatches(ObjectSpec player, ObjectSpec target, float tolerance)
    {
        switch (target.shape)
        {
            case ShapeType.Sphere:
                return true; // a sphere looks identical in every orientation

            case ShapeType.Cylinder:
            {
                Vector3 a = player.Rotation * Vector3.up;
                Vector3 b = target.Rotation * Vector3.up;
                float angle = Mathf.Min(Vector3.Angle(a, b), Vector3.Angle(a, -b));
                return angle <= tolerance;
            }

            default: // cube
            {
                Quaternion p = player.Rotation;
                Quaternion t = target.Rotation;
                float best = 360f;
                foreach (Quaternion symmetry in CubeSymmetries)
                {
                    best = Mathf.Min(best, Quaternion.Angle(p, t * symmetry));
                }
                return best <= tolerance;
            }
        }
    }
}
