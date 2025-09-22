# Data Portal Operation Delete and DeleteSelf

The CSLA data portal supports two delete operations: `Delete` and `DeleteSelf`. The `Delete` operation is used to delete an object based on a provided identifier, while the `DeleteSelf` operation is used to delete the current instance of the business object.

Here is an example of how to implement both operations in a CSLA .NET business class:

```csharp
[Delete]
private async Task Delete(int id, [Inject] ICustomerDal customerDal)
{
    // Call DAL Delete method to delete data by id
    await customerDal.Delete(id);
}

[DeleteSelf]
private async Task DeleteSelf([Inject] ICustomerDal customerDal)
{
    // Call DAL Delete method to delete data by the current object's id
    await customerDal.Delete(ReadProperty(IdProperty));

    // Mark the object as new after deletion
    // because the object no longer reflects data in the database
    MarkNew();
}
```

In this example, the `Delete` method takes an identifier as a parameter and calls the `Delete` method of the injected data access layer (DAL) to perform the deletion. The `DeleteSelf` method uses the current object's identifier, retrieved using the `ReadProperty` method, to delete the object from the DAL. After deletion, it calls `MarkNew()` to reset the object's state, indicating that it no longer represents data in the database.
