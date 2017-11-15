using Examine;
using Examine.LuceneEngine.SearchCriteria;
using Examine.SearchCriteria;
using System;
using System.Security;
using System.Collections.Specialized;
using Nest;
using Test.ElasticExamineProvider.DocumentTypes;
using Examine.LuceneEngine;
using System.Collections;
using System.Text;
using System.Linq;

namespace Test.ElasticExamineProvider.Searchers
{
    public class DefaultPublishedContentSearcher : UmbracoExamine.UmbracoExamineSearcher
    {
        private const int AllResults = -1;
        private string _indexName { get { return this.IndexSetName.ToLower(); } }
        private readonly log4net.ILog _logger;
        private static Nest.IElasticClient _elasticClient = null;

        public DefaultPublishedContentSearcher()
        {
            _logger = log4net.LogManager.GetLogger(this.GetType());
        }

        public override void Initialize(string name, NameValueCollection config)
        {
            base.Initialize(name, config);
            _elasticClient = Indexers.DefaultPublishedContentIndexer.InitElasticClient(_indexName);
        }

        protected override string[] GetSearchFields()
        {
            return new[] { "_all" };    // _all - tells Elastic to search all fields
        }

        public override ISearchResults Search(ISearchCriteria searchParams)
        {
            return Search(searchParams, AllResults);
        }

        public override ISearchResults Search(ISearchCriteria searchParams, int maxResults)
        {
            _logger.Info("Search(ISearchCriteria searchParams, int maxResults)");
            // extract out the terms
            Hashtable terms = new Hashtable();
            ((LuceneSearchCriteria)searchParams).Query.ExtractTerms(terms);

            var luceneTextQuery = "";
            foreach (var key in terms.Keys)
                luceneTextQuery += " " + terms[key];

            // search using the terms
            var elasticResults = SearchElastic(luceneTextQuery, maxResults);

            return new ElasticSearchResults(elasticResults);
        }

        public override ISearchResults Search(string searchText, bool useWildcards)
        {
            return Search(searchText, useWildcards, null);
        }

        public override ISearchResults Search(string searchText, bool useWildcards, string indexType)
        {
            _logger.Info("Search(string searchText, bool useWildcards, string indexType)");
            var elasticResults = SearchElastic($"_all:{searchText}");

            return new ElasticSearchResults(elasticResults);
        }

        private ISearchResponse<PublishedContent> SearchElastic(string luceneTerm, int maxResults = AllResults)
        {
            var searchRequest = new SearchRequest() { Query = new QueryStringQuery() { Query = luceneTerm } };
            if (maxResults != AllResults)
                searchRequest.Size = maxResults;

            return _elasticClient.Search<PublishedContent>(searchRequest);
        }
    }
}
