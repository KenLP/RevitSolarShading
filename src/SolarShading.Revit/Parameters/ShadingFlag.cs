using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;

namespace SolarShading.Revit.Parameters;

/// <summary>
/// The Yes/No shared parameter that marks an element as a shading device, so the shading
/// computation can filter candidate occluders. Created via the ForgeTypeId API.
/// </summary>
public static class ShadingFlag
{
    /// <summary>The parameter the add-in writes when tagging.</summary>
    public const string Name = "SS_SHADING_DEVICE";

    /// <summary>
    /// Yes/No parameter names recognised as a shading-device flag when READING. Includes the
    /// add-in's own name plus a common "SHADING DEVICE" flag, so a model already tagged with an
    /// existing flag is picked up without re-tagging. Add your own name here if different.
    /// </summary>
    public static readonly string[] RecognizedNames = { Name, "SHADING DEVICE" };

    private const string GroupName = "SolarShading";

    // Categories a shading device can belong to. Walls are intentionally excluded — a wall is
    // the host, not a sun-shade — so it can't be tagged by accident.
    public static readonly BuiltInCategory[] CandidateCategories =
    {
        BuiltInCategory.OST_GenericModel,
        BuiltInCategory.OST_Roofs,
        BuiltInCategory.OST_Floors,
        BuiltInCategory.OST_StructuralColumns,
        BuiltInCategory.OST_StructuralFraming,
        BuiltInCategory.OST_Columns,
        BuiltInCategory.OST_Casework,
        BuiltInCategory.OST_Mass,
        BuiltInCategory.OST_CurtainWallMullions,
    };

    public static void EnsureBound(Document doc, Application app)
    {
        DefinitionFile file = SharedParameterFile.Ensure(app);
        DefinitionGroup group = file.Groups.get_Item(GroupName) ?? file.Groups.Create(GroupName);
        Definition def = group.Definitions.get_Item(Name)
            ?? group.Definitions.Create(
                new ExternalDefinitionCreationOptions(Name, SpecTypeId.Boolean.YesNo) { GUID = ParameterGuids.ShadingDevice });

        if (doc.ParameterBindings.Contains(def))
            return;

        var cats = app.Create.NewCategorySet();
        foreach (BuiltInCategory bic in CandidateCategories)
        {
            Category cat = Category.GetCategory(doc, bic);
            if (cat != null && cat.AllowsBoundParameters)
                cats.Insert(cat);
        }
        InstanceBinding binding = app.Create.NewInstanceBinding(cats);
        doc.ParameterBindings.Insert(def, binding, GroupTypeId.Data);
    }

    public static bool IsShadingDevice(Element e)
    {
        foreach (string name in RecognizedNames)
        {
            Parameter? p = e.LookupParameter(name);
            if (p is { HasValue: true } && p.StorageType == StorageType.Integer && p.AsInteger() == 1)
                return true;
        }
        return false;
    }

    public static void Set(Element e, bool value)
    {
        Parameter? p = e.LookupParameter(Name);
        p?.Set(value ? 1 : 0);
    }
}
