using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MSBuildJenkins
{
    public class WarningsNGLogger : Logger
    {
        private readonly JsonSerializerOptions _jsonOpts;
        private readonly string _workDir = Environment.CurrentDirectory;
        private StreamWriter _file;

        public WarningsNGLogger() : base()
        {
            _jsonOpts = new JsonSerializerOptions();
            _jsonOpts.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
            _jsonOpts.AllowTrailingCommas = false;
            _jsonOpts.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        }


        private struct IssueObject
        {
            public string FileName { get; set; }
            public string Severity { get; set; }
            public string ModuleName { get; set; }
            public int LineStart { get; set; }
            public int LineEnd { get; set; }
            public int ColumnStart { get; set; }
            public int ColumnEnd { get; set; }
            public string Category { get; set; }
            public string Type { get; set; }
            public string Message { get; set; }
            public string Reference { get; set; }
        }


        public override void Initialize(IEventSource eventSource)
        {
            var filename = "issues.json.log";
            if (Parameters != null)
            {
                string[] parameters = Parameters.Split(';');
                filename = parameters[0];
            }
            _file = new StreamWriter(filename);

            eventSource.ErrorRaised += ErrorRaised;
            eventSource.WarningRaised += WarningRaised;
        }

        private void WarningRaised(object sender, BuildWarningEventArgs e)
        {
            var (project, file) = RelativeFilePath(e.ProjectFile, e.File);
            var obj = new IssueObject()
            {
                FileName = file,
                Severity = "NORMAL",
                ModuleName = project,
                LineStart = e.LineNumber,
                LineEnd = e.EndLineNumber,
                ColumnStart = e.ColumnNumber,
                ColumnEnd = e.EndColumnNumber,
                Category = EmptyToNull(e.Subcategory),
                Type = EmptyToNull(e.Code),
                Message = EmptyToNull(e.Message),
                Reference = EmptyToNull(e.HelpLink),
            };
            var jsonObj = JsonSerializer.Serialize(obj, _jsonOpts);
            _file.WriteLine(jsonObj);
        }

        private void ErrorRaised(object sender, BuildErrorEventArgs e)
        {
            var (project, file) = RelativeFilePath(e.ProjectFile, e.File);
            var obj = new IssueObject()
            {
                FileName = file,
                Severity = "ERROR",
                ModuleName = project,
                LineStart = e.LineNumber,
                LineEnd = e.EndLineNumber,
                ColumnStart = e.ColumnNumber,
                ColumnEnd = e.EndColumnNumber,
                Category = EmptyToNull(e.Subcategory),
                Type = EmptyToNull(e.Code),
                Message = EmptyToNull(e.Message),
                Reference = EmptyToNull(e.HelpLink),
            };
            var jsonObj = JsonSerializer.Serialize(obj, _jsonOpts);
            _file.WriteLine(jsonObj);
        }

        private static string EmptyToNull(string str)
        {
            return string.IsNullOrWhiteSpace(str) ? null : str;
        }

        private (string ProjectName, string RelativeFilePath) RelativeFilePath(string projectPath, string filePath)
        {
            projectPath = EmptyToNull(projectPath);
            filePath = EmptyToNull(filePath);
            if (projectPath == null || filePath == null)
                return (projectPath, filePath);
            var projDirPath = Path.GetDirectoryName(projectPath);
            var projName = Path.GetFileName(projectPath);
            var fileNameIsInProject = filePath.StartsWith(_workDir);
            var finalFilePath = filePath;
            if (fileNameIsInProject)
            {
                finalFilePath = filePath.Substring(_workDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            return (projName, finalFilePath);
        }

        public override void Shutdown()
        {
            _file.Flush();
            _file.Dispose();
            base.Shutdown();
        }
    }
}