//-----------------------------------------------------------------------
// <copyright file="ServerClient.cs" company="Hibernating Rhinos LTD">
//     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Raven.Abstractions.Connection;
using Raven.Abstractions.Replication;
using Raven.Client.Changes;
using Raven.Abstractions.Commands;
using Raven.Abstractions.Data;
using Raven.Abstractions.Extensions;
using Raven.Abstractions.Indexing;
using Raven.Abstractions.Util;
using Raven.Client.Connection.Async;
using Raven.Client.Connection.Implementation;
using Raven.Client.Connection.Profiling;
using Raven.Client.Connection.Request;
using Raven.Client.Document;
using Raven.Client.Exceptions;
using Raven.Client.Indexes;
using Raven.Client.Metrics;
using Raven.Database.Data;
using Raven.Json.Linq;

namespace Raven.Client.Connection
{
    public class ServerClient : IDatabaseCommands, IInfoDatabaseCommands
    {
        private readonly AsyncServerClient asyncServerClient;

        public event EventHandler<FailoverStatusChangedEventArgs> FailoverStatusChanged
        {
            add { asyncServerClient.RequestExecuter.FailoverStatusChanged += value; }
            remove { asyncServerClient.RequestExecuter.FailoverStatusChanged -= value; }
        }

        public ServerClient(AsyncServerClient asyncServerClient)
        {
            this.asyncServerClient = asyncServerClient;
        }

        public IInfoDatabaseCommands Info
        {
            get
            {
                return this;
            }
        }

        public OperationCredentials PrimaryCredentials
        {
            get { return asyncServerClient.PrimaryCredentials; }
        }

        public DocumentConvention Convention
        {
            get { return asyncServerClient.convention; }
        }

        public IRequestExecuter RequestExecuter
        {
            get { return asyncServerClient.RequestExecuter; }
        }

        #region IDatabaseCommands Members

        public NameValueCollection OperationsHeaders
        {
            get { return asyncServerClient.OperationsHeaders; }
            set { asyncServerClient.OperationsHeaders = value; }
        }

        public JsonDocument Get(string key)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.GetAsync(key));
        }

        public IGlobalAdminDatabaseCommands GlobalAdmin =>
            new AdminServerClient(asyncServerClient, new AsyncAdminServerClient(asyncServerClient));

        public JsonDocument[] StartsWith(string keyPrefix, string matches, int start, int pageSize,
                                         RavenPagingInformation pagingInformation = null, bool metadataOnly = false,
                                         string exclude = null, string transformer = null,
                                         Dictionary<string, RavenJToken> transformerParameters = null,
                                         string skipAfter = null)
        {
            return
                AsyncHelpers.RunSync(() => asyncServerClient.StartsWithAsync(keyPrefix, matches, start, pageSize, pagingInformation, metadataOnly, exclude,
                                                  transformer, transformerParameters, skipAfter)
                                 );
        }

        public RavenJToken ExecuteGetRequest(string requestUrl)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.ExecuteGetRequest(requestUrl));
        }

        internal T ExecuteWithReplication<T>(HttpMethod method, Func<OperationMetadata, IRequestTimeMetric, T> operation)
        {
            return
                AsyncHelpers.RunSync(() => asyncServerClient.ExecuteWithReplication(method, (operationMetadata, requestTimeMetric) => Task.FromResult(operation(operationMetadata, requestTimeMetric))));
        }

        public JsonDocument[] GetDocuments(int start, int pageSize, bool metadataOnly = false)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.GetDocumentsAsync(start, pageSize, metadataOnly));
        }

        public JsonDocument[] GetDocuments(Etag fromEtag, int pageSize, bool metadataOnly = false)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.GetDocumentsAsync(fromEtag, pageSize, metadataOnly));
        }

        public PutResult Put(string key, Etag etag, RavenJObject document, RavenJObject metadata)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.PutAsync(key, etag, document, metadata));
        }

        public void Delete(string key, Etag etag)
        {
            AsyncHelpers.RunSync(() => asyncServerClient.DeleteAsync(key, etag));
        }

        [Obsolete("Use RavenFS instead.")]
        public void PutAttachment(string key, Etag etag, Stream data, RavenJObject metadata)
        {
            AsyncHelpers.RunSync(() => asyncServerClient.PutAttachmentAsync(key, etag, data, metadata));
        }

        [Obsolete("Use RavenFS instead.")]
        public void UpdateAttachmentMetadata(string key, Etag etag, RavenJObject metadata)
        {
            AsyncHelpers.RunSync(() => asyncServerClient.UpdateAttachmentMetadataAsync(key, etag, metadata));
        }

        [Obsolete("Use RavenFS instead.")]
        public IEnumerable<Attachment> GetAttachmentHeadersStartingWith(string idPrefix, int start, int pageSize)
        {
            return asyncServerClient.GetSyncAttachmentHeadersStartingWithAsyncSyncEnumerable(idPrefix, start, pageSize).Result;

        }

        [Obsolete("Use RavenFS instead.")]
        public Attachment GetAttachment(string key)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.GetAttachmentAsync(key));
        }

        [Obsolete("Use RavenFS instead.")]
        public Attachment HeadAttachment(string key)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.HeadAttachmentAsync(key));
        }

        [Obsolete("Use RavenFS instead.")]
        public void DeleteAttachment(string key, Etag etag)
        {
            AsyncHelpers.RunSync(() => asyncServerClient.DeleteAttachmentAsync(key, etag));
        }

        public string[] GetDatabaseNames(int pageSize, int start = 0)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.GlobalAdmin.GetDatabaseNamesAsync(pageSize, start));
        }

        public string[] GetIndexNames(int start, int pageSize)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.GetIndexNamesAsync(start, pageSize));
        }

        public IndexDefinition[] GetIndexes(int start, int pageSize)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.GetIndexesAsync(start, pageSize));
        }

        public TransformerDefinition[] GetTransformers(int start, int pageSize)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.GetTransformersAsync(start, pageSize));
        }

        public TransformerDefinition GetTransformer(string name)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.GetTransformerAsync(name));
        }

        public void DeleteTransformer(string name)
        {
            AsyncHelpers.RunSync(() => asyncServerClient.DeleteTransformerAsync(name));
        }

        public void SetTransformerLock(string name, TransformerLockMode lockMode)
        {
            AsyncHelpers.RunSync(() => asyncServerClient.SetTransformerLockAsync(name, lockMode));
        }

        public void ResetIndex(string name)
        {
            AsyncHelpers.RunSync(() => asyncServerClient.ResetIndexAsync(name));
        }
        public void SetIndexLock(string name, IndexLockMode unLockMode)
        {
            AsyncHelpers.RunSync(() => asyncServerClient.SetIndexLockAsync(name, unLockMode));
        }
        public void SetIndexPriority(string name, IndexingPriority priority)
        {
            AsyncHelpers.RunSync(() => asyncServerClient.SetIndexPriorityAsync(name, priority));
        }

        public IndexDefinition GetIndex(string name)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.GetIndexAsync(name));
        }

        public IndexingPerformanceStatistics[] GetIndexingPerformanceStatistics()
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.GetIndexingPerformanceStatisticsAsync());
        }

        internal void ReplicateIndex(string name)
        {
            AsyncHelpers.RunSync(() => asyncServerClient.ReplicateIndexAsync(name));
        }

        public string PutIndex(string name, IndexDefinition definition)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.PutIndexAsync(name, definition, false));
        }

        public string[] PutIndexes(IndexToAdd[] indexesToAdd)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.PutIndexesAsync(indexesToAdd));
        }

        public string[] PutSideBySideIndexes(IndexToAdd[] indexesToAdd, Etag minimumEtagBeforeReplace = null, DateTime? replaceTimeUtc = null)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.PutSideBySideIndexesAsync(indexesToAdd, minimumEtagBeforeReplace, replaceTimeUtc));
        }

        public bool IndexHasChanged(string name, IndexDefinition indexDef)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.IndexHasChangedAsync(name, indexDef));
        }

        public string PutTransformer(string name, TransformerDefinition transformerDef)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.PutTransformerAsync(name, transformerDef));
        }

        public string PutIndex(string name, IndexDefinition definition, bool overwrite)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.PutIndexAsync(name, definition, overwrite));
        }

        public string PutIndex<TDocument, TReduceResult>(string name,
            IndexDefinitionBuilder<TDocument, TReduceResult> indexDef)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.PutIndexAsync(name, indexDef, default(CancellationToken)));
        }

        public string PutIndex<TDocument, TReduceResult>(string name,
            IndexDefinitionBuilder<TDocument, TReduceResult> indexDef, bool overwrite)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.PutIndexAsync(name, indexDef, overwrite));
        }

        public string DirectPutIndex(string name, OperationMetadata operationMetadata, IRequestTimeMetric requestTimeMetric, bool overwrite,
            IndexDefinition definition)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.DirectPutIndexAsync(name, definition, overwrite, operationMetadata, requestTimeMetric));
        }

        public QueryResult Query(string index, IndexQuery query, string[] includes = null, bool metadataOnly = false,
            bool indexEntriesOnly = false)
        {
            try
            {
                return AsyncHelpers.RunSync(() => asyncServerClient.QueryAsync(index, query, includes, metadataOnly, indexEntriesOnly));
            }
            catch (Exception e)
            {
                if (e is ConflictException)
                    throw;

                throw new InvalidOperationException("Query failed. See inner exception for details.", e);
            }
        }

        public IEnumerator<RavenJObject> StreamQuery(string index, IndexQuery query,
            out QueryHeaderInformation queryHeaderInfo)
        {
            var reference = new Reference<QueryHeaderInformation>();
            var streamQuery = AsyncHelpers.RunSync(() => asyncServerClient.StreamQueryAsyncWithSyncEnumerator(index, query, reference));
            queryHeaderInfo = reference.Value;
            return streamQuery;
        }

        public IEnumerator<RavenJObject> StreamDocs(Etag fromEtag = null, string startsWith = null, string matches = null, int start = 0, int pageSize = int.MaxValue, string exclude = null, RavenPagingInformation pagingInformation = null, string skipAfter = null, string transformer = null, Dictionary<string, RavenJToken> transformerParameters = null)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.StreamDocsAsyncWithSyncEnumerator(fromEtag, startsWith, matches, start, pageSize, exclude, pagingInformation, skipAfter, transformer, transformerParameters));
        }

        public void DeleteIndex(string name)
        {
            AsyncHelpers.RunSync(() => asyncServerClient.DeleteIndexAsync(name));
        }

        public MultiLoadResult Get(string[] ids, string[] includes, string transformer = null,
            Dictionary<string, RavenJToken> transformerParameters = null, bool metadataOnly = false)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.GetAsync(ids, includes, transformer, transformerParameters, metadataOnly));
        }

        public BatchResult[] Batch(IEnumerable<ICommandData> commandDatas, BatchOptions options = null)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.BatchAsync(commandDatas, options));
        }

#if !DNXCORE50
        public void Commit(string txId)
        {
            AsyncHelpers.RunSync(() => asyncServerClient.CommitAsync(txId));
        }

        public void Rollback(string txId)
        {
            AsyncHelpers.RunSync(() => asyncServerClient.RollbackAsync(txId));
        }

        public void PrepareTransaction(string txId, Guid? resourceManagerId, byte[] recoveryInformation)
        {
            AsyncHelpers.RunSync(() => asyncServerClient.PrepareTransactionAsync(txId));
        }
#endif

        public BuildNumber GetBuildNumber()
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.GetBuildNumberAsync());
        }

        public IndexMergeResults GetIndexMergeSuggestions()
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.GetIndexMergeSuggestionsAsync());
        }

        public LogItem[] GetLogs(bool errorsOnly)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.GetLogsAsync(errorsOnly));
        }

        public LicensingStatus GetLicenseStatus()
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.GetLicenseStatusAsync());
        }

        public ILowLevelBulkInsertOperation GetBulkInsertOperation(BulkInsertOptions options, IDatabaseChanges changes)
        {
            return asyncServerClient.GetBulkInsertOperation(options, changes);
        }

        public HttpJsonRequest CreateReplicationAwareRequest(string currentServerUrl, string requestUrl, HttpMethod method, bool disableRequestCompression = false, bool disableAuthentication = false, TimeSpan? timeout = null)
        {
            return asyncServerClient.CreateReplicationAwareRequest(currentServerUrl, requestUrl, method, disableRequestCompression, disableAuthentication, timeout);
        }

        [Obsolete("Use RavenFS instead.")]
        public AttachmentInformation[] GetAttachments(int start, Etag startEtag, int pageSize)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.GetAttachmentsAsync(start, startEtag, pageSize));
        }

        public IDatabaseCommands With(ICredentials credentialsForSession)
        {
            return new ServerClient(asyncServerClient.WithInternal(credentialsForSession));
        }

        public IDisposable ForceReadFromMaster()
        {
            return asyncServerClient.ForceReadFromMaster();
        }

        public IDatabaseCommands ForDatabase(string database)
        {
            var newAsyncServerClient = asyncServerClient.ForDatabaseInternal(database);
            if (asyncServerClient == newAsyncServerClient)
                return this;

            return new ServerClient(newAsyncServerClient);
        }

        public IDatabaseCommands ForSystemDatabase()
        {
            return new ServerClient(asyncServerClient.ForSystemDatabaseInternal());
        }

        public string Url
        {
            get { return asyncServerClient.Url; }
        }

        public Operation DeleteByIndex(string indexName, IndexQuery queryToDelete, BulkOperationOptions options = null)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.DeleteByIndexAsync(indexName, queryToDelete, options));
        }

        public Operation UpdateByIndex(string indexName, IndexQuery queryToUpdate, PatchRequest[] patchRequests,
            BulkOperationOptions options = null)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.UpdateByIndexAsync(indexName, queryToUpdate, patchRequests, options));
        }

        public Operation UpdateByIndex(string indexName, IndexQuery queryToUpdate, ScriptedPatchRequest patch,
            BulkOperationOptions options = null)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.UpdateByIndexAsync(indexName, queryToUpdate, patch, options));
        }

        public SuggestionQueryResult Suggest(string index, SuggestionQuery suggestionQuery)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.SuggestAsync(index, suggestionQuery));
        }

        public MultiLoadResult MoreLikeThis(MoreLikeThisQuery query)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.MoreLikeThisAsync(query));
        }

        public DatabaseStatistics GetStatistics()
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.GetStatisticsAsync());
        }

        public UserInfo GetUserInfo()
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.GetUserInfoAsync());
        }

        public UserPermission GetUserPermission(string database, bool readOnly)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.GetUserPermissionAsync(database, readOnly));
        }

        public long NextIdentityFor(string name)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.NextIdentityForAsync(name));
        }

        public long SeedIdentityFor(string name, long value)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.SeedIdentityForAsync(name, value));
        }

        public void SeedIdentities(List<KeyValuePair<string, long>> identities)
        {
            AsyncHelpers.RunSync(() => asyncServerClient.SeedIdentitiesAsync(identities));
        }

        public string UrlFor(string documentKey)
        {
            return asyncServerClient.UrlFor(documentKey);
        }

        public JsonDocumentMetadata Head(string key)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.HeadAsync(key));
        }

        public GetResponse[] MultiGet(GetRequest[] requests)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.MultiGetAsync(requests));
        }

        public IEnumerable<string> GetTerms(string index, string field, string fromValue, int pageSize)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.GetTermsAsync(index, field, fromValue, pageSize));
        }

        public FacetResults GetFacets(string index, IndexQuery query, string facetSetupDoc, int start, int? pageSize)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.GetFacetsAsync(index, query, facetSetupDoc, start, pageSize));
        }

        public FacetResults[] GetMultiFacets(FacetQuery[] facetedQueries)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.GetMultiFacetsAsync(facetedQueries));
        }

        public FacetResults GetFacets(string index, IndexQuery query, List<Facet> facets, int start, int? pageSize)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.GetFacetsAsync(index, query, facets, start, pageSize));
        }

        public RavenJObject Patch(string key, PatchRequest[] patches)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.PatchAsync(key, patches, null));
        }

        public RavenJObject Patch(string key, PatchRequest[] patches, bool ignoreMissing)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.PatchAsync(key, patches, ignoreMissing));
        }

        public RavenJObject Patch(string key, ScriptedPatchRequest patch)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.PatchAsync(key, patch, null));
        }

        public RavenJObject Patch(string key, ScriptedPatchRequest patch, bool ignoreMissing)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.PatchAsync(key, patch, ignoreMissing));
        }

        public RavenJObject Patch(string key, PatchRequest[] patches, Etag etag)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.PatchAsync(key, patches, etag));
        }

        public RavenJObject Patch(string key, PatchRequest[] patchesToExisting, PatchRequest[] patchesToDefault,
            RavenJObject defaultMetadata)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.PatchAsync(key, patchesToExisting, patchesToDefault, defaultMetadata));
        }

        public RavenJObject Patch(string key, ScriptedPatchRequest patch, Etag etag)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.PatchAsync(key, patch, etag));
        }

        public RavenJObject Patch(string key, ScriptedPatchRequest patchExisting, ScriptedPatchRequest patchDefault,
            RavenJObject defaultMetadata)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.PatchAsync(key, patchExisting, patchDefault, defaultMetadata));
        }

        public HttpJsonRequest CreateRequest(string relativeUrl, HttpMethod method, bool disableRequestCompression = false, bool disableAuthentication = false, TimeSpan? timeout = null)
        {
            return asyncServerClient.CreateRequest(relativeUrl, method, disableRequestCompression, disableAuthentication, timeout);
        }

        public IDisposable DisableAllCaching()
        {
            return asyncServerClient.DisableAllCaching();
        }

        internal ReplicationDocumentWithClusterInformation DirectGetReplicationDestinations(OperationMetadata operationMetadata, IRequestTimeMetric requestTimeMetric)
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.DirectGetReplicationDestinationsAsync(operationMetadata, requestTimeMetric));
        }

        #endregion

        public ProfilingInformation ProfilingInformation
        {
            get { return asyncServerClient.ProfilingInformation; }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            asyncServerClient.Dispose();
        }

        ~ServerClient()
        {
            Dispose();
        }

        public ReplicationStatistics GetReplicationInfo()
        {
            return AsyncHelpers.RunSync(() => asyncServerClient.Info.GetReplicationInfoAsync());
        }

        public IAdminDatabaseCommands Admin
        {
            get { return new AdminServerClient(asyncServerClient, new AsyncAdminServerClient(asyncServerClient)); }
        }
    }
}
