using System.IO;
using System.Reflection;

namespace SolarShading.Revit.Geometry;

/// <summary>
/// Resolves the active <see cref="ITessellator"/>. On first use it looks for a
/// <c>SolarShading.Private.dll</c> next to the add-in and, if present, uses the first public
/// class it finds that implements <see cref="ITessellator"/> (a proprietary, closed-source
/// tessellator). If the file is absent or anything fails, it falls back to
/// <see cref="DefaultTessellator"/>. The private DLL is distributed alongside the add-in but is
/// NOT part of the public source repository.
/// </summary>
public static class Tessellation
{
    private const string PrivateAssembly = "SolarShading.Private.dll";

    private static ITessellator? _active;

    /// <summary>The tessellator in effect (private plugin if available, else the default).</summary>
    public static ITessellator Active => _active ??= Load();

    /// <summary>Override the tessellator explicitly (e.g. for tests).</summary>
    public static void Set(ITessellator tessellator) => _active = tessellator;

    private static ITessellator Load()
    {
        try
        {
            string? dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string path = Path.Combine(dir ?? "", PrivateAssembly);
            if (File.Exists(path))
            {
                Assembly asm = Assembly.LoadFrom(path);
                Type? impl = asm.GetTypes().FirstOrDefault(t =>
                    typeof(ITessellator).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false });
                if (impl != null && Activator.CreateInstance(impl) is ITessellator plugin)
                    return plugin;
            }
        }
        catch
        {
            // fall back to the default tessellator
        }
        return new DefaultTessellator();
    }
}
