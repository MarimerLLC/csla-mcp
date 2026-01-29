# CSLA .NET Glossary of Terms

This glossary defines terms and concepts commonly used in CSLA .NET development. It is optimized for AI assistants helping developers write CSLA code.

**Version Focus**: This document primarily covers CSLA .NET version 10, which uses code generation via `[CslaImplementProperties]`. Version 9 examples exist in `v9/` subdirectory for reference.

**Related Documents**:
- `ObjectStereotypes.md` - Overview of all stereotypes and when to use each base class
- `DataPortalGuide.md` - In-depth guide to data portal architecture, proxies, channels, and root vs child operations
- `Data-Access.md` - Concrete DAL implementation examples using ADO.NET and Entity Framework Core
- `v10/Properties.md` - Property declaration patterns for CSLA 10
- `DataPortalOperation*.md` - Detailed examples for each data portal operation (Create, Fetch, Insert, Update, Delete, Execute)
- `v10/EditableRoot.md`, `v10/EditableChild.md`, etc. - Complete stereotype implementations with full code

## Architecture

CSLA is designed around a layered architecture with three primary tiers. For a comprehensive guide to solution structure and project organization, see `SolutionArchitecture.md`.

### Presentation Tier

The presentation tier handles all user or system interaction:

| Layer | Description | Examples |
| --- | --- | --- |
| Interface | The visual or API surface users interact with | HTML/Razor, JSON API, XAML, WinForms, MAUI |
| Interface Control | Code that manages the interface behavior | ViewModels, Controllers, Presenters, Code-Behind, Page Models |

### Business Tier

The business tier contains all domain logic:

| Layer | Description | Implementation |
| --- | --- | --- |
| Business Logic | Domain objects with all validation, calculation, manipulation, and authorization logic | CSLA base classes and stereotypes with the business rules system |

### Data Access Tier

The data access tier handles all data persistence:

| Layer | Description | Examples |
| --- | --- | --- |
| Data Access Abstraction | Interfaces and DTOs that define data contracts | `IPersonDal`, `PersonData` |
| Data Access Concrete | Implementation of abstraction using a specific technology | Entity Framework, ADO.NET, Dapper |
| Data Storage | Physical storage for data | SQL databases, files, external APIs |

## CSLA Attributes

CSLA uses attributes to mark classes, methods, and properties for specific behaviors.

### Class-level Attributes

| Attribute | Purpose | Usage |
| --- | --- | --- |
| `[CslaImplementProperties]` | Enables automatic code generation for partial properties | Place on a partial class containing partial property declarations - requires a reference to the `Csla.Generator.AutoImplementProperties.CSharp` package |
| `[ObjectAuthorizationRules]` | Marks a static method that defines object-level authorization rules | Place on a static method like `AddObjectAuthorizationRules()` |

> **Note**: The `[Serializable]` attribute is no longer required or recommended in CSLA 9 and later. CSLA uses its own serialization mechanism and does not require classes to be marked with this attribute.

### Data Portal Operation Attributes

These attributes mark methods that implement data portal operations. Methods are typically private and can be sync or async.

| Attribute | Operation Type | Used In | Detailed Doc |
| --- | --- | --- | --- |
| `[Create]` | Initialize a new object with default values | Root and child objects | `DataPortalOperationCreate.md` |
| `[Fetch]` | Retrieve an existing object from data store | Root and child objects | `DataPortalOperationFetch.md` |
| `[Insert]` | Save a new object to the data store | Root and child objects | `DataPortalOperationInsert.md` |
| `[Update]` | Update an existing object in the data store | Root and child objects | `DataPortalOperationUpdate.md` |
| `[Delete]` | Delete an object by criteria without fetching it first | Root objects only | `DataPortalOperationDelete.md` |
| `[DeleteSelf]` | Delete the current object instance | Root and child objects | `DataPortalOperationDelete.md` |
| `[Execute]` | Execute a command operation | Command objects only | `DataPortalOperationExecute.md` |

### Other Method/Property Attributes

| Attribute | Purpose | Usage |
| --- | --- | --- |
| `[Inject]` | Marks a method parameter for dependency injection | Use on DAL or service parameters in data portal methods |
| `[CslaIgnoreProperty]` | Excludes a property automatic code generation with the `CslaImplementProperties` behavior | Place on properties that should not be managed by CSLA |

## Stereotypes

There are object-oriented stereotypes supported by the CSLA base classes. For a more detailed overview of when to use each stereotype, see `ObjectStereotypes.md`.

| Stereotype | Base class | Definition | Generic parameters |
| --- | --- | --- | --- |
| Editable root | `BusinessBase<T>` | An editable business domain type that enables the use of the full rules engine, data binding, and data portal operations | T is the type of business class |
| Editable child | `BusinessBase<T>` | An editable business domain type that enables the use of the full rules engine, data binding, and child data portal operations; always a child of a parent object | T is the type of business class |
| Editable root list | `BusinessListBase<T, C>` | A business domain type representing a list of editable child objects; supports data binding, and data portal create, fetch, and update operations | T is the type of the list, C is the type of child class |
| Editable child list | `BusinessListBase<T, C>` | A business domain type representing a list of editable child objects; supports data binding, and child data portal operations; always a child of a parent object | T is the type of the list, C is the type of child class |
| Read-only root | `ReadOnlyBase<T>` | A read-only business domain type that supports authorization rules, read-only properties, data binding, and the data portal fetch operation | T is the type the business class |
| Read-only child | `ReadOnlyBase<T>` | A read-only business domain type that supports authorization rules, read-only properties, data binding, and the child data portal child fetch operation | T is the type of business class |
| Read-only root list | `ReadOnlyListBase<T, C>` | A read-only business domain type that supports authorization rules, read-only properties, data binding, and the data portal fetch operation | T is the type of the list, C is the type of child class |
| Read-only child list | `ReadOnlyListBase<T, C>` | A read-only business domain type that supports authorization rules, read-only properties, data binding, and the child data portal child fetch operation | T is the type of the list, C is the type of child class |
| Dynamic root list | `DynamicListBase<T, C>` | A business domain type representing a list of editable root objects; supports data binding, and data portal create and fetch operations; individual root objects are updated or deleted individually; primarily designed for data binding against an data grid control in the UI |  T is the type of the list, C is the type of the contained editable root class |
| Command | `CommandBase<T>` | A business domain type representing a command that can be executed within the business domain; examples: does a person exist?, ship an order, archive an invoice | T is the type of the command class |
| Unit of work (read or fetch data) | A business type representing an operation where multiple other types of root objects are retrieved at once; this type has a property for each of the root object types being retrieved, and its data portal fetch operation contains code to call the data portal to fetch each of the root objects to be returned | `ReadOnlyBase<T>` | T is the type of the unit of work business class |
| Unit of work (modify or update data) | A business type representing an operation where multiple other types of root objects are updated, saved, or deleted at once; this type has a property for each of the root object types being modified, and its data portal execute operation contains code to call the data portal to save each of the root objects | `CommandBase<T>` | T is the type of the unit of work business class |

## Data portal and data access

CSLA is not an ORM, and doesn't implement any data access itself. All data access code should be in the data access layer. For an in-depth architectural guide to the data portal, see `DataPortalGuide.md`. For concrete DAL implementation examples, see `Data-Access.md`.

CSLA has an important construct called the "data portal" which abstracts persistence of all business domain types. The data portal supports two concepts: root objects and child objects.

A root object is a business domain type that might contain child objects. The root object and all its child objects are called an _object graph_.

A parent object contains child objects.

A child object is always contained within a parent object. The top-level parent object is the root object - the root of the object graph.

### Mobile objects and location transparency

Root objects represent an object graph of one or more objects. CSLA supports the concept of _mobile objects_, where an object graph can move from one process or computer to another. The object graph is cloned across the process or network boundary by the root data portal.

Only the data within the object graph is actually moved. It is serialized, transferred, and deserialized. The _code_ (.NET assembly) must be deployed ahead of time on any computers involved (such as the client and server devices).

This concept of mobile objects is implemented by the root data portal. The data portal abstracts this concept entirely, so any code _using_ the data portal is unchanged regardless of whether object graphs actually move or not.

The data portal relies on configuration to determine whether object graphs remain on the same computer - moving between _logical_ client and server, or actually move across boundaries between _physical_ client and server. This is _location transparency_, where the calling code has no indication of runtime behavior differences between 1-tier, 2-tier, 3-tier, or n-tier deployments.

### Root data portal

The root data portal provides a set of operations that operate on the root object of an object graph. All of these operations have synchronous and asynchronous versions. The async versions have `Async` at the end of the operation name and return some type of `Task`.

If the root object of the graph is also a parent object (it contains child objects), it will use the child data portal to cascade appropriate operations down through the rest of the graph.

To use the root data portal, calling code needs to use dependency injection to get an instance of type `IDataPortal<T>`, where `T` is the type of the business domain root class for the object graph.

| Operation | Description |
| --- | --- |
| Create | Creates a new instance of an object graph, initializing the object with any required default values, possibly from the data access layer |
| Fetch | Retrieves or gets an instance of an object graph, loading the object with appropriate data from the data access layer |
| Insert | Inserts the data from the object graph into the data store by invoking the data access layer |
| Update | Updates the data in the object graph into the data store by invoking the data access layer |
| DeleteSelf | Uses the data access layer to delete the data represented by the object graph |
| Delete | Like a command object, this uses the data access layer to delete the data by key, without having to fetch the object graph first |
| Execute | Executes the command on the logical server; the command object can do whatever operations on the server necessary to implement the command |

### Saving an object graph

Although the root data portal has insert, update, and deleteself operations, those are not normally invoked directly by any code that uses the object graph. Instead, editable root and editable root list types have methods to save the graph.

(each method has an async equivalent, with `Async` appended to the name)

In this table, the term "calling code" refers to the code that called the root data portal or the root object's save methods.

| Method | Description |
| --- | --- |
| SaveAndMerge | Invokes the root data portal, causing the data portal to automatically invoke the insert, update, or deleteself operations; the resulting object graph is merged back into the original object graph, so the calling code can continue using its existing reference to the objects in the graph |
| Save | Invokes the root data portal, causing the data portal to automatically invoke the insert, update, or deleteself operations; the result of `Save` is a _new_ object graph with any changes that occurred during the logical server operations; the calling code must discard the old reference to the objects in the graph and use the new object graph |

### Child data portal

The child data portal provides a set of operations that are invoked _on the logical server only_ to work with child types such as an edtiable child or read-only child. All of these operations have synchronous and asynchronous versions. The async versions have `Async` at the end of the operation name and return some type of `Task`.

The child data portal operates only on child object types, and is designed to be invoked within the data portal operation of a parent.

To use the child data portal, calling code needs to use dependency injection to get an instance of type `IChildDataPortal<T>`, where `T` is the type of the business domain child class.

| Operation | Description |
| --- | --- |
| CreateChild | Creates a new instance of a child object, initializing the object with any required default values, possibly from the data access layer |
| FetchChild | Retrieves or gets an instance of a child object, loading the object with appropriate data from the data access layer |
| InsertChild | Inserts the data from the child object into the data store by invoking the data access layer |
| UpdateChild | Updates the data in the child object into the data store by invoking the data access layer |
| DeleteSelfChild | Uses the data access layer to delete the data represented by the child object |

### Interacting with the data access layer

There are four models that can be used to interact with the data access layer. These are listed in order of recommendation, so the last option is the least desirable.

| Model | Description |
| --- | --- |
| Encapsulated invocation | Data portal operation method is in the business class and it invokes an external data access layer to get or modify data; clean separation of concerns, minimal code |
| Factory implementation |  Data portal operation method is in a separate factory class and it directly implements data access code to get or modify data; the factory class is the data access layer; clean separation of concerns, minimal code |
| Factory invocation |  Data portal operation method is in a separate factory class and it invokes an external data access layer to get or modify data; unnecessary layers of abstraction in most cases |
| Encapsulated implementation |  Data portal operation method is in the business class and it directly implements data access code to get or modify data; the data access layer is embedded in the business class |

_Encapsulated invocation_ is the best model for most application scenarios.

## Property Conventions

Property declarations are covered in the version-specific `Properties.md` file (for example `v10/Properties.md`), and they vary from CSLA 9 to CSLA 10.

The common way to declare a property is similar to this:

```csharp
    public static readonly PropertyInfo<string> NameProperty = RegisterProperty<string>(nameof(Name));
    public string Name
    {
        get => GetProperty(NameProperty);
        set => SetProperty(NameProperty, value);
    }
```

There is a simpler way to do this in CSLA 10 and later.

Also, there are differences between read-write and read-only properties, and how properties are declared in editable, read-only, and command stereotypes. All this is covered in more specific documentation files.

### Property Backing Fields

Each property has a corresponding `PropertyInfo<T>` field with "Property" suffix:

For example, a `Name` property will have a `NameProperty` field with information about the property. Many other CSLA methods rely on this corresponding field.

### Property Access Methods

| Method | Purpose | When to Use |
| --- | --- | --- |
| `LoadProperty(propertyInfo, value)` | Sets a property value internally, bypassing validation and change tracking | In data portal operations when loading data from DAL |
| `ReadProperty(propertyInfo)` | Gets a property value internally | In data portal operations when reading values to send to DAL |
| `GetProperty(propertyInfo)` | Gets a property value (used in property getters) | In manually-implemented property getters (CSLA 9 style) |
| `SetProperty(propertyInfo, value)` | Sets a property value, triggering validation and change tracking | In manually-implemented property setters (CSLA 9 style) |

### Using BypassPropertyChecks

Alternative approach to `LoadProperty` for setting multiple properties:

```csharp
using (BypassPropertyChecks)
{
    Id = customerData.Id;
    Name = customerData.Name;
    Email = customerData.Email;
}
```

## Object State Management

### Object Metastate

CSLA tracks object metastate for various reasons.

| State | Description | Methods to Check |
| --- | --- | --- |
| Busy | Object is running one or more async business rules | `IsBusy` property |
| New | Object is new and needs to be inserted | `IsNew` property |
| Clean | Object exists and has no changes | `!IsDirty` |
| Dirty | Object has unsaved changes | `IsDirty` property |
| Deleted | Object is marked for deletion | `IsDeleted` property |
| Valid | Object passes all business rules | `IsValid` property |
| Savable | Object can be saved (dirty and valid, or deleted) | `IsSavable` property |

### Metastate Manipulation Methods

| Method | Purpose | When to Use |
| --- | --- | --- |
| `MarkDeleted()` | Marks object for deletion | In business methods that soft-delete |
| `MarkNew()` | Marks object as new | Rarely needed; data portal handles this |
| `MarkClean()` | Marks object as unchanged | Rarely needed; data portal handles this |
| `MarkOld()` | Marks object as existing (not new) | Rarely needed; data portal handles this |

## Business and Authorization Rules

### Business Rules

Business rules provide validation and calculation logic. Added in the `AddBusinessRules()` override method using `BusinessRules.AddRule()`.

**Common rule types**: `Required`, `MaxLength`, `MinLength`, `Range`, `RegEx`, `Dependency`

**Data Annotations**: CSLA automatically converts standard data annotations (`[Required]`, `[StringLength]`, `[EmailAddress]`, etc.) into business rules.

### Authorization Rules

Authorization rules control who can perform operations on objects and properties using `Rules.CommonRules.IsInRole`.

#### Object-Level Authorization

Defined in a static method with `[ObjectAuthorizationRules]` attribute. Controls entire object access.

**Authorization actions**: `CreateObject`, `GetObject`, `EditObject`, `DeleteObject`

#### Property-Level Authorization

Added in `AddBusinessRules()` method. Controls access to specific properties.

**Authorization actions**: `ReadProperty`, `WriteProperty`, `ExecuteMethod`

Example: `BusinessRules.AddRule(new Rules.CommonRules.IsInRole(AuthorizationActions.WriteProperty, SalaryProperty, "Admin", "HR"));`

### Rule Checking

Call `BusinessRules.CheckRules()` or `await BusinessRules.CheckRulesAsync()` at the end of data portal operations to run all rules and populate validation state.

**When to use async**: Use `CheckRulesAsync()` when the business class has any async business rules. It also works with synchronous rules, making it the recommended default approach.

## Common Patterns and Best Practices

### Dependency Injection

Use `[Inject]` attribute on data portal method parameters to inject DAL interfaces, services, and other dependencies.

### Error Handling

Let exceptions propagate from data portal operations. The data portal handles them and returns them to the caller. The data portal relies on exceptions to know that it should rollback any transaction in progress.

### Async vs Sync

All data portal operations support both sync and async. Use async methods when calling async DAL operations or services.

### Data Access Models

**Recommended**: Encapsulated invocation - Data portal methods in business class invoke external DAL.

**Alternative**: Factory implementation - Data portal methods in separate factory class.

See `Data-Access.md` for detailed patterns.
