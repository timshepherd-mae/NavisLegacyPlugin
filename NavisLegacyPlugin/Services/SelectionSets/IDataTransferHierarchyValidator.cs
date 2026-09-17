using Autodesk.Navisworks.Api;

namespace NavisLegacyPlugin.Services.SelectionSets
{
    public interface IDataTransferHierarchyValidator
    {
        DataTransferHierarchyValidationResult Validate(
            Document document);

        DataTransferHierarchyValidationResult ValidateScope(
            Document document,
            string scopeName);
    }
}
