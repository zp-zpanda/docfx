// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DocAsCode.Metadata.ManagedReference
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;

    using Newtonsoft.Json;
    using YamlDotNet.Serialization;

    using Microsoft.DocAsCode.DataContracts.Common;
    using Microsoft.DocAsCode.DataContracts.ManagedReference;

    public class ReferenceItem
    {
        [YamlMember(Alias = "name")]
        [JsonProperty("name")]
        public SortedList<SyntaxLanguage, List<LinkItem>> NameParts { get; set; }

        [YamlMember(Alias = "nameWithType")]
        [JsonProperty("nameWithType")]
        public SortedList<SyntaxLanguage, List<LinkItem>> NameWithTypeParts { get; set; }

        [YamlMember(Alias = "qualifiedName")]
        [JsonProperty("qualifiedName")]
        public SortedList<SyntaxLanguage, List<LinkItem>> QualifiedNameParts { get; set; }

        [YamlMember(Alias = "isDefinition")]
        [JsonProperty("isDefinition")]
        public bool? IsDefinition { get; set; }

        [YamlMember(Alias = "definition")]
        [JsonProperty("definition")]
        public string Definition { get; set; }

        [YamlMember(Alias = "parent")]
        [JsonProperty("parent")]
        public string Parent { get; set; }

        [YamlMember(Alias = Constants.PropertyName.CommentId)]
        [JsonProperty(Constants.PropertyName.CommentId)]
        public string CommentId { get; set; }

        public ReferenceItem Clone()
        {
            var result = (ReferenceItem)MemberwiseClone();
            result.NameParts = CloneParts(result.NameParts);
            result.NameWithTypeParts = CloneParts(result.NameWithTypeParts);
            result.QualifiedNameParts = CloneParts(result.QualifiedNameParts);
            return result;

            SortedList<SyntaxLanguage, List<LinkItem>> CloneParts(SortedList<SyntaxLanguage, List<LinkItem>> parts)
            {
                if (parts is null)
                    return null;

                var dict = new SortedList<SyntaxLanguage, List<LinkItem>>(parts.Count);
                foreach (var item in parts)
                {
                    dict.Add(item.Key, (from x in item.Value select x.Clone()).ToList());
                }
                return dict;
            }
        }

        private static T? Merge<T>(T? source, T? target) where T : struct
        {
            Debug.Assert(source == null || target == null || Nullable.Equals(source, target));
            return source ?? target;
        }

        private static T Merge<T>(T source, T target) where T : class
        {
            Debug.Assert(source == null || target == null || object.Equals(source, target));
            return source ?? target;
        }

        private static string MergeCommentId(string source, string target)
        {
            bool sourceIsNotError = source?.StartsWith("!:", StringComparison.Ordinal) == false;
            bool targetIsNotError = target?.StartsWith("!:", StringComparison.Ordinal) == false;
            if (sourceIsNotError && targetIsNotError)
            {
                return Merge(source, target);
            }
            if (sourceIsNotError)
            {
                return source;
            }
            if (targetIsNotError)
            {
                return target;
            }
            return null;
        }

        public void Merge(ReferenceItem other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }
            IsDefinition = Merge(other.IsDefinition, IsDefinition);
            Definition = Merge(other.Definition, Definition);
            Parent = Merge(other.Parent, Parent);
            if (IsDefinition == true)
            {
                CommentId = MergeCommentId(other.CommentId, CommentId);
            }

            NameParts = MergeParts(NameParts, other.NameParts);
            NameWithTypeParts = MergeParts(NameWithTypeParts, other.NameWithTypeParts);
            QualifiedNameParts = MergeParts(QualifiedNameParts, other.QualifiedNameParts);

            SortedList<SyntaxLanguage, List<LinkItem>> MergeParts(SortedList<SyntaxLanguage, List<LinkItem>> self, SortedList<SyntaxLanguage, List<LinkItem>> other)
            {
                if (other != null && self != null)
                {
                    foreach (var pair in other)
                    {
                        var sourceParts = pair.Value;
                        if (self.TryGetValue(pair.Key, out List<LinkItem> targetParts))
                        {
                            if (sourceParts.Count == 0)
                            {
                                continue;
                            }
                            if (targetParts.Count == 0)
                            {
                                targetParts.AddRange(sourceParts);
                                continue;
                            }

                            Debug.Assert(sourceParts.Count == targetParts.Count);

                            if (sourceParts.Count == targetParts.Count)
                            {
                                for (int i = 0; i < sourceParts.Count; i++)
                                {
                                    Debug.Assert(sourceParts[i].Name == targetParts[i].Name);
                                    targetParts[i].IsExternalPath &= sourceParts[i].IsExternalPath;
                                    targetParts[i].Href = targetParts[i].Href ?? sourceParts[i].Href;
                                }
                            }
                        }
                        else
                        {
                            self.Add(pair.Key, pair.Value);
                        }
                    }
                    return self;
                }
                else
                {
                    return self ?? other;
                }
            }
        }
    }

    public class LinkItem
    {
        [YamlMember(Alias = "id")]
        [JsonProperty("id")]
        public string Name { get; set; }

        [YamlMember(Alias = "name")]
        [JsonProperty("name")]
        public string DisplayName { get; set; }

        /// <summary>
        /// The external path for current source if it is not locally available
        /// </summary>
        [YamlMember(Alias = "isExternal")]
        [JsonProperty("isExternal")]
        public bool IsExternalPath { get; set; }

        /// <summary>
        /// The url path for current source, should be resolved at some late stage
        /// </summary>
        [YamlMember(Alias = Constants.PropertyName.Href)]
        [JsonProperty(Constants.PropertyName.Href)]
        public string Href { get; set; }

        public LinkItem Clone() => (LinkItem)MemberwiseClone();

        public override string ToString() => DisplayName;
    }
}
