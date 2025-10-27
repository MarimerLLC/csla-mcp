# Data Portal Operation: Execute

The `[Execute]` operation performs command objects that don't fit standard CRUD operations. Commands encapsulate business logic, calculations, or operations that don't map to a single entity.

## When to Use

- Operations that don't map to standard CRUD (Create, Read, Update, Delete)
- Batch operations or calculations
- Business workflows or processes
- Operations that affect multiple entities
- Stateless operations with inputs and outputs

## Command Basics

Commands inherit from `CommandBase<T>` and use CSLA 10's property generation:

```csharp
using Csla;

namespace MyApp.Business
{
    [CslaImplementProperties]
    public partial class CalculateTax : CommandBase<CalculateTax>
    {
        // Input properties
        public partial decimal Amount { get; private set; }
        public partial string Region { get; private set; }
        
        // Output properties
        public partial decimal TaxAmount { get; private set; }
        public partial decimal Total { get; private set; }
        
        [Execute]
        private async Task Execute(decimal amount, string region, [Inject] ITaxService taxService)
        {
            // Perform calculation
            var taxRate = await taxService.GetTaxRateAsync(region);
            var tax = amount * taxRate;
            
            using (BypassPropertyChecks)
            {
                Amount = amount;         // Store input
                Region = region;
                TaxAmount = tax;         // Store output
                Total = amount + tax;
            }
        }
    }
}

// Client usage
var command = await taxPortal.ExecuteAsync(amount: 100.00m, region: "CA");
Console.WriteLine($"Tax: {command.TaxAmount}, Total: {command.Total}");
```

## Execute Patterns

### Pattern 1: Execute and Evaluate (Recommended)

Simpler - pass parameters directly to Execute:

```csharp
[CslaImplementProperties]
public partial class SendEmail : CommandBase<SendEmail>
{
    public partial bool Success { get; private set; }
    public partial string Message { get; private set; }
    
    [Execute]
    private async Task Execute(string to, string subject, string body, [Inject] IEmailService emailService)
    {
        try
        {
            await emailService.SendAsync(to, subject, body);
            
            LoadProperty(SuccessProperty, true);
            LoadProperty(MessageProperty, "Email sent successfully");
        }
        catch (Exception ex)
        {
            LoadProperty(SuccessProperty, false);
            LoadProperty(MessageProperty, $"Failed: {ex.Message}");
        }
    }
}

// Client usage
var command = await emailPortal.ExecuteAsync(
    to: "user@example.com",
    subject: "Hello",
    body: "Message content");

if (command.Success)
    Console.WriteLine("Email sent!");
```

### Pattern 2: Create, Load, Execute, Evaluate

Older pattern - set properties before execution:

```csharp
[CslaImplementProperties]
public partial class ProcessOrder : CommandBase<ProcessOrder>
{
    // Input properties (public setters)
    public partial int OrderId { get; set; }
    public partial string ProcessingNotes { get; set; }
    
    // Output properties (private setters)
    public partial bool Success { get; private set; }
    public partial DateTime ProcessedDate { get; private set; }
    
    [Execute]
    private async Task Execute([Inject] IOrderService orderService)
    {
        // Read input properties
        var orderId = ReadProperty(OrderIdProperty);
        var notes = ReadProperty(ProcessingNotesProperty);
        
        // Process order
        var result = await orderService.ProcessAsync(orderId, notes);
        
        // Set output properties
        LoadProperty(SuccessProperty, result.Success);
        LoadProperty(ProcessedDateProperty, result.ProcessedDate);
    }
}

// Client usage
var command = await orderCommandPortal.CreateAsync();
command.OrderId = 123;
command.ProcessingNotes = "Rush order";
await command.ExecuteAsync();

if (command.Success)
    Console.WriteLine($"Processed on {command.ProcessedDate}");
```

## Important Notes

1. **Commands are stateless** - each execution is independent
2. **Use `LoadProperty` or `BypassPropertyChecks`** to set output values
3. **Input via Execute parameters (Pattern 1) is preferred** over input properties (Pattern 2)
4. **Output properties should have private setters**
5. **No business rules** - `CommandBase` doesn't support validation
6. **Use `[Transactional]` attribute** if command needs transaction
7. **Commands don't have `IsNew`, `IsDirty`, or change tracking**

## Common Command Examples

For additional command patterns including batch operations, validations, multi-entity operations, and report generation, see the Command.md documentation file.
