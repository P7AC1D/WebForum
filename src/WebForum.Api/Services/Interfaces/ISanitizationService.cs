namespace WebForum.Api.Services.Interfaces;

/// <summary>
/// Service interface for input sanitization to prevent XSS and injection attacks
/// </summary>
public interface ISanitizationService
{
  /// <summary>
  /// Sanitize user input to prevent XSS and other injection attacks
  /// </summary>
  /// <param name="input">Raw user input</param>
  /// <returns>Sanitized input with dangerous content encoded</returns>
  string SanitizeInput(string input);

  /// <summary>
  /// Sanitize HTML content while preserving safe tags
  /// </summary>
  /// <param name="htmlInput">Raw HTML input</param>
  /// <returns>Sanitized HTML with only safe tags preserved</returns>
  string SanitizeHtml(string htmlInput);

  /// <summary>
  /// Remove or encode SQL injection patterns
  /// </summary>
  /// <param name="input">Raw user input</param>
  /// <returns>Input with SQL injection patterns neutralized</returns>
  string SanitizeSql(string input);

  /// <summary>
  /// Remove or encode script injection patterns (XSS)
  /// </summary>
  /// <param name="input">Raw user input</param>
  /// <returns>Input with script injection patterns neutralized</returns>
  string SanitizeScripts(string input);
}
