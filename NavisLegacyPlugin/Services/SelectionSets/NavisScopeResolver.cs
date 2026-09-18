using Autodesk.Navisworks.Api;
using NavisLegacyPlugin.Models.Scopes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NavisLegacyPlugin.Services.SelectionSets
{
    public sealed class NavisScopeResolver
        : ITransferScopeResolver
    {
        private readonly SelectionSetPathResolver
            _selectionSetPathResolver;

        public NavisScopeResolver()
            : this(new SelectionSetPathResolver())
        {
        }

        public NavisScopeResolver(
            SelectionSetPathResolver selectionSetPathResolver)
        {
            _selectionSetPathResolver =
                selectionSetPathResolver;
        }

        public ScopeResolution Resolve(
            Document document,
            ScopeDefinition definition)
        {
            if (document == null)
            {
                throw new ArgumentNullException(
                    "document");
            }

            if (definition == null)
            {
                throw new ArgumentNullException(
                    "definition");
            }

            IReadOnlyList<ModelItem> items =
                _selectionSetPathResolver.ResolveRequired(
                    document,
                    definition.ScopePath);

            Dictionary<Guid, ModelItem> unique =
                new Dictionary<Guid, ModelItem>();

            foreach (ModelItem item in items)
            {
                if (!unique.ContainsKey(
                    item.InstanceGuid))
                {
                    unique.Add(
                        item.InstanceGuid,
                        item);
                }
            }

            return new ScopeResolution(
                definition,
                unique.Values.ToList().AsReadOnly(),
                DateTime.UtcNow);
        }
    }
}
