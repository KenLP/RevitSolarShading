using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;

namespace SolarShading.Revit.Parameters;

/// <summary>
/// Creates and binds the shared parameters the add-in writes results into, using the
/// modern <see cref="ForgeTypeId"/> API (the deprecated <c>ParameterType</c> enum and
/// <c>BuiltInParameterGroup</c> were removed in Revit 2024+). Result strings are stored
/// per element so they appear in schedules; numeric SC2 is stored as a number.
/// </summary>
public static class ResultParameters
{
    public const string Sc2 = "SS_EXTERNAL_SC2";
    public const string ShadedSeriesMarch = "SS_SHADED_MARCH";
    public const string ShadedSeriesJune = "SS_SHADED_JUNE";
    public const string ShadedSeriesDec = "SS_SHADED_DEC";

    private const string GroupName = "SolarShading";

    private static readonly (string Name, ForgeTypeId Spec, Guid Guid)[] Params =
    {
        (ShadedSeriesMarch, SpecTypeId.String.Text, ParameterGuids.ShadedMarch),
        (ShadedSeriesJune, SpecTypeId.String.Text, ParameterGuids.ShadedJune),
        (ShadedSeriesDec, SpecTypeId.String.Text, ParameterGuids.ShadedDec),
        (Sc2, SpecTypeId.Number, ParameterGuids.Sc2),
    };

    /// <summary>Ensure all result parameters exist and are bound to the given categories. Needs a transaction.</summary>
    public static void EnsureBound(Document doc, Application app, IEnumerable<BuiltInCategory> categories)
    {
        DefinitionFile file = SharedParameterFile.Ensure(app);
        DefinitionGroup group = file.Groups.get_Item(GroupName) ?? file.Groups.Create(GroupName);

        var catSet = app.Create.NewCategorySet();
        foreach (BuiltInCategory bic in categories)
        {
            Category cat = Category.GetCategory(doc, bic);
            if (cat != null)
                catSet.Insert(cat);
        }

        foreach ((string name, ForgeTypeId spec, Guid guid) in Params)
            BindIfMissing(doc, app, group, name, spec, guid, catSet);
    }

    private static void BindIfMissing(
        Document doc, Application app, DefinitionGroup group,
        string name, ForgeTypeId spec, Guid guid, CategorySet categories)
    {
        Definition def = group.Definitions.get_Item(name)
            ?? group.Definitions.Create(new ExternalDefinitionCreationOptions(name, spec) { GUID = guid });

        if (doc.ParameterBindings.Contains(def))
            return;

        InstanceBinding binding = app.Create.NewInstanceBinding(categories);
        doc.ParameterBindings.Insert(def, binding, GroupTypeId.Data);
    }
}
