using System;
using System.Collections.Generic;
using System.Linq;

namespace NavisLegacyPlugin.Models.Scopes
{
    public sealed class ScopeDefinition
    {
        public ScopeDefinition(
            TransferScopeType scopeType,
            DocumentLocation documentLocation,
            IEnumerable<string> scopePath)
        {
            if (documentLocation == null)
            {
                throw new ArgumentNullException(
                    "documentLocation");
            }

            if (scopePath == null)
            {
                throw new ArgumentNullException(
                    "scopePath");
            }

            ScopeType = scopeType;
            DocumentLocation = documentLocation;
            ScopePath = scopePath.ToArray();
        }

        public TransferScopeType ScopeType
        {
            get;
            private set;
        }

        public DocumentLocation DocumentLocation
        {
            get;
            private set;
        }

        public string[] ScopePath
        {
            get;
            private set;
        }

        public string ScopePathText
        {
            get
            {
                return string.Join("/", ScopePath);
            }
        }
    }
}
