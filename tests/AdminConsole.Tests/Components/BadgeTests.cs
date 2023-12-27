using Bunit;
using Passwordless.AdminConsole.TagHelpers;
using Xunit;
using Badge = Passwordless.AdminConsole.Components.Shared.Badge;

namespace Passwordless.AdminConsole.Tests.Components;

public class BadgeTests : TestContext
{
    #region Class
    [Theory]
    [InlineData(ColorVariant.Primary)]
    [InlineData(ColorVariant.Success)]
    [InlineData(ColorVariant.Danger)]
    [InlineData(ColorVariant.Warning)]
    [InlineData(ColorVariant.Info)]
    public void Badge_Renders_WhiteText(ColorVariant variant)
    {
        // Arrange
        var cut = RenderComponent<Badge>(parameters => parameters
            .Add(p => p.Variant, variant));

        // Act
        var actual = cut.Markup;

        // Assert
        Assert.Contains("text-white", actual);
    }

    public static IEnumerable<object[]> BackgroundClassData = new List<object[]>
    {
        new object[] {ColorVariant.Primary, "bg-blue-600"},
        new object[] {ColorVariant.Success, "bg-green-600"},
        new object[] {ColorVariant.Danger, "bg-red-600"},
        new object[] {ColorVariant.Warning, "bg-yellow-600"},
        new object[] {ColorVariant.Info, "bg-blue-600"}
    };

    [Theory]
    [MemberData(nameof(BackgroundClassData))]
    public void Badge_Renders_ExpectedBackground_Variant(ColorVariant variant, string expectedClass)
    {
        // Arrange
        var cut = RenderComponent<Badge>(parameters => parameters
            .Add(p => p.Variant, variant));

        // Act
        var actual = cut.Markup;

        // Assert
        Assert.Contains(expectedClass, actual);
    }
    #endregion

    #region Message
    [Fact]
    public void Badge_Renders_ExpectedText()
    {
        // Arrange
        var cut = RenderComponent<Badge>(parameters => parameters
            .Add(p => p.Message, "Active"));
        var span = cut.Find("span");

        // Act

        // Assert
        Assert.Contains("Active", span.TextContent);
        Assert.Contains("Active", span.InnerHtml);
    }
    #endregion
}