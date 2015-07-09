using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Http;

using MetadataApiCommon;
using MetadataApiServer.Models;

using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace MetadataApiServer.Controllers
{
    public class MyMetadataController : ApiController
    {
        #region CONSTANTS

        private const string CONST_CLASS_NAME_START_IND = "public class";
        private const string CONST_CLASS_NAME_END_IND   = ":";

        private const string CONST_RETRIEVE_METADATA_SQL =
@"
SELECT
    Action
    , Type
    , Parameters
    , Payload
    , ExecutableDLL
    , ExecutableCode
FROM
    [TEST].[dbo].[ServerMetadata]
";
        #endregion

        #region STATIC MEMBERS

        /// <summary>
        ///     Lock object used to sychronize access to the cache.
        /// </summary>
        private static object             moCacheLock     = new object();

        /// <summary>
        ///     In-memory cache of the metadata table.
        /// </summary>
        private static List<MetadataItem> moMetadataCache = new List<MetadataItem>();

        #endregion

        /// <summary>
        /// 
        ///     GET api/mymetadata
        ///     
        ///     Handler for the HTTP Get requests to the service.  
        ///     It's used mainly to retrieve data needed by the mobile application.
        /// </summary>
        /// <param name="actionType">The type of data being requested</param>
        /// <param name="mainParam">The accompanying value needed in order to fulfill the request</param>
        /// <returns>The sought value (Ex. the XML that describes a mobile app's interface)</returns>
        public string Get(string actionType, string mainParam)
        {
            string       payload = "{ 'contents' : 'none' }";
            MetadataItem item    = GetCacheItem("GET", actionType, mainParam);

            if      (actionType == "getMetadata")   payload = GetMetadata();
            else if (actionType == "getNavigation") payload = GetNavPayload(item, mainParam);
            else if (actionType == "getFileList")   payload = GetFileList(item, mainParam);
            else if (actionType == "getFile")       payload = GetFileContents(item, mainParam);

            return payload;
        }

        /// <summary>
        /// 
        ///     POST api/mymetadata
        ///     
        ///     Handler for the HTTP Post requests to the service.  
        ///     It's used mainly to execute a function that is indicated by the metadata, existing as either:
        ///         a.) a compiled DLL that will be loaded via DLL injection
        ///         b.) a code block that will be compiled and executed during runtime
        ///     
        /// </summary>
        /// <param name="body">The values needed in order to fulfill the request successfully</param>
        /// <returns>The HTML Response that carries the output and/or status of the code execution</returns>
        public HttpResponseMessage Post(List<Dictionary<string, string>> body)
        {
            var action    = GetAction(body);
            var mainParam = GetCommand(body);
            var respBody  = body;
            var response  = Request.CreateResponse<List<Dictionary<string, string>>>(HttpStatusCode.OK, respBody);
            var uri       = Url.Link("DefaultApi", new { id = action });

            response.Headers.Location = new Uri(uri);

            MetadataItem item = GetCacheItem("POST", action, mainParam);

            if (action == "executeCommand")
            {
                if (ValidateParamaters(item.Parameters, body))
                {
                    respBody = LoadAndExecuteDLL(item.Parameters, item.ExecutableDLL, body);
                    response = Request.CreateResponse<List<Dictionary<string, string>>>(HttpStatusCode.OK, respBody);
                }
            }
            else if (action == "executeDynamic")
            {
                if (ValidateParamaters(item.Parameters, body))
                {
                    respBody = CompileAndExecuteCode(item.Parameters, item.ExecutableCode, body);
                    response = Request.CreateResponse<List<Dictionary<string, string>>>(HttpStatusCode.OK, respBody);
                }
            }

            return response;
        }

        #region Other Requests
        /// <summary>
        /// 
        ///     PUT api/mymetadata
        ///     
        ///     Handler for the HTTP Put requests to the service.  
        ///     
        ///     It could be used to store the contents of saved files, but it is still under construction.
        /// 
        /// </summary>
        public void Put(int id, [FromBody]string value)
        {
            /*
            // Setup the connection to Azure Storage
            var storageAccount = CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue("MyConnectionString"));
            var blobClient = storageAccount.CreateCloudBlobClient();
            // Get and create the container
            var blobContainer = blobClient.GetContainerReference("quicklap");
            blobContainer.CreateIfNotExists();
            // upload a text blob
            var blob = blobContainer.GetBlockBlobReference(Guid.NewGuid().ToString());
            blob.UploadText("Hello Azure");
             */
        }

        #endregion

        #region Support Methods

        /// <summary>
        /// 
        ///     This code makes the call to compile and execute a block of code
        ///     
        /// </summary>
        /// <param name="UrlParameters">The parameters used for the code compilation/execution</param>
        /// <param name="ExecCode">The block of code that will be compiled and then executed</param>
        /// <param name="Body">The values that will be used by the block of code</param>
        /// <returns>The return value(s) desired by the code execution</returns>
        private List<Dictionary<string, string>> CompileAndExecuteCode(string UrlParameters, string ExecCode, List<Dictionary<string, string>> Body)
        {
            bool bSafeMode = (GetMode(Body) == "safe");

            if (bSafeMode)
                return RunnableExecutor.CompileAndExecuteCodeSafe(UrlParameters, ExecCode, Body);
            else
            {
                RunnableExecutor executor = new RunnableExecutor();
                return executor.CompileAndExecuteCode(UrlParameters, ExecCode, Body);
            }
        }

        /// <summary>
        /// 
        ///     A simple way to retrieve the desired type of a request
        ///     
        /// </summary>
        /// <param name="body">The values provided by the request</param>
        /// <returns>The type requested</returns>
        private string GetAction(List<Dictionary<string, string>> body) { return body[0]["name"]; }

        /// <summary>
        /// 
        ///     This function finds and then returns the relevant record from the metadata cache
        ///     
        /// </summary>
        /// <param name="psType">The HTTP type of the request</param>
        /// <param name="psAction">The API type of the request</param>
        /// <param name="psMainParam">The API subtype of the request</param>
        /// <returns>The relevant record of the cache</returns>
        private static MetadataItem GetCacheItem(string psType, string psAction, string psMainParam) 
        {
            MetadataItem oMDItem = new MetadataItem();

            try
            {
                if (moMetadataCache.Count == 0)
                    PullCache();

                lock (moCacheLock)
                {
                    foreach (MetadataItem TempItem in moMetadataCache)
                    {
                        if ((TempItem.Action == psAction) && !String.IsNullOrEmpty(psType) && (TempItem.Type == psType))
                        {
                            if ((TempItem.Type == "GET") && (TempItem.PvalName == psMainParam))
                            {
                                oMDItem = TempItem;
                                break;
                            }
                            else if ((TempItem.Type == "POST") && (TempItem.PvalCommand == psMainParam))
                            {
                                oMDItem = TempItem;
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine(ex);
            }

            return oMDItem; 
        }

        /// <summary>
        /// 
        ///     A simple way to retrieve the desired subtype of a request
        ///     
        /// </summary>
        /// <param name="body">The values provided by the request</param>
        /// <returns>The subtype requested</returns>
        private string GetCommand(List<Dictionary<string, string>> body) { return body[0]["command"]; }

        /// <summary>
        ///     Under Construction
        /// </summary>
        private string GetMetadata() { return "<payload>RAW METADATA</payload>"; }

        /// <summary>
        /// 
        ///     A simple way to retrieve the mode of a request
        ///     
        /// </summary>
        /// <param name="body">The values provided by the request</param>
        /// <returns>The mode requested</returns>
        private string GetMode(List<Dictionary<string, string>> body)
        {
            if (body[0].ContainsKey("mode"))
                return body[0]["mode"];
            else
                return "";
        }

        /// <summary>
        /// 
        ///     This function returns the XML content that will serve as the navigational payload 
        ///     for a mobile app screen.
        ///     
        /// </summary>
        /// <param name="item">The metadata needed in order to retrieve the XML content from the target file.</param>
        /// <param name="param1">An additional value in order to assist with fulfilling the request</param>
        /// <returns>The XML content that serves as the navigational payload</returns>
        private string GetNavPayload(MetadataItem item, string param1) 
        {
            var NavPayload     = "<value>TESTING123</value>";
            var PayloadBuilder = new StringBuilder();

            if (!String.IsNullOrEmpty(item.Payload))
            {
                var MDRoot          = RoleEnvironment.GetConfigurationSettingValue("MetadataRootFileSystem");
                var PayloadFilepath = MDRoot + "\\" + item.Payload;

                using (StreamReader reader = new StreamReader(PayloadFilepath))
                {
                    String line = "";

                    while ((line = reader.ReadLine()) != null)
                        PayloadBuilder.Append(line).Append("\n");
                }

                NavPayload = PayloadBuilder.ToString();
            }

            return NavPayload;
        }

        /// <summary>
        ///     Under Construction
        /// </summary>
        private string GetNavPayload(MetadataItem item, List<Dictionary<string, string>> body) { return "<value>TESTING123</value>"; }

        /// <summary>
        ///     Under Construction
        /// </summary>
        private string GetFileList(MetadataItem item, string param1) { return "<value>TESTING456</value>"; }

        /// <summary>
        ///     Under Construction
        /// </summary>
        private string GetFileList(MetadataItem item, List<Dictionary<string, string>> body) { return "<value>TESTING456</value>"; }

        /// <summary>
        ///     Under Construction
        /// </summary>
        private string GetFileContents(MetadataItem item, string param1) { return "<value>TESTING789</value>"; }

        /// <summary>
        ///     Under Construction
        /// </summary>
        private string GetFileContents(MetadataItem item, List<Dictionary<string, string>> body) { return "<value>TESTING789</value>"; }

        /// <summary>
        /// 
        ///     This code makes the call to inject a DLL and then execute its targeted functionality
        ///     
        /// </summary>
        /// <param name="Parameters">The parameters used for the DLL injection and execution</param>
        /// <param name="ExecDLL">The filepath to the DLL</param>
        /// <param name="Body">The values that will be used by the DLL</param>
        /// <returns>The return value(s) desired by the code execution</returns>
        private List<Dictionary<string, string>> LoadAndExecuteDLL(string Parameters, string ExecDLL, List<Dictionary<string, string>> Body)
        {
            bool bSafeMode = (GetMode(Body) == "safe");
            var  MDRoot    = RoleEnvironment.GetConfigurationSettingValue("MetadataRootFileSystem");

            if (bSafeMode)
                return RunnableExecutor.LoadAndExecuteDLLSafe(MDRoot, Parameters, ExecDLL, Body);
            else 
                return RunnableExecutor.LoadAndExecuteDLL(MDRoot, Parameters, ExecDLL, Body);
        }

        /// <summary>
        ///     This code pulls the metadata from a SQL Server table and stores it within a local cache.
        /// </summary>
        private static void PullCache()
        {
            var dbConnString  = RoleEnvironment.GetConfigurationSettingValue("MetadataDBConnectionString");

            lock (moCacheLock)
            {
                using (var dbConn = new SqlConnection(dbConnString))
                {
                    dbConn.Open();

                    var cmdRetrieveMD = new SqlCommand(CONST_RETRIEVE_METADATA_SQL, dbConn);

                    using (SqlDataReader reader = cmdRetrieveMD.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            MetadataItem oNewItem = new MetadataItem(reader);

                            moMetadataCache.Add(oNewItem);
                        }
                    }
                }
            }
        }

        /// <summary>
        ///     Under Construction
        /// </summary>
        private string SaveFile(MetadataItem item, List<Dictionary<string, string>> body) { return ""; }

        /// <summary>
        ///     Under Construction
        /// </summary>
        private bool   ValidateParamaters(string Parameters, List<Dictionary<string, string>> body) { return true; }

        #endregion
    }
}
