﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Raven.Abstractions.Counters.Notifications;
using Raven.Abstractions.Extensions;
using Raven.Client.Changes;
using Raven.Client.Connection;
using Raven.Database.Util;
using Raven.Json.Linq;

namespace Raven.Client.Counters.Changes
{

    public class CountersChangesClient : RemoteChangesClientBase<ICountersChanges, CountersConnectionState>, ICountersChanges
    {
		private readonly ConcurrentSet<string> watchedChanges = new ConcurrentSet<string>();
		private readonly ConcurrentSet<string> watchedPrefixes = new ConcurrentSet<string>();
		private readonly ConcurrentSet<string> watchedCountersInGroup = new ConcurrentSet<string>();
		private readonly ConcurrentSet<string> watchedBulkOperations = new ConcurrentSet<string>();
		private bool watchAllCounters;

		public CountersChangesClient(string url, string apiKey,
                                       ICredentials credentials,
                                       HttpJsonRequestFactory jsonRequestFactory, Convention conventions,
                                       Action onDispose)
            : base(url, apiKey, credentials, jsonRequestFactory, conventions.ShouldCacheRequest, onDispose)
        {

        }

        protected override async Task SubscribeOnServer()
		{
			if (watchAllCounters)
				await Send("watch-counters", null).ConfigureAwait(false);

			foreach (var matchingChange in watchedChanges)
            {
				await Send("watch-counter-change", matchingChange).ConfigureAwait(false);
            }

			foreach (var matchingPrefix in watchedPrefixes)
			{
				await Send("watch-counters-prefix", matchingPrefix).ConfigureAwait(false);
			}

			foreach (var matchingCountersInGroup in watchedCountersInGroup)
			{
				await Send("watch-counters-in-group", matchingCountersInGroup).ConfigureAwait(false);
			}

			foreach (var matchingBulkOperation in watchedBulkOperations)
			{
				await Send("watch-bulk-operation", matchingBulkOperation).ConfigureAwait(false);
			}
        }

		protected override void NotifySubscribers(string type, RavenJObject value, IEnumerable<KeyValuePair<string, CountersConnectionState>> connections)
        {
            switch (type)
            {
				case "ChangeNotification":
					var changeNotification = value.JsonDeserialization<ChangeNotification>();
                    foreach (var counter in connections)
                    {
                        counter.Value.Send(changeNotification);
                    }
                    break;               
				case "StartingWithNotification":
					var counterStartingWithNotification = value.JsonDeserialization<StartingWithNotification>();
					foreach (var counter in connections)
					{
						counter.Value.Send(counterStartingWithNotification);
					}
					break;
				case "InGroupNotification":
					var countersInGroupNotification = value.JsonDeserialization<InGroupNotification>();
                    foreach (var counter in connections)
                    {
						counter.Value.Send(countersInGroupNotification);
                    }
                    break;
				case "BulkOperationNotification":
					var bulkOperationNotification = value.JsonDeserialization<BulkOperationNotification>();
                    foreach (var counter in connections)
                    {
                        counter.Value.Send(bulkOperationNotification);
                    }
                    break;
                default:
                    break;
            }
        }

		public IObservableWithTask<ChangeNotification> ForAllCounters()
		{
			var counter = GetOrAddConnectionState("all-counters", "watch-counter-change", "unwatch-counter-change",
				() => watchAllCounters = true,
				() => watchAllCounters = false,
				null);
			/*var counter = Counters.GetOrAdd("all-counters", s =>
			{
				var documentSubscriptionTask = AfterConnection(() =>
				{
					watchAllCounters = true;
					return Send("watch-docs", null);
				});
				return new CountersConnectionState(
					() =>
					{
						watchAllCounters = false;
						Send("unwatch-docs", null);
						Counters.Remove("all-docs");
					},
					documentSubscriptionTask);
			});*/
			var taskedObservable = new TaskedObservable<ChangeNotification, CountersConnectionState>(
				counter,
				notification => true);

			counter.OnChangeNotification += taskedObservable.Send;
			counter.OnError += taskedObservable.Error;

			return taskedObservable;
		}

	    public IObservableWithTask<ChangeNotification> ForChange(string groupName, string counterName)
	    {
			if (string.IsNullOrWhiteSpace(groupName))
				throw new ArgumentException("Group name cannot be empty!");

			if (string.IsNullOrWhiteSpace(counterName))
				throw new ArgumentException("Counter name cannot be empty");

			var fullCounterName = FullCounterName(groupName, counterName);
			var key = string.Concat("counter-change/", fullCounterName);
			var counter = GetOrAddConnectionState(key, "watch-counter-change", "unwatch-counter-change",
				() => watchedChanges.TryAdd(fullCounterName),
				() => watchedChanges.TryRemove(fullCounterName),
				fullCounterName);
			/*var counter = Counters.GetOrAdd(key, s =>
			{
				var changeSubscriptionTask = AfterConnection(() =>
				{
					watchedChanges.TryAdd(fullCounterName);
					return Send("watch-counter-change", fullCounterName);
				});

				return new CountersConnectionState(
					() =>
					{
						watchedChanges.TryRemove(fullCounterName);
						Send("unwatch-counter-change", fullCounterName);
						Counters.Remove(key);
					},
					changeSubscriptionTask);
			});*/

			
			var taskedObservable = new TaskedObservable<ChangeNotification, CountersConnectionState>(
								counter,
								notification => string.Equals(notification.GroupName, groupName, StringComparison.InvariantCulture) &&
												string.Equals(notification.CounterName, counterName, StringComparison.InvariantCulture));
			counter.OnChangeNotification += taskedObservable.Send;
			counter.OnError += taskedObservable.Error;

			return taskedObservable;
	    }

	    private static string FullCounterName(string groupName, string counterName)
	    {
			return string.Concat(groupName, "/", counterName);
	    }

		public IObservableWithTask<StartingWithNotification> ForCountersStartingWith(string groupName, string prefixForName)
	    {
			if (string.IsNullOrWhiteSpace(groupName))
				throw new ArgumentException("Group name cannot be empty!");

			if (string.IsNullOrWhiteSpace(prefixForName))
				throw new ArgumentException("Prefix for counter name cannot be empty");

			var counterPrefix = FullCounterName(groupName, prefixForName);
			var key = string.Concat("counters-starting-with/", counterPrefix);
			var counter = GetOrAddConnectionState(key, "watch-counters-prefix", "unwatch-counters-prefix",
				() => watchedPrefixes.TryAdd(counterPrefix),
				() => watchedPrefixes.TryRemove(counterPrefix),
				counterPrefix);
			/*var counter = Counters.GetOrAdd(key, s =>
			{
				var changeSubscriptionTask = AfterConnection(() =>
				{
					watchedPrefixes.TryAdd(counterPrefix);
					return Send("watch-counters-prefix", counterPrefix);
				});

				return new CountersConnectionState(
					() =>
					{
						watchedPrefixes.TryRemove(counterPrefix);
						Send("unwatch-counters-prefix", counterPrefix);
						Counters.Remove(key);
					},
					changeSubscriptionTask);
			});*/

			var taskedObservable = new TaskedObservable<StartingWithNotification, CountersConnectionState>(
				counter,
				notification =>
				{
					var t = string.Equals(notification.GroupName, groupName, StringComparison.InvariantCulture) &&
							notification.CounterName.StartsWith(prefixForName, StringComparison.InvariantCulture);
					return t;
				});
			counter.OnCountersStartingWithNotification += taskedObservable.Send;
			counter.OnError += taskedObservable.Error;

			return taskedObservable;
	    }

		public IObservableWithTask<InGroupNotification> ForCountersInGroup(string groupName)
	    {
			if (string.IsNullOrWhiteSpace(groupName))
				throw new ArgumentException("Group name cannot be empty!");

			var key = string.Concat("counters-in-group/", groupName);
			var counter = GetOrAddConnectionState(key, "watch-counters-in-group", "unwatch-counters-in-group", 
				() => watchedCountersInGroup.TryAdd(groupName), 
				() => watchedCountersInGroup.TryRemove(groupName), 
				groupName);

			/*var counter = Counters.GetOrAdd(key, s =>
			{
				var changeSubscriptionTask = AfterConnection(() =>
				{
					watchedCountersInGroup.TryAdd(groupName);
					return Send("watch-counters-in-group", groupName);
				});

				return new CountersConnectionState(
					() =>
					{
						watchedCountersInGroup.TryRemove(groupName);
						Send("unwatch-counters-in-group", groupName);
						Counters.Remove(key);
					},
					changeSubscriptionTask);
			});*/

			var taskedObservable = new TaskedObservable<InGroupNotification, CountersConnectionState>(
								counter,
								notification => string.Equals(notification.GroupName, groupName, StringComparison.InvariantCulture));
			counter.OnCountersInGroupNotification += taskedObservable.Send;
			counter.OnError += taskedObservable.Error;

			return taskedObservable;
	    }

	    public IObservableWithTask<BulkOperationNotification> ForBulkOperation(Guid? operationId = null)
	    {
			var id = operationId != null ? operationId.ToString() : string.Empty;

		    var key = "bulk-operations/" + id;
		   var counter = Counters.GetOrAdd(key, s =>
			{
				watchedBulkOperations.TryAdd(id);
				var bulkOperationSubscriptionTask = AfterConnection(() =>
				{
					if (watchedBulkOperations.Contains(id)) // might have been removed in the meantime
						return Send("watch-bulk-operation", id);
					return Task;
				});

				return new CountersConnectionState(
					() =>
					{
						watchedBulkOperations.TryRemove(id);
						Send("unwatch-bulk-operation", id);
						Counters.Remove(key);
					},
					existingConnectionState =>
					{
						CountersConnectionState _;
						if (Counters.TryGetValue("bulk-operations/" + id, out _))
							return _.Task;

						Counters.GetOrAdd("bulk-operations/" + id, x => (CountersConnectionState)existingConnectionState);

						return AfterConnection(() =>
						{
							if (watchedBulkOperations.Contains(id)) // might have been removed in the meantime
								return Send("watch-bulk-operation", id);
							return Task;
						});
					},
					bulkOperationSubscriptionTask);
			});

			var taskedObservable = new TaskedObservable<BulkOperationNotification, CountersConnectionState>(
				counter,
				notification => operationId == null || notification.OperationId == operationId);

			counter.OnBulkOperationNotification += taskedObservable.Send;
			counter.OnError += taskedObservable.Error;

			return taskedObservable;
	    }

		private CountersConnectionState GetOrAddConnectionState(string name, string watchCommand, string unwatchCommand, Action afterConnection, Action beforeDisconnect, string value)
		{
			var counter = Counters.GetOrAdd(name, s =>
			{
				var counterSubscriptionTask = AfterConnection(() =>
				{
					afterConnection();
					return Send(watchCommand, value);
				});

				return new CountersConnectionState(
					() =>
					{
						beforeDisconnect();
						Send(unwatchCommand, value);
						Counters.Remove(name);
					},
					existingConnectionState =>
					{
						CountersConnectionState _;
						if (Counters.TryGetValue(name, out _))
							return _.Task;

						Counters.GetOrAdd(name, x => existingConnectionState);

						return AfterConnection(() =>
						{
							afterConnection();
							return Send(watchCommand, value);
						});
					},
					counterSubscriptionTask);
			});

			return counter;
		}
    }
}
