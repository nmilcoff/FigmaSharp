﻿/* 
 * ViewRenderService.cs
 * 
 * Author:
 *   Jose Medrano <josmed@microsoft.com>
 *
 * Copyright (C) 2018 Microsoft, Corp
 *
 * Permission is hereby granted, free of charge, to any person obtaining
 * a copy of this software and associated documentation files (the
 * "Software"), to deal in the Software without restriction, including
 * without limitation the rights to use, copy, modify, merge, publish,
 * distribute, sublicense, and/or sell copies of the Software, and to permit
 * persons to whom the Software is furnished to do so, subject to the
 * following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS
 * OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
 * MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN
 * NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
 * DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
 * OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE
 * USE OR OTHER DEALINGS IN THE SOFTWARE.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FigmaSharp.Converters;
using FigmaSharp.Models;
using FigmaSharp.PropertyConfigure;
using FigmaSharp.Views;

namespace FigmaSharp.Services
{
    public class ViewRenderService : RenderService
    {
        public List<ViewNode> NodesProcessed = new List<ViewNode>();
        public readonly List<ViewNode> ImageVectors = new List<ViewNode>();

        protected IView container;

        protected FigmaNode firstNode;

        public ViewPropertyConfigureBase PropertySetter { get; }

        public ViewRenderService(INodeProvider figmaProvider, NodeConverter[] figmaViewConverters = null) : this (figmaProvider, figmaViewConverters, AppContext.Current.GetViewPropertyConfigure ())
        {
          
        }

        public ViewRenderService(INodeProvider figmaProvider, NodeConverter[] figmaViewConverters, ViewPropertyConfigureBase propertySetter) : base(figmaProvider, figmaViewConverters ?? AppContext.Current.GetFigmaConverters ())
        {
            this.PropertySetter = propertySetter;
        }

        protected virtual bool SkipsNode(FigmaNode currentNode, ViewNode parent, ViewRenderServiceOptions options)
        {
            if (options != null && options.ToIgnore != null && options.ToIgnore.Contains(currentNode))
                return true;
            return false;
        }

        protected virtual bool NodeScansChildren(FigmaNode currentNode, NodeConverter converter, ViewRenderServiceOptions options)
        {
            if (converter == null)
                return false;

            if (!converter.ScanChildren(currentNode))
            {
                return false;
            }

            if (!options.ScanChildrenFromFigmaInstances && (currentNode is FigmaInstance || currentNode is FigmaComponentEntity))
            {
                return false;
            }

            return true;
        }

        public void ProcessFromNode(FigmaNode figmaNode, IView View, ViewRenderServiceOptions options)
        {
            try
            {
                var processedParentView = new ViewNode(figmaNode, View);
                NodesProcessed.Add(processedParentView);

                //in canvas we want calculate the bounds size
                if (figmaNode is FigmaCanvas canvas)
                {
                    canvas.absoluteBoundingBox = canvas.GetCurrentBounds();
                }

                if (figmaNode is FigmaCanvas || !options.GenerateMainView)
                {
                    if (figmaNode is IFigmaNodeContainer container)
                    {
                        foreach (var item in container.children)
                            GenerateViewsRecursively(item, processedParentView, options);
                    }
                }
                else
                {
                    GenerateViewsRecursively(figmaNode, processedParentView, options);
                }

                //Images
                if (options.AreImageProcessed)
                {
                    foreach (var processedNode in NodesProcessed)
                    {
                        if (FileProvider.RendersAsImage(processedNode.FigmaNode))
                        {
                            ImageVectors.Add(processedNode);
                        }
                    }

                    fileProvider.ImageLinksProcessed += FileProvider_ImageLinksProcessed;
                    fileProvider.OnStartImageLinkProcessing(ImageVectors);
                }

                Console.WriteLine("View generation finished.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading resource");
                Console.WriteLine(ex);
            }
        }

        private void FileProvider_ImageLinksProcessed(object sender, EventArgs e)
        {
            Console.WriteLine($"Image Links ended.");
        }

        public void Refresh(ViewRenderServiceOptions options)
        {
            //on refresh we want clear results
            ImageVectors.Clear();
            NodesProcessed.Clear();

            Console.WriteLine($"Reading successfull");

            FigmaCanvas canvas;
            if (options.StartPage >= 0 && options.StartPage <= fileProvider.Response.document.children.Length)
            {
                canvas = fileProvider.Response.document.children[options.StartPage];
            }
            else
            {
                canvas = fileProvider.Response.document.children.FirstOrDefault();
            }
            ProcessFromNode(canvas, container, options);
        }

        #region Rendering

        public void RenderInWindow(IWindow mainWindow, ViewRenderServiceOptions options = null)
        {
            var allCanvas = fileProvider.Nodes
                .OfType<FigmaCanvas>()
                .ToArray();
            if (allCanvas.Length == 0)
            {
                return;
            }

            var startPage = options != null ? options.StartPage : 0;
            var canvas = allCanvas[startPage];
            if (canvas != null)
            {
                canvas.absoluteBoundingBox = canvas.GetCurrentBounds();
                RenderInWindow(mainWindow, canvas, options);
            }
        }

        public void RenderInWindow(IWindow mainWindow, string nodeName, ViewRenderServiceOptions options = null)
        {
            var node = fileProvider.Nodes
                .FirstOrDefault(s =>s.name == nodeName || (s.TryGetNodeCustomName(out string name) && name == nodeName));

            if (node == null)
                throw new Exception($"nodename {nodeName} not found");
            RenderInWindow(mainWindow, node, options);
        }

        public virtual void RenderInWindow(IWindow mainWindow, FigmaNode node, ViewRenderServiceOptions options = null)
        {
            if (node is IAbsoluteBoundingBox bounNode) {
                mainWindow.Size = new Size(bounNode.absoluteBoundingBox.Width, bounNode.absoluteBoundingBox.Height);
            }

            if (options == null) {
                options = new ViewRenderServiceOptions();
            }

            ProcessFromNode(node, mainWindow.Content, options);

            var processedNode = FindProcessedNodeById(node.id);
            RecursivelyConfigureViews(processedNode, options);
        }

        public T RenderByFullPath<T> (IView parent, ViewRenderServiceOptions options,  string path) where T : IView
        {
            FigmaNode node = fileProvider.FindByPath (path);
            if (node == null)
                return default (T);
            return (T)RenderByNode (node, parent, options);
        }

        public T RenderByPath<T> (ViewRenderServiceOptions options, IView parent, params string[] path) where T : IView
        {
            FigmaNode node = fileProvider.FindByPath (path);
            if (node == null)
                return default (T);
            return (T)RenderByNode (node, parent, options);
        }

        public IView RenderByNode(FigmaNode node, IView parent, ViewRenderServiceOptions options = null)
        {
            if (options == null)
                options = new ViewRenderServiceOptions();

            firstNode = node;

            ProcessFromNode(node, parent, options);
            var processedNode = FindProcessedNodeById (node.id);
            RecursivelyConfigureViews (processedNode, options);

            firstNode = null;

            return processedNode.View;
        }

        public T RenderByNode<T>(FigmaNode node, IView parent, ViewRenderServiceOptions options = null) where T : IView
        {
            return (T)RenderByNode(node, parent, options);
        }

        public T RenderByName<T> (string figmaName, IView parent, ViewRenderServiceOptions options = null) where T : IView
        {
            var node = FindNodeByName (figmaName);
            if (node == null)
                return default (T);
            return (T)RenderByNode (node, parent, options);
        }

        #endregion

        #region Find view nodes

        public T FindViewStartsWith<T>(string name, StringComparison stringComparison = StringComparison.InvariantCultureIgnoreCase) where T : IView
        {
            foreach (var node in NodesProcessed)
            {
                if (node.View is T && node.FigmaNode.name.StartsWith(name, stringComparison))
                {
                    return (T)node.View;
                }
            }
            return default(T);
        }

        public T FindViewByName<T>(string name) where T : IView
        {
            foreach (var node in NodesProcessed)
            {
                if (node.View is T && node.FigmaNode.name == name)
                {
                    return (T)node.View;
                }
            }
            return default(T);
        }

        public T FindViewByPath<T>(params string[] path) where T : IView
        {
            var node = fileProvider.FindByPath(path);
            if (node == null)
                return default(T);
            var processed = NodesProcessed.FirstOrDefault(s => s.FigmaNode == node);
            if (processed == null)
                return default(T);

            return (T)processed.View;
        }

        public IEnumerable<T> FindViewsStartsWith<T>(string name, StringComparison stringComparison = StringComparison.InvariantCultureIgnoreCase)
        {
            foreach (var node in NodesProcessed)
            {
                if (node.View is T && node.FigmaNode.name.StartsWith(name, stringComparison))
                {
                    yield return (T)node.View;
                }
            }
        }

        public IEnumerable<T> FindViewsByName<T>(string name)
        {
            foreach (var node in NodesProcessed)
            {
                if (node.View is T && node.FigmaNode.name == name)
                {
                    yield return (T)node.View;
                }
            }
        }

        public IView FindViewByName(string name)
        {
            foreach (var node in NodesProcessed)
            {
                if (node.FigmaNode.name == name)
                {
                    return node.View;
                }
            }
            return null;
        }

        public ViewNode FindProcessedNodeByName(string name)
        {
            return NodesProcessed.FirstOrDefault(s => s.FigmaNode.name == name);
        }

        public ViewNode FindProcessedNodeById(string Id)
        {
            return NodesProcessed.FirstOrDefault(s => s.FigmaNode.id == Id);
        }

        #endregion

        protected void RecursivelyConfigureViews (ViewNode parentNode, ViewRenderServiceOptions options)
        {
            var children = NodesProcessed.Where(s => s.ParentView == parentNode);
            foreach (var child in children)
            {
                if (child.View == null)
                {
                    Console.WriteLine("Node {0} has no view to process... skipping", child.FigmaNode);
                    continue;
                }

                if (RendersAddChild(child, parentNode, this))
                    PropertySetter.Configure(PropertyNames.AddChild, child.View, child.FigmaNode, parentNode.View, parentNode.FigmaNode, this);

                if (RendersSize (child, parentNode, this))
                    PropertySetter.Configure(PropertyNames.Frame, child.View, child.FigmaNode, parentNode.View, parentNode.FigmaNode, this);

                if (RendersConstraints(child, parentNode, this))
                    PropertySetter.Configure(PropertyNames.Constraints, child.View, child.FigmaNode, parentNode.View, parentNode.FigmaNode, this);

                RecursivelyConfigureViews (child, options);
            }
        }

        protected virtual bool RendersAddChild (ViewNode currentNode, ViewNode parent, RenderService rendererService)
        {
            return true;
        }

        protected virtual bool RendersConstraints (ViewNode currentNode,ViewNode parent, RenderService rendererService)
        {
            return !((currentNode != null && firstNode == currentNode.FigmaNode) || (currentNode.FigmaNode is FigmaCanvas || currentNode.FigmaNode.Parent is FigmaCanvas));
        }

        protected virtual bool RendersSize (ViewNode currentNode, ViewNode parent, RenderService rendererService)
        {
            return true;
        }

        public async Task StartAsync (string figmaName, IView container, ViewRenderServiceOptions options = null)
		{
            if (options == null) {
                options = new ViewRenderServiceOptions();
            }

            Console.WriteLine("[FigmaViewRenderer] Starting process..");
            Console.WriteLine($"Reading {figmaName} from resources..");

            this.container = container;

            try
            {
                if (options.LoadFileProvider) {
                    await fileProvider.LoadAsync(figmaName ?? fileProvider.File);
                }

                //we generate all the processed nodes
                Refresh(options);

                //we render only if there is a canvas and GenerateViews is enabled
                var canvas = NodesProcessed.FirstOrDefault(s => s.FigmaNode is FigmaCanvas);
                if (canvas != null && options.ConfigureViews)
                {
                    RecursivelyConfigureViews(canvas, options);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading resource");
                Console.WriteLine(ex);
            }
        }

        public void Start(string figmaName, IView container, ViewRenderServiceOptions options = null)
        {
            if (options == null) {
                options = new ViewRenderServiceOptions();
            }

            Console.WriteLine("[FigmaViewRenderer] Starting process..");
            Console.WriteLine($"Reading {figmaName} from resources..");

            this.container = container;

            try
            {
                if (options.LoadFileProvider)
                    fileProvider.Load(figmaName ?? fileProvider.File);

                //we generate all the processed nodes
                Refresh(options);

                //we render only if there is a canvas and GenerateViews is enabled
                var canvas = NodesProcessed.FirstOrDefault(s => s.FigmaNode is FigmaCanvas);
                if (canvas != null && options.ConfigureViews) {
                    RecursivelyConfigureViews(canvas, options);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading resource");
                Console.WriteLine(ex);
            }
        }

        //TODO: This 
        protected void GenerateViewsRecursively(FigmaNode currentNode, ViewNode parent, ViewRenderServiceOptions options)
        {
            Console.WriteLine("[{0}.{1}] Processing {2}..", currentNode?.id, currentNode?.name, currentNode?.GetType());

            //if (currentNode.name.StartsWith ("#") || currentNode.name.StartsWith ("//")) {
            //    Console.WriteLine ("[{0}.{1}] Detected skipped flag in name.. Skipping...", currentNode?.id, currentNode?.name, currentNode?.GetType ());
            //    return;
            //}

            if (SkipsNode(currentNode, parent, options))
                return;

            var converter = GetProcessedConverter(currentNode, CustomConverters);
            if (converter == null)
            {
                converter = GetProcessedConverter(currentNode, DefaultConverters);
            }

            ViewNode currentProcessedNode = null;
            if (converter != null)
            {
                var currentView = options.IsToViewProcessed ? converter.ConvertToView(currentNode, parent, this) : null;
                currentProcessedNode = new ViewNode(currentNode, currentView, parent);
                NodesProcessed.Add(currentProcessedNode);
            }
            else
            {
                Console.WriteLine("[{1}.{2}] There is no Converter for this type: {0}", currentNode.GetType(), currentNode.id, currentNode.name);
            }

            if (NodeScansChildren(currentNode, converter, options))
            {
                foreach (var item in GetCurrentChildren(currentNode, parent?.FigmaNode, converter, options))
                {
                    GenerateViewsRecursively(item, currentProcessedNode ?? parent, options);
                }
            }
        }

        protected virtual IEnumerable<FigmaNode> GetCurrentChildren(FigmaNode currentNode, FigmaNode parentNode, NodeConverter converter,ViewRenderServiceOptions options)
        {
            if (currentNode is IFigmaNodeContainer nodeContainer)
            {
                return nodeContainer.children;
            }
            return Enumerable.Empty<FigmaNode>();
        }
    }
}
