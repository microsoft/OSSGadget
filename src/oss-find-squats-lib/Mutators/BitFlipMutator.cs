// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.CST.OpenSource.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    /// <summary>
    /// Generates mutations for a flipped bit.
    /// </summary>
    public class BitFlipMutator : Mutator
    {
        public MutatorType Kind { get; } = MutatorType.BitFlip;

        public IEnumerable<Mutation> Generate(string arg)
        {
            var byteArray = Encoding.UTF8.GetBytes(arg);
            for (int i = 0; i < byteArray.Length; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    byte mask = (byte)(1 << j);
                    byteArray[i] = (byte)(byteArray[i] ^ mask);
                    var newString = Encoding.UTF8.GetString(byteArray);
                    var valid = true;

                    for (int k = 0; k < newString.Length && valid; k++)
                    {
                        if (!Uri.IsWellFormedUriString(newString, UriKind.Relative))
                        {
                            valid = false;
                        }
                    }
                    if (valid)
                    {
                        yield return new Mutation(
                            mutated: newString,
                            original: arg,
                            mutator: Kind,
                            reason: "Bit Flips");
                    }
                }
            }
        }
    }
}