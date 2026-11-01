using BusinessOS.Application.Features.Agents;
using BusinessOS.Application.Features.Agents.Enums;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Infrastructure.AI.Copilot;
using FluentAssertions;

namespace BusinessOS.UnitTests.AI;

public class ConversationLanguageTests
{
    [Theory]
    [InlineData("Show my total sales this month.", "en")]
    [InlineData("اس مہینے کی کل سیلز کتنی ہیں؟", "ur")]
    [InlineData("Is mahine ki total sales kitni hain?", "ur-Latn")]
    [InlineData("Mujhe is month ki total sales batao.", "ur-Latn")]
    [InlineData("Mujhe low stock products ki list dikhao.", "ur-Latn")]
    [InlineData("كم بلغت مبيعات هذا الشهر؟", "ar")]
    [InlineData("Kon sa product zyada sell ho raha hai?", "ur-Latn")]
    [InlineData("Kitna stock baqi hai?", "ur-Latn")]
    [InlineData("ما هي المنتجات الأكثر مبيعاً؟", "ar")]
    [InlineData("Now explain that in Urdu.", "ur")]
    [InlineData("Please explain this in English.", "en")]
    public void Resolve_DetectsTheUsersLanguage(string message, string expected)
    {
        ConversationLanguage.Resolve(message, AgentLanguages.English).Language.Should().Be(expected);
    }

    [Fact]
    public void Resolve_ShortFollowUp_KeepsTheConversationLanguage()
    {
        var detected = ConversationLanguage.Resolve("اس مہینے کی کل سیلز کتنی ہیں؟", "en");
        ConversationLanguage.Resolve("Why?", detected.Language).Language.Should().Be("ur");
    }

    [Fact]
    public void Resolve_LockedPreference_ChangesOnlyOnAnExplicitSwitch()
    {
        ConversationLanguage.Resolve("Show my total sales this month.", "ur", preferenceLocked: true)
            .Language.Should().Be("ur");
        ConversationLanguage.Resolve("Please explain this in English.", "ur", preferenceLocked: true)
            .Language.Should().Be("en");
    }

    [Fact]
    public void ExpandForRouting_AddsEnglishCuesForArabicAndUrdu()
    {
        ConversationLanguage.ExpandForRouting("كم بلغت مبيعات هذا الشهر؟")
            .Should().Contain("sales").And.Contain("this month");
        ConversationLanguage.ExpandForRouting("پچھلے مہینے کی سیلز")
            .Should().Contain("last month").And.Contain("sales");
    }

    [Theory]
    [InlineData("Show my total sales this month.", AiToolName.GetSalesSummary)]
    [InlineData("اس مہینے کی کل سیلز کتنی ہیں؟", AiToolName.GetSalesSummary)]
    [InlineData("Is mahine ki total sales kitni hain?", AiToolName.GetSalesSummary)]
    [InlineData("كم بلغت مبيعات هذا الشهر؟", AiToolName.GetSalesSummary)]
    [InlineData("Mujhe low stock products ki list dikhao.", AiToolName.GetLowStock)]
    [InlineData("Kon sa product zyada sell ho raha hai?", AiToolName.GetBestSellingProducts)]
    [InlineData("Kitna stock baqi hai?", AiToolName.GetInventorySummary)]
    [InlineData("نیا کسٹمر بناؤ", AiToolName.CreateCustomer)]
    [InlineData("ما هي المنتجات الأكثر مبيعاً؟", AiToolName.GetBestSellingProducts)]
    public void Detect_SelectsTheSameBusinessToolInEveryLanguage(string message, AiToolName tool)
    {
        var result = new AiIntentDetector().Detect(message, new AiPageContextDto(), new AiMemoryStateDto());
        result.SuggestedTools.Should().Contain(tool);
    }
}
