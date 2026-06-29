namespace SolarShading.Core.Geometry;

/// <summary>Axis-aligned bounding box used for cheap spatial culling of occluders.</summary>
public readonly struct Aabb
{
    public readonly Vec3 Min;
    public readonly Vec3 Max;

    public Aabb(Vec3 min, Vec3 max)
    {
        Min = min;
        Max = max;
    }

    public static Aabb FromPoints(IEnumerable<Vec3> points)
    {
        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
        foreach (Vec3 p in points)
        {
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.Z < minZ) minZ = p.Z;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
            if (p.Z > maxZ) maxZ = p.Z;
        }
        return new Aabb(new Vec3(minX, minY, minZ), new Vec3(maxX, maxY, maxZ));
    }

    /// <summary>The eight corner points of the box.</summary>
    public IEnumerable<Vec3> Corners()
    {
        yield return new Vec3(Min.X, Min.Y, Min.Z);
        yield return new Vec3(Max.X, Min.Y, Min.Z);
        yield return new Vec3(Min.X, Max.Y, Min.Z);
        yield return new Vec3(Max.X, Max.Y, Min.Z);
        yield return new Vec3(Min.X, Min.Y, Max.Z);
        yield return new Vec3(Max.X, Min.Y, Max.Z);
        yield return new Vec3(Min.X, Max.Y, Max.Z);
        yield return new Vec3(Max.X, Max.Y, Max.Z);
    }
}
