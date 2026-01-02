using System.Net;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;

namespace Rbac.Shared.CosmosDb;

/// <summary>
/// Generic repository for CosmosDB operations.
/// </summary>
/// <typeparam name="T">The entity type</typeparam>
public class CosmosDbRepository<T> where T : class
{
    private readonly Container _container;

    public CosmosDbRepository(Container container)
    {
        _container = container;
    }

    public CosmosDbRepository(ICosmosDbClient client, string containerName)
    {
        _container = client.GetContainer(containerName);
    }

    /// <summary>
    /// Gets an item by ID and partition key.
    /// </summary>
    public async Task<T?> GetByIdAsync(string id, string partitionKey)
    {
        try
        {
            var response = await _container.ReadItemAsync<T>(
                id,
                new PartitionKey(partitionKey));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <summary>
    /// Creates a new item.
    /// </summary>
    public async Task<T> CreateAsync(T item, string partitionKey)
    {
        var response = await _container.CreateItemAsync(
            item,
            new PartitionKey(partitionKey));
        return response.Resource;
    }

    /// <summary>
    /// Updates an existing item with optimistic concurrency.
    /// </summary>
    public async Task<T> UpdateAsync(T item, string id, string partitionKey, string? etag = null)
    {
        var options = new ItemRequestOptions();
        if (!string.IsNullOrEmpty(etag))
        {
            options.IfMatchEtag = etag;
        }

        var response = await _container.ReplaceItemAsync(
            item,
            id,
            new PartitionKey(partitionKey),
            options);
        return response.Resource;
    }

    /// <summary>
    /// Upserts an item.
    /// </summary>
    public async Task<T> UpsertAsync(T item, string partitionKey)
    {
        var response = await _container.UpsertItemAsync(
            item,
            new PartitionKey(partitionKey));
        return response.Resource;
    }

    /// <summary>
    /// Deletes an item.
    /// </summary>
    public async Task DeleteAsync(string id, string partitionKey)
    {
        await _container.DeleteItemAsync<T>(
            id,
            new PartitionKey(partitionKey));
    }

    /// <summary>
    /// Queries items with a SQL query.
    /// </summary>
    public async Task<List<T>> QueryAsync(string query, Dictionary<string, object>? parameters = null)
    {
        var queryDefinition = new QueryDefinition(query);

        if (parameters != null)
        {
            foreach (var param in parameters)
            {
                queryDefinition = queryDefinition.WithParameter($"@{param.Key}", param.Value);
            }
        }

        var results = new List<T>();
        using var iterator = _container.GetItemQueryIterator<T>(queryDefinition);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }

        return results;
    }

    /// <summary>
    /// Queries items within a specific partition.
    /// </summary>
    public async Task<List<T>> QueryInPartitionAsync(
        string query,
        string partitionKey,
        Dictionary<string, object>? parameters = null)
    {
        var queryDefinition = new QueryDefinition(query);

        if (parameters != null)
        {
            foreach (var param in parameters)
            {
                queryDefinition = queryDefinition.WithParameter($"@{param.Key}", param.Value);
            }
        }

        var options = new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(partitionKey)
        };

        var results = new List<T>();
        using var iterator = _container.GetItemQueryIterator<T>(queryDefinition, requestOptions: options);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }

        return results;
    }

    /// <summary>
    /// Gets all items in a partition.
    /// </summary>
    public async Task<List<T>> GetAllInPartitionAsync(string partitionKey)
    {
        return await QueryInPartitionAsync("SELECT * FROM c", partitionKey);
    }
}
