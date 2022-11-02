// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Reflection.Metadata;
using System.Text;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.OutputVisitor;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.Documentation;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.Output;
using ICSharpCode.Decompiler.TypeSystem;
using YamlDotNet.Serialization;

public static class DotnetApiDocs
{
    enum TypeLabel { None, Classes, Interfaces, Structs, Enums, Delegates }

    public static void ToYaml(string assemblyFileName, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        var module = new PEFile(assemblyFileName);
        var settings = new DecompilerSettings { CSharpFormattingOptions = FormattingOptionsFactory.CreateAllman() };
        var resolver = new UniversalAssemblyResolver(assemblyFileName, false, module.Metadata.DetectTargetFrameworkId());
        var typeSystem = new DecompilerTypeSystem(module, resolver, settings);

        var typeSystemAstBuilder = new TypeSystemAstBuilder
        {
            ShowAttributes = true,
            AlwaysUseShortTypeNames = true,
            AddResolveResultAnnotations = true,
            UseNullableSpecifierForValueTypes = settings.LiftNullables,
            SupportInitAccessors = settings.InitAccessors,
            SupportRecordClasses = settings.RecordClasses,
            SupportRecordStructs = settings.RecordStructs
        };

        var csharpNameAmbience = new CSharpAmbience
        {
            ConversionFlags =
                ConversionFlags.UseNullableSpecifierForValueTypes |
                ConversionFlags.ShowTypeParameterList |
                ConversionFlags.ShowTypeParameterVarianceModifier,
        };

        var csharpDeclarationAmbience = new CSharpAmbience
        {
            ConversionFlags =
                ConversionFlags.UseNullableSpecifierForValueTypes |
                ConversionFlags.ShowTypeParameterList |
                ConversionFlags.ShowTypeParameterVarianceModifier,
        };

        var csharpJumplistAmbience = new CSharpAmbience
        {
            ConversionFlags = csharpNameAmbience.ConversionFlags |
                ConversionFlags.ShowParameterList |
                ConversionFlags.ShowParameterModifiers |
                ConversionFlags.ShowParameterDefaultValues |
                ConversionFlags.ShowParameterNames,
        };

        var xmlDoc = XmlDocLoader.LoadDocumentation(module);

        var serializer = new SerializerBuilder()
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
            .Build();

        var toc = new List<(string @namespace, TypeLabel label, string name, string href)>();

        foreach (var type in typeSystem.MainModule.TopLevelTypeDefinitions)
        {
            var label = type.Kind switch
            {
                TypeKind.Class => TypeLabel.Classes,
                TypeKind.Interface => TypeLabel.Interfaces,
                TypeKind.Enum => TypeLabel.Enums,
                TypeKind.Struct => TypeLabel.Structs,
                TypeKind.Delegate or TypeKind.FunctionPointer => TypeLabel.Delegates,
                _ => TypeLabel.None,
            };

            if (label is TypeLabel.None || ShouldIgnoreType())
                continue;

            var comment = XmlComment.Parse(xmlDoc?.GetDocumentation(type));
            var typeName = csharpNameAmbience.ConvertSymbol(type);

            var page = new ReferencePage
            {
                id = type.FullTypeName.ToString(),
                languageId= "csharp",
                title = $"{typeName} {type.Kind}",
                summary = comment?.summary,
                fact = new()
                {
                    ["Namespace"] = type.Namespace,
                    ["Assembly"] = $"{type.ParentModule?.Name}.dll",
                },
            };

            page.body.Add(new { declaration = FormatCSharpDeclaration(type) });

            if (comment?.example?.Any() ?? false)
                page.body.Add(new { section = "Examples", body = comment.example.Select(e => new { markdown = e }).ToArray() });

            if (!string.IsNullOrEmpty(comment?.remarks))
                page.body.Add(new { section = "Remarks", body = new[] { new { markdown = comment.remarks } } });

            DumpPage();

            if (comment?.seealso?.Any() ?? false)
                page.body.Add(new { section = "See also", body = comment.seealso.Select(e => new { markdown = e }).ToArray() });

            File.WriteAllText(Path.Join(outputDirectory, $"{type.FullTypeName}.yml"), "#YamlMime:Reference\n" + serializer.Serialize(page));

            toc.Add((type.Namespace, label, typeName, $"{type.FullTypeName}.yml"));

            bool ShouldIgnoreType()
            {
                if (type.Accessibility != Accessibility.Public)
                    return true;

                if (type.Name == "<Module>" || type.Name == "GeneratedInternalTypeHelper" || type.Namespace == "XamlGeneratedNamespace")
                    return true;

                return false;
            }

            bool DumpPage()
            {
                return type.Kind switch
                {
                    TypeKind.Class or TypeKind.Struct or TypeKind.Interface => DumpClassLike(),
                    TypeKind.Enum => DumpEnum(),
                    TypeKind.Delegate => DumpDelegate(),
                    _ => throw new NotSupportedException($"Unsupported type kind {type.Kind}"),
                };
            }

            bool DumpClassLike()
            {
                AddTypeParameters("Type Parameters", type.TypeParameters, comment?.typeparam);

                AddJumplist("Constructors", type.GetConstructors());
                AddJumplist("Fields", type.GetFields(options: GetMemberOptions.IgnoreInheritedMembers));
                AddJumplist("Properties", type.GetProperties(options: GetMemberOptions.IgnoreInheritedMembers));
                AddJumplist("Methods", type.GetMethods(m => !m.IsOperator && !m.IsExplicitInterfaceImplementation, options: GetMemberOptions.IgnoreInheritedMembers));
                AddJumplist("Operators", type.GetMethods(m => m.IsOperator, options: GetMemberOptions.IgnoreInheritedMembers));
                AddJumplist("Explicit Interface Implementations", type.GetMethods(m => m.IsExplicitInterfaceImplementation, options: GetMemberOptions.IgnoreInheritedMembers));
                AddJumplist("Events", type.GetEvents(options: GetMemberOptions.IgnoreInheritedMembers));

                return true;
            }

            bool DumpEnum()
            {
                var parameters = type
                    .GetFields(item => item.IsStatic, GetMemberOptions.IgnoreInheritedMembers)
                    .Select(item => new
                    {
                        name = item.Name,
                        description = XmlComment.Parse(xmlDoc?.GetDocumentation(item))?.summary,
                        @default = $"{item.GetConstantValue()}",
                    })
                    .ToList();

                if (parameters.Count > 0)
                    page.body.Add(new { section = "Fields", body = new[] { new { parameters } } });

                return true;
            }

            bool DumpDelegate()
            {
                AddTypeParameters("Type Parameters", type.TypeParameters, comment?.typeparam);
                AddParameterList("Parameters", type.Methods.First(method => method.Name == "Invoke").Parameters, comment.param);

                return true;
            }

            void AddJumplist(string section, IEnumerable<IEntity> items)
            {
                if (!items.Any())
                    return;

                var jumplist = items
                    .Select(item => new
                    {
                        name = csharpJumplistAmbience.ConvertSymbol(item),
                        description = XmlComment.Parse(xmlDoc?.GetDocumentation(item))?.summary,
                    })
                    .ToList();

                page.body.Add(new { section, body = new[] { new { jumplist } } });
            }

            void AddParameterList(string section, IEnumerable<ISymbol> items, Dictionary<string, string>? comment = null)
            {
                if (!items.Any())
                    return;

                var parameters = items
                    .Select(item => new
                    {
                        name = item.Name,
                        type = FormatCSharpType(item),
                        description = TryGetValue(comment, item.Name),
                    })
                    .ToList();

                page.body.Add(new { section, body = new[] { new { parameters } } });
            }

            void AddTypeParameters(string section, IEnumerable<ISymbol> items, Dictionary<string, string>? comment = null)
            {
                if (!items.Any())
                    return;

                var parameters = items.Select(item => new
                {
                    name = item.Name,
                    description = TryGetValue(comment, item.Name),
                }).ToList();

                page.body.Add(new { section, body = new[] { new { parameters } } });
            }

            File.WriteAllText(
                Path.Join(outputDirectory, "TOC.yml"),
                serializer.Serialize(
                    toc.GroupBy(e => e.@namespace).OrderBy(g => g.Key).Select(g => new
                    {
                        name = g.Key,
                        items = g.GroupBy(e => e.label).OrderBy(g => g.Key).SelectMany(g =>
                            new object[] { new { label = g.Key.ToString() } }.Concat(
                                g.OrderBy(e => e.name).Select(e => new { name = e.name, href = e.href }))).ToList(),
                    }).ToList()));
        }

        string FormatCSharpDeclaration(ITypeDefinition type)
        {
            var result = new StringBuilder();
            var ast = typeSystemAstBuilder.ConvertSymbol(type);

            if (type.Properties.Any(p => p.IsIndexer) && ast is EntityDeclaration entityDecl)
            {
                // Remove the [DefaultMember] attribute if the class contains indexers
                // https://github.com/icsharpcode/ILSpy/blob/v7.2.1/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs#L1323
                RemoveAttribute(entityDecl, "System.Reflection.DefaultMemberAttribute");
            }

            using var writer = new StringWriter(result);
            ast.AcceptVisitor(new CSharpOutputVisitor(writer, settings.CSharpFormattingOptions));
            return result.ToString().Trim(' ', '{', '}', '\r', '\n', ';');
        }

        static void RemoveAttribute(EntityDeclaration entityDecl, string attributeType)
        {
            foreach (var section in entityDecl.Attributes)
            {
                foreach (var attr in section.Attributes)
                {
                    var symbol = attr.Type.GetSymbol();
                    if (symbol is ITypeDefinition td && td.FullName == attributeType)
                    {
                        attr.Remove();
                    }
                }
                if (section.Attributes.Count == 0)
                {
                    section.Remove();
                }
            }
        }

        string FormatCSharpType(ISymbol symbol)
        {
            var result = new StringBuilder();
            var ast = typeSystemAstBuilder.ConvertSymbol(symbol);
            var type = ast.GetChildByRole(Roles.Type);
            using var writer = new StringWriter(result);
            type.AcceptVisitor(new CSharpOutputVisitor(writer, settings.CSharpFormattingOptions));
            return result.ToString();
        }

        static string? TryGetValue(Dictionary<string, string>? dictionary, string key)
        {
            return dictionary != null && dictionary.TryGetValue(key, out var value) ? value : null;
        }
    }
}
