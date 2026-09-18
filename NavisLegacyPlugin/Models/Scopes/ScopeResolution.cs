using Autodesk.Navisworks.Api;
using System;
using System.Collections.Generic;

namespace NavisLegacyPlugin.Models.Scopes
{
    public sealed class ScopeResolution
    {
        public ScopeResolution(
            ScopeDefinition definition,
            IReadOnlyCollection<ModelItem> items,
            DateTime resolvedAt)
        {
            Definition = definition;
            Items = items;
            ResolvedAt = resolvedAt;
        }

        public ScopeDefinition Definition
        {
            get;
            private set;
        }

        public IReadOnlyCollection<ModelItem> Items
        {
            get;
            private set;
        }

        public int ItemCount
        {
            get
            {
                return Items.Count;
            }
        }

        public string ScopePath
        {
            get
            {
                return Definition.ScopePathText;
            }
        }

        public DateTime ResolvedAt
        {
            get;
            private set;
        }
    }
}
