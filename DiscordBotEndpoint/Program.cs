using Discord;
using Discord.WebSocket;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DiscordBotEndpoint;

/// <summary>
/// Represents an incoming message request from the HTTP endpoint.
/// This class defines the structure of JSON payloads that clients should send.
/// </summary>
public class MessageRequest
{
    /// <summary>
    /// The target type for the message. Currently only supports "user".
    /// </summary>
    [JsonPropertyName("target")]
    public string? Target { get; set; }
    
    /// <summary>
    /// The Discord user ID to send the message to.
    /// </summary>
    [JsonPropertyName("userId")]
    public ulong UserId { get; set; }
    
    /// <summary>
    /// The text content of the message to send.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    /// <summary>
    /// Optional embedded rich content to include with the message.
    /// </summary>
    [JsonPropertyName("embed")]
    public EmbedRequest? Embed { get; set; }
}

/// <summary>
/// Represents the structure of a Discord embed message.
/// Embeds are rich content containers that can include formatted text, fields, and styling.
/// </summary>
public class EmbedRequest
{
    /// <summary>
    /// The title of the embed.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// The main content/description of the embed.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// A collection of fields to display in the embed.
    /// </summary>
    [JsonPropertyName("fields")]
    public List<EmbedFieldRequest>? Fields { get; set; }

    /// <summary>
    /// The color of the embed's left border (in decimal format).
    /// </summary>
    [JsonPropertyName("color")]
    public uint? Color { get; set; }

    /// <summary>
    /// ISO 8601 timestamp to display in the embed.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }
}

/// <summary>
/// Represents a field within a Discord embed.
/// Fields are named sections that can be displayed in columns.
/// </summary>
public class EmbedFieldRequest
{
    /// <summary>
    /// The name/title of the field.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>
    /// The content of the field.
    /// </summary>
    [JsonPropertyName("value")]
    public string Value { get; set; } = "";

    /// <summary>
    /// Whether the field should be displayed inline with other fields.
    /// </summary>
    [JsonPropertyName("inline")]
    public bool Inline { get; set; }
}

/// <summary>
/// Main program class that handles Discord bot initialization and HTTP endpoint setup.
/// This service allows other applications to send Discord messages through an HTTP API.
/// </summary>
public class Program
{
    // Static references to maintain Discord client state
    private static DiscordSocketClient? _discordClient;
    private static bool _isReady = false;

    /// <summary>
    /// Entry point of the application. Initializes Discord bot and HTTP listener.
    /// </summary>
    public static async Task Main(string[] args)
    {
        try
        {
            Console.WriteLine("Starting Discord bot endpoint...");

            // Load Discord bot token from environment variables
            var botToken = Environment.GetEnvironmentVariable("DISCORD_TOKEN");

            if (string.IsNullOrEmpty(botToken))
            {
                throw new Exception("DISCORD_TOKEN environment variable must be set");
            }

            Console.WriteLine("Configuration loaded successfully");

            // Initialize Discord client with minimal permissions
            // Only requesting DirectMessages intent for security
            _discordClient = new DiscordSocketClient(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.DirectMessages,
                LogLevel = LogSeverity.Debug
            });

            // Setup event handlers
            _discordClient.Log += LogAsync;

            // Set ready flag when Discord connection is established
            _discordClient.Ready += () =>
            {
                _isReady = true;
                Console.WriteLine("Discord bot is ready!");
                return Task.CompletedTask;
            };

            // Connect to Discord
            Console.WriteLine("Logging in to Discord...");
            await _discordClient.LoginAsync(TokenType.Bot, botToken);
            Console.WriteLine("Starting Discord client...");
            await _discordClient.StartAsync();

            // Wait for Discord connection with timeout
            Console.WriteLine("Waiting for Discord client to be ready...");
            var timeout = TimeSpan.FromSeconds(30);
            var startTime = DateTime.UtcNow;

            while (!_isReady)
            {
                if (DateTime.UtcNow - startTime > timeout)
                {
                    throw new TimeoutException("Discord client failed to become ready within 30 seconds");
                }
                await Task.Delay(100);
            }

            // Setup HTTP endpoint
            var listener = new HttpListener();
            listener.Prefixes.Add("http://+:80/");
            listener.Start();

            Console.WriteLine("Server listening on port 80");

            // Main request handling loop
            while (true)
            {
                var context = await listener.GetContextAsync();
                _ = HandleRequestAsync(context);  // Handle requests asynchronously
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Handles Discord client logging by writing messages to console.
    /// </summary>
    private static Task LogAsync(LogMessage log)
    {
        Console.WriteLine(log.ToString());
        return Task.CompletedTask;
    }

    /// <summary>
    /// Processes incoming HTTP requests and sends Discord messages accordingly.
    /// Validates request format, finds target user, and sends either regular or embed messages.
    /// </summary>
    private static async Task HandleRequestAsync(HttpListenerContext context)
    {
        try
        {
            // Verify Discord client is ready
            if (_discordClient == null || !_isReady)
            {
                await SendResponseAsync(context.Response, 503, "Discord client is not ready");
                return;
            }

            // Only accept POST requests
            if (context.Request.HttpMethod != "POST")
            {
                await SendResponseAsync(context.Response, 405, "Method not allowed");
                return;
            }

            // Parse request body
            using var reader = new StreamReader(context.Request.InputStream);
            var body = await reader.ReadToEndAsync();
            Console.WriteLine($"Received request body: {body}");
            
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            // Deserialize and validate request
            var messageRequest = JsonSerializer.Deserialize<MessageRequest>(body, options);
            if (messageRequest == null)
            {
                await SendResponseAsync(context.Response, 400, "Invalid request format");
                return;
            }

            Console.WriteLine($"Parsed request - Target: {messageRequest.Target}, UserId: {messageRequest.UserId}");
            Console.WriteLine($"Embed present: {messageRequest.Embed != null}");

            // Validate target type (currently only supporting user messages)
            if (string.IsNullOrEmpty(messageRequest.Target) || messageRequest.Target.ToLower() != "user")
            {
                await SendResponseAsync(context.Response, 400, $"Only 'user' target is supported at this time. Received target: '{messageRequest.Target}'");
                return;
            }

            // Find target Discord user
            var user = await _discordClient.GetUserAsync(messageRequest.UserId);
            if (user == null)
            {
                await SendResponseAsync(context.Response, 404, "User not found");
                return;
            }

            Console.WriteLine($"Found user: {user.Username}");

            // Handle embed messages
            if (messageRequest.Embed != null)
            {
                Console.WriteLine("Building embed...");
                var embed = new EmbedBuilder();
                
                // Add embed components if provided
                if (!string.IsNullOrEmpty(messageRequest.Embed.Title))
                {
                    embed.WithTitle(messageRequest.Embed.Title);
                    Console.WriteLine($"Added title: {messageRequest.Embed.Title}");
                }
                
                if (!string.IsNullOrEmpty(messageRequest.Embed.Description))
                {
                    embed.WithDescription(messageRequest.Embed.Description);
                    Console.WriteLine($"Added description: {messageRequest.Embed.Description}");
                }

                if (messageRequest.Embed.Color.HasValue)
                {
                    embed.WithColor(new Color(messageRequest.Embed.Color.Value));
                    Console.WriteLine($"Added color: {messageRequest.Embed.Color.Value}");
                }

                if (!string.IsNullOrEmpty(messageRequest.Embed.Timestamp))
                {
                    if (DateTime.TryParse(messageRequest.Embed.Timestamp, out var timestamp))
                    {
                        embed.WithTimestamp(timestamp);
                        Console.WriteLine($"Added timestamp: {timestamp}");
                    }
                }

                if (messageRequest.Embed.Fields != null)
                {
                    foreach (var field in messageRequest.Embed.Fields)
                    {
                        embed.AddField(field.Name, field.Value, field.Inline);
                        Console.WriteLine($"Added field - Name: {field.Name}, Value: {field.Value}, Inline: {field.Inline}");
                    }
                }

                Console.WriteLine("Sending message with embed...");
                await user.SendMessageAsync(embed: embed.Build());
            }
            // Handle regular text messages
            else if (!string.IsNullOrEmpty(messageRequest.Message))
            {
                Console.WriteLine($"Sending regular message: {messageRequest.Message}");
                await user.SendMessageAsync(messageRequest.Message);
            }

            await SendResponseAsync(context.Response, 200, "Message sent successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex}");
            await SendResponseAsync(context.Response, 500, $"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Sends an HTTP response with the specified status code and message.
    /// Response is formatted as JSON with a message field.
    /// </summary>
    private static async Task SendResponseAsync(HttpListenerResponse response, int statusCode, string message)
    {
        response.StatusCode = statusCode;
        response.ContentType = "application/json";
        
        var responseObj = new { message = message };
        var jsonResponse = JsonSerializer.Serialize(responseObj);
        var buffer = System.Text.Encoding.UTF8.GetBytes(jsonResponse);
        
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer);
        response.Close();
    }
}
