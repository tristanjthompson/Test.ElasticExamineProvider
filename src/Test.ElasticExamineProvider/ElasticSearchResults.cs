using Examine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using Nest;
using Test.ElasticExamineProvider.DocumentTypes;

namespace Test.ElasticExamineProvider
{
    public class ElasticSearchResults : List<SearchResult>, ISearchResults
    {
        public int TotalItemCount => this.Count;

        public ElasticSearchResults() : base() { }
        public ElasticSearchResults(ISearchResponse<PublishedContent> elasticResults)
        {
            foreach(var result in elasticResults.Hits.OrderByDescending(x => x.Score))
            {
                Add(new ElasticSearchResult(result.Source.Properties)
                {
                    DocId = result.Source.Id,
                    Id = result.Source.Id,
                    Score = result.Score.HasValue ? (float)result.Score.Value : 0
                });
            }
        }

        public IEnumerable<SearchResult> Skip(int skip)
        {
            return System.Linq.Enumerable.Skip(this, skip);
        }
    }
}
