# CSLA .NET Version 10 Glossary of Terms

This is a gloassary or index of terms that are commonly used in the CSLA .NET environment. They are a type of jargon.

In most cases, these terms and related concepts are more fully documented in other files available using the `search` tool. Detailed documents often also include code examples for supported versions of CSLA .NET.

## Architecture

CSLA is designed around the concept that each application has the following logical layers:

* Interface (user interface or application interface; often Blazor, JSON, etc.)
* Interface control (code that manages the interface; often a controller, viewmodel, or code-behind a page)
* Business logic (business domain types created using CSLA base classes and stereotypes)
* Data access (code that interacts with the data store(s) such as databases, files, or other storage mediums; often implemented with ADO.NET or Entity Framework)
* Data storage (databases, files, spreadsheets, service APIs, or other locations where data might be retrieved and saved)

## Stereotypes

There are object-oriented stereotypes supported by the CSLA base classes.

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

CSLA is not an ORM, and doesn't implement any data access itself. All data access code should be in the data access layer.

However, CSLA does have an important construct called the "data portal" which abstracts persistence of all business domain types. The data portal supports two concepts: root objects and child objects.

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

### Ineracting with the data access layer

There are four models that can be used to interact with the data access layer. These are listed in order of recommendation, so the last option is the least desirable.

| Model | Description |
| --- | --- |
| Encapsulated invocation | Data portal operation method is in the business class and it invokes an external data access layer to get or modify data; clean separation of concerns, minimal code |
| Factory implementation |  Data portal operation method is in a separate factory class and it directly implements data access code to get or modify data; the factory class is the data access layer; clean separation of concerns, minimal code |
| Factory invocation |  Data portal operation method is in a separate factory class and it invokes an external data access layer to get or modify data; unnecessary layers of abstraction in most cases |
| Encapsulated implementation |  Data portal operation method is in the business class and it directly implements data access code to get or modify data; the data access layer is embedded in the business class |

_Encapsulated invocation_ is the best model for most application scenarios.
