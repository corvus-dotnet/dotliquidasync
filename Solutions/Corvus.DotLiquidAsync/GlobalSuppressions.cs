// <copyright file="GlobalSuppressions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <summary>
// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.
// </summary>

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Design",
    "RCS1194:Implement exception constructors.",
    Justification = "These exceptions are designed to be thrown by this project, and are not for general use by other code, so they do not need all the normal overloads",
    Scope = "namespaceanddescendants",
    Target = "DotLiquid.Exceptions")]
