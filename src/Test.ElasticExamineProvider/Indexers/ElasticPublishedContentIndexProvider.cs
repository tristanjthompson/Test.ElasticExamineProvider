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
    public class ElasticPublishedContentIndexProvider : UmbracoExamine.BaseUmbracoIndexer
    {
        private string IndexName { get { return this.Name.ToLower(); } }

        /// <summary>
        /// If used in a load-balanced environment, this will stop bulk actions from running on anything but a nominated "Master" node
        /// </summary>
        private static bool IsMaster = Convert.ToBoolean(ConfigurationManager.AppSettings["ElasticSearchProvider:IsMaster"] ?? "true");
        private static string _elasticConnectionString = ConfigurationManager.AppSettings["ElasticSearchProvider:ConnectionString"] ?? "http://localhost:9200";

        private static Nest.IElasticClient _elasticClient = null;

        private readonly UmbracoHelper _umbracoHelper;
        private readonly log4net.ILog _logger;

        // let Umbraco know that we support content (as opposed to media, etc.)
        private static List<string> _supportedTypes = new List<string>() { PublishedContentItem.DocumentTypeName };

        protected override IEnumerable<string> SupportedTypes => _supportedTypes;

        public ElasticPublishedContentIndexProvider()
        {
            _logger = log4net.LogManager.GetLogger(this.GetType());
            _logger.Info("Constructor call");
            _umbracoHelper = new UmbracoHelper(UmbracoContext.Current);
        }
        

        public override void Initialize(string name, NameValueCollection config)
        {
            base.Initialize(name, config);

            _logger.Info($"Initialize: name = {name}, IndexName: {IndexName}");

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

            InitElasticClient(IndexName);
        }

        private void ElasticPublishedContentIndexProvider_IgnoringNode(object sender, IndexingNodeDataEventArgs e)
        {
            _logger.Info($"IgnoringNode() {e.NodeId}");
        }

        protected override void OnNodeIndexed(IndexedNodeEventArgs e)
        {
            _logger.Info($"OnNodeIndexed() {e.NodeId}");
            base.OnNodeIndexed(e);
        }

        public override void DeleteFromIndex(string nodeId)
        {
            _logger.Info("DeleteFromIndex() - " + nodeId);

            var result = _elasticClient.Delete(new DeleteRequest(IndexName, PublishedContentItem.DocumentTypeName, nodeId));

            _logger.Info("DeleteFromIndex() - Result: " + result.IsValid);
        }

        public override void IndexAll(string type)
        {
            _logger.Info("IndexAll() - " + type);

            if (!IsMaster || !_supportedTypes.Contains(type))
                return;

            RebuildIndex();
        }

        public override bool IndexExists()
        {
            _logger.Info("IndexExists()");
            if (!IsMaster)
                return true;

            var result = _elasticClient.IndexExists(IndexName);

            _logger.Info($"IndexExists() = {result.Exists}");

            return result.Exists;
        }

        public override void RebuildIndex()
        {
            if (!IsMaster)
            {
                _logger.Info("RebuildIndex() - not run as not on master node");
                return;
            }

            _logger.Info("RebuildIndex()");

            var timer = new System.Diagnostics.Stopwatch();
            timer.Start();

            // TODO: consider when this is appropriate - maybe only when index doesn't exist?
            // drop all indexes
            _elasticClient.DeleteIndex(IndexName);

            // loop through all content and index it
            var items = GetAllContent();
            _logger.Info($"RebuildIndex() - {items.Count} nodes found to index");

            var searchItems = new List<PublishedContentItem>();
            foreach (var item in items)
            {
                searchItems.Add(new PublishedContentItem(item));
            }

            // TODO: send up in batches (of 1000?) instead of all at once in case of large data sets
            _logger.Info($"Indexing {searchItems.Count} nodes");
            var addToIndexTasks = new List<System.Threading.Tasks.Task>
            {
                _elasticClient.IndexManyAsync(searchItems, IndexName)
            };

            // wait for all the indexing to finish
            _logger.Info($"RebuildIndex() - waiting for indexing to finish");
            System.Threading.Tasks.Task.WaitAll(addToIndexTasks.ToArray());

            timer.Stop();
            _logger.Info($"RebuildIndex() - finished indexing {items.Count} nodes in {timer.Elapsed.TotalSeconds} seconds");
        }

        public override void ReIndexNode(XElement node, string type)
        {
            var idAttribute = node.Attribute(XName.Get("id"));
            _logger.Info($"ReIndexNode(type: {type}, nodeId: {idAttribute?.Value}");

            if (!IsMaster)
                return;

            // load up the node value and index it
            var nodeToIndex = _umbracoHelper.TypedContent(idAttribute?.Value);
            if (nodeToIndex == null)
            {
                _logger.Info($"ReIndexNode - could find node with id: {idAttribute?.Value}");
                return;
            }

            // index the node
            var indexResult = _elasticClient.Index(new PublishedContentItem(nodeToIndex), x=> x.Index(IndexName));

            _logger.Info($"Reindexed ({nodeToIndex.Id}) {nodeToIndex.Name} - Success? {indexResult.IsValid}");
        }

        // ******************************************
        // ******* private helper methods ***********
        // ******************************************


        private void InitElasticClient(string indexName)
        {
            if (_elasticClient != null)
                return;

            _logger.Info("InitElasticClient() - start");
            _elasticClient = new Nest.ElasticClient(
                new ConnectionSettings(
                    new SingleNodeConnectionPool(
                        new Uri(_elasticConnectionString)
                    )
                )
            );
            _logger.Info("InitElasticClient() - done");
        }

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
