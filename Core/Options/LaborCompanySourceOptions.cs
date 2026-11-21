namespace Core.Options
{
    /// <summary>
    /// Optional SELECT against the external companies database.
    /// Columns, in order: number, name, address, specialty.
    /// One statement only. Leave empty until the source view is known.
    /// </summary>
    public sealed class LaborCompanySourceOptions
    {
        public const string SectionName = "LaborCompanies";

        public string ExternalCommand { get; set; } = "";
    }
}
