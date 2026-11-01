using System.Globalization;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;

namespace BusinessOS.Infrastructure.AI.Agents.Runtime;

public interface ISaleOrderDraftService
{
    Task<AgentClarificationDecision> ApplyAsync(
        AgentClarificationDecision decision,
        string message,
        Guid sessionId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Binds a sale draft to tenant records before Sophia asks for confirmation or creates the order.
/// </summary>
public sealed class SaleOrderDraftService : ISaleOrderDraftService
{
    private readonly ISaleOrderCatalog _catalog;

    public SaleOrderDraftService(ISaleOrderCatalog catalog) => _catalog = catalog;

    public async Task<AgentClarificationDecision> ApplyAsync(
        AgentClarificationDecision decision,
        string message,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        if (decision.Tool != AiToolName.CreateSale || (decision.PendingJson is null && !decision.ShouldExecute))
            return decision;

        var pending = AgentActionClarifier.Read(decision.PendingJson) ?? new AgentPendingAction
        {
            Tool = AiToolName.CreateSale,
            OriginalRequest = message
        };
        var slots = pending.Slots;
        ApplyChoice(message, pending, slots);

        var customer = await _catalog.ResolveCustomerAsync(
            Get(slots, "customerId"),
            Get(slots, "customerName"),
            cancellationToken);
        var product = await _catalog.ResolveProductAsync(
            Get(slots, "productId"),
            Get(slots, "productName"),
            cancellationToken);

        ApplyCustomer(slots, customer);
        ApplyProduct(slots, product);

        if (customer.Kind == SaleMatchKind.Ambiguous)
        {
            return Ask(
                pending,
                slots,
                CustomerChoiceQuestion(customer.Options),
                "customer",
                ChoiceSuggestions(customer.Options.Select(person => person.Name)));
        }

        if (customer.Kind == SaleMatchKind.Inactive && customer.Value is not null)
        {
            return Ask(
                pending,
                slots,
                $"{customer.Value.Name} is inactive, so I can't create an order for them. Which customer should I use?",
                "customer");
        }

        if (customer.Kind == SaleMatchKind.NotFound && Has(slots, "customerName"))
        {
            return Ask(
                pending,
                slots,
                CustomerNotFoundQuestion(slots, pending.OriginalRequest),
                "customer");
        }

        if (product.Kind == SaleMatchKind.Ambiguous)
        {
            var lines = product.Options.Select((item, index) =>
                $"{index + 1}. {item.Name} [{item.Sku}] — {Money(item.SalePrice)}");
            return Ask(
                pending,
                slots,
                "I found more than one matching product. Which one should I use?\n" + string.Join("\n", lines),
                "product",
                ChoiceSuggestions(product.Options.Select(item => item.Name)));
        }

        if (product.Kind == SaleMatchKind.Inactive && product.Value is not null)
        {
            return Ask(
                pending,
                slots,
                $"{product.Value.Name} is inactive, so I can't add it to an order. Which product should I use?",
                "product");
        }

        if (product.Kind == SaleMatchKind.NotFound && Has(slots, "productName"))
        {
            return Ask(
                pending,
                slots,
                $"I couldn't find an inventory item matching \"{Get(slots, "productName")}\". I can search the catalog, or create the product if you want to add it.",
                "product");
        }

        if (!SaleOrderLanguage.TryGetSlotQuantity(slots, out var quantity))
        {
            var who = customer.Value?.Name ?? Get(slots, "customerName");
            var prefix = string.IsNullOrWhiteSpace(who)
                ? "I can create that order."
                : $"I'll create the order for {who}.";
            var question = customer.Kind == SaleMatchKind.Unique && product.Kind == SaleMatchKind.Unique
                ? $"{prefix} How many {product.Value!.Name} should I add?"
                : prefix + " Which product, and how many?\nExample: HP laptop generation 5, quantity 2.";
            return Ask(pending, slots, question, product.Kind == SaleMatchKind.Unique ? "quantity" : "product");
        }

        if (customer.Kind != SaleMatchKind.Unique || product.Kind != SaleMatchKind.Unique || customer.Value is null || product.Value is null)
        {
            pending.Slots = slots;
            return Copy(decision, pending, AdjustQuestion(decision.Question, customer));
        }

        var quote = BuildQuote(customer.Value, product.Value, quantity, slots, pending.OriginalRequest);
        if (!quote.CanCreate)
            return Ask(pending, slots, quote.Text, "quantity");

        pending.AwaitingConfirmation = true;
        pending.NextField = null;
        pending.Slots = slots;
        if (!decision.ShouldExecute)
        {
            return Ask(
                pending,
                slots,
                quote.Text,
                nextField: null,
                suggestions:
                [
                    new AiSuggestionDto { Label = "Yes, create the order", Message = "Yes, proceed" },
                    new AiSuggestionDto { Label = "No, cancel", Message = "Cancel" }
                ],
                awaitingConfirmation: true);
        }

        _ = sessionId;
        return new AgentClarificationDecision
        {
            ShouldExecute = true,
            Tool = AiToolName.CreateSale,
            Question = "",
            PendingJson = AgentActionClarifier.Write(pending),
            Args = AgentActionClarifier.BuildExecutionArgs(AiToolName.CreateSale, slots),
            Suggestions = decision.Suggestions
        };
    }

    private static void ApplyChoice(string message, AgentPendingAction pending, Dictionary<string, string?> slots)
    {
        if (pending.AwaitingConfirmation || SaleOrderLanguage.IsConfirmation(message))
            return;

        if (pending.NextField is "customer" or "customerName"
            && SaleOrderLanguage.TrySelectChoice(message, Get(slots, "customerChoices"), out var customerId, out var customerLabel))
        {
            slots["customerId"] = customerId.ToString();
            slots["customerName"] = customerLabel;
            slots["customerQuery"] = customerLabel;
            slots.Remove("customerChoices");
        }

        if (pending.NextField is "product" or "productName"
            && SaleOrderLanguage.TrySelectChoice(message, Get(slots, "productChoices"), out var productId, out var productLabel))
        {
            slots["productId"] = productId.ToString();
            slots["productName"] = productLabel;
            slots["productQuery"] = productLabel;
            slots.Remove("productChoices");
        }
    }

    private static void ApplyCustomer(Dictionary<string, string?> slots, SaleMatch<SaleCustomerHit> match)
    {
        if (match.Kind == SaleMatchKind.Unique && match.Value is not null)
        {
            slots["customerId"] = match.Value.Id.ToString();
            slots["customerName"] = match.Value.Name;
            slots.Remove("customerChoices");
            return;
        }

        if (match.Kind is SaleMatchKind.Ambiguous or SaleMatchKind.NotFound or SaleMatchKind.Inactive)
            slots.Remove("customerId");

        if (match.Kind == SaleMatchKind.Ambiguous)
        {
            slots["customerChoices"] = SaleOrderLanguage.WriteChoices(
                match.Options.Select(person => (person.Id, person.Name)));
        }
    }

    private static void ApplyProduct(Dictionary<string, string?> slots, SaleMatch<SaleProductHit> match)
    {
        if (match.Kind == SaleMatchKind.Unique && match.Value is not null)
        {
            slots["productId"] = match.Value.Id.ToString();
            slots["productName"] = match.Value.Name;
            slots.Remove("productChoices");
            return;
        }

        if (match.Kind is SaleMatchKind.Ambiguous or SaleMatchKind.NotFound or SaleMatchKind.Inactive)
            slots.Remove("productId");

        if (match.Kind == SaleMatchKind.Ambiguous)
        {
            slots["productChoices"] = SaleOrderLanguage.WriteChoices(
                match.Options.Select(item => (item.Id, item.Name)));
        }
    }

    private static string CustomerNotFoundQuestion(Dictionary<string, string?> slots, string? originalRequest)
    {
        var name = Get(slots, "customerName");
        var productName = Get(slots, "productName");
        var held = SaleOrderLanguage.TryGetSlotQuantity(slots, out var quantity) && !string.IsNullOrWhiteSpace(productName)
            ? $" I still have {quantity.ToString("0.##", CultureInfo.InvariantCulture)} × {productName} for the order once {name} exists."
            : "";
        var price = SaleOrderLanguage.TryGetRequestedUnitPrice(originalRequest, out var requested)
            ? $" Orders use the catalog price, so {Money(requested)} each is not saved as the unit price."
            : "";
        return $"I couldn't find a customer named {name}.{held} Create {name} first, then ask me to make the sale.{price}";
    }

    private static QuoteResult BuildQuote(
        SaleCustomerHit customer,
        SaleProductHit product,
        decimal quantity,
        Dictionary<string, string?> slots,
        string? originalRequest)
    {
        var discount = ReadMoney(slots, "discount");
        var tax = ReadMoney(slots, "tax");
        var subtotal = Math.Round(quantity * product.SalePrice, 2, MidpointRounding.AwayFromZero);
        var total = Math.Round(subtotal - discount + tax, 2, MidpointRounding.AwayFromZero);
        var lines = new List<string>
        {
            $"Customer: {customer.Name}",
            $"Product: {product.Name} [{product.Sku}]",
            $"Quantity: {quantity.ToString("0.##", CultureInfo.InvariantCulture)}",
            $"Unit price: {Money(product.SalePrice)}",
            $"Subtotal: {Money(subtotal)}",
            $"Discount: {Money(discount)}",
            $"Tax: {Money(tax)}",
            $"Total: {Money(total)}"
        };
        var priceNote = PriceNote(originalRequest, product.SalePrice);
        if (!string.IsNullOrWhiteSpace(priceNote))
            lines.Insert(4, priceNote);

        if (!product.HasInventory)
        {
            return new QuoteResult(
                false,
                "I found the product, but there is no inventory record for it, so I can't create the order yet.\n"
                + Bullet(lines));
        }

        lines.Add($"In stock: {product.Stock.ToString("0.##", CultureInfo.InvariantCulture)}");
        if (product.Stock < quantity)
        {
            return new QuoteResult(
                false,
                $"I can't create this order yet. {product.Name} has {product.Stock.ToString("0.##", CultureInfo.InvariantCulture)} in stock and the order needs {quantity.ToString("0.##", CultureInfo.InvariantCulture)}.\n"
                + Bullet(lines));
        }

        if (discount < 0 || tax < 0 || total < 0)
        {
            return new QuoteResult(false, "That discount makes the order total negative. Tell me a smaller discount.\n" + Bullet(lines));
        }

        return new QuoteResult(
            true,
            $"I found {customer.Name} and {product.Name}. The order will contain {quantity.ToString("0.##", CultureInfo.InvariantCulture)} unit(s).\n"
            + Bullet(lines)
            + "\nShould I create this order?");
    }

    private static string AdjustQuestion(string question, SaleMatch<SaleCustomerHit> customer)
    {
        if (customer.Kind == SaleMatchKind.Unique
            && customer.Value is not null
            && question.Contains("Which customer", StringComparison.OrdinalIgnoreCase))
        {
            return $"I'll create the order for {customer.Value.Name}. Which product, and how many?";
        }

        return question;
    }

    private static AgentClarificationDecision Ask(
        AgentPendingAction pending,
        Dictionary<string, string?> slots,
        string question,
        string? nextField,
        IReadOnlyList<AiSuggestionDto>? suggestions = null,
        bool awaitingConfirmation = false)
    {
        pending.Slots = slots;
        pending.AwaitingConfirmation = awaitingConfirmation;
        pending.NextField = nextField;
        return new AgentClarificationDecision
        {
            ShouldExecute = false,
            Tool = AiToolName.CreateSale,
            Question = question,
            PendingJson = AgentActionClarifier.Write(pending),
            Suggestions = suggestions ?? [new AiSuggestionDto { Label = "Cancel", Message = "Cancel" }]
        };
    }

    private static AgentClarificationDecision Copy(
        AgentClarificationDecision decision,
        AgentPendingAction pending,
        string question) =>
        new()
        {
            ShouldExecute = false,
            Tool = decision.Tool,
            Question = question,
            PendingJson = AgentActionClarifier.Write(pending),
            Suggestions = decision.Suggestions
        };

    private static IReadOnlyList<AiSuggestionDto> ChoiceSuggestions(IEnumerable<string> labels) =>
        labels.Take(5).Select(label => new AiSuggestionDto { Label = label, Message = label }).ToList();

    private static string CustomerChoiceQuestion(IReadOnlyList<SaleCustomerHit> people)
    {
        var lines = people.Select((person, index) => $"{index + 1}. {person.Name}");
        return "I found more than one matching customer. Which one is this order for?\n" + string.Join("\n", lines);
    }

    private static decimal ReadMoney(Dictionary<string, string?> slots, string key)
    {
        var raw = Get(slots, key);
        if (string.IsNullOrWhiteSpace(raw))
            return 0;
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : 0;
    }

    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string PriceNote(string? message, decimal catalogPrice)
    {
        if (!SaleOrderLanguage.TryGetRequestedUnitPrice(message, out var requested) || requested == catalogPrice)
            return "";

        return $"You mentioned {Money(requested)} each. This order uses the catalog price instead.";
    }

    private static string Bullet(IEnumerable<string> lines) =>
        string.Join("\n", lines.Select(line => "• " + line));

    private static bool Has(Dictionary<string, string?> slots, string key) =>
        !string.IsNullOrWhiteSpace(Get(slots, key));

    private static string? Get(Dictionary<string, string?> slots, string key) =>
        slots.TryGetValue(key, out var value) ? value : null;

    private readonly record struct QuoteResult(bool CanCreate, string Text);
}
