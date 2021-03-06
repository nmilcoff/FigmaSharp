﻿using System;
using System.Collections.Generic;
using System.Text;

namespace FigmaSharp
{
    public abstract class FigmaClassBase
    {
        protected const string InitializeComponentMethodName = "InitializeComponent";

        public List<CodeObject> Methods = new List<CodeObject>();

        public List<string> Usings { get; } = new List<string>();
        public List<string> Comments { get; } = new List<string>();

        public FigmaManifest Manifest { get; set; }

        public bool ShowManifestComments => Manifest != null;

        protected int CurrentTabIndex = 0;

        public void RemoveTabLevel() => CurrentTabIndex--;
        public void AddTabLevel() => CurrentTabIndex++;

        protected void GenerateComments(StringBuilder builder)
        {
            if (ShowManifestComments)
            {
                Manifest.ToComment(builder);
            }
            foreach (var current in Comments)
            {
                builder.AppendLine($"// {current}");
            }
        }

        public void Save(string filePath)
        {
            var code = Generate();
            try
            {
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);
                System.IO.File.WriteAllText(filePath, code);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Fail(ex.ToString());
            }
        }

        protected void GenerateUsings(StringBuilder builder)
        {
            builder.AppendLine();

            foreach (var current in Usings)
                builder.AppendLine($"using {current};");
        }

        internal void GenerateEnum(StringBuilder sb, string name, CodeObjectModifierType objectModifier)
        {
            AppendLine(sb, $"{objectModifier.ToString().ToLower()} enum {name}");
            OpenBracket(sb);
        }

        internal void GenerateMethod(StringBuilder sb, string methodName,
            CodeObjectModifierType modifier = CodeObjectModifierType.Private, List<(string, string)> arguments = null)
        {
            string args = string.Empty;

            if (arguments != null)
            {
                for (int i = 0; i < arguments.Count; i++)
                {
                    var argument = arguments[i];

                    if (i > 0)
                        args += ", ";

                    args += $"{argument.Item1} {argument.Item2}";
                }
            }
            AppendLine(sb, $"{modifier.ToString().ToLower()} void {methodName} ({args})");
            OpenBracket(sb);
        }

        internal void CloseMethod(StringBuilder sb) => CloseBracket(sb);

        protected void GenerateNamespace(StringBuilder sb, string fullNamespace)
        {
            sb.AppendLine();
            AppendLine(sb, $"namespace {fullNamespace}");
            OpenBracket(sb);
        }
        protected void CloseNamespace(StringBuilder sb) => CloseBracket(sb);

        protected void GenerateConstructor(StringBuilder sb, string className)
        {
            AppendLine(sb, $"public {className} ()");
            OpenBracket(sb);
        }
        protected void CloseConstructor(StringBuilder sb) => CloseBracket(sb);

        internal void CloseBracket(StringBuilder sb, bool removeTabIndex = true)
        {
            AppendLine(sb, "}");
            if (removeTabIndex)
                CurrentTabIndex--;
        }

        internal void OpenBracket(StringBuilder sb, bool addTabIndex = true)
        {
            AppendLine(sb, "{");
            if (addTabIndex)
                CurrentTabIndex++;
        }

        public void AppendLine(StringBuilder sb, string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                sb.AppendLine();
                return;
            }
            sb.AppendLine($"{new string('\t', CurrentTabIndex)}{line}");
        }

        protected void AppendLine(StringBuilder sb) => sb.AppendLine();

        public string Generate()
        {
            CurrentTabIndex = 0;
            return OnGenerate();
        }

        protected abstract string OnGenerate();

    }
}
