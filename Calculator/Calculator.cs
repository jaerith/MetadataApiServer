using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MetadataApiCommon;

namespace Calculator
{
    public class Calculator : IRunnable
    {
        public Calculator() {}

        public List<Dictionary<string, string>> Run(List<Dictionary<string, string>> PostBody)
        {
            List<Dictionary<string, string>> oResultBody = new List<Dictionary<string, string>>();

            if ((PostBody != null) && (PostBody.Count > 0))
            {
                int nFirstValue  = 0;
                int nSecondValue = 0;

                Dictionary<string, string> oValues = PostBody[0];

                if (oValues.ContainsKey("FirstValue"))
                {
                    try
                    {
                        nFirstValue = Convert.ToInt32(oValues["FirstValue"]);
                    }
                    catch (Exception ex) {}
                }

                if (oValues.ContainsKey("SecondValue"))
                {
                    try
                    {
                        nSecondValue = Convert.ToInt32(oValues["SecondValue"]);
                    }
                    catch (Exception ex) { }
                }

                int nResultValue = nFirstValue * nSecondValue;

                Dictionary<string, string> oResult = new Dictionary<string, string>();
                oResult["Result"] = Convert.ToString(nResultValue);

                oResultBody.Add(oResult);
            }

            return oResultBody;
        }
    }
}
