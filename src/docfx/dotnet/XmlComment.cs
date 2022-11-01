// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

using System.Globalization;

public class XmlComment
{
    public string? summary { get; init; }

    public string? remarks { get; init; }

    public string? returns { get; init; }

    public List<string>? exception { get; init; }

    public List<string>? seealso { get; init; }

    public List<string>? example { get; init; }

    public Dictionary<string, string>? param { get; init; }

    public Dictionary<string, string>? typeparam { get; init; }

    public string? inheritdoc { get; init; }

    public static XmlComment? Parse(string? xmlDoc)
    {
        if (xmlDoc is null)
            return null;

        try
        {
            var xml = XDocument.Parse($"<xmldoc>{xmlDoc}</xmldoc>");

            return new()
            {
                summary = GetString("summary"),
                remarks = GetString("remarks"),
                returns = GetString("returns"),
                inheritdoc = GetString("inheritdoc"),
                typeparam = GetDictionary("typeparam"),
                param = GetDictionary("param"),
                example = GetList("example"),
                seealso = GetList("seealso"),
                exception = GetList("exception"),
            };

            string? GetString(string key)
            {
                return xml.Root?.Element(key)?.Value?.Trim();
            }

            Dictionary<string, string>? GetDictionary(string key)
            {
                return xml.Root?.Elements(key)?.ToDictionary(
                    e => e.Attribute("name")?.Value?.Trim() ?? "",
                    e => e.Value?.Trim() ?? "");
            }

            List<string>? GetList(string key)
            {
                return xml.Root?.Elements(key)?.Select(e => e?.Value?.Trim() ?? "").ToList();
            }
        }
        catch (XmlException)
        {
            return null;
        }
    }
}
