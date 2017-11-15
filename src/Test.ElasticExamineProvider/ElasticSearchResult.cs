using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.ElasticExamineProvider
{
    public class ElasticSearchResult : Examine.SearchResult
    {
        public ElasticSearchResult(Dictionary<string,string> fields)
        {
            foreach (string key in fields.Keys)
                this.Fields.Add(key, fields[key]);
        }
    }
}
