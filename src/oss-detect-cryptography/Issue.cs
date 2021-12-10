// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource
{
    using Microsoft.ApplicationInspector.RulesEngine;

    public record Issue
    {
        public Boundary Boundary { get; }
        public Location StartLocation { get; }
        public Location EndLocation { get; }
        public Rule Rule { get; }

        public Issue(Boundary Boundary, Location StartLocation, Location EndLocation, Rule Rule)
        {
            this.Boundary = Boundary;
            this.StartLocation = StartLocation;
            this.EndLocation = EndLocation;
            this.Rule = Rule;
        }
    }
}