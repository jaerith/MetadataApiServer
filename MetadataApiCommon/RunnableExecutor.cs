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

namespace MetadataApiCommon
{
    public class RunnableExecutor : MarshalByRefObject
    {
        public RunnableExecutor()
        {}

        public List<Dictionary<string, string>> CompileAndExecuteCode(string UrlParameters, string ExecCode, List<Dictionary<string, string>> Body)
        {
            // var sandboxDomain = RunnableExecutor.ProduceSecureDomain();

            var AssemblyNames = (from a in AppDomain.CurrentDomain.GetAssemblies()
                                 where !a.IsDynamic
                                 select a.Location).ToArray();

            CSharpCodeProvider provider   = new CSharpCodeProvider();
            CompilerParameters parameters = new CompilerParameters(AssemblyNames);

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

            /*
            int        StartIndex   = ExecCode.IndexOf(CONST_CLASS_NAME_START_IND) + CONST_CLASS_NAME_START_IND.Length;
            int        EndIndex     = ExecCode.IndexOf(CONST_CLASS_NAME_END_IND, StartIndex);
            string     ClassName    = ExecCode.Substring(StartIndex, (EndIndex - StartIndex)).Trim();
            Type       RunnableType = assembly.GetType(ClassName);
             */

            var typesInAssembly = assembly.GetTypes();

            var type = typesInAssembly.First();

            // MethodInfo runMethod = instance.GetMethod("Run");

            return RunnableExecutor.InvokeRunnable(type, Body);
        }

        public static List<Dictionary<string, string>> CompileAndExecuteCodeSafe(string UrlParameters, string ExecCode, List<Dictionary<string, string>> Body)
        {
            AppDomain domain = null;

            try
            {
                AppDomain root = AppDomain.CurrentDomain;

                AppDomainSetup setup = new AppDomainSetup() { ApplicationBase = root.SetupInformation.ApplicationBase };

                domain = AppDomain.CreateDomain("Sandbox", null, setup);

                RunnableExecutor runnableExecutor = (RunnableExecutor) domain.CreateInstanceFromAndUnwrap("MetadataApiCommon.dll", "MetadataApiCommon.RunnableExecutor");

                return runnableExecutor.CompileAndExecuteCode(UrlParameters, ExecCode, Body);
            }
            finally
            {
                if (domain != null)
                    AppDomain.Unload(domain);
            }
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

        public static List<Dictionary<string, string>> LoadAndExecuteDLL(string RootPath, string Parameters, string ExecDLL, List<Dictionary<string, string>> Body)
        {
            List<Dictionary<string, string>> oResultBody = new List<Dictionary<string, string>>();

            var ExecDllFilepath = RootPath + "\\" + ExecDLL;

            var asm = Assembly.LoadFile(ExecDllFilepath);

            /*
             * OLD WAY
             * 
            // var DllInfo = new FileInfo(ExecDllFilepath);

            var typeName = Path.GetFileNameWithoutExtension(ExecDllFilepath);
            var type     = asm.GetType(typeName);

            var runnable = Activator.CreateInstance(
            var runnable = Activator.CreateInstance(type) as IRunnable;
             */

            //get types from assembly
            var typesInAssembly = asm.GetTypes();

            var type = typesInAssembly.First();

            return RunnableExecutor.InvokeRunnable(type, Body);
        }

        public static IRunnable ProduceRunnableClass(string CodeBlock)
        {
            IRunnable oRunnableCodeClass = null;

            return oRunnableCodeClass;
        }

        /*
         * NOTE: This method needs some work
         * 
        public static AppDomain ProduceSecureDomain()
        {
            var adSetup = new AppDomainSetup()
            {
                ApplicationBase = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tmp"),
                ApplicationName = "Sandbox",
                DisallowBindingRedirects = true,
                DisallowCodeDownload = true,
                DisallowPublisherPolicy = true
            };

            // adSetup.ApplicationBase = "C:\\tmp";

            PermissionSet permSet = new PermissionSet(PermissionState.None);
            permSet.AddPermission(new SecurityPermission(SecurityPermissionFlag.Execution));

            StrongName fullTrustAssembly = typeof(RunnableExecutor).Assembly.Evidence.GetHostEvidence<StrongName>();

            AppDomain secureDomain = AppDomain.CreateDomain("Sandbox", null, adSetup, permSet, fullTrustAssembly);

            return secureDomain;
        }
         */
    }
}