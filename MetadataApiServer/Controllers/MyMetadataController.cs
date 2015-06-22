using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
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
        private const string CONST_RETRIEVE_METADATA_SQL =
@"
SELECT
    Action
    , Type
    , Parameters
    , Payload
    , ExecutableDLL
FROM
    [TEST].[dbo].[ServerMetadata]
";
        #endregion

        #region STATIC MEMBERS
        private static object             moCacheLock     = new object();
        private static List<MetadataItem> moMetadataCache = new List<MetadataItem>();
        #endregion

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

        // POST api/mymetadata
        public HttpResponseMessage Post(List<Dictionary<string, string>> body)
        {
            var action    = GetAction(body);
            var mainParam = GetCommand(body);
            var respBody  = body;
            var response  = Request.CreateResponse<List<Dictionary<string, string>>>(HttpStatusCode.OK, respBody);
            var uri       = Url.Link("DefaultApi", new { id = action });

            response.Headers.Location = new Uri(uri);

            MetadataItem item = GetCacheItem("POST", action, mainParam);

            if (ValidateParamaters(item.Parameters, body))
            {
                respBody = LoadAndExecuteDLL(item.Parameters, item.ExecutableDLL, body);
                response = Request.CreateResponse<List<Dictionary<string, string>>>(HttpStatusCode.OK, respBody);
            }

            return response;
        }

        #region Other Requests
        // PUT api/mymetadata/5
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

        // DELETE api/mymetadata/5
        public void Delete(int id)
        {
        }
        #endregion

        #region Support Methods

        private string GetAction(List<Dictionary<string, string>> body) { return body[0]["name"]; }

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

        private string GetCommand(List<Dictionary<string, string>> body) { return body[0]["command"]; }

        private string GetMetadata() { return "<payload>RAW METADATA</payload>"; }

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

        private string GetNavPayload(MetadataItem item, List<Dictionary<string, string>> body) { return "<value>TESTING123</value>"; }

        private string GetFileList(MetadataItem item, string param1) { return "<value>TESTING456</value>"; }

        private string GetFileList(MetadataItem item, List<Dictionary<string, string>> body) { return "<value>TESTING456</value>"; }

        private string GetFileContents(MetadataItem item, string param1) { return "<value>TESTING789</value>"; }

        private string GetFileContents(MetadataItem item, List<Dictionary<string, string>> body) { return "<value>TESTING789</value>"; }

        private List<Dictionary<string, string>> LoadAndExecuteDLL(string Parameters, string ExecDLL, List<Dictionary<string, string>> body)
        {
            var MDRoot = RoleEnvironment.GetConfigurationSettingValue("MetadataRootFileSystem");

            List<Dictionary<string, string>> oResultBody = new List<Dictionary<string, string>>();

            var ExecDllFilepath = MDRoot + "\\" + ExecDLL;

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

            //get types from assemblt
            var typesInAssembly = asm.GetTypes();

            var type = typesInAssembly.First();

            //create instance
            var runnable = Activator.CreateInstance(type) as IRunnable;

            if (runnable == null) throw new Exception("broke");

            oResultBody = runnable.Run(body);

            return oResultBody;
        }

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

        private string SaveFile(MetadataItem item, List<Dictionary<string, string>> body) { return ""; }

        private bool   ValidateParamaters(string Parameters, List<Dictionary<string, string>> body) { return true; }

        #endregion
    }
}
