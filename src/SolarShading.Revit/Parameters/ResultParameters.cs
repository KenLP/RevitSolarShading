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

    private static readonly (string Name, ForgeTypeId Spec)[] TextParams =
    {
        (ShadedSeriesMarch, SpecTypeId.String.Text),
        (ShadedSeriesJune, SpecTypeId.String.Text),
        (ShadedSeriesDec, SpecTypeId.String.Text),
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

        foreach ((string name, ForgeTypeId spec) in TextParams)
            BindIfMissing(doc, app, group, name, spec, catSet);
        BindIfMissing(doc, app, group, Sc2, SpecTypeId.Number, catSet);
    }

    private static void BindIfMissing(
        Document doc, Application app, DefinitionGroup group,
        string name, ForgeTypeId spec, CategorySet categories)
    {
        Definition def = group.Definitions.get_Item(name)
            ?? group.Definitions.Create(new ExternalDefinitionCreationOptions(name, spec));

        if (doc.ParameterBindings.Contains(def))
            return;

        InstanceBinding binding = app.Create.NewInstanceBinding(categories);
        doc.ParameterBindings.Insert(def, binding, GroupTypeId.Data);
    }
}
