# Editable Child Stereotype

This example demonstrates a complete CSLA business class named `OrderItemEdit` that includes various property types, business rules, authorization rules, and data access methods. The class derives from `BusinessBase<T>` and includes both read-only and read-write properties.

This class demonstrates the editable child business class stereotype.

It also shows how to implement business rules for validation, including required fields and range constraints. Additionally, it includes object-level authorization rules to control access based on user roles.

It also includes data portal operation methods for creating, fetching, inserting, updating, and deleting order item records. Note that the data access methods contain placeholder comments where actual data access logic should be invoked.

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using Csla;

public class OrderItemEdit : BusinessBase<OrderItemEdit>
{
    public static readonly PropertyInfo<int> IdProperty = RegisterProperty<int>(nameof(Id));
    public int Id
    {
        get => GetProperty(IdProperty);
        private set => LoadProperty(IdProperty, value);
    }

    public static readonly PropertyInfo<string> ProductNameProperty = RegisterProperty<string>(nameof(ProductName));
    [Required]
    [StringLength(100)]
    public string ProductName
    {
        get => GetProperty(ProductNameProperty);
        set => SetProperty(ProductNameProperty, value);
    }

    public static readonly PropertyInfo<int> QuantityProperty = RegisterProperty<int>(nameof(Quantity));
    [Range(1, 1000)]
    public int Quantity
    {
        get => GetProperty(QuantityProperty);
        set => SetProperty(QuantityProperty, value);
    }

    public static readonly PropertyInfo<decimal> PriceProperty = RegisterProperty<decimal>(nameof(Price));
    [Range(0.01, 10000.00)]
    public decimal Price
    {
        get => GetProperty(PriceProperty);
        set => SetProperty(PriceProperty, value);
    }

    protected override void AddBusinessRules()
    {
        base.AddBusinessRules();
        // Add any custom business rules here if needed
    }

    protected override void AddAuthorizationRules()
    {
        base.AddAuthorizationRules();
        // Example: Only users in the "Manager" role can edit the Price property
        BusinessRules.AddRule(new Csla.Rules.CommonRules.IsInRole(PriceProperty, "Manager"));
    }

    [CreateChild]
    private void CreateChild()
    {
        // Initialize default values here if needed
        LoadProperty(QuantityProperty, 1);
        LoadProperty(PriceProperty, 0.01);
    }

    [FetchChild]
    private void FetchChild(OrderDetailData data)
    {
        // Load properties from data object
        LoadProperty(IdProperty, data.Id);
        LoadProperty(ProductNameProperty, data.ProductName);
        LoadProperty(QuantityProperty, data.Quantity);
        LoadProperty(PriceProperty, data.Price);
    }

    [InsertChild]
    private void InsertChild([Inject] IOrderDetailDal dal)
    {
        var data = new OrderDetailData
        {
            ProductName = ReadProperty(ProductNameProperty),
            Quantity = ReadProperty(QuantityProperty),
            Price = ReadProperty(PriceProperty)
        };
        var newId = dal.Insert(data);
        LoadProperty(IdProperty, newId);
    }

    [UpdateChild]
    private void UpdateChild([Inject] IOrderDetailDal dal)
    {
        var data = new OrderDetailData
        {
            Id = ReadProperty(IdProperty),
            ProductName = ReadProperty(ProductNameProperty),
            Quantity = ReadProperty(QuantityProperty),
            Price = ReadProperty(PriceProperty)
        };
        dal.Update(data);
    }

    [DeleteSelfChild]
    private void DeleteSelfChild([Inject] IOrderDetailDal dal)
    {
        dal.Delete(ReadProperty(IdProperty));
    }
}
```
