using Examine;
using Examine.SearchCriteria;
using System;

namespace Test.ElasticExamineProvider.Searchers
{
    public class ElasticPublishedContentIndexSearcher : Examine.Providers.BaseSearchProvider
    {
        private readonly log4net.ILog _logger;

        public ElasticPublishedContentIndexSearcher()
        {
            _logger = log4net.LogManager.GetLogger(this.GetType());
            _logger.Info("Constructor call");
        }

        public override ISearchCriteria CreateSearchCriteria()
        {
            _logger.Info("CreateSearchCriteria()");
            throw new NotImplementedException();
        }

        public override ISearchCriteria CreateSearchCriteria(string type)
        {
            _logger.Info($"CreateSearchCriteria(type: {type})");
            throw new NotImplementedException();
        }

        public override ISearchCriteria CreateSearchCriteria(BooleanOperation defaultOperation)
        {
            _logger.Info($"CreateSearchCriteria(defaultOperation: {defaultOperation})");
            throw new NotImplementedException();
        }

        public override ISearchCriteria CreateSearchCriteria(string type, BooleanOperation defaultOperation)
        {
            _logger.Info($"CreateSearchCriteria(type: {type}, defaultOperation: {defaultOperation})");
            throw new NotImplementedException();
        }

        public override ISearchResults Search(string searchText, bool useWildcards)
        {
            _logger.Info($"Search(searchText: {searchText}, useWildcards: {useWildcards})");
            throw new NotImplementedException();
        }

        public override ISearchResults Search(ISearchCriteria searchParams)
        {
            _logger.Info($"Search(SearchIndexType: {searchParams.SearchIndexType})");
            throw new NotImplementedException();
        }
    }
}
