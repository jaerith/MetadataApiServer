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

        /// <summary>
        /// 
        ///     This code compiles a block of code into an assembly, in order to execute and fulfill a request by the server.
        ///     
        /// </summary>
        /// <param name="ExecCode">The code to be compiled and assembled into an assembly</param>
        /// <param name="Assemblies">Any other assemblies needed for successful compilation</param>
        /// <param name="InMemory">Indicates whether the compilation should store the assembly in memory or to the local filesystem</param>
        /// <returns>The compiled Assembly of the provided code</returns>
        public static Assembly CompileCode(string ExecCode, string[] Assemblies, bool InMemory = true)
        {
            CSharpCodeProvider provider   = new CSharpCodeProvider();
            CompilerParameters parameters = new CompilerParameters(Assemblies);

            // parameters.ReferencedAssemblies.Add("MetadataCommonApi.dll");

            parameters.GenerateInMemory   = InMemory;
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

        /// <summary>
        /// 
        ///     This code compiles a block of code into an Assembly and then executes it.
        ///     
        /// </summary>
        /// <param name="UrlParameters">Parameters that might be needed in the future</param>
        /// <param name="ExecCode">The code to be compiled and assembled into an assembly</param>
        /// <param name="Body">The values needed by the code in order to successfully execute the code</param>
        /// <returns>The value(s) sought by the caller and returned by the compiled code block</returns>
        public List<Dictionary<string, string>> CompileAndExecuteCode(string                           UrlParameters, 
                                                                      string                           ExecCode, 
                                                                      List<Dictionary<string, string>> Body)
        {
            var AssemblyNames = (from a in AppDomain.CurrentDomain.GetAssemblies()
                                 where !a.IsDynamic
                                 select a.Location).ToArray();

            return CompileAndExecuteCode(UrlParameters, ExecCode, Body, AssemblyNames);
        }

        /// <summary>
        /// 
        ///     This code compiles a block of code into an Assembly, finds the first type in the Assembly with the IRunnable type,
        ///     and then instantiates and executes that class.
        ///     
        /// </summary>
        /// <param name="UrlParameters">Parameters that might be needed in the future</param>
        /// <param name="ExecCode">The code to be compiled and assembled into an assembly</param>
        /// <param name="Body">The values needed by the code in order to successfully execute the code</param>
        /// <param name="Assemblies">Any other assemblies needed for successful compilation</param>
        /// <returns>The value(s) sought by the caller and returned by the compiled code block</returns>
        public List<Dictionary<string, string>> CompileAndExecuteCode(string                           UrlParameters, 
                                                                      string                           ExecCode, 
                                                                      List<Dictionary<string, string>> Body,
                                                                      string[]                         Assemblies)
        {
            Assembly dynamicAssembly = RunnableExecutor.CompileCode(ExecCode, Assemblies);

            var typesInAssembly = dynamicAssembly.GetTypes();

            var type = typesInAssembly.First();

            return RunnableExecutor.InvokeRunnable(type, Body);
        }

        /// <summary>
        /// 
        ///     This code compiles a block of code into an Assembly, embeds it within a safe Application Domain, 
        ///     finds the first type in the Assembly with the IRunnable type, and then instantiates and executes that class.
        ///     
        ///     UNDER CONSTRUCTION - DOES NOT CURRENTLY WORK
        /// 
        /// </summary>
        /// <param name="UrlParameters">Parameters that might be needed in the future</param>
        /// <param name="ExecCode">The code to be compiled and assembled into an assembly</param>
        /// <param name="Body">The values needed by the code in order to successfully execute the code</param>
        /// <returns>The value(s) sought by the caller and returned by the compiled code block</returns>
        public static List<Dictionary<string, string>> CompileAndExecuteCodeSafe(string                           UrlParameters, 
                                                                                 string                           ExecCode, 
                                                                                 List<Dictionary<string, string>> Body)
        {
            AppDomain sandbox = null;

            HashSet<string> additionalAssemblyDirs = new HashSet<string>();

            try
            {
                string targetAssemblyPath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;

                var assemblyNames = (from a in AppDomain.CurrentDomain.GetAssemblies()
                                      where !a.IsDynamic
                                      select a.Location).ToArray();

                foreach (string tmpAssembly in assemblyNames)
                {
                    // additionalAssemblyDirs.Add(Path.GetDirectoryName(tmpAssembly));

                    if (tmpAssembly.Contains("MetadataApiCommon"))
                    {
                        targetAssemblyPath = tmpAssembly;
                        additionalAssemblyDirs.Add(Path.GetDirectoryName(targetAssemblyPath));
                    }
                }

                Assembly dynamicAssembly = RunnableExecutor.CompileCode(ExecCode, assemblyNames, false);

                additionalAssemblyDirs.Add(RunnableExecutor.GetAssemblyDirectory(dynamicAssembly));

                /*
                 * 
                AppDomainSetup setup = new AppDomainSetup() { ApplicationBase = Path.GetDirectoryName(targetAssemblyPath) };

                domain = AppDomain.CreateDomain("Sandbox", null, setup);
                 */

                // sandbox = ProduceSecureDomain(targetAssemblyPath);
                sandbox = ProduceSecureDomain(additionalAssemblyDirs.ToArray());
                // sandbox = ProduceSimpleDomain();

                sandbox.Load(RunnableExecutor.GetAssemblyPath(dynamicAssembly));

                /*
                byte[] assemblyAsArray = null;
                using (MemoryStream stream = new MemoryStream())
                {
                    System.Runtime.Serialization.Formatters.Binary.BinaryFormatter formatter =
                        new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();

                    formatter.Serialize(stream, dynamicAssembly);

                    assemblyAsArray = stream.ToArray();
                }

                if (assemblyAsArray != null)
                    sandbox.Load(assemblyAsArray);
                 */

                var typesInAssembly = dynamicAssembly.GetTypes();

                var type = typesInAssembly.First();

                /*
                object dynamicObject =
                    sandbox.CreateInstanceFromAndUnwrap(GetAssemblyPath(dynamicAssembly), type.FullName);

                IRunnable runnable = (IRunnable) dynamicObject;

                return runnable.Run(Body);
                 */

                RunnableExecutor runnableExecutor =
                    (RunnableExecutor)sandbox.CreateInstanceFromAndUnwrap(targetAssemblyPath, "MetadataApiCommon.RunnableExecutor") as RunnableExecutor;

                // MethodInfo runMethod = instance.GetMethod("Run");

                return runnableExecutor.ExecuteRunnable(type, Body);
            }
            finally
            {
                if (sandbox != null)
                    AppDomain.Unload(sandbox);
            }
        }

        /// <summary>
        /// 
        ///     This instance member method will instantiate a provided type (that supposedly inherits from IRunnable) and then
        ///     execute the IRunnable's sole method.
        ///     
        /// </summary>
        /// <param name="Type">Type of IRunnable class to be instantiated</param>
        /// <param name="Body">The values needed by the IRunnable class in order to successfully execute the code</param>
        /// <returns>The value(s) sought by the caller and returned by the IRunnable instantiation</returns>
        public List<Dictionary<string, string>> ExecuteRunnable(Type runnableType, List<Dictionary<string, string>> Body)
        {
            List<Dictionary<string, string>> oResultBody = new List<Dictionary<string, string>>();

            //create instance
            var runnable = Activator.CreateInstance(runnableType) as IRunnable;

            if (runnable == null) throw new Exception("broke");

            oResultBody = runnable.Run(Body);

            return oResultBody;
        }

        /// <summary>
        /// 
        ///     This code returns the directory of the Assembly file.
        ///     
        /// </summary>
        /// <param name="targetAssembly">The Assembly of interest</param>
        /// <returns>The directory in which the Assembly resides</returns>
        public static string GetAssemblyDirectory(Assembly targetAssembly)
        {
            string path = GetAssemblyPath(targetAssembly);

            return Path.GetDirectoryName(path);
        }

        /// <summary>
        /// 
        ///     This code returns the filepath of the Assembly file.
        ///     
        /// </summary>
        /// <param name="targetAssembly">The Assembly of interest</param>
        /// <returns>The filepath that leads to the Assembly</returns>
        public static string GetAssemblyPath(Assembly targetAssembly)
        {
            string codeBase = targetAssembly.CodeBase;

            UriBuilder uri = new UriBuilder(codeBase);

            string path = Uri.UnescapeDataString(uri.Path);

            return path;
        }

        /// <summary>
        /// 
        ///     This static member method will instantiate a provided type (that supposedly inherits from IRunnable) and then
        ///     execute the IRunnable's sole method.
        ///     
        /// </summary>
        /// <param name="Type">Type of IRunnable class to be instantiated</param>
        /// <param name="Body">The values needed by the IRunnable class in order to successfully execute the code</param>
        /// <returns>The value(s) sought by the caller and returned by the IRunnable instantiation</returns>
        public static List<Dictionary<string, string>> InvokeRunnable(Type runnableType, List<Dictionary<string, string>> Body)
        {
            List<Dictionary<string, string>> oResultBody = new List<Dictionary<string, string>>();

            //create instance
            var runnable = Activator.CreateInstance(runnableType) as IRunnable;

            if (runnable == null) throw new Exception("broke");

            oResultBody = runnable.Run(Body);

            return oResultBody;
        }

        /// <summary>
        /// 
        ///     This code loads the DLL specified, finds the first type in the Assembly with the IRunnable type,
        ///     and then instantiates and executes that class.
        ///     
        /// </summary>
        /// <param name="RootPath">Directory where the desired DLL is to be found</param>
        /// <param name="Parameters">Parameters that might be needed in the future</param>
        /// <param name="ExecDLL">The filename of the desired DLL</param>
        /// <param name="Body">The values needed by the code in order to successfully execute the request</param>
        /// <returns>The value(s) sought by the caller and returned by the loaded DLL</returns>
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

        /// <summary>
        /// 
        ///     This code loads the DLL specified, injects it into a safe Application Domain, 
        ///     finds the first type in the Assembly with the IRunnable type, and then instantiates and executes that class.
        ///     
        /// </summary>
        /// <param name="RootPath">Directory where the desired DLL is to be found</param>
        /// <param name="Parameters">Parameters that might be needed in the future</param>
        /// <param name="ExecDLL">The filename of the desired DLL</param>
        /// <param name="Body">The values needed by the code in order to successfully execute the request</param>
        /// <returns>The value(s) sought by the caller and returned by the loaded DLL</returns>
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

        /// <summary>
        /// 
        ///     UNDER CONSTRUCTION
        ///     
        /// </summary>
        public static AppDomain ProduceSimpleDomain()
        {
            return AppDomain.CreateDomain("Sandbox");
        }

        public static AppDomain ProduceSecureDomain(string ExecDllPath)
        {
            return ProduceSecureDomain(new string[] { ExecDllPath });
        }

        /// <summary>
        /// 
        ///     This function creates an Application Domain that will serve as a secure sandbox for the execution
        ///     of any dynamically loaded/compiled code.
        ///     
        /// </summary>
        /// <param name="DllPaths">Directories needed for access by the created sandbox</param>
        /// <param name="UseStandardPermissions">A switch that indicates whether or not to use the .NET standard permission set for a sandbox</param>
        /// <returns>The Application Domain that will serve as a secure sandbox</returns>
        public static AppDomain ProduceSecureDomain(string[] DllPaths, bool UseStandardPermissions = false)
        {
            string        tempPath     = null;
            PermissionSet permSet      = new PermissionSet(PermissionState.None);
            AppDomain     secureDomain = null;

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
                if (!String.IsNullOrEmpty(tempAssembly))
                {
                    tempPath = Path.GetDirectoryName(tempAssembly);
                    AccessDirectories.Add(tempPath);
                }
            }

            if (UseStandardPermissions)
            {
                Evidence ev = new Evidence();
                ev.AddHostEvidence(new Zone(SecurityZone.Internet));

                permSet = SecurityManager.GetStandardSandbox(ev);

                StrongName fullTrustAssembly = typeof(RunnableExecutor).Assembly.Evidence.GetHostEvidence<StrongName>();

                secureDomain = AppDomain.CreateDomain("Sandbox", ev, adSetup, permSet, fullTrustAssembly);
            }
            else
            {
                permSet = new PermissionSet(PermissionState.None);
                permSet.AddPermission(new SecurityPermission(SecurityPermissionFlag.Execution));
                // permSet.AddPermission(new ReflectionPermission(ReflectionPermissionFlag.RestrictedMemberAccess));
                permSet.AddPermission(new FileIOPermission(FileIOPermissionAccess.AllAccess, AccessDirectories.ToArray()));

                StrongName fullTrustAssembly = typeof(RunnableExecutor).Assembly.Evidence.GetHostEvidence<StrongName>();

                secureDomain = AppDomain.CreateDomain("Sandbox", null, adSetup, permSet, fullTrustAssembly);
            }

            return secureDomain;
        }
    }
}