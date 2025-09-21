# ReadOnlyProperty

This snippet demonstrates how to define a read-only property using the CSLA property registration system. This is useful for creating read-only properties that are part of an editable business class that derives from `BusinessBase<T>` or a read-only business class that dervies from ReadOnlyBase<T>.

Note that the property has a private setter, which is typically used in conjunction with the `LoadProperty` method to set the property's value internally within the class. The `LoadProperty` method bypasses any business rules or validation, making it suitable for initializing read-only properties.

```csharp
        public static readonly PropertyInfo<int> IdProperty = RegisterProperty<int>(nameof(Id));
        public int Id
        {
            get => GetProperty(IdProperty);
            private set => LoadProperty(IdProperty, value);
        }
```
