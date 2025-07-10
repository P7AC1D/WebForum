using System.Text.Json;
using WebForum.IntegrationTests.Infrastructure;

namespace WebForum.IntegrationTests.Utilities;

/// <summary>
/// HTTP utilities for making API requests in tests
/// </summary>
public static class HttpUtilities
{
  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
  };

  /// <summary>
  /// Makes a GET request and deserializes the response
  /// </summary>
  /// <typeparam name="T">Response type</typeparam>
  /// <param name="client">HTTP client</param>
  /// <param name="url">Request URL</param>
  /// <returns>Deserialized response</returns>
  public static async Task<T> GetAsync<T>(HttpClient client, string url)
  {
    var response = await client.GetAsync(url);
    response.EnsureSuccessStatusCode();
    var content = await response.Content.ReadAsStringAsync();
    return JsonSerializer.Deserialize<T>(content, JsonOptions)!;
  }

  /// <summary>
  /// Makes a POST request with JSON payload and deserializes the response
  /// </summary>
  /// <typeparam name="TRequest">Request type</typeparam>
  /// <typeparam name="TResponse">Response type</typeparam>
  /// <param name="client">HTTP client</param>
  /// <param name="url">Request URL</param>
  /// <param name="payload">Request payload</param>
  /// <returns>Deserialized response</returns>
  public static async Task<TResponse> PostAsync<TRequest, TResponse>(
      HttpClient client,
      string url,
      TRequest payload)
  {
    var json = JsonSerializer.Serialize(payload, JsonOptions);
    var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

    var response = await client.PostAsync(url, content);
    response.EnsureSuccessStatusCode();

    var responseContent = await response.Content.ReadAsStringAsync();
    return JsonSerializer.Deserialize<TResponse>(responseContent, JsonOptions)!;
  }

  /// <summary>
  /// Makes a POST request with JSON payload
  /// </summary>
  /// <typeparam name="T">Request type</typeparam>
  /// <param name="client">HTTP client</param>
  /// <param name="url">Request URL</param>
  /// <param name="payload">Request payload</param>
  /// <returns>HTTP response</returns>
  public static async Task<HttpResponseMessage> PostAsync<T>(
      HttpClient client,
      string url,
      T payload)
  {
    var json = JsonSerializer.Serialize(payload, JsonOptions);
    var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
    return await client.PostAsync(url, content);
  }

  /// <summary>
  /// Makes a PUT request with JSON payload
  /// </summary>
  /// <typeparam name="T">Request type</typeparam>
  /// <param name="client">HTTP client</param>
  /// <param name="url">Request URL</param>
  /// <param name="payload">Request payload</param>
  /// <returns>HTTP response</returns>
  public static async Task<HttpResponseMessage> PutAsync<T>(
      HttpClient client,
      string url,
      T payload)
  {
    var json = JsonSerializer.Serialize(payload, JsonOptions);
    var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
    return await client.PutAsync(url, content);
  }

  /// <summary>
  /// Makes a DELETE request
  /// </summary>
  /// <param name="client">HTTP client</param>
  /// <param name="url">Request URL</param>
  /// <returns>HTTP response</returns>
  public static async Task<HttpResponseMessage> DeleteAsync(HttpClient client, string url)
  {
    return await client.DeleteAsync(url);
  }

  /// <summary>
  /// Creates an authenticated HTTP client for testing
  /// </summary>
  /// <param name="factory">Test factory</param>
  /// <param name="userId">User ID</param>
  /// <param name="username">Username</param>
  /// <param name="roles">User roles</param>
  /// <returns>Authenticated HTTP client</returns>
  public static HttpClient CreateAuthenticatedClient(
      WebForumTestFactory factory,
      int userId,
      string username,
      WebForum.Api.Models.UserRoles roles = WebForum.Api.Models.UserRoles.User)
  {
    return TestAuthenticationHelper.CreateAuthenticatedClient(factory, userId, username, roles);
  }

  /// <summary>
  /// Deserializes response content to specified type
  /// </summary>
  /// <typeparam name="T">Type to deserialize to</typeparam>
  /// <param name="response">HTTP response</param>
  /// <returns>Deserialized object</returns>
  public static async Task<T> ReadAsAsync<T>(HttpResponseMessage response)
  {
    var content = await response.Content.ReadAsStringAsync();
    return JsonSerializer.Deserialize<T>(content, JsonOptions)!;
  }

  /// <summary>
  /// Creates JSON string content for HTTP requests
  /// </summary>
  /// <param name="obj">Object to serialize</param>
  /// <returns>String content with JSON</returns>
  public static StringContent CreateJsonContent(object obj)
  {
    var json = JsonSerializer.Serialize(obj, JsonOptions);
    return new StringContent(json, System.Text.Encoding.UTF8, "application/json");
  }
}
