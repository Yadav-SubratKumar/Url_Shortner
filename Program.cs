using System.Collections.Generic;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// In-memory storage for URL mappings
var urlMappings = new Dictionary<string, string>();
var requestCounts = new Dictionary<string, int>();

// Middleware
app.UseRouting();
app.UseCors("AllowAll");
app.UseStaticFiles();

// API Endpoints

// GET: Home page
app.MapGet("/", async (HttpContext context) =>
{
    var html = @"
    <!DOCTYPE html>
    <html lang='en'>
    <head>
        <meta charset='UTF-8'>
        <meta name='viewport' content='width=device-width, initial-scale=1.0'>
        <title>URL Shortener</title>
        <style>
            * {
                margin: 0;
                padding: 0;
                box-sizing: border-box;
            }

            body {
                font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
                min-height: 100vh;
                display: flex;
                justify-content: center;
                align-items: center;
                padding: 20px;
            }

            .container {
                background: white;
                border-radius: 15px;
                box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
                padding: 40px;
                max-width: 500px;
                width: 100%;
            }

            .header {
                text-align: center;
                margin-bottom: 40px;
            }

            .header h1 {
                color: #333;
                font-size: 32px;
                margin-bottom: 10px;
            }

            .header p {
                color: #666;
                font-size: 16px;
            }

            .form-group {
                margin-bottom: 20px;
            }

            label {
                display: block;
                color: #333;
                font-weight: 600;
                margin-bottom: 8px;
                font-size: 14px;
            }

            input {
                width: 100%;
                padding: 12px 15px;
                border: 2px solid #e0e0e0;
                border-radius: 8px;
                font-size: 14px;
                transition: border-color 0.3s;
            }

            input:focus {
                outline: none;
                border-color: #667eea;
            }

            button {
                width: 100%;
                padding: 12px;
                background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
                color: white;
                border: none;
                border-radius: 8px;
                font-size: 16px;
                font-weight: 600;
                cursor: pointer;
                transition: transform 0.2s, box-shadow 0.2s;
            }

            button:hover {
                transform: translateY(-2px);
                box-shadow: 0 10px 20px rgba(102, 126, 234, 0.4);
            }

            button:active {
                transform: translateY(0);
            }

            #result {
                margin-top: 30px;
                padding: 20px;
                background: #f8f9fa;
                border-radius: 8px;
                display: none;
            }

            #result.show {
                display: block;
                animation: slideIn 0.3s ease;
            }

            @keyframes slideIn {
                from {
                    opacity: 0;
                    transform: translateY(-10px);
                }
                to {
                    opacity: 1;
                    transform: translateY(0);
                }
            }

            .result-title {
                color: #666;
                font-size: 12px;
                font-weight: 600;
                text-transform: uppercase;
                margin-bottom: 8px;
            }

            .short-url {
                background: white;
                padding: 12px 15px;
                border-radius: 6px;
                border: 2px solid #667eea;
                margin-bottom: 12px;
                text-align: center;
                gap: 10px;
            }

            .short-url-text {
                color: #667eea;
                font-weight: 600;
                font-size: 14px;
                word-break: break-all;
                margin-bottom: 0.5rem;
                flex: 1;
            }

            .copy-btn {
                size: 25%;
                background: #667eea;
                color: white;
                border: none;
                padding: 8px 16px;
                border-radius: 6px;
                cursor: pointer;
                font-size: 12px;
                font-weight: 600;
                white-space: nowrap;
            }

            .copy-btn:hover {
                background: #764ba2;
            }

            .copy-btn.copied {
                background: #4CAF50;
            }

            .error {
                color: #d32f2f;
                font-size: 14px;
                margin-top: 10px;
                display: none;
            }

            .error.show {
                display: block;
            }

            .stats {
                text-align: center;
                margin-top: 20px;
                padding-top: 20px;
                border-top: 1px solid #e0e0e0;
                color: #666;
                font-size: 12px;
            }

            .stats-number {
                font-size: 24px;
                font-weight: 700;
                color: #667eea;
                margin: 5px 0;
            }

            .loading {
                display: none;
                text-align: center;
                color: #667eea;
                margin-top: 10px;
            }
        </style>
    </head>
    <body>
        <div class='container'>
            <div class='header'>
                <h1>🔗 URL Shortener</h1>
                <p>Make your URLs shorter and sweeter</p>
            </div>

            <form id='urlForm'>
                <div class='form-group'>
                    <label for='longUrl'>Enter Long URL:</label>
                    <input 
                        type='url' 
                        id='longUrl' 
                        name='longUrl' 
                        placeholder='https://example.com/very/long/path/with/parameters' 
                        required>
                </div>
                <div class='loading' id='loading'>Processing...</div>
                <button type='submit'>Shorten URL</button>
                <div class='error' id='error'></div>
            </form>

            <div id='result'>
                <div class='result-title'>Your Short URL:</div>
                <div class='short-url'>
                    <span class='short-url-text' id='shortUrlText'></span>
                    <button class='copy-btn' id='copyBtn' type='button'>Copy</button>
                </div>
                <div class='stats'>
                    <div>URLs Created: <div class='stats-number' id='statsCount'>0</div></div>
                </div>
            </div>
        </div>

        <script>
            const form = document.getElementById('urlForm');
            const longUrlInput = document.getElementById('longUrl');
            const resultDiv = document.getElementById('result');
            const shortUrlText = document.getElementById('shortUrlText');
            const copyBtn = document.getElementById('copyBtn');
            const errorDiv = document.getElementById('error');
            const loadingDiv = document.getElementById('loading');
            const statsCount = document.getElementById('statsCount');

            form.addEventListener('submit', async (e) => {
                e.preventDefault();
                
                const longUrl = longUrlInput.value.trim();
                
                if (!longUrl) {
                    showError('Please enter a valid URL');
                    return;
                }

                loadingDiv.style.display = 'block';
                errorDiv.classList.remove('show');

                try {
                    const response = await fetch('/api/shorten', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                        },
                        body: JSON.stringify({ longUrl: longUrl })
                    });

                    loadingDiv.style.display = 'none';

                    if (!response.ok) {
                        const error = await response.json();
                        showError(error.message || 'Failed to shorten URL');
                        return;
                    }

                    const data = await response.json();
                    displayResult(data.shortUrl, data.totalUrls);
                    longUrlInput.value = '';

                } catch (error) {
                    loadingDiv.style.display = 'none';
                    showError('Error: ' + error.message);
                }
            });

            copyBtn.addEventListener('click', () => {
                const url = shortUrlText.textContent;
                navigator.clipboard.writeText(url).then(() => {
                    copyBtn.textContent = '✓ Copied!';
                    copyBtn.classList.add('copied');
                    setTimeout(() => {
                        copyBtn.textContent = 'Copy';
                        copyBtn.classList.remove('copied');
                    }, 2000);
                });
            });

            function displayResult(shortUrl, totalUrls) {
                shortUrlText.textContent = shortUrl;
                statsCount.textContent = totalUrls;
                resultDiv.classList.add('show');
            }

            function showError(message) {
                errorDiv.textContent = message;
                errorDiv.classList.add('show');
            }
        </script>
    </body>
    </html>
    ";
    
    await context.Response.WriteAsync(html);
});

// POST: Create shortened URL
app.MapPost("/api/shorten", (ShortenRequest request, HttpRequest httpRequest) =>
{
    if (string.IsNullOrWhiteSpace(request.LongUrl))
    {
        return Results.BadRequest(new { message = "URL cannot be empty" });
    }

    string shortCode = GenerateShortCode();

    while (urlMappings.ContainsKey(shortCode))
    {
        shortCode = GenerateShortCode();
    }

    urlMappings[shortCode] = request.LongUrl;
    requestCounts[shortCode] = 0;

    string shortUrl =
        $"{httpRequest.Scheme}://{httpRequest.Host}/r/{shortCode}";

    return Results.Ok(new
    {
        shortUrl,
        totalUrls = urlMappings.Count
    });
});
// GET: Redirect to original URL
app.MapGet("/r/{shortCode}", (string shortCode) =>
{
    if (urlMappings.TryGetValue(shortCode, out var originalUrl))
    {
        requestCounts[shortCode]++;
        return Results.Redirect(originalUrl);
    }

    return Results.NotFound("Short URL not found! 🔍");
});

// GET: Get statistics
app.MapGet("/api/stats", (HttpContext context) =>
{
    var stats = urlMappings.Select(kvp => new
    {
        shortCode = kvp.Key,
        originalUrl = kvp.Value,
        clicks = requestCounts.TryGetValue(kvp.Key, out var count) ? count : 0
    }).ToList();

    return stats;
});

// GET: Get all URLs (for debugging)
app.MapGet("/api/urls", () =>
{
    return new { totalUrls = urlMappings.Count, urls = urlMappings };
});

app.Run();

// Helper function to generate random short code
string GenerateShortCode()
{
    const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    var random = new Random();
    return new string(Enumerable.Range(0, 6)
        .Select(_ => chars[random.Next(chars.Length)])
        .ToArray());
}

// Request model
public class ShortenRequest
{
    public required string LongUrl { get; set; }
}
