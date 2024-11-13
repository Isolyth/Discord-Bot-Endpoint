using Discord;
using Discord.WebSocket;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DiscordBotEndpoint;

public class MessageRequest
{
    [JsonPropertyName("target")]
    public string? Target { get; set; }
    
    [JsonPropertyName("userId")]
    public ulong UserId { get; set; }
    
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("embed")]
    public EmbedRequest? Embed { get; set; }
}

public class EmbedRequest
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("fields")]
    public List<EmbedFieldRequest>? Fields { get; set; }

    [JsonPropertyName("color")]
    public uint? Color { get; set; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }
}

public class EmbedFieldRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";

    [JsonPropertyName("inline")]
    public bool Inline { get; set; }
}

public class Program
{
    private static DiscordSocketClient? _discordClient;
    private static bool _isReady = false;

    public static async Task Main(string[] args)
    {
        try
        {
            Console.WriteLine("Starting Discord bot endpoint...");

            // Get configuration from environment variables
            var botToken = Environment.GetEnvironmentVariable("DISCORD_TOKEN");

            if (string.IsNullOrEmpty(botToken))
            {
                throw new Exception("DISCORD_TOKEN environment variable must be set");
            }

            Console.WriteLine("Configuration loaded successfully");

            // Configure Discord client with minimal intents
            _discordClient = new DiscordSocketClient(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.DirectMessages,
                LogLevel = LogSeverity.Debug
            });

            // Add logging handler
            _discordClient.Log += LogAsync;

            _discordClient.Ready += () =>
            {
                _isReady = true;
                Console.WriteLine("Discord bot is ready!");
                return Task.CompletedTask;
            };

            Console.WriteLine("Logging in to Discord...");
            await _discordClient.LoginAsync(TokenType.Bot, botToken);
            Console.WriteLine("Starting Discord client...");
            await _discordClient.StartAsync();

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

            // Configure HTTP listener
            var listener = new HttpListener();
            listener.Prefixes.Add("http://+:80/");
            listener.Start();

            Console.WriteLine("Server listening on port 80");

            while (true)
            {
                var context = await listener.GetContextAsync();
                _ = HandleRequestAsync(context);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex}");
            throw;
        }
    }

    private static Task LogAsync(LogMessage log)
    {
        Console.WriteLine(log.ToString());
        return Task.CompletedTask;
    }

    private static async Task HandleRequestAsync(HttpListenerContext context)
    {
        try
        {
            if (_discordClient == null || !_isReady)
            {
                await SendResponseAsync(context.Response, 503, "Discord client is not ready");
                return;
            }

            if (context.Request.HttpMethod != "POST")
            {
                await SendResponseAsync(context.Response, 405, "Method not allowed");
                return;
            }

            // Read request body
            using var reader = new StreamReader(context.Request.InputStream);
            var body = await reader.ReadToEndAsync();
            Console.WriteLine($"Received request body: {body}");
            
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            var messageRequest = JsonSerializer.Deserialize<MessageRequest>(body, options);
            if (messageRequest == null)
            {
                await SendResponseAsync(context.Response, 400, "Invalid request format");
                return;
            }

            Console.WriteLine($"Parsed request - Target: {messageRequest.Target}, UserId: {messageRequest.UserId}");
            Console.WriteLine($"Embed present: {messageRequest.Embed != null}");

            if (string.IsNullOrEmpty(messageRequest.Target) || messageRequest.Target.ToLower() != "user")
            {
                await SendResponseAsync(context.Response, 400, $"Only 'user' target is supported at this time. Received target: '{messageRequest.Target}'");
                return;
            }

            var user = await _discordClient.GetUserAsync(messageRequest.UserId);
            if (user == null)
            {
                await SendResponseAsync(context.Response, 404, "User not found");
                return;
            }

            Console.WriteLine($"Found user: {user.Username}");

            if (messageRequest.Embed != null)
            {
                Console.WriteLine("Building embed...");
                var embed = new EmbedBuilder();
                
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
