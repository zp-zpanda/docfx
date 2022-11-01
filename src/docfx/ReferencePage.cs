// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Reflection;

public class ReferencePage
{
    public string id { get; set; } = "";

    public string languageId { get; set; } = "";

    public string title { get; set; } = "";

    public string? summary { get; set; }

    public Dictionary<string, object>? fact { get; set; }

    public List<object> body { get; set; } = new();
}
