﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using MoreLinq;
using Sdl.DesktopEditor.EditorApi;
using Sdl.LanguagePlatform.TranslationMemory;
using Sdl.ProjectAutomation.Core;
using Sdl.TranslationStudioAutomation.IntegrationApi;

namespace Sdl.Community.AddSourceTM
{
    public static class Extension
    {
        public static Uri GetInnerProviderUri(this Uri addSourceTmProviderUri)
        {
            if (addSourceTmProviderUri.AbsoluteUri.StartsWith(AddSourceTmTranslationProvider.ProviderUriScheme, StringComparison.InvariantCultureIgnoreCase))
            {
                return
                    new Uri(
                        addSourceTmProviderUri.AbsoluteUri.Substring(
                            AddSourceTmTranslationProvider.ProviderUriScheme.Length));
            }

            return addSourceTmProviderUri;
        }

        public static string GetFilePath(this TranslationUnit translationUnit)
        {
            var filePath = translationUnit.FileProperties.FileConversionProperties.InputFilePath;
            //inputFilePath is not working correct in certain cases so we need to try and get the current file from 
            //the editor
            var editorController = SdlTradosStudio.Application.GetController<EditorController>();
            if (editorController == null) return filePath;
            if (editorController.ActiveDocument == null) return filePath;

            var activeFile = editorController.ActiveDocument.TryGetActiveFile();

            filePath = activeFile.LocalFilePath;

            return filePath;
        }

        public static ProjectFile TryGetActiveFile(this Document document)
        {

            if (document.ActiveFile != null) return document.ActiveFile;
            //at the time of implementation using ActiveFile property is not working always for the virtual merged files - for some reasons in the editor
            //there more file nodes for one file. This is a workaround to try and resolve it.
            var segmentPair = document.ActiveSegmentPair;
            var type = document.GetType();
            var method = type.GetMethod("GetTargetSegmentContainerNodeById",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var result = method.Invoke(document, new object[] { segmentPair.Target.Properties.Id.Id });

            var targetSegmentContainer = result as ISegmentContainerNode;

            if (targetSegmentContainer == null) return null;

            IAbstractContainerNode node = targetSegmentContainer;
            while (node != null && !(node is IFileContainerNode))
            {
                node = node.Parent;
            }
            var fileContainerNode = node as IFileContainerNode;
            if (fileContainerNode == null) return null;

            var assembly = Assembly.LoadFrom("Sdl.TranslationStudio.Common.dll");
            var filterFramweworkUtilitiesType =
                assembly.GetType("Sdl.TranslationStudio.Common.Editing.FilterFrameworkUtilities");
            var internalDocument =
                type.GetProperty("InternalDocument", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(document, null);
            var nativeBillingualDocument =
                internalDocument.GetType()
                    .GetProperty("NativeBilingualDocument")
                    .GetValue(internalDocument, null);
            var target = nativeBillingualDocument.GetType()
                .GetProperty("Target")
                .GetValue(nativeBillingualDocument, null);
            var rootContainer = target.GetType()
                .GetProperty("RootContainer")
                .GetValue(target, null);
            var getFileContainerNodesMethod = filterFramweworkUtilitiesType.GetMethod("GetFileContainerNodes",
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            var fileContainers = getFileContainerNodesMethod.Invoke(null, new[] { rootContainer, null });
            var fileContainerNodes = fileContainers as List<IFileContainerNode>;
            if (fileContainerNodes == null) return null;


            var distinctFileContainerNodes = fileContainerNodes.DistinctBy(x => x.FileProperties.FileConversionProperties.FileId).ToList();

            var index =
                distinctFileContainerNodes.FindIndex(
                    f =>
                        f.FileProperties.FileConversionProperties.FileId.Equals(
                            fileContainerNode.FileProperties.FileConversionProperties.FileId));
            if (0 <= index && index < document.Files.Count())
            {
                return document.Files.ElementAt(index);
            }
            return null;
        }
    }
}