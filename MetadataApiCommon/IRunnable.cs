using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetadataApiCommon
{
    public interface IRunnable
    {
        List<Dictionary<string, string>> Run(List<Dictionary<string, string>> PostBody);
    }
}
