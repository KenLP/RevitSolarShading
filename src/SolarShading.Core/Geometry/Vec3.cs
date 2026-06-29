namespace SolarShading.Core.Geometry;

/// <summary>
/// Lightweight immutable 3D vector. Revit-independent so the geometry core can be
/// unit-tested without the Revit API. Convention used across the core: X = East,
/// Y = North, Z = Up; lengths in metres unless stated otherwise.
/// </summary>
public readonly struct Vec3
{
    public readonly double X;
    public readonly double Y;
    public readonly double Z;

    public Vec3(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public static readonly Vec3 Zero = new(0, 0, 0);
    public static readonly Vec3 UnitX = new(1, 0, 0);
    public static readonly Vec3 UnitY = new(0, 1, 0);
    public static readonly Vec3 UnitZ = new(0, 0, 1);

    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3 operator -(Vec3 a) => new(-a.X, -a.Y, -a.Z);
    public static Vec3 operator *(Vec3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);
    public static Vec3 operator *(double s, Vec3 a) => a * s;

    public double Dot(Vec3 b) => X * b.X + Y * b.Y + Z * b.Z;

    public Vec3 Cross(Vec3 b) => new(
        Y * b.Z - Z * b.Y,
        Z * b.X - X * b.Z,
        X * b.Y - Y * b.X);

    public double Length => Math.Sqrt(Dot(this));

    public Vec3 Normalized()
    {
        double len = Length;
        if (len < 1e-12)
            throw new InvalidOperationException("Cannot normalize a zero-length vector.");
        return this * (1.0 / len);
    }

    public override string ToString() => $"({X:F4}, {Y:F4}, {Z:F4})";
}
