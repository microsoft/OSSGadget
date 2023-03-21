// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.FindSquats.Mutators
{
    /// <summary>
    /// An enum used by classes that implement <see cref="IMutator"/> to specify which kind they are.
    /// </summary>
    public enum MutatorType
    {
        /// <summary>
        /// The value has not been set.
        /// </summary>
        Unspecified,
        /// <summary>
        /// A mutator which was not included with oss-find-squats-lib.
        /// </summary>
        Custom,
        /// <summary>
        /// The <see cref="AddCharacterMutator"/> mutator.
        /// </summary>
        AddCharacter,
        /// <summary>
        /// The <see cref="AddContainingCharacterMutator"/> mutator.
        /// </summary>
        AddContainingCharacter,
        /// <summary>
        /// The <see cref="AsciiHomoglyphMutator"/> mutator.
        /// </summary>
        AsciiHomoglyph,
        /// <summary>
        /// The <see cref="BitFlipMutator"/> mutator.
        /// </summary>
        BitFlip,
        /// <summary>
        /// The <see cref="CloseLettersMutator"/> mutator.
        /// </summary>
        CloseLetters,
        /// <summary>
        /// The <see cref="DoubleHitMutator"/> mutator.
        /// </summary>
        DoubleHit,
        /// <summary>
        /// The <see cref="DuplicatorMutator"/> mutator.
        /// </summary>
        Duplicator,
        /// <summary>
        /// The <see cref="NamespaceInNameMutator"/> mutator.
        /// </summary>
        NamespaceInName,
        /// <summary>
        /// The <see cref="PrefixMutator"/> mutator.
        /// </summary>
        Prefix,
        /// <summary>
        /// The <see cref="RemovedCharacterMutator"/> mutator.
        /// </summary>
        RemovedCharacter,
        /// <summary>
        /// The <see cref="RemoveNamespaceMutator"/> mutator.
        /// </summary>
        RemovedNamespace,
        /// <summary>
        /// The <see cref="RemoveSeparatedSectionMutator"/> mutator.
        /// </summary>
        RemoveSeparatedSection,
        /// <summary>
        /// The <see cref="ReplaceCharacterMutator"/> mutator.
        /// </summary>
        ReplaceCharacter,
        /// <summary>
        /// The <see cref="SeparatorChangedMutator"/> mutator.
        /// </summary>
        SeparatorChanged,
        /// <summary>
        /// The <see cref="SeparatorRemovedMutator"/> mutator.
        /// </summary>
        SeparatorRemoved,
        /// <summary>
        /// The <see cref="SubstitutionMutator"/> mutator.
        /// </summary>
        Substitution,
        /// <summary>
        /// The <see cref="SuffixMutator"/> mutator.
        /// </summary>
        Suffix,
        /// <summary>
        /// The <see cref="SwapOrderOfLettersMutator"/> mutator.
        /// </summary>
        SwapOrderOfLetters,
        /// <summary>
        /// The <see cref="UnicodeHomoglyphMutator"/> mutator.
        /// </summary>
        UnicodeHomoglyph,
        /// <summary>
        /// The <see cref="VowelSwapMutator"/> mutator.
        /// </summary>
        VowelSwap
    }
}
