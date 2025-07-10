using WebForum.Api.Services.Interfaces;

namespace WebForum.Api.Services.Implementations;

/// <summary>
/// Service implementation for input sanitization to prevent XSS and injection attacks
/// </summary>
public class SanitizationService : ISanitizationService
{
  /// <summary>
  /// Sanitize user input to prevent XSS and other injection attacks
  /// </summary>
  /// <param name="input">Raw user input</param>
  /// <returns>Sanitized input with dangerous content encoded</returns>
  public string SanitizeInput(string input)
  {
    if (string.IsNullOrEmpty(input))
      return input;

    // Apply all sanitization methods in sequence
    var sanitized = input;
    sanitized = SanitizeScripts(sanitized);
    sanitized = SanitizeSql(sanitized);
    sanitized = SanitizeTemplateInjection(sanitized);
    sanitized = SanitizePhpTags(sanitized);

    return sanitized;
  }

  /// <summary>
  /// Sanitize HTML content while preserving safe tags
  /// </summary>
  /// <param name="htmlInput">Raw HTML input</param>
  /// <returns>Sanitized HTML with only safe tags preserved</returns>
  public string SanitizeHtml(string htmlInput)
  {
    if (string.IsNullOrEmpty(htmlInput))
      return htmlInput;

    // For now, encode all HTML to prevent XSS
    // In the future, could use a library like HtmlSanitizer to allow safe tags
    return System.Net.WebUtility.HtmlEncode(htmlInput);
  }

  /// <summary>
  /// Remove or encode SQL injection patterns
  /// </summary>
  /// <param name="input">Raw user input</param>
  /// <returns>Input with SQL injection patterns neutralized</returns>
  public string SanitizeSql(string input)
  {
    if (string.IsNullOrEmpty(input))
      return input;

    return input
      .Replace("DROP TABLE", "DROP&#32;TABLE", StringComparison.OrdinalIgnoreCase)
      .Replace("DELETE FROM", "DELETE&#32;FROM", StringComparison.OrdinalIgnoreCase)
      .Replace("TRUNCATE TABLE", "TRUNCATE&#32;TABLE", StringComparison.OrdinalIgnoreCase)
      .Replace("INSERT INTO", "INSERT&#32;INTO", StringComparison.OrdinalIgnoreCase)
      .Replace("UPDATE SET", "UPDATE&#32;SET", StringComparison.OrdinalIgnoreCase)
      .Replace("SELECT FROM", "SELECT&#32;FROM", StringComparison.OrdinalIgnoreCase)
      .Replace("UNION SELECT", "UNION&#32;SELECT", StringComparison.OrdinalIgnoreCase)
      .Replace("'; DROP", "'&#59;&#32;DROP", StringComparison.OrdinalIgnoreCase)
      .Replace("\"; DROP", "\"&#59;&#32;DROP", StringComparison.OrdinalIgnoreCase)
      .Replace("--", "&#45;&#45;")
      .Replace("/*", "&#47;&#42;")
      .Replace("*/", "&#42;&#47;");
  }

  /// <summary>
  /// Remove or encode script injection patterns (XSS)
  /// </summary>
  /// <param name="input">Raw user input</param>
  /// <returns>Input with script injection patterns neutralized</returns>
  public string SanitizeScripts(string input)
  {
    if (string.IsNullOrEmpty(input))
      return input;

    return input
      // Script tags
      .Replace("<script", "&lt;script", StringComparison.OrdinalIgnoreCase)
      .Replace("</script>", "&lt;/script&gt;", StringComparison.OrdinalIgnoreCase)

      // JavaScript and VBScript protocols
      .Replace("javascript:", "javascript&#58;", StringComparison.OrdinalIgnoreCase)
      .Replace("vbscript:", "vbscript&#58;", StringComparison.OrdinalIgnoreCase)
      .Replace("data:", "data&#58;", StringComparison.OrdinalIgnoreCase)

      // Event handlers
      .Replace("onload=", "onload&#61;", StringComparison.OrdinalIgnoreCase)
      .Replace("onerror=", "onerror&#61;", StringComparison.OrdinalIgnoreCase)
      .Replace("onclick=", "onclick&#61;", StringComparison.OrdinalIgnoreCase)
      .Replace("onmouseover=", "onmouseover&#61;", StringComparison.OrdinalIgnoreCase)
      .Replace("onmouseout=", "onmouseout&#61;", StringComparison.OrdinalIgnoreCase)
      .Replace("onfocus=", "onfocus&#61;", StringComparison.OrdinalIgnoreCase)
      .Replace("onblur=", "onblur&#61;", StringComparison.OrdinalIgnoreCase)
      .Replace("onchange=", "onchange&#61;", StringComparison.OrdinalIgnoreCase)
      .Replace("onsubmit=", "onsubmit&#61;", StringComparison.OrdinalIgnoreCase)

      // Dangerous HTML tags
      .Replace("<iframe", "&lt;iframe", StringComparison.OrdinalIgnoreCase)
      .Replace("</iframe>", "&lt;/iframe&gt;", StringComparison.OrdinalIgnoreCase)
      .Replace("<object", "&lt;object", StringComparison.OrdinalIgnoreCase)
      .Replace("</object>", "&lt;/object&gt;", StringComparison.OrdinalIgnoreCase)
      .Replace("<embed", "&lt;embed", StringComparison.OrdinalIgnoreCase)
      .Replace("</embed>", "&lt;/embed&gt;", StringComparison.OrdinalIgnoreCase)
      .Replace("<img", "&lt;img", StringComparison.OrdinalIgnoreCase)
      .Replace("<form", "&lt;form", StringComparison.OrdinalIgnoreCase)
      .Replace("</form>", "&lt;/form&gt;", StringComparison.OrdinalIgnoreCase)
      .Replace("<input", "&lt;input", StringComparison.OrdinalIgnoreCase)
      .Replace("<textarea", "&lt;textarea", StringComparison.OrdinalIgnoreCase)
      .Replace("</textarea>", "&lt;/textarea&gt;", StringComparison.OrdinalIgnoreCase);
  }

  /// <summary>
  /// Remove or encode template injection patterns
  /// </summary>
  /// <param name="input">Raw user input</param>
  /// <returns>Input with template injection patterns neutralized</returns>
  private string SanitizeTemplateInjection(string input)
  {
    if (string.IsNullOrEmpty(input))
      return input;

    return input
      // Template expressions
      .Replace("{{", "&#123;&#123;")
      .Replace("}}", "&#125;&#125;")
      .Replace("${", "&#36;&#123;")
      .Replace("#{", "&#35;&#123;")

      // JNDI injection patterns
      .Replace("${jndi:", "&#36;&#123;jndi&#58;", StringComparison.OrdinalIgnoreCase)
      .Replace("${ldap:", "&#36;&#123;ldap&#58;", StringComparison.OrdinalIgnoreCase)
      .Replace("${rmi:", "&#36;&#123;rmi&#58;", StringComparison.OrdinalIgnoreCase)
      .Replace("${dns:", "&#36;&#123;dns&#58;", StringComparison.OrdinalIgnoreCase);
  }

  /// <summary>
  /// Remove or encode PHP tags
  /// </summary>
  /// <param name="input">Raw user input</param>
  /// <returns>Input with PHP tags neutralized</returns>
  private string SanitizePhpTags(string input)
  {
    if (string.IsNullOrEmpty(input))
      return input;

    return input
      .Replace("<?php", "&lt;&#63;php", StringComparison.OrdinalIgnoreCase)
      .Replace("<?=", "&lt;&#63;&#61;")
      .Replace("<?", "&lt;&#63;")
      .Replace("?>", "&#63;&gt;");
  }
}
