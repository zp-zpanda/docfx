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

        var csharpSignatureAmbience = new CSharpAmbience
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
            DumpType(type);
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

        void DumpType(ITypeDefinition type)
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
                return;

            var comment = XmlComment.Parse(xmlDoc?.GetDocumentation(type));
            var typeName = csharpNameAmbience.ConvertSymbol(type);

            var page = new ReferencePage
            {
                id = type.FullTypeName.ToString(),
                languageId = "csharp",
                title = $"{typeName} {type.Kind}",
                summary = comment?.summary,
                fact = new()
                {
                    ["Namespace"] = type.Namespace,
                    ["Assembly"] = $"{type.ParentModule?.Name}.dll",
                },
            };

            page.body.Add(new { declaration = FormatCSharpDeclaration(type) });

            AddTypeParameters(page.body, "Type Parameters", type.TypeParameters, comment?.typeparam);

            AddExamplesAndRemarks(page.body, comment);

            DumpPage();

            AddSeeAlso(page.body, comment);

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
                    TypeKind.Class or TypeKind.Struct or TypeKind.Interface => DumpClassLike(page.body),
                    TypeKind.Enum => DumpEnum(page.body),
                    TypeKind.Delegate => DumpDelegate(page.body),
                    _ => throw new NotSupportedException($"Unsupported type kind {type.Kind}"),
                };
            }

            bool DumpClassLike(List<object> body)
            {
                var constructors = type.GetConstructors();
                var fields = type.GetFields(options: GetMemberOptions.IgnoreInheritedMembers);
                var properties = type.GetProperties(options: GetMemberOptions.IgnoreInheritedMembers);
                var methods = type.GetMethods(m => !m.IsOperator && !m.IsExplicitInterfaceImplementation, options: GetMemberOptions.IgnoreInheritedMembers);
                var operators = type.GetMethods(m => m.IsOperator, options: GetMemberOptions.IgnoreInheritedMembers);
                var eiis = type.GetMethods(m => m.IsExplicitInterfaceImplementation, options: GetMemberOptions.IgnoreInheritedMembers);
                var events = type.GetEvents(options: GetMemberOptions.IgnoreInheritedMembers);

                AddJumplist(body, "Constructors", constructors);
                AddJumplist(body, "Fields", fields);
                AddJumplist(body, "Properties", properties);
                AddJumplist(body, "Methods", methods);
                AddJumplist(body, "Operators", operators);
                AddJumplist(body, "Explicit Interface Implementations", eiis);
                AddJumplist(body, "Events", events);

                AddMemberDetails(body, "Constructors", constructors);
                AddMemberDetails(body, "Fields", fields);
                AddMemberDetails(body, "Properties", properties);
                AddMemberDetails(body, "Methods", methods);
                AddMemberDetails(body, "Operators", operators);
                AddMemberDetails(body, "Explicit Interface Implementations", eiis);
                AddMemberDetails(body, "Events", events);

                return true;
            }

            bool DumpEnum(List<object> body)
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
                    body.Add(new { section = "Fields", body = new[] { new { parameters } } });

                return true;
            }

            bool DumpDelegate(List<object> body)
            {
                var method = type.Methods.First(method => method.Name == "Invoke");
                AddParameterList(body, "Parameters", method.Parameters, comment?.param);
                AddReturnValue(body, "Returns", method, comment);

                return true;
            }

            void AddJumplist(List<object> body, string section, IEnumerable<IEntity> items)
            {
                if (!items.Any())
                    return;

                var jumplist = items
                    .Select(item => new
                    {
                        name = csharpSignatureAmbience.ConvertSymbol(item),
                        description = XmlComment.Parse(xmlDoc?.GetDocumentation(item))?.summary,
                    })
                    .OrderBy(item => item.name)
                .ToList();

                body.Add(new { section, body = new[] { new { jumplist } } });
            }

            void AddMemberDetails(List<object> body, string section, IEnumerable<IEntity> items)
            {
                if (!items.Any())
                    return;

                var sections = items
                    .Select(item => new
                    {
                        section = csharpSignatureAmbience.ConvertSymbol(item),
                        body = DumpMemberDetails(item),
                    })
                    .OrderBy(item => item.section)
                    .ToList();

                body.Add(new { section, body = sections });

                List<object> DumpMemberDetails(IEntity item)
                {
                    var body = new List<object>();
                    var comment = XmlComment.Parse(xmlDoc?.GetDocumentation(item));

                    if (!string.IsNullOrEmpty(comment?.summary))
                        body.Add(new { markdown = comment.summary });

                    body.Add(new { declaration = FormatCSharpDeclaration(item) });

                    if (item is IParameterizedMember parameterizedMember)
                        AddParameterList(body, "Parameters", parameterizedMember.Parameters, comment?.param);

                    if (item is IMember member)
                    {
                        var returnSection = member switch
                        {
                            IMethod => "Returns",
                            IProperty => "Property Value",
                            IField => "Field Value",
                            _ => null,
                        };

                        if (returnSection != null)
                            AddReturnValue(body, returnSection, member, comment);
                    }

                    AddExceptions(body, comment);
                    AddExamplesAndRemarks(body, comment);
                    AddSeeAlso(body, comment);

                    return body;
                }
            }

            void AddParameterList(List<object> body, string section, IEnumerable<ISymbol> items, Dictionary<string, string>? comment = null)
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

                body.Add(new { section, body = new[] { new { parameters } } });
            }

            void AddTypeParameters(List<object> body, string section, IEnumerable<ISymbol> items, Dictionary<string, string>? comment = null)
            {
                if (!items.Any())
                    return;

                var parameters = items.Select(item => new
                {
                    name = item.Name,
                    description = TryGetValue(comment, item.Name),
                }).ToList();

                body.Add(new { section, body = new[] { new { parameters } } });
            }

            void AddReturnValue(List<object> body, string section, IMember member, XmlComment? comment)
            {
                if (member.ReturnType.Kind == TypeKind.Void)
                    return;

                var parameters = new[] { new { type = FormatCSharpType(member), description = comment?.returns } };

                body.Add(new { section, body = new[] { new { parameters } } });
            }

            void AddExceptions(List<object> body, XmlComment? comment)
            {
                if (comment?.exception?.Any() ?? false)
                {
                    var exceptions = comment.exception.Select(e => new { markdown = e }).ToList();
                    body.Add(new { section = "Exceptions", body = exceptions });
                }
            }

            void AddExamplesAndRemarks(List<object> body, XmlComment? comment)
            {
                if (comment?.example?.Any() ?? false)
                    body.Add(new { section = "Examples", body = comment.example.Select(e => new { markdown = e }).ToArray() });

                if (!string.IsNullOrEmpty(comment?.remarks))
                    body.Add(new { section = "Remarks", body = new[] { new { markdown = comment.remarks } } });
            }

            void AddSeeAlso(List<object> body, XmlComment? comment)
            {
                if (comment?.seealso?.Any() ?? false)
                    body.Add(new { section = "See also", body = comment.seealso.Select(e => new { markdown = e }).ToArray() });
            }
        }

        string FormatCSharpDeclaration(ISymbol symbol)
        {
            var result = new StringBuilder();
            var ast = typeSystemAstBuilder.ConvertSymbol(symbol);

            if (symbol is ITypeDefinition type && type.Properties.Any(p => p.IsIndexer) && ast is EntityDeclaration entityDecl)
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
