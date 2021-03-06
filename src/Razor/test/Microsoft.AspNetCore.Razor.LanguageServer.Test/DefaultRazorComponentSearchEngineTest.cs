﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Razor.Extensions.Version2_X;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Moq;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test
{
    public class DefaultRazorComponentSearchEngineTest : LanguageServerTestBase
    {
        private static ProjectSnapshotManager _projectSnapshotManager = CreateProjectSnapshotManager();
        private static readonly DefaultRazorComponentSearchEngine _searchEngine = new DefaultRazorComponentSearchEngine(_projectSnapshotManager);

        [Fact]
        public async void Handle_SearchFound()
        {
            // Arrange
            var tagHelperDescriptor1 = CreateRazorComponentTagHelperDescriptor("First", "First.Components", "Component1");
            var tagHelperDescriptor2 = CreateRazorComponentTagHelperDescriptor("Second", "Second.Components", "Component3");

            // Act
            var documentSnapshot1 = await _searchEngine.TryLocateComponentAsync(tagHelperDescriptor1).ConfigureAwait(false);
            var documentSnapshot2 = await _searchEngine.TryLocateComponentAsync(tagHelperDescriptor2).ConfigureAwait(false);

            // Assert
            Assert.NotNull(documentSnapshot1);
            Assert.NotNull(documentSnapshot2);
        }

        [Fact]
        public async void Handle_SearchFound_SetNamespace()
        {
            // Arrange
            var tagHelperDescriptor = CreateRazorComponentTagHelperDescriptor("First", "Test", "Component2");

            // Act
            var documentSnapshot = await _searchEngine.TryLocateComponentAsync(tagHelperDescriptor).ConfigureAwait(false);

            // Assert
            Assert.NotNull(documentSnapshot);
        }

        [Fact]
        public async void Handle_SearchMissing_IncorrectAssembly()
        {
            // Arrange
            var tagHelperDescriptor = CreateRazorComponentTagHelperDescriptor("Third", "First.Components", "Component3");

            // Act
            var documentSnapshot = await _searchEngine.TryLocateComponentAsync(tagHelperDescriptor).ConfigureAwait(false);

            // Assert
            Assert.Null(documentSnapshot);
        }

        [Fact]
        public async void Handle_SearchMissing_IncorrectNamespace()
        {
            // Arrange
            var tagHelperDescriptor = CreateRazorComponentTagHelperDescriptor("First", "First.Components", "Component2");

            // Act
            var documentSnapshot = await _searchEngine.TryLocateComponentAsync(tagHelperDescriptor).ConfigureAwait(false);

            // Assert
            Assert.Null(documentSnapshot);
        }

        [Fact]
        public async void Handle_SearchMissing_IncorrectComponent()
        {
            // Arrange
            var tagHelperDescriptor = CreateRazorComponentTagHelperDescriptor("First", "First.Components", "Component3");

            // Act
            var documentSnapshot = await _searchEngine.TryLocateComponentAsync(tagHelperDescriptor).ConfigureAwait(false);

            // Assert
            Assert.Null(documentSnapshot);
        }

        internal static TagHelperDescriptor CreateRazorComponentTagHelperDescriptor(string assemblyName, string namespaceName, string tagName)
        {
            var fullyQualifiedName = $"{namespaceName}.{tagName}";
            var builder1 = TagHelperDescriptorBuilder.Create(ComponentMetadata.Component.TagHelperKind, fullyQualifiedName, assemblyName);
            builder1.TagMatchingRule(rule => rule.TagName = tagName);
            return builder1.Build();
        }

        internal static DocumentSnapshot CreateRazorDocumentSnapshot(string text, string filePath, string rootNamespaceName)
        {
            var sourceDocument = TestRazorSourceDocument.Create(text, filePath: filePath, relativePath: filePath);
            var projectEngine = RazorProjectEngine.Create(RazorConfiguration.Default, TestRazorProjectFileSystem.Empty, builder => {
                builder.AddDirective(NamespaceDirective.Directive);
                builder.SetRootNamespace(rootNamespaceName);
            });
            var codeDocument = projectEngine.Process(sourceDocument, FileKinds.Component, Array.Empty<RazorSourceDocument>(), Array.Empty<TagHelperDescriptor>());

            var namespaceNode = (NamespaceDeclarationIntermediateNode)codeDocument
                .GetDocumentIntermediateNode()
                .FindDescendantNodes<IntermediateNode>()
                .FirstOrDefault(n => n is NamespaceDeclarationIntermediateNode);
            namespaceNode.Content = rootNamespaceName;

            var documentSnapshot = Mock.Of<DocumentSnapshot>(d =>
                d.GetGeneratedOutputAsync() == Task.FromResult(codeDocument) &&
                d.FilePath == filePath &&
                d.FileKind == FileKinds.Component);
            return documentSnapshot;
        }
    
        internal static ProjectSnapshotManager CreateProjectSnapshotManager()
        {
            var firstProject = Mock.Of<ProjectSnapshot>(p =>
                p.FilePath == "c:/First/First.csproj" &&
                p.DocumentFilePaths == new[] { "c:/First/Component1.razor", "c:/First/Component2.razor" } &&
                p.GetDocument("c:/First/Component1.razor") == CreateRazorDocumentSnapshot("", "c:/First/Component1.razor", "First.Components") &&
                p.GetDocument("c:/First/Component2.razor") == CreateRazorDocumentSnapshot("@namespace Test", "c:/First/Component2.razor", "Test"));

            var secondProject = Mock.Of<ProjectSnapshot>(p =>
                p.FilePath == "c:/Second/Second.csproj" &&
                p.DocumentFilePaths == new[] { "c:/Second/Component3.razor" } &&
                p.GetDocument("c:/Second/Component3.razor") == CreateRazorDocumentSnapshot("", "c:/Second/Component3.razor", "Second.Components"));

            return Mock.Of<ProjectSnapshotManager>(p => p.Projects == new[] { firstProject, secondProject });
        }
    }
}
