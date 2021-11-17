using System;

namespace Microsoft.CST.OpenSource.FindSquats
{
    public class Mutation
    {
        public string Mutated { get; set; }
        public string Original { get; set; }
        public string Reason { get; set; }
        public Type Mutator { get; set; }
    }
}