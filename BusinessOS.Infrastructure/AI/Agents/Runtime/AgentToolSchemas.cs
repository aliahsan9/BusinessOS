using BusinessOS.Application.Features.AI.Enums;

namespace BusinessOS.Infrastructure.AI.Agents.Runtime;

/// <summary>
/// Canonical JSON schema descriptions for structured argument extraction.
/// </summary>
public static class AgentToolSchemas
{
    public static string? For(AiToolName tool) => tool switch
    {
        AiToolName.CreateCustomer => """
            {"type":"object","properties":{
              "firstName":{"type":"string"},
              "lastName":{"type":"string"},
              "fullName":{"type":"string"},
              "email":{"type":"string"},
              "phone":{"type":"string"},
              "address":{"type":"string"},
              "city":{"type":"string"},
              "country":{"type":"string"},
              "postalCode":{"type":"string"},
              "company":{"type":"string"},
              "notes":{"type":"string"}
            },"required":["firstName"]}
            """,
        AiToolName.UpdateCustomer => """
            {"type":"object","properties":{
              "customerId":{"type":"string","format":"uuid"},
              "name":{"type":"string"},
              "firstName":{"type":"string"},
              "lastName":{"type":"string"},
              "email":{"type":"string"},
              "phone":{"type":"string"},
              "address":{"type":"string"},
              "city":{"type":"string"},
              "country":{"type":"string"},
              "postalCode":{"type":"string"},
              "company":{"type":"string"},
              "isActive":{"type":"boolean"}
            }}
            """,
        AiToolName.DeleteCustomer => """
            {"type":"object","properties":{
              "customerId":{"type":"string","format":"uuid"},
              "name":{"type":"string"}
            }}
            """,
        AiToolName.SearchCustomer => """
            {"type":"object","properties":{
              "query":{"type":"string"},
              "phone":{"type":"string"},
              "email":{"type":"string"}
            },"required":["query"]}
            """,
        AiToolName.CreateProduct => """
            {"type":"object","properties":{
              "name":{"type":"string"},
              "sku":{"type":"string"},
              "description":{"type":"string"},
              "categoryId":{"type":"string","format":"uuid"},
              "categoryName":{"type":"string"},
              "costPrice":{"type":"number"},
              "salePrice":{"type":"number"},
              "reorderLevel":{"type":"integer"}
            },"required":["name"]}
            """,
        AiToolName.UpdateProduct => """
            {"type":"object","properties":{
              "productId":{"type":"string","format":"uuid"},
              "name":{"type":"string"},
              "sku":{"type":"string"},
              "description":{"type":"string"},
              "costPrice":{"type":"number"},
              "salePrice":{"type":"number"},
              "reorderLevel":{"type":"integer"},
              "isActive":{"type":"boolean"}
            }}
            """,
        AiToolName.DeleteProduct => """
            {"type":"object","properties":{
              "productId":{"type":"string","format":"uuid"},
              "name":{"type":"string"},
              "sku":{"type":"string"}
            }}
            """,
        AiToolName.SearchProduct => """
            {"type":"object","properties":{
              "query":{"type":"string"},
              "sku":{"type":"string"}
            },"required":["query"]}
            """,
        AiToolName.AdjustInventory => """
            {"type":"object","properties":{
              "productId":{"type":"string","format":"uuid"},
              "productName":{"type":"string"},
              "sku":{"type":"string"},
              "quantity":{"type":"number"},
              "transactionType":{"type":"string"},
              "notes":{"type":"string"}
            },"required":["quantity"]}
            """,
        AiToolName.ReceiveStock => """
            {"type":"object","properties":{
              "productId":{"type":"string","format":"uuid"},
              "productName":{"type":"string"},
              "sku":{"type":"string"},
              "quantity":{"type":"number"},
              "referenceNumber":{"type":"string"},
              "notes":{"type":"string"}
            },"required":["quantity"]}
            """,
        AiToolName.CreateSale => """
            {"type":"object","properties":{
              "customerId":{"type":"string","format":"uuid"},
              "customerName":{"type":"string"},
              "discount":{"type":"number"},
              "tax":{"type":"number"},
              "items":{"type":"array","items":{"type":"object","properties":{
                "productId":{"type":"string"},
                "productName":{"type":"string"},
                "sku":{"type":"string"},
                "quantity":{"type":"number"}
              }}}
            }}
            """,
        AiToolName.CreateInvoice => """
            {"type":"object","properties":{
              "orderId":{"type":"string","format":"uuid"},
              "customerId":{"type":"string","format":"uuid"},
              "customerName":{"type":"string"},
              "dueDays":{"type":"integer"},
              "notes":{"type":"string"}
            }}
            """,
        AiToolName.CancelInvoice => """
            {"type":"object","properties":{
              "invoiceId":{"type":"string","format":"uuid"},
              "invoiceNumber":{"type":"string"}
            }}
            """,
        AiToolName.SearchInvoice => """
            {"type":"object","properties":{
              "query":{"type":"string"},
              "invoiceNumber":{"type":"string"},
              "customerName":{"type":"string"}
            }}
            """,
        AiToolName.CreatePurchaseOrder or AiToolName.CreatePurchaseOrderDraft => """
            {"type":"object","properties":{
              "supplierId":{"type":"string","format":"uuid"},
              "supplierName":{"type":"string"},
              "items":{"type":"array","items":{"type":"object","properties":{
                "productId":{"type":"string"},
                "productName":{"type":"string"},
                "quantity":{"type":"number"},
                "unitCost":{"type":"number"}
              }}}
            }}
            """,
        AiToolName.ApprovePurchaseOrder => """
            {"type":"object","properties":{
              "purchaseOrderId":{"type":"string","format":"uuid"},
              "poNumber":{"type":"string"}
            }}
            """,
        AiToolName.ReceivePurchase => """
            {"type":"object","properties":{
              "purchaseOrderId":{"type":"string","format":"uuid"},
              "poNumber":{"type":"string"}
            }}
            """,
        AiToolName.CreateSupplier => """
            {"type":"object","properties":{
              "name":{"type":"string"},
              "email":{"type":"string"},
              "phone":{"type":"string"},
              "address":{"type":"string"},
              "contactPerson":{"type":"string"},
              "notes":{"type":"string"}
            },"required":["name"]}
            """,
        AiToolName.UpdateSupplier => """
            {"type":"object","properties":{
              "supplierId":{"type":"string","format":"uuid"},
              "name":{"type":"string"},
              "email":{"type":"string"},
              "phone":{"type":"string"},
              "address":{"type":"string"},
              "contactPerson":{"type":"string"}
            }}
            """,
        AiToolName.DeleteSupplier => """
            {"type":"object","properties":{
              "supplierId":{"type":"string","format":"uuid"},
              "name":{"type":"string"}
            }}
            """,
        AiToolName.SearchSupplier => """
            {"type":"object","properties":{
              "query":{"type":"string"}
            },"required":["query"]}
            """,
        AiToolName.UpdateCompanyProfile => """
            {"type":"object","properties":{
              "name":{"type":"string"},
              "businessType":{"type":"string"},
              "email":{"type":"string"},
              "phone":{"type":"string"},
              "address":{"type":"string"},
              "website":{"type":"string"},
              "description":{"type":"string"}
            }}
            """,
        AiToolName.UpdateTaxDefaults => """
            {"type":"object","properties":{
              "taxRate":{"type":"number"},
              "currency":{"type":"string"},
              "invoicePrefix":{"type":"string"}
            }}
            """,
        _ => null
    };
}
