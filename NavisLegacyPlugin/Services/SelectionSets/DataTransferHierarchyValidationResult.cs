using System.Collections.Generic;
using System.Linq;

namespace NavisLegacyPlugin.Services.SelectionSets
{
    public sealed class DataTransferHierarchyValidationResult
    {
        private readonly List<DataTransferHierarchyValidationError>
            _errors;

        public DataTransferHierarchyValidationResult()
        {
            _errors =
                new List<DataTransferHierarchyValidationError>();
        }

        public IReadOnlyList<DataTransferHierarchyValidationError>
            Errors
        {
            get
            {
                return _errors;
            }
        }

        public bool IsValid
        {
            get
            {
                return _errors.Count == 0;
            }
        }

        public void AddError(
            string code,
            string message,
            string path)
        {
            _errors.Add(
                new DataTransferHierarchyValidationError(
                    code,
                    message,
                    path));
        }

        public bool ContainsError(
            string code)
        {
            return _errors.Any(
                x => x.Code == code);
        }

        public string ToDisplayMessage()
        {
            if (IsValid)
            {
                return
                    "The MAE-4D DATA-TRANSFER hierarchy is valid.";
            }

            return string.Join(
                System.Environment.NewLine,
                _errors.Select(
                    x => x.ToString()));
        }
    }
}
