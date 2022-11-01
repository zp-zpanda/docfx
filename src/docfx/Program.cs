// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

var outputOption = new Option<string>(new[] { "-o", "--output" }, "Output directory in which to place built artifacts.");

var rootCommand = new RootCommand();
rootCommand.AddGlobalOption(outputOption);

rootCommand.SetHandler(
    context => DotnetApiDocs.ToYaml(
        Environment.CurrentDirectory,
        context.ParseResult.GetValueForOption(outputOption) ?? "api"));

rootCommand.Invoke(args);
