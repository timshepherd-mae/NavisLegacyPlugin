using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Autodesk.Navisworks.Api;

namespace NavisLegacyPlugin.Services.Collections
{
    /// <summary>
    /// Defines in-process ModelItem population identity by object reference.
    /// InstanceGuid and model properties are matching/diagnostic attributes only.
    /// </summary>
    internal sealed class ModelItemReferenceComparer : IEqualityComparer<ModelItem>
    {
        public static readonly ModelItemReferenceComparer Instance =
            new ModelItemReferenceComparer();

        private ModelItemReferenceComparer()
        {
        }

        public bool Equals(ModelItem x, ModelItem y)
        {
            return object.ReferenceEquals(x, y);
        }

        public int GetHashCode(ModelItem obj)
        {
            return obj == null ? 0 : RuntimeHelpers.GetHashCode(obj);
        }
    }
}
