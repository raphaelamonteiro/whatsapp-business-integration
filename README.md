# WhatsApp Business Integration (.NET / C#)

This repository is a **mockup** built during my internship to explore automated messaging, notifications, and communication workflows using the WhatsApp Business API, implemented in **C# and .NET**.

## Features
* Send and receive messages via WhatsApp Business API
* Automate notifications and alerts
* Experiment with message templates
* Handle webhooks for real-time events

## Implementation
* **Language:** C#
* **Framework:** .NET 10 (or latest LTS)
* **Project Type:** Console app / Class library (demonstrates API integration)
* **HTTP Requests:** Using `HttpClient` to communicate with WhatsApp Business API
* **Serialization:** JSON via `System.Text.Json`
* **Webhook Handling:** Minimal API or ASP.NET Core Web API

## Getting Started

1. Clone the repository:

   ```bash
   git clone https://github.com/raphaelamonteiro/whatsapp-business-integration.git
   ```
2. Open in Visual Studio or VS Code.
3. Configure your WhatsApp Business API credentials in `appsettings.json`.
4. Run the console app / API:

   ```bash
   dotnet run
   ```
5. Test sending and receiving messages.

## Purpose
This project is a **mockup** for internship tasks, showcasing integration of external systems with WhatsApp Business using C# and .NET. It’s meant for demonstration and learning purposes only.


💡 **Tips for C#/.NET Implementation:**
* Use `HttpClientFactory` for proper HTTP client management.
* Create a `WhatsAppService` class to encapsulate API calls (send messages, handle templates, receive webhooks).
* If you want to demo webhooks, a small **ASP.NET Core Minimal API** is easy to set up.
* Use `.env` or `appsettings.json` for credentials so you don’t hardcode tokens.

---
