# Command Stereotype

The Command stereotype is used for operations that don't fit the standard CRUD pattern. Commands encapsulate server-side operations like calculations, validations, or actions that don't involve managing object state.

**Common use cases**:

* Check if a record exists
* Perform calculations
* Execute business operations (e.g., ship order, archive invoice)
* Validate data without creating/fetching a full object

## Key Characteristics

* Derives from `CommandBase<T>`
* Uses `[Execute]` data portal operation
* Properties hold input parameters and output results
* Does not support business rules or n-level undo
* Lightweight - designed for simple operations

## Implementation Example

This example demonstrates a command that checks if a customer exists by email address.

```csharp
using System;
using System.Threading.Tasks;
using Csla;

namespace MyApp.Business
{
    [CslaImplementProperties]
    public partial class CustomerExists : CommandBase<CustomerExists>
    {
        // Output result
        public partial bool Exists { get; private set; }

        [Execute]
        private async Task Execute(string email, [Inject] ICustomerDal dal)
        {
            var customer = await dal.GetByEmailAsync(email);
            LoadProperty(ExistsProperty, customer != null);
        }
    }
}
```

> **Note:** Commands use `LoadProperty` to set property values since they don't have property setters in the traditional sense. The `CommandBase` class does not support business rules.

## Execution Pattern

### Execute and Evaluate (Recommended)

Pass input parameters directly to `ExecuteAsync`:

```csharp
// Inject IDataPortal<CustomerExists> via dependency injection
var command = await customerExistsPortal.ExecuteAsync("customer@example.com");
bool customerExists = command.Exists;
```

This is the modern, recommended approach where the command is created and executed in one call.

### Create, Load, Execute, Evaluate (Alternative)

For scenarios where you need to set multiple input properties before execution:

```csharp
[CslaImplementProperties]
public partial class ProcessOrder : CommandBase<ProcessOrder>
{
    // Input properties
    public partial int OrderId { get; set; }
    public partial DateTime ShipDate { get; set; }
    
    // Output property
    public partial string TrackingNumber { get; private set; }

    [Execute]
    private async Task Execute([Inject] IOrderDal dal)
    {
        var tracking = await dal.ShipOrderAsync(
            ReadProperty(OrderIdProperty), 
            ReadProperty(ShipDateProperty));
        LoadProperty(TrackingNumberProperty, tracking);
    }
}
```

Usage:

```csharp
var command = await processOrderPortal.CreateAsync();
command.OrderId = 12345;
command.ShipDate = DateTime.Today;
command = await processOrderPortal.ExecuteAsync(command);
string tracking = command.TrackingNumber;
```

## Property Access in Commands

* Use `LoadProperty()` to set output values in the `Execute` method
* Use `ReadProperty()` to read input values in the `Execute` method
* Properties can be read-write (inputs) or read-only with private setter (outputs)

## Dependency Injection

Use `[Inject]` attribute to inject DAL interfaces or services into the `Execute` method. Registration happens in the application startup configuration.
