using System;
using System.Collections.Generic;
using Umbraco.Core.Models;
using Nest;

namespace Test.ElasticExamineProvider.DocumentTypes
{
    [ElasticsearchType(Name = DocumentTypeName)]
    public class PublishedContentItem
    {
        public const string DocumentTypeName = "content";  
        public int Id { get; set; }
        public string DocumentTypeAlias { get; set; }
        public int DocumentTypeId { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime UpdateDate { get; set; }
        public string Url { get; set; }
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();

        public PublishedContentItem(IPublishedContent item)
        {
            Id = item.Id;
            DocumentTypeAlias = item.DocumentTypeAlias;
            DocumentTypeId = item.DocumentTypeId;
            CreateDate = item.CreateDate;
            UpdateDate = item.UpdateDate;
            Url = item.Url;

            foreach(var prop in item.Properties)
            {
                if (prop == null || string.IsNullOrWhiteSpace(prop.PropertyTypeAlias) || !prop.HasValue)
                    continue;

                Properties.Add(prop.PropertyTypeAlias, prop.DataValue?.ToString());
            }
        }
    }
}
