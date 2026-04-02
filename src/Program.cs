using System.Net.Http;

var httpClient = new HttpClient();
var service = new WhatsAppService(
    httpClient,
    "YOUR_PHONE_NUMBER_ID",
    "YOUR_ACCESS_TOKEN",
    "https://graph.facebook.com/v17.0"
);

await service.SendMessageAsync("RECIPIENT_PHONE_NUMBER", "Hello from my internship project!");
Console.WriteLine("Message sent!");