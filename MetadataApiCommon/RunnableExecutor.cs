using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Security;
using System.Security.Permissions;
using System.Security.Policy;
using System.Threading;

using Microsoft.CSharp;

// This assembly attribute is needed for dynamic compilation/execution within a secure domain
[assembly: AllowPartiallyTrustedCallers]
namespace MetadataApiCommon
{
    public class RunnableExecutor : MarshalByRefObject
    {
        public RunnableExecutor()
        {}

        public static Assembly CompileCode(string ExecCode, string[] Assemblies)
        {
            CSharpCodeProvider provider   = new CSharpCodeProvider();
            CompilerParameters parameters = new CompilerParameters(Assemblies);

            // parameters.ReferencedAssemblies.Add("MetadataCommonApi.dll");

            parameters.GenerateInMemory   = true;
            parameters.GenerateExecutable = false;

            CompilerResults results = provider.CompileAssemblyFromSource(parameters, ExecCode);

            if (results.Errors.HasErrors)
            {
                StringBuilder sb = new StringBuilder();

                foreach (CompilerError error in results.Errors)
                    sb.AppendLine(String.Format("Error ({0}): {1}", error.ErrorNumber, error.ErrorText));

                throw new InvalidOperationException(sb.ToString());
            }

            Assembly assembly = results.CompiledAssembly;

            return assembly;
        }

        public List<Dictionary<string, string>> CompileAndExecuteCode(string                           UrlParameters, 
                                                                      string                           ExecCode, 
                                                                      List<Dictionary<string, string>> Body)
        {
            var AssemblyNames = (from a in AppDomain.CurrentDomain.GetAssemblies()
                                 where !a.IsDynamic
                                 select a.Location).ToArray();

            return CompileAndExecuteCode(UrlParameters, ExecCode, Body, AssemblyNames);

        }

        public List<Dictionary<string, string>> CompileAndExecuteCode(string                           UrlParameters, 
                                                                      string                           ExecCode, 
                                                                      List<Dictionary<string, string>> Body,
                                                                      string[]                         Assemblies)
        {
            Assembly dynamicAssembly = RunnableExecutor.CompileCode(ExecCode, Assemblies);

            /*
int        StartIndex   = ExecCode.IndexOf(CONST_CLASS_NAME_START_IND) + CONST_CLASS_NAME_START_IND.Length;
int        EndIndex     = ExecCode.IndexOf(CONST_CLASS_NAME_END_IND, StartIndex);
string     ClassName    = ExecCode.Substring(StartIndex, (EndIndex - StartIndex)).Trim();
Type       RunnableType = assembly.GetType(ClassName);
 */

            var typesInAssembly = dynamicAssembly.GetTypes();

            var type = typesInAssembly.First();

            // MethodInfo runMethod = instance.GetMethod("Run");

            return RunnableExecutor.InvokeRunnable(type, Body);
        }

        public static List<Dictionary<string, string>> CompileAndExecuteCodeSafe(string                           UrlParameters, 
                                                                                 string                           ExecCode, 
                                                                                 List<Dictionary<string, string>> Body)
        {
            AppDomain sandbox = null;

            try
            {
                string targetAssemblyPath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;

                var assemblyNames = (from a in AppDomain.CurrentDomain.GetAssemblies()
                                      where !a.IsDynamic
                                      select a.Location).ToArray();

                Assembly dynamicAssembly = RunnableExecutor.CompileCode(ExecCode, assemblyNames);

                foreach (string tmpAssembly in assemblyNames)
                {
                    if (tmpAssembly.Contains("MetadataApiCommon"))
                    {
                        targetAssemblyPath = tmpAssembly;
                        break;
                    }
                }

                /*
                 * 
                AppDomainSetup setup = new AppDomainSetup() { ApplicationBase = Path.GetDirectoryName(targetAssemblyPath) };

                domain = AppDomain.CreateDomain("Sandbox", null, setup);
                 */

                // sandbox = ProduceSecureDomain(targetAssemblyPath);
                sandbox = ProduceSecureDomain( new string[] { targetAssemblyPath, GetAssemblyDirectory(dynamicAssembly) } );

                /*
                 * 
                foreach (string tmpAssembly in assemblyNames)
                    sandbox.Load(tmpAssembly);
                 */

                var typesInAssembly = dynamicAssembly.GetTypes();

                var type = typesInAssembly.First();

                RunnableExecutor runnableExecutor =
                    (RunnableExecutor)sandbox.CreateInstanceFromAndUnwrap(targetAssemblyPath, "MetadataApiCommon.RunnableExecutor");

                // MethodInfo runMethod = instance.GetMethod("Run");

                return runnableExecutor.ExecuteRunnable(type, Body);
            }
            finally
            {
                if (sandbox != null)
                    AppDomain.Unload(sandbox);
            }
        }

        public List<Dictionary<string, string>> ExecuteRunnable(Type runnableType, List<Dictionary<string, string>> Body)
        {
            List<Dictionary<string, string>> oResultBody = new List<Dictionary<string, string>>();

            //create instance
            var runnable = Activator.CreateInstance(runnableType) as IRunnable;

            if (runnable == null) throw new Exception("broke");

            oResultBody = runnable.Run(Body);

            return oResultBody;
        }

        public static string GetAssemblyDirectory(Assembly targetAssembly)
        {
            string codeBase = targetAssembly.CodeBase;

            UriBuilder uri = new UriBuilder(codeBase);

            string path = Uri.UnescapeDataString(uri.Path);

            return Path.GetDirectoryName(path);
        }

        public static List<Dictionary<string, string>> InvokeRunnable(Type runnableType, List<Dictionary<string, string>> Body)
        {
            List<Dictionary<string, string>> oResultBody = new List<Dictionary<string, string>>();

            //create instance
            var runnable = Activator.CreateInstance(runnableType) as IRunnable;

            if (runnable == null) throw new Exception("broke");

            oResultBody = runnable.Run(Body);

            return oResultBody;
        }

        public static List<Dictionary<string, string>> LoadAndExecuteDLL(string                           RootPath, 
                                                                         string                           Parameters, 
                                                                         string                           ExecDLL, 
                                                                         List<Dictionary<string, string>> Body)
        {
            var       ExecDllFilepath = RootPath + "\\" + ExecDLL;
            var       oResultBody     = new List<Dictionary<string, string>>();
            AppDomain domain          = null;

            try
            {

                var asm = Assembly.LoadFile(ExecDllFilepath);

                //get types from assembly
                var typesInAssembly = asm.GetTypes();

                var type = typesInAssembly.First();

                return RunnableExecutor.InvokeRunnable(type, Body);
            }
            finally
            {
                if (domain != null)
                    AppDomain.Unload(domain);
            }
        }

        public static List<Dictionary<string, string>> LoadAndExecuteDLLSafe(string                           RootPath, 
                                                                             string                           Parameters, 
                                                                             string                           ExecDLL, 
                                                                             List<Dictionary<string, string>> Body)
        {
            var ExecDllFilepath = RootPath + "\\" + ExecDLL;

            List<Dictionary<string, string>> oResultBody = new List<Dictionary<string, string>>();

            var asm     = Assembly.LoadFile(ExecDllFilepath);
            var type    = asm.GetTypes().First();
            var sandbox = ProduceSecureDomain(ExecDllFilepath);

            var runnable = 
                sandbox.CreateInstanceFromAndUnwrap(ExecDllFilepath, type.Namespace + "." + type.Name) as IRunnable;

            if (runnable == null) throw new Exception("broke");

            oResultBody = runnable.Run(Body);

            return oResultBody;
        }

        public static IRunnable ProduceRunnableClass(string CodeBlock)
        {
            IRunnable oRunnableCodeClass = null;

            return oRunnableCodeClass;
        }

        public static AppDomain ProduceSecureDomain(string ExecDllPath)
        {
            return ProduceSecureDomain(ExecDllPath);
        }

        public static AppDomain ProduceSecureDomain(params string[] DllPaths)
        {
            string tempPath = null;

            HashSet<string> AccessDirectories = new HashSet<string>();

            var assemblyNames = (from a in AppDomain.CurrentDomain.GetAssemblies()
                                 where !a.IsDynamic
                                 select a.Location).ToArray();

            var adSetup = new AppDomainSetup()
            {
                ApplicationBase = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tmp"),
                ApplicationName = "Sandbox",
                DisallowBindingRedirects = true,
                DisallowCodeDownload = true,
                DisallowPublisherPolicy = true
            };

            AccessDirectories.Add(AppDomain.CurrentDomain.SetupInformation.ApplicationBase);
            AccessDirectories.Add(AppDomain.CurrentDomain.BaseDirectory);
            foreach (string tmpDllPath in DllPaths)
                AccessDirectories.Add(tmpDllPath);

            foreach (string tempAssembly in assemblyNames)
            {
                if (!String.IsNullOrEmpty(tempPath))
                {
                    tempPath = Path.GetDirectoryName(tempAssembly);
                    AccessDirectories.Add(tempPath);
                }
            }

            PermissionSet permSet = new PermissionSet(PermissionState.None);
            permSet.AddPermission(new SecurityPermission(SecurityPermissionFlag.Execution));
            // permSet.AddPermission(new ReflectionPermission(ReflectionPermissionFlag.RestrictedMemberAccess));
            permSet.AddPermission(new FileIOPermission(FileIOPermissionAccess.AllAccess, AccessDirectories.ToArray()));

            StrongName fullTrustAssembly = typeof(RunnableExecutor).Assembly.Evidence.GetHostEvidence<StrongName>();

            AppDomain secureDomain = AppDomain.CreateDomain("Sandbox", null, adSetup, permSet, fullTrustAssembly);

            return secureDomain;
        }
    }
}