﻿// -----------------------------------------------------------------------
//  <copyright file="DatabaseSmugglerRemoteDestination.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Raven.Abstractions.Data;
using Raven.Abstractions.Extensions;
using Raven.Abstractions.Smuggler;
using Raven.Abstractions.Smuggler.Data;
using Raven.Abstractions.Util;
using Raven.Client;
using Raven.Json.Linq;

namespace Raven.Smuggler.Database.Impl.Remote
{
	public class DatabaseSmugglerRemoteDestination : IDatabaseSmugglerDestination
	{
		private readonly IDocumentStore _store;

		private readonly DatabaseSmugglerRemoteDestinationOptions _options;

		private Report _report;

		public DatabaseSmugglerRemoteDestination(IDocumentStore store, DatabaseSmugglerRemoteDestinationOptions options = null)
		{
			_store = store;
			_options = options ?? new DatabaseSmugglerRemoteDestinationOptions();
		}

		public void Dispose()
		{
		}

		public bool SupportsOperationState => true;

		public bool SupportsWaitingForIndexing => true;

		public Task InitializeAsync(DatabaseSmugglerOptions options, Report report, CancellationToken cancellationToken)
		{
			_report = report;
			return new CompletedTask();
		}

		public IDatabaseSmugglerIndexActions IndexActions()
		{
			return new DatabaseSmugglerRemoteIndexActions(_store);
		}

		public IDatabaseSmugglerDocumentActions DocumentActions()
		{
			return new DatabaseSmugglerRemoteDocumentActions(_store);
		}

		public IDatabaseSmugglerTransformerActions TransformerActions()
		{
			return new DatabaseSmugglerRemoteTransformerActions(_store);
		}

		public IDatabaseSmugglerDocumentDeletionActions DocumentDeletionActions()
		{
			return new DatabaseSmugglerRemoteDocumentDeletionActions(_store);
		}

		public IDatabaseSmugglerIdentityActions IdentityActions()
		{
			return new DatabaseSmugglerRemoteIdentityActions(_store);
		}

		public async Task<OperationState> LoadOperationStateAsync(DatabaseSmugglerOptions options, CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(_options.ContinuationToken) == false)
			{
				var continuationDocId = "Raven/Smuggler/Continuation/" + _options.ContinuationToken;

				try
				{
					var continuationDocument = await _store
						.AsyncDatabaseCommands
						.GetAsync(continuationDocId)
						.ConfigureAwait(false);

					if (continuationDocument != null)
						return continuationDocument.DataAsJson.JsonDeserialization<OperationState>();
				}
				catch (Exception e)
				{
					if (options.IgnoreErrorsAndContinue == false)
						throw;

					_report.ShowProgress("Failed loading continuation state. Message: {0}", e.Message);
				}
			}

			return null;
		}

		public async Task SaveOperationStateAsync(DatabaseSmugglerOptions options, OperationState state, CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(_options.ContinuationToken) == false)
			{
				var continuationDocId = "Raven/Smuggler/Continuation/" + _options.ContinuationToken;

				try
				{
					await _store
						.AsyncDatabaseCommands
						.PutAsync(continuationDocId, null, RavenJObject.FromObject(state), null, cancellationToken)
						.ConfigureAwait(false);
				}
				catch (Exception e)
				{
					if (options.IgnoreErrorsAndContinue == false)
						throw;

					_report.ShowProgress("Failed saving continuation state. Message: {0}", e.Message);
				}
			}
		}

		public async Task WaitForIndexingAsOfLastWriteAsync(CancellationToken cancellationToken)
		{
			if (_options.WaitForIndexing == false)
				return;

			var stopwatch = Stopwatch.StartNew();
			var justIndexingWait = Stopwatch.StartNew();

			var stats = await _store
				.AsyncDatabaseCommands
				.GetStatisticsAsync(cancellationToken)
				.ConfigureAwait(false);

			var tries = 0;
			var cutOffEtag = stats.LastDocEtag;
			while (true)
			{
				if (stats.Indexes.All(x => x.LastIndexedEtag.CompareTo(cutOffEtag) >= 0))
				{
					_report.ShowProgress("\rWaited {0} for indexing ({1} total).", justIndexingWait.Elapsed, stopwatch.Elapsed);
					break;
				}

				if (tries++ % 10 == 0)
					_report.ShowProgress("\rWaiting {0} for indexing ({1} total).", justIndexingWait.Elapsed, stopwatch.Elapsed);

				Thread.Sleep(1000);
				stats = await _store
					.AsyncDatabaseCommands
					.GetStatisticsAsync(cancellationToken)
					.ConfigureAwait(false);
			}

			stopwatch.Stop();
			justIndexingWait.Stop();
		}
	}
}