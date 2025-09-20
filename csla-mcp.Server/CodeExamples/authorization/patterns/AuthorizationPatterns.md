# CSLA Authorization Patterns

CSLA provides comprehensive authorization capabilities that integrate seamlessly with .NET's security model. Authorization in CSLA operates at multiple levels:

## Object-Level Authorization

Control who can create, read, update, or delete entire business objects:

```csharp
// Only HR can create new customers
BusinessRules.AddRule(new IsInRole(AuthorizationActions.CreateObject, "HR"));

// Managers and HR can edit existing customers  
BusinessRules.AddRule(new IsInRole(AuthorizationActions.EditObject, "Manager", "HR"));
```

## Property-Level Authorization

Control access to individual properties:

```csharp
// Only managers can read/write salary information
BusinessRules.AddRule(new IsInRole(AuthorizationActions.ReadProperty, 
    SalaryProperty, "Manager", "HR"));
BusinessRules.AddRule(new IsInRole(AuthorizationActions.WriteProperty, 
    SalaryProperty, "Manager", "HR"));
```

## Custom Authorization Rules

Create custom authorization logic:

```csharp
public class CanEditOwnRecord : AuthorizationRule
{
    public CanEditOwnRecord(AuthorizationActions action) : base(action)
    {
    }

    protected override void Execute(IAuthorizationContext context)
    {
        var user = ApplicationContext.User;
        var customer = (Customer)context.Target;
        
        if (customer.UserId != user.Identity.Name)
        {
            context.HasPermission = false;
            context.BrokenRules.Add(new BrokenRule("You can only edit your own record"));
        }
    }
}
```

## Per-Instance Authorization

Apply different rules based on object state:

```csharp
protected override void AddBusinessRules()
{
    // Base authorization
    BusinessRules.AddRule(new IsInRole(AuthorizationActions.EditObject, "User"));
    
    // Additional rule: users can only edit records they created
    BusinessRules.AddRule(new CanEditOwnRecord(AuthorizationActions.EditObject));
}