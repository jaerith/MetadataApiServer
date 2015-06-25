using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Web;

namespace MetadataApiServer.Models
{
    public class MetadataItem
    {
        public MetadataItem()
        {
            Type     = Action      = Parameters = Payload = ExecutableDLL = ExecutableCode = "";
            PvalName = PvalCommand = "";
        }

        public MetadataItem(SqlDataReader reader)
        {
            if (reader != null)
            {
                Type           ItemType = typeof(MetadataItem);
                PropertyInfo[] Props    = ItemType.GetProperties();

                foreach (PropertyInfo oTmpProperty in Props)
                {
                    Type   PropType  = oTmpProperty.PropertyType;
                    string PropName  = oTmpProperty.Name;

                    if (!PropName.StartsWith("Pval"))
                    {
                        string PropValue = reader[PropName].ToString();

                        try
                        {
                            //Set the value of the property
                            oTmpProperty.SetValue(this, PropValue, null);
                        }
                        catch (Exception ex)
                        {
                            throw new Exception("ERROR!  There was a problem with setting Property(" + PropName + ") with value (" + ((string)PropValue) + ")");
                        }
                    }
                }

                if (!String.IsNullOrEmpty(Parameters))
                {
                    if (Parameters.StartsWith("name="))
                        PvalName = Parameters.Substring(5);
                    else if (Parameters.StartsWith("command="))
                        PvalCommand = Parameters.Substring(8);
                }
            }
        }

        public string Type           { get; set; }
        public string Action         { get; set; }
        public string Parameters     { get; set; }
        public string Payload        { get; set; }
        public string ExecutableDLL  { get; set; }
        public string ExecutableCode { get; set; }

        public string PvalName       { get; set; }
        public string PvalCommand    { get; set; }

        private void Init()
        {
            Type     = Action = Parameters = Payload = ExecutableDLL = ExecutableCode = "";
            PvalName = PvalCommand = "";
        }

        private static bool IsNullableType(Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition().Equals(typeof(Nullable<>));
        }
    }
}