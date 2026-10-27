using Autodesk.Navisworks.Api;
using NavisLegacyPlugin.Models.Scopes;
using System;
using System.Collections.Generic;
using System.Linq;
using NavisLegacyPlugin.Services.Collections;

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

            List<ModelItem> unique = items
                .Where(item => item != null)
                .Distinct(ModelItemReferenceComparer.Instance)
                .ToList();
            return new ScopeResolution(
                definition,
                unique.AsReadOnly(),
                DateTime.UtcNow);
        }
    }
}
