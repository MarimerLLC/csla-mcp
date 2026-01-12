# CSLA Object Stereotypes

The CSLA .NET framework provides a set of base classes designed to help developers create different types of business objects, each adhering to a specific "stereotype." A stereotype is a broad grouping of objects with similar behaviors or roles within an application. Understanding these stereotypes is crucial for effectively designing and implementing business logic with CSLA.

This document summarizes the primary object stereotypes supported by CSLA, their purpose, and the corresponding base classes to inherit from.

## Table of CSLA Object Stereotypes and Base Classes

| Stereotype | Description | Base Class |
| :--------- | :---------- | :--------- |
| **Editable Root** | An object containing read-write properties; this object can be retrieved and stored directly to the database. It represents a top-level business entity. | `BusinessBase<T>` |
| **Editable Child** | An object containing read-write properties; this object is contained within another object (typically an editable root or editable child list) and cannot be retrieved or stored directly to the database independently. | `BusinessBase<T>` |
| **Editable Root List** | A list object containing editable child objects; this list can be retrieved and stored directly to the database. It represents a collection of business entities that collectively form a root. | `BusinessListBase<T,C>` or `BusinessBindingListBase<T,C>` (for Windows Forms) |
| **Editable Child List** | A list object containing editable child objects; this list is contained within another object and cannot be retrieved or stored directly to the database independently. | `BusinessListBase<T,C>` or `BusinessBindingListBase<T,C>` (for Windows Forms) |
| **Dynamic Root List** | A list object containing editable root objects; this list is retrieved directly from the database and supports dynamic loading/unloading of items. | `DynamicListBase<C>` or `DynamicBindingListBase<C>` (for Windows Forms) |
| **Command** | An object that executes a command (business process) on the application server and reports back with the results. This is often used for operations that don't directly map to CRUD on a single business object, such as a "ship order" command or a "validate user" command. | `CommandBase<T>` |
| **Read-only Root** | An object containing read-only properties; this object can be retrieved directly from the database. It represents a read-only view of a top-level business entity. | `ReadOnlyBase<T>` |
| **Read-only Child** | An object containing read-only properties; this object is contained within another object (typically a read-only root or read-only child list) and cannot be retrieved directly from the database independently. | `ReadOnlyBase<T>` |
| **Read-only Root List** | A list object containing read-only child objects; this list can be retrieved directly from the database. It represents a collection of read-only business entities. | `ReadOnlyListBase<T,C>` or `ReadOnlyBindingListBase<T,C>` (for Windows Forms) |
| **Read-only Child List** | A list object containing read-only child objects; this list is contained within another object and cannot be retrieved directly from the database independently. | `ReadOnlyListBase<T,C>` or `ReadOnlyBindingListBase<T,C>` (for Windows Forms) |
| **Name/Value List** | A specialized read-only list object containing key/value pairs, typically used for populating drop-down list controls or other selection mechanisms. | `NameValueListBase<K,V>` |

## Understanding the Purpose of Stereotypes

CSLA's stereotype-based design promotes consistency and reduces boilerplate code. By inheriting from the appropriate base class, your business object automatically gains a rich set of behaviors tailored to its role, including:

* **Change Tracking:** (`IsDirty`, `IsNew`, `IsDeleted`) for editable objects.
* **Validation and Business Rules:** Integration with the CSLA rules engine for enforcing business logic.
* **N-Level Undo:** For editable objects, allowing changes to be reverted.
* **Data Binding Support:** Implementation of interfaces like `INotifyPropertyChanged` and `IEditableObject` for seamless UI integration.
* **Data Portal Integration:** Standardized mechanisms for persistence (Create, Fetch, Update, Delete, Execute) that can be configured to run locally or remotely.

When designing your application's business layer, identify the role each business entity plays (e.g., is it an independent editable entity, a read-only lookup item, or a child of another object?) and choose the corresponding CSLA stereotype and base class. This approach streamlines development, enhances maintainability, and ensures adherence to best practices in object-oriented business application design.
