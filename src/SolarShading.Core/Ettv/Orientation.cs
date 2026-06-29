namespace SolarShading.Core.Ettv;

/// <summary>Eight compass orientations used for façade grouping and solar correction factors.</summary>
public enum Orientation
{
    N, NE, E, SE, S, SW, W, NW
}

public static class OrientationExtensions
{
    /// <summary>Classify a façade outward azimuth (degrees clockwise from North) into an octant.</summary>
    public static Orientation FromAzimuth(double azimuthDeg)
    {
        double a = ((azimuthDeg % 360.0) + 360.0) % 360.0;
        int octant = (int)Math.Round(a / 45.0) % 8;
        return (Orientation)octant;
    }
}
