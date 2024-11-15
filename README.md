![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/isolyth/Discord-Bot-Endpoint/.github%2Fworkflows%2Fdocker-publish.yml) ![Docker Pulls](https://img.shields.io/docker/pulls/isolyth/discord-bot-endpoint) ![GitHub License](https://img.shields.io/github/license/isolyth/discord-bot-endpoint)


# Discord Bot Endpoint

A .NET-based HTTP endpoint that allows other programs to send messages through a Discord bot to users.

## Features

- Send plain text messages to Discord users
- Send rich embed messages with customizable fields
- Simple HTTP POST interface
- Docker support for easy deployment

## Getting Started

### Prerequisites

- Docker (optional, for containerized deployment)
- Discord bot token
- .NET 8.0 SDK (for local development)

### Docker Deployment

1. Build the image:
```bash
docker build -t discord-bot-endpoint .
```

2. Run the container:
```bash
docker run -d \
  --name discord-bot \
  -p PORT:80 \
  -e DISCORD_TOKEN=your-token-here \
  isolyth/discord-bot-endpoint:latest
```

### Docker Compose Deployment

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
      - "PORT:80" //Change PORT to whatever port you would this to be accessible at
    restart: unless-stopped
```

2. Run with docker-compose:
```bash
docker-compose up -d
```

### Local Development

1. Restore dependencies:
```bash
dotnet restore
```

2. Run the application:
```bash
dotnet run
```

## API Documentation

### Endpoint Details
- **URL**: `http://localhost:PORT`
- **Method**: POST
- **Content-Type**: application/json

### Request Schema
```json
{
  "target": "user",  // Required: Currently only "user" is supported
  "userId": 123456789,  // Required: Discord user ID (numeric)
  "message": "Hello world",  // Optional: Plain text message
  "embed": {  // Optional: Rich embed object
    "title": "Title here",
    "description": "Main content here",
    "color": 4144959,  // Decimal color value
    "timestamp": "2024-01-24T11:21:00Z",  // ISO 8601 timestamp
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

### Example Requests

#### Sending a Simple Message
```bash
curl -X POST http://localhost:PORT \
  -H "Content-Type: application/json" \
  -d '{
    "target": "user",
    "userId": 123456789,
    "message": "Hello from the Discord bot!"
  }'
```

#### Sending an Embed Message
```bash
curl -X POST http://localhost:PORT \
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

## Configuration Options

### Environment Variables
- `DISCORD_TOKEN`: Your Discord bot token (required)

## GitHub Actions

The repository includes CI/CD workflows that:
1. Build and test the application
2. Create and publish Docker images
3. Run on pushes to main and pull requests

## Contributing

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to your fork
5. Create a Pull Request

### Useful things you can help with

1. I don't have much experience with docker, if you know of a way to make the image smaller, that would be great! The 80mb download isn't ideal
2. Test it! Make sure it works on your machines too!

## License

This project is licensed under the GNU General Public License v3.0 - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

This program has sections written by LLM. The code has been tested and verified to work (On my machine anyway). As always, excercise caution where LLMs are involved and don't rely on this program as your final line of knowing-about-things-happening.
