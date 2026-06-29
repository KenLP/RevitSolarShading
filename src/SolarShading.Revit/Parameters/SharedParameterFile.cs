using System.IO;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;

namespace SolarShading.Revit.Parameters;

/// <summary>Ensures a shared parameter file is available for the add-in's definitions.</summary>
public static class SharedParameterFile
{
    public static DefinitionFile Ensure(Application app)
    {
        string? path = app.SharedParametersFilename;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            string dir = Path.Combine(Path.GetTempPath(), "SolarShading");
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
