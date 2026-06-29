using System.IO;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;

namespace SolarShading.Revit.Parameters;

/// <summary>Ensures a shared parameter file is available for the add-in's definitions.</summary>
public static class SharedParameterFile
{
    /// <summary>
    /// The add-in keeps its OWN shared-parameter file at a stable LocalApplicationData path so
    /// the parameters (created with fixed GUIDs) are identical across projects and machines.
    /// If the user has their own shared-parameter file set, we keep it and add our group to it.
    /// </summary>
    public static DefinitionFile Ensure(Application app)
    {
        string? path = app.SharedParametersFilename;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            // A discoverable, stable location the user can find (and the parameters have fixed
            // GUIDs, so the file can live anywhere). Documents\SolarShading.
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SolarShading");
            Directory.CreateDirectory(dir);
            path = Path.Combine(dir, "SolarShading_SharedParameters.txt");
            if (!File.Exists(path))
                File.WriteAllText(path, string.Empty);
            app.SharedParametersFilename = path;
        }
        return app.OpenSharedParameterFile()
            ?? throw new InvalidOperationException("Could not open the shared parameter file.");
    }
}
