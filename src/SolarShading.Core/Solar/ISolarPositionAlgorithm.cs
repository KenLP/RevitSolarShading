namespace SolarShading.Core.Solar;

/// <summary>
/// Abstraction over the solar-position algorithm so the engine (NOAA today) can be
/// upgraded to NREL SPA or Grena without changing callers.
/// </summary>
public interface ISolarPositionAlgorithm
{
    /// <param name="instant">The instant of interest (carries its own UTC offset).</param>
    /// <param name="latitudeDeg">Site latitude, degrees (north positive).</param>
    /// <param name="longitudeDeg">Site longitude, degrees (east positive).</param>
    SunVector Compute(DateTimeOffset instant, double latitudeDeg, double longitudeDeg);
}
