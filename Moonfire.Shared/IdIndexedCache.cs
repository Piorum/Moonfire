using System.Collections.Concurrent;

namespace Moonfire.Shared;

public abstract class IdIndexedCache<TCreationData, TDataObject, TReturnInfo> : IdIndexedCache<TCreationData, object, TDataObject, TReturnInfo>
    where TDataObject : class
    where TCreationData : notnull
{
    protected abstract TDataObject CreateObject(TCreationData creationData);
    protected override TDataObject CreateObject(TCreationData creationData, object helperData) =>
        CreateObject(creationData);

    protected abstract TDataObject Update(TDataObject dataObject, TCreationData creationData);
    protected override TDataObject Update(TDataObject dataObject, TCreationData creationData, object helperData) =>
        Update(dataObject, creationData);

    public void AddOrUpdate(TCreationData creationData) =>
        AddOrUpdate(creationData);
    public TReturnInfo GetOrAdd(TCreationData creationData) =>
        GetOrAdd(creationData, new());
}

public abstract class IdIndexedCache<TCreationData, THelperData, TDataObject, TReturnInfo>
    where TDataObject : class
    where TCreationData : notnull
{
    private static readonly Lock writeLock = new();
    private static readonly List<TDataObject> data = [];
    private static readonly ConcurrentDictionary<TCreationData, int> index = [];

    protected abstract TDataObject CreateObject(TCreationData creationData, THelperData helperData);
    protected abstract TReturnInfo CreateInfo(int id, TDataObject dataOjbect);
    protected abstract TDataObject Update(TDataObject dataObject, TCreationData creationData, THelperData helperData);

    public void AddOrUpdate(TCreationData creationData, THelperData helperData)
    {
        index.AddOrUpdate(
            creationData,
            key => 
            {
                var (id, obj) = Add(creationData, helperData);
                return id;
            },
            (key, existingId) =>
            {
                lock(writeLock)
                {
                    data[existingId] = Update(data[existingId], creationData, helperData);
                }
                return existingId;
            });
    }

    public TReturnInfo GetOrAdd(TCreationData creationData, THelperData helperData)
    {
        var id = index.GetOrAdd(
            creationData,
            key => 
            {
                var (newId, _) = Add(key, helperData);
                return newId;
            });
        
        return CreateInfo(id, data[id]);
    }

    private (int id, TDataObject dataObject) Add(TCreationData creationData, THelperData helperData)
    {
        var newData = CreateObject(creationData, helperData);

        lock(writeLock)
        {
            var newId = data.Count;
            data.Add(newData);

            return (newId, newData);
        }
    }

    public TDataObject? TryGet(int index) =>
        index >= 0 && index < data.Count
            ? data[index]
            : default;

    public TDataObject Get(int index) =>
        data[index];
}
