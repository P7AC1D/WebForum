using System.Text.Json;
using System.Text.Json.Serialization;
using WebForum.Api.Models;

namespace WebForum.Api.Converters;

/// <summary>
/// Custom JSON converter for UserRoles enum that supports both string and integer values
/// </summary>
public class UserRolesJsonConverter : JsonConverter<UserRoles?>
{
  public override UserRoles? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    switch (reader.TokenType)
    {
      case JsonTokenType.String:
        var stringValue = reader.GetString();
        if (string.IsNullOrEmpty(stringValue))
          return null;

        // Try case-insensitive enum parsing
        if (Enum.TryParse<UserRoles>(stringValue, ignoreCase: true, out var enumValue))
          return enumValue;

        throw new JsonException($"Invalid role value: '{stringValue}'. Valid values are: {string.Join(", ", Enum.GetNames<UserRoles>())}");

      case JsonTokenType.Number:
        var intValue = reader.GetInt32();
        if (Enum.IsDefined(typeof(UserRoles), intValue))
          return (UserRoles)intValue;

        throw new JsonException($"Invalid role value: {intValue}. Valid values are: 0 (User), 1 (Moderator)");

      case JsonTokenType.Null:
        return null;

      default:
        throw new JsonException($"Invalid token type for UserRoles: {reader.TokenType}");
    }
  }

  public override void Write(Utf8JsonWriter writer, UserRoles? value, JsonSerializerOptions options)
  {
    if (value == null)
    {
      writer.WriteNullValue();
    }
    else
    {
      // Always write as string for consistency
      writer.WriteStringValue(value.ToString());
    }
  }
}
