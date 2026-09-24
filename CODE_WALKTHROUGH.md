# Code Walkthrough - URL Shortener

This document explains every part of the URL Shortener application.

## 📚 Table of Contents
1. [Project Setup](#project-setup)
2. [In-Memory Storage](#in-memory-storage)
3. [API Endpoints](#api-endpoints)
4. [Frontend HTML/CSS/JS](#frontend-htmlcssjs)
5. [Helper Functions](#helper-functions)

---

## Project Setup

### UrlShortener.csproj
This XML file defines your project configuration:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

**What each line does:**
- `Sdk="Microsoft.NET.Sdk.Web"` - This is a web project, not a console app
- `net8.0` - Target the latest .NET version
- `Nullable>enable` - Enable nullable reference types (safer code)
- `ImplicitUsings` - Automatically includes common namespaces (less code to write)

### Program.cs - The Main File

```csharp
using System.Collections.Generic;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);
```

**Explanation:**
- `WebApplication.CreateBuilder()` - Creates a web app builder
- This sets up the host, configuration, logging, and dependency injection

### Adding Services

```csharp
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
```

**What this does:**
- `AddControllers()` - Adds MVC controller support (not needed for minimal APIs, but useful)
- `AddCors()` - Adds Cross-Origin Resource Sharing
  - Allows browser requests from different origins (localhost, different ports, etc.)
  - Our frontend can call the backend API without CORS errors

### Building the App

```csharp
var app = builder.Build();
```

**Creates the actual application instance** from the builder configuration.

### Middleware Setup

```csharp
app.UseRouting();
app.UseCors("AllowAll");
app.UseStaticFiles();
```

**What each middleware does:**
- `UseRouting()` - Enables routing (matching URLs to handlers)
- `UseCors("AllowAll")` - Enables CORS for all endpoints
- `UseStaticFiles()` - Serves static files (CSS, JS, images from `wwwroot` folder)

---

## In-Memory Storage

### The Dictionary

```csharp
var urlMappings = new Dictionary<string, string>();
var requestCounts = new Dictionary<string, int>();
```

**How it works:**

```
urlMappings:
┌─────────────────────────────────────────────────┐
│ Key (ShortCode) │ Value (OriginalUrl)          │
├─────────────────┼──────────────────────────────┤
│ "abc123"        │ "https://github.com/dotnet" │
│ "def456"        │ "https://google.com"        │
│ "ghi789"        │ "https://microsoft.com"     │
└─────────────────────────────────────────────────┘

requestCounts:
┌──────────────────────┐
│ Key    │ Value       │
├────────┼─────────────┤
│ "abc123" │ 5 (clicks) │
│ "def456" │ 2 (clicks) │
│ "ghi789" │ 0 (clicks) │
└──────────────────────┘
```

**Why two dictionaries?**
- `urlMappings` - Maps short codes to original URLs (primary data)
- `requestCounts` - Tracks how many times each link was clicked

---

## API Endpoints

### 1. GET / (Homepage)

```csharp
app.MapGet("/", async (HttpContext context) =>
{
    var html = @"...HTML content...";
    await context.Response.WriteAsync(html);
});
```

**What it does:**
- When user visits `http://localhost:5000/`
- Sends back the HTML, CSS, and JavaScript for the UI
- The `@` symbol means it's a verbatim string (can span multiple lines)

**Flow:**
```
Browser: GET /
  ↓
Server: Sends HTML file
  ↓
Browser: Renders the page
```

### 2. POST /api/shorten (Create Short URL)

```csharp
app.MapPost("/api/shorten", (HttpContext context, ShortenRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.LongUrl))
    {
        context.Response.StatusCode = 400;
        return new { message = "URL cannot be empty" };
    }
```

**Validation:**
- Checks if URL is empty
- Returns HTTP 400 (Bad Request) if invalid

```csharp
    string shortCode = GenerateShortCode();
    
    while (urlMappings.ContainsKey(shortCode))
    {
        shortCode = GenerateShortCode();
    }
```

**Generate Unique Code:**
- Creates a random 6-character code
- Checks if it already exists (unlikely but possible)
- If exists, generates another one
- This ensures all codes are unique

```csharp
    urlMappings[shortCode] = request.LongUrl;
    
    if (!requestCounts.ContainsKey(shortCode))
    {
        requestCounts[shortCode] = 0;
    }
```

**Store the Mapping:**
- Saves: `shortCode → originalUrl`
- Initializes click counter to 0

```csharp
    var protocol = context.Request.Scheme;
    var host = context.Request.Host;
    string shortUrl = $"{protocol}://{host}/r/{shortCode}";

    return new { shortUrl = shortUrl, totalUrls = urlMappings.Count };
});
```

**Generate Response:**
- Gets the protocol (http/https) from the request
- Gets the host (localhost:5000)
- Builds the short URL: `http://localhost:5000/r/abc123`
- Returns JSON with short URL and total URL count

**Example Flow:**
```
Frontend: POST /api/shorten
         { "longUrl": "https://github.com" }
  ↓
Server: Generate code, store mapping
        Return { "shortUrl": "http://localhost:5000/r/abc123" }
  ↓
Frontend: Display short URL
```

### 3. GET /r/{shortCode} (Redirect)

```csharp
app.MapGet("/r/{shortCode}", (string shortCode, HttpContext context) =>
{
    if (urlMappings.TryGetValue(shortCode, out var originalUrl))
    {
        if (requestCounts.ContainsKey(shortCode))
        {
            requestCounts[shortCode]++;
        }
        return Results.Redirect(originalUrl);
    }

    context.Response.StatusCode = 404;
    return Results.Text("Short URL not found! 🔍");
});
```

**What happens:**
- User clicks: `http://localhost:5000/r/abc123`
- Server looks up `abc123` in urlMappings
- If found:
  - Increments the click counter
  - Returns HTTP 302 redirect to original URL
  - Browser automatically follows redirect
- If not found:
  - Returns HTTP 404 (Not Found)
  - Shows error message

**Example:**
```
Browser: GET /r/abc123
  ↓
Server: Finds "abc123" → "https://github.com"
        Increments click count from 0 to 1
        Sends: HTTP 302 → https://github.com
  ↓
Browser: Automatically visits https://github.com
```

### 4. GET /api/stats (View Statistics)

```csharp
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
```

**What it does:**
- Loops through all URL mappings
- Creates an object for each with: code, original URL, and clicks
- Returns as JSON array

**Example Response:**
```json
[
  {
    "shortCode": "abc123",
    "originalUrl": "https://github.com",
    "clicks": 5
  },
  {
    "shortCode": "def456",
    "originalUrl": "https://google.com",
    "clicks": 2
  }
]
```

### 5. GET /api/urls (Debug Endpoint)

```csharp
app.MapGet("/api/urls", () =>
{
    return new { totalUrls = urlMappings.Count, urls = urlMappings };
});
```

**What it does:**
- Returns all stored URLs (for debugging)
- Shows count and the raw dictionary

---

## Frontend HTML/CSS/JS

### The HTML Form

```html
<form id='urlForm'>
    <div class='form-group'>
        <label for='longUrl'>Enter Long URL:</label>
        <input 
            type='url' 
            id='longUrl' 
            name='longUrl' 
            placeholder='https://example.com/...' 
            required>
    </div>
    <button type='submit'>Shorten URL</button>
</form>
```

**Elements:**
- `<input type='url'>` - Validates that input is a valid URL
- `required` - Browser won't let you submit empty
- Form with `id='urlForm'` - We'll reference this in JavaScript

### CSS Styling

```css
body {
    font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    min-height: 100vh;
    display: flex;
    justify-content: center;
    align-items: center;
}
```

**What this does:**
- Sets font for the whole page
- Creates a purple gradient background
- Centers content on screen using flexbox

### JavaScript - Handling the Form

```javascript
const form = document.getElementById('urlForm');
const longUrlInput = document.getElementById('longUrl');
const resultDiv = document.getElementById('result');

form.addEventListener('submit', async (e) => {
    e.preventDefault();  // Don't submit form normally
    
    const longUrl = longUrlInput.value.trim();
    
    if (!longUrl) {
        showError('Please enter a valid URL');
        return;
    }
```

**What happens:**
- Get references to HTML elements
- Listen for form submission
- `e.preventDefault()` - Don't reload page (use AJAX instead)
- Get the URL value and trim whitespace
- Validate it's not empty

### Making the API Call

```javascript
    const response = await fetch('/api/shorten', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify({ longUrl: longUrl })
    });

    if (!response.ok) {
        const error = await response.json();
        showError(error.message || 'Failed to shorten URL');
        return;
    }

    const data = await response.json();
    displayResult(data.shortUrl, data.totalUrls);
```

**Fetch Request:**
1. Send POST to `/api/shorten`
2. Body: `{ "longUrl": "..." }` in JSON format
3. Server responds with JSON
4. If successful: Display the short URL
5. If failed: Show error message

**Network Flow:**
```
Frontend (JavaScript):
  ↓
  fetch('/api/shorten', {
    method: 'POST',
    body: '{"longUrl": "https://..."}'
  })
  ↓
Server (C#):
  ↓
  Receives POST request
  Stores URL
  Returns JSON
  ↓
Frontend (JavaScript):
  ↓
  Display result to user
```

### Copy to Clipboard

```javascript
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
```

**What it does:**
1. When "Copy" button is clicked
2. Gets the short URL from the page
3. Uses `navigator.clipboard.writeText()` to copy to clipboard
4. Changes button text to "✓ Copied!"
5. Changes button color (CSS class)
6. After 2 seconds, changes back

---

## Helper Functions

### GenerateShortCode()

```csharp
string GenerateShortCode()
{
    const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    var random = new Random();
    return new string(Enumerable.Range(0, 6)
        .Select(_ => chars[random.Next(chars.Length)])
        .ToArray());
}
```

**Step by step:**

1. `chars` - String of 62 possible characters (A-Z, a-z, 0-9)

2. `random.Next(chars.Length)` - Generates a random number 0-61

3. `chars[randomNumber]` - Gets a random character

4. `Enumerable.Range(0, 6)` - Creates a sequence: 0, 1, 2, 3, 4, 5

5. `.Select()` - For each number, get a random character

6. `.ToArray()` - Convert to character array

7. `new string()` - Convert array to string

**Example:**
```
Random selections:
  Position 0: chars[42] = 'Q'
  Position 1: chars[15] = 'P'
  Position 2: chars[61] = '9'
  Position 3: chars[8]  = 'I'
  Position 4: chars[33] = 'h'
  Position 5: chars[52] = 'K'

Result: "QP9IhK"
```

### Request Model

```csharp
public class ShortenRequest
{
    public required string LongUrl { get; set; }
}
```

**What this does:**
- Defines the shape of incoming JSON
- When you POST `{ "longUrl": "..." }`, ASP.NET automatically:
  1. Deserializes JSON to this class
  2. Validates it has required properties
  3. Passes it to the endpoint handler

---

## Data Flow - Complete Example

### User shortens a URL:

```
1. USER TYPES URL
   ├─ Types: "https://github.com/dotnet/runtime"
   └─ Clicks: Shorten URL

2. FRONTEND (JavaScript)
   ├─ Gets input value
   ├─ Makes fetch request: POST /api/shorten
   └─ Sends JSON: { "longUrl": "https://github.com/dotnet/runtime" }

3. SERVER (C#)
   ├─ Receives POST request
   ├─ Generates code: "aBcDeF"
   ├─ Stores: urlMappings["aBcDeF"] = "https://github.com/dotnet/runtime"
   ├─ Initializes: requestCounts["aBcDeF"] = 0
   └─ Returns JSON:
      {
        "shortUrl": "http://localhost:5000/r/aBcDeF",
        "totalUrls": 1
      }

4. FRONTEND (JavaScript)
   ├─ Receives response
   ├─ Displays: "http://localhost:5000/r/aBcDeF"
   └─ Shows copy button

5. USER CLICKS LINK
   ├─ Clicks: "http://localhost:5000/r/aBcDeF"
   └─ Browser sends: GET /r/aBcDeF

6. SERVER (C#)
   ├─ Receives: GET /r/aBcDeF
   ├─ Looks up: urlMappings["aBcDeF"]
   ├─ Finds: "https://github.com/dotnet/runtime"
   ├─ Increments: requestCounts["aBcDeF"] = 1
   └─ Responds: HTTP 302 redirect to GitHub

7. BROWSER
   ├─ Sees HTTP 302
   └─ Automatically visits: https://github.com/dotnet/runtime
```

---

## Key Concepts

### Async/Await
```csharp
async (HttpContext context) =>
```
- `async` - Function can do asynchronous work
- `await` - Waits for operations without blocking

### Dependency Injection
```csharp
(HttpContext context, ShortenRequest request)
```
- Parameters are automatically injected by ASP.NET
- `HttpContext` - Request/response information
- `ShortenRequest` - Deserialized from JSON body

### LINQ
```csharp
urlMappings.Select(kvp => new { ... }).ToList()
```
- `Select` - Transform each item
- `kvp` - Key-Value Pair
- Creates new list with transformed items

---

## Summary

The application works in a simple loop:

```
1. Frontend sends HTTP request
         ↓
2. Server processes request (read/write to Dictionary)
         ↓
3. Server sends HTTP response
         ↓
4. Frontend updates UI
         ↓
Back to 1...
```

That's it! No database, no complex logic. Just simple, clean code.

---

**Questions? Check the comments in Program.cs!** 💡
