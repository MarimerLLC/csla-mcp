# Data Portal Operation Execute

The `Execute` data portal operation is used in CSLA .NET to perform operations that do not fit into the standard CRUD (Create, Read, Update, Delete) operations. This is typically used for command objects that encapsulate a specific action or operation.

There are two forms of the `Execute` operation:

1. Execute and Evaluate: This form is used when you need to execute a command object and then evaluate the results immediately after execution. It is the simpler and more modern of the two forms.
2. Create, Load, Execute, Evaluate: This form is used when you need to create a new instance of a command object, load it with data, execute the operation, and then evaluate the results.

In either case, you need a command class that derives from `CommandBase<T>` that represents the command you want to execute.

```csharp
public class MyCommand : CommandBase<MyCommand>
{
    // Define properties for input and output as needed
    public static readonly PropertyInfo<int> InputProperty = RegisterProperty<int>(c => c.Input);
    public int Input
    {
        get => ReadProperty(InputProperty);
        set => LoadProperty(InputProperty, value);
    }

    public static readonly PropertyInfo<string> ResultProperty = RegisterProperty<string>(c => c.Result);
    public string Result
    {
        get => ReadProperty(ResultProperty);
        private set => LoadProperty(ResultProperty, value);
    }

    [Create]
    private void Create()
    { }

    [Execute]
    private async Task Execute(int input, [Inject] IMyService myService)
    {
        //NOTE: the Input property is not necessary in this scenario
        // Perform the command operation
        Result = await myService.ProcessInputAsync(input);
    }

    [Execute]
    private async Task Execute([Inject] IMyService myService)
    {
        // NOTE: the Input property is necessary in this scenario
        // Perform the command operation
        Result = await myService.ProcessInputAsync(Input);
    }
}
```

In this example, `MyCommand` is a command class that has an input property and a result property. The `Execute` method is decorated with the `[Execute]` attribute, indicating that it is the method to be called when the command is executed. 

The method uses an injected service (`IMyService`) to perform the operation and sets the result property based on the operation's outcome. This might be a data access layer (DAL) or any other service that performs the required action.

The `ReadProperty` and `LoadProperty` methods are used to get and set property values internally within the class, bypassing any business rules or validation. The `CommandBase` class does not support business rules, so these methods must be used to manage property values.

You can use the Execute and Evaluate form like this:

```csharp
var command = await myCommandPortal.ExecuteAsync("Initial Input");
var result = command.Result;
```

In this case, the command object is created on the logical server, executed, and the result is evaluated after execution. This is the simpler and more modern approach.

Or the Create, Load, Execute, Evaluate form like this:

```csharp
var command = await myCommandPortal.CreateAsync<MyCommand>();
command.Input = "Initial Input";
await DataPortal.ExecuteAsync(command);
var result = command.Result;
```

In this case, the command object is created, provided to the client, loaded with values, executed, and the result is evaluated after execution.

These examples assume you have a pre-existing `myCommandPortal` instance of `IDataPortal<MyCommand>` via dependency injection or other means.
