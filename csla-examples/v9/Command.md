# Command Stereotype

This example demonstrates a complete CSLA business class named `CustomerExists` that includes various property types and data access methods. The class derives from `CommandBase<T>` and includes properties for input parameters and output results.

This class demonstrates the command business class stereotype.

It also includes a data portal operation method for executing the command. Note that the data access method contains a placeholder comment where actual data access logic should be invoked.

```csharp
using System;
using Csla;

public class CustomerExists : CommandBase<CustomerExists>
{
    public static readonly PropertyInfo<bool> ExistsProperty = RegisterProperty<bool>(nameof(Exists));
    public bool Exists
    {
        get => ReadProperty(ExistsProperty);
        private set => LoadProperty(ExistsProperty, value);
    }

    [Execute]
    private async Task Execute(string email, [Inject] ICustomerDal dal)
    {
        // Placeholder for actual data access logic
        var customer = await dal.GetByEmailAsync(email);
        Exists = customer != null;
    }
}
```

Note: The `ICustomerDal` interface is assumed to be defined elsewhere in your codebase and is responsible for data access operations related to customers. The `GetByEmailAsync` method is a placeholder for the actual implementation that retrieves a customer by their email address.

This class can be used to check if a customer with a specific email address exists in the data store by setting the `Email` property and then calling the data portal to execute the command. The result will be available in the `Exists` property.

To use this command, you would typically do something like the following:

```csharp
var command = await customerExistsPortal.ExecuteAsync("customer@example.com");
bool customerExists = command.Exists;
```
