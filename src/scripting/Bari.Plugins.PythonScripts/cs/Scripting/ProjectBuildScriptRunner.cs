﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bari.Core.Generic;
using Bari.Core.Model;
using IronPython.Compiler;
using IronPython.Hosting;
using IronPython.Runtime;
using Microsoft.Scripting;

namespace Bari.Plugins.PythonScripts.Scripting
{
    public class ProjectBuildScriptRunner : IProjectBuildScriptRunner
    {
        private readonly IFileSystemDirectory targetRoot;

        public ProjectBuildScriptRunner([TargetRoot] IFileSystemDirectory targetRoot)
        {
            this.targetRoot = targetRoot;
        }

        /// <summary>
        /// Executes the given build script on the given project, returning the set of
        /// target files the script generated.
        /// 
        /// <para>
        /// The script's global scope has the following predefined variables set:
        /// - <c>project</c> refers to the project being built
        /// - <c>sourceSet</c> is the project's source set associated with the build script
        /// - <c>targetDir</c> is where the project's output should be built
        /// </para>
        /// </summary>
        /// <param name="project">The input project to build</param>
        /// <param name="buildScript">The build script to execute</param>
        /// <returns>Returns the set of files generated by the script. They have to be
        /// indicated in the script's <c>results</c> variable, relative to <c>targetDir</c>.</returns>
        public ISet<TargetRelativePath> Run(Project project, IBuildScript buildScript)
        {
            var engine = Python.CreateEngine();
            var runtime = engine.Runtime;
            try
            {
                var scope = runtime.CreateScope();

                scope.SetVariable("project", project);
                scope.SetVariable("sourceSet",
                                  project.GetSourceSet(buildScript.SourceSetName)
                                         .Files.Select(srp => (string) srp)
                                         .ToList());

                var targetDir = targetRoot.GetChildDirectory(project.Module.Name, createIfMissing: true);
                var localTargetDir = targetDir as LocalFileSystemDirectory;
                if (localTargetDir != null)
                {
                    scope.SetVariable("targetDir", localTargetDir.AbsolutePath);

                    var pco = (PythonCompilerOptions)engine.GetCompilerOptions();
                    pco.Module |= ModuleOptions.Optimized;

                    var script = engine.CreateScriptSourceFromString(buildScript.Source, SourceCodeKind.File);
                    script.Compile(pco);
                    script.Execute(scope);

                    return new HashSet<TargetRelativePath>(
                        scope.GetVariable<IList<object>>("results")
                             .Cast<string>()
                             .Select(t => GetTargetRelativePath(targetDir, t)));
                }
                else
                {
                    throw new NotSupportedException("Only local file system is supported for python scripts!");
                }
            }
            finally
            {
                runtime.Shutdown();
            }
        }

        private TargetRelativePath GetTargetRelativePath(IFileSystemDirectory innerRoot, string path)
        {
            return new TargetRelativePath(
                Path.Combine(targetRoot.GetRelativePath(innerRoot), path));
        }
    }
}