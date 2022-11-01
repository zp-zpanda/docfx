// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Xunit;
using YamlDotNet.Serialization;

[UsesVerify]
public static class DotnetTest
{
    public static TheoryData<string> GetTestCases()
    {
        var testCases = new TheoryData<string>();
        foreach (var testCase in Directory.GetDirectories("dotnet"))
        {
            testCases.Add(testCase.Replace('\\', '/'));
        }
        return testCases;
    }

    [Theory]
    [MemberData(nameof(GetTestCases))]
    public static async Task ToYaml(string testCase)
    {
        var outputDirectory = Path.Join(testCase, "api");
        var inputDirectory = BuildProject(testCase);

        if (Directory.Exists(outputDirectory))
            Directory.Delete(outputDirectory, recursive: true);

        DotnetApiDocs.ToYaml(inputDirectory, outputDirectory);

        await Verify(ReadDirectory(outputDirectory));
    }

    private static string BuildProject(string input)
    {
        Process.Start(new ProcessStartInfo { FileName = "dotnet", Arguments = "build", WorkingDirectory = input })?.WaitForExit();

        return Path.ChangeExtension(Directory.GetFiles(Path.Join(input, "bin"), "*.pdb", SearchOption.AllDirectories).First(), "dll");
    }

    private static Dictionary<string, object> ReadDirectory(string path)
    {
        var deserializer = new DeserializerBuilder().Build();

        return Directory.GetFiles(path).ToDictionary(
            file => file.Replace('\\', '/'),
            file => deserializer.Deserialize<object>(File.ReadAllText(file)));
    }
}
