using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Examine;
using Nest;
using System.Configuration;
using Elasticsearch.Net;
using Umbraco.Web;
using Umbraco.Core.Models;
using System.Collections.Specialized;
using Test.ElasticExamineProvider.DocumentTypes;

namespace Test.ElasticExamineProvider.Indexers
{
    public class DefaultPublishedContentIndexer : UmbracoExamine.BaseUmbracoIndexer
    {
        private string _indexName { get { return this.IndexSetName.ToLower(); } }

        /// <summary>
        /// If used in a load-balanced environment, this will stop bulk actions from running on anything but a nominated "Master" node
        /// </summary>
        private static bool _isMaster = Convert.ToBoolean(ConfigurationManager.AppSettings["ElasticSearchProvider:IsMaster"] ?? "true");
        private static string _elasticConnectionString = ConfigurationManager.AppSettings["ElasticSearchProvider:ConnectionString"] ?? "http://localhost:9200";

        private static Nest.IElasticClient _elasticClient = null;
        private static log4net.ILog _logger = null;

        private readonly UmbracoHelper _umbracoHelper;

        // let Umbraco know that we only support content (as opposed to media, etc.)
        private static List<string> _supportedTypes = new List<string>() { UmbracoExamine.IndexTypes.Content };

        protected override IEnumerable<string> SupportedTypes => _supportedTypes;

        public DefaultPublishedContentIndexer()
        {
            if(_logger == null)
                _logger = log4net.LogManager.GetLogger(this.GetType());

            _umbracoHelper = new UmbracoHelper(UmbracoContext.Current);
        }
        

        public override void Initialize(string name, NameValueCollection config)
        {
            base.Initialize(name, config);

            _logger.Info($"Initialize: name = {name}, IndexName: {_indexName}");

            if (config.Keys.Count == 0)
            {
                _logger.Info($"Config empty");
            }
            else
            {
                _logger.Info($"Config values: ");
                foreach (string key in config.Keys)
                {
                    _logger.Info($" - Key: {key}, Value: {config[key]}");
                }
            }

            InitElasticClient(_indexName);
        }

        public override bool IndexExists()
        {
            _logger.Info("IndexExists()");
            if (!_isMaster)
                return true;

            var result = _elasticClient.IndexExists(_indexName);

            _logger.Info($"IndexExists() = {result.Exists}");

            return result.Exists;
        }

        public override void IndexAll(string type)
        {
            _logger.Info("IndexAll() - " + type);

            if (!_isMaster || !_supportedTypes.Contains(type))
                return;

            BuildIndex(dropIndexFirst: false);
        }

        public override void RebuildIndex()
        { 
            BuildIndex(dropIndexFirst: true);
        }

        private void BuildIndex(bool dropIndexFirst)
        {
            if (!_isMaster)
            {
                _logger.Info("RebuildIndex() - not run as not on master node");
                return;
            }

            _logger.Info("RebuildIndex()");

            var timer = new System.Diagnostics.Stopwatch();
            timer.Start();

            // TODO: consider when it's appropriate to drop the index
            if (dropIndexFirst)
            {
                _elasticClient.DeleteIndex(_indexName);
            }

            // loop through all content and index it
            var items = GetAllContent();
            _logger.Info($"RebuildIndex() - {items.Count} nodes found to index");

            var searchItems = new List<PublishedContent>();
            foreach (var item in items)
            {
                searchItems.Add(new PublishedContent(item));
            }

            // TODO: send up in batches (of 1000?) asynchronously in case large chunks of data causes issues
            _logger.Info($"Indexing {searchItems.Count} nodes");
            var addToIndexTasks = new List<System.Threading.Tasks.Task>
            {
                _elasticClient.IndexManyAsync(searchItems, _indexName)
            };

            // wait for all the indexing to finish
            _logger.Info($"RebuildIndex() - waiting for indexing to finish");
            System.Threading.Tasks.Task.WaitAll(addToIndexTasks.ToArray());

            timer.Stop();
            _logger.Info($"RebuildIndex() - finished indexing {items.Count} nodes in {timer.Elapsed.TotalSeconds} seconds");
        }

        public override void DeleteFromIndex(string nodeId)
        {
            _logger.Info("DeleteFromIndex() - " + nodeId);

            var result = _elasticClient.Delete(new DeleteRequest(_indexName, PublishedContent.DocumentTypeName, nodeId));

            _logger.Info("DeleteFromIndex() - Result: " + result.IsValid);
        }
        
        public override void ReIndexNode(XElement node, string type)
        {
            var idAttribute = node.Attribute(XName.Get("id"));
            _logger.Info($"ReIndexNode(type: {type}, nodeId: {idAttribute?.Value}");

            if (!_isMaster)
                return;

            // Cheat!  Load up the published node value and index it
            // TODO: figure out whether this is a good idea and look at how/why Examine parses the XElement instead of doing this
            var nodeToIndex = _umbracoHelper.TypedContent(idAttribute?.Value);
            if (nodeToIndex == null)
            {
                _logger.Info($"ReIndexNode - could find node with id: {idAttribute?.Value}");
                return;
            }

            // index the node
            var indexResult = _elasticClient.Index(new PublishedContent(nodeToIndex), x=> x.Index(_indexName));

            _logger.Info($"Reindexed ({nodeToIndex.Id}) {nodeToIndex.Name} - Success? {indexResult.IsValid}");
        }
        public static IElasticClient InitElasticClient(string indexName)
        {
            if (_elasticClient != null)
                return _elasticClient;

            _logger.Info("InitElasticClient() - start");
            _elasticClient = new Nest.ElasticClient(
                new ConnectionSettings(
                    new SingleNodeConnectionPool(
                        new Uri(_elasticConnectionString)
                    )
                )
            );
            _logger.Info("InitElasticClient() - done");


            return _elasticClient;
        }

        // ******************************************
        // ******* PRIVATE HELPER METHODS ***********
        // ******************************************

        private List<IPublishedContent> GetAllContent()
        {
            var results = new List<IPublishedContent>();
            foreach(var rootItem in _umbracoHelper.TypedContentAtRoot())
            {
                results.Add(rootItem);
                if(rootItem.Children.Any())
                {
                    results.AddRange(GetDescendents(rootItem));
                }
            }

            return results;
        }

        private List<IPublishedContent> GetDescendents(IPublishedContent content)
        {
            var results = new List<IPublishedContent>();

            foreach(var child in content.Children)
            {
                results.Add(child);
                if (child.Children.Any())
                {
                    results.AddRange(GetDescendents(child));
                }
            }

            return results;
        }
    }
}
