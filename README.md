# Discord Bot Endpoint

A .NET-based HTTP endpoint that allows other programs to send messages through a Discord bot to users.

## Features

- Send plain text messages to Discord users
- Send rich embed messages with customizable fields
- Simple HTTP POST interface
- Docker support for easy deployment

## Quick Start

```bash
# Pull and run with Docker
docker run -d \
  --name discord-bot \
  -p 5551:80 \
  -e DISCORD_TOKEN=your-token-here \
  isolyth/discord-bot-endpoint:latest
```

## Docker Compose

1. Create a docker-compose.yml:
```yaml
version: '3.8'

services:
  discord-bot:
    image: isolyth/discord-bot-endpoint:latest
    container_name: discord-bot
    environment:
      - DISCORD_TOKEN=your-token-here
    ports:
      - "5551:80"
    restart: unless-stopped
```

2. Run with docker-compose:
```bash
docker-compose up -d
```

## API Documentation

### Endpoint Details

- **URL**: `http://localhost:5551`
- **Method**: POST
- **Content-Type**: application/json

### Request Schema

```json
{
    "target": "user",           // Required: Currently only "user" is supported
    "userId": 123456789,        // Required: Discord user ID (numeric)
    "message": "Hello world",   // Optional: Plain text message
    "embed": {                  // Optional: Rich embed object
        "title": "Title here",
        "description": "Main content here",
        "color": 4144959,      // Decimal color value
        "timestamp": "2024-01-24T11:21:00Z", // ISO 8601 timestamp
        "fields": [
            {
                "name": "Field Title",
                "value": "Field content",
                "inline": false
            }
        ]
    }
}
```

### Notes:
- Either `message` or `embed` must be provided
- `userId` must be a numeric value (not a string)
- All embed fields are optional

### Examples

#### Sending a Simple Message

```bash
curl -X POST http://localhost:5551 \
  -H "Content-Type: application/json" \
  -d '{
    "target": "user",
    "userId": 123456789,
    "message": "Hello from the Discord bot!"
  }'
```

#### Sending an Embed Message

```bash
curl -X POST http://localhost:5551 \
  -H "Content-Type: application/json" \
  -d '{
    "target": "user",
    "userId": 123456789,
    "embed": {
      "title": "Important Notification",
      "description": "This is the main content of the embed",
      "fields": [
        {
          "name": "Status",
          "value": "Active",
          "inline": false
        }
      ],
      "color": 4144959,
      "timestamp": "2024-01-24T11:21:00Z"
    }
  }'
```

### Response Codes

- **200**: Message sent successfully
- **400**: Invalid request format or parameters
- **404**: User not found
- **405**: Method not allowed (only POST is supported)
- **500**: Server error
- **503**: Discord client is not ready

### Response Format

```json
{
    "message": "Status message here"
}
```

## Building from Source

If you want to build the image yourself:

1. Clone the repository
2. Build the Docker image:
```bash
docker build -t discord-bot-endpoint .
```

## License

This project is licensed under the GNU General Public License v3.0 - see the [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
