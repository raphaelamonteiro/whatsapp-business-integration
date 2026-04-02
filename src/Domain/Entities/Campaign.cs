public class Campaign
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;       // "Promoção Pizza Sexta"
    public string TemplateName { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public CampaignStatus Status { get; set; }

    // Custo estimado antes do disparo
    public int RecipientCount { get; set; }
    public decimal EstimatedCostBrl { get; set; }          // RecipientCount × 0.40
    public decimal? ActualCostBrl { get; set; }            // preenchido após disparo

    public ICollection<MessageLog> Messages { get; set; } = [];
}

public class MessageLog
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CampaignId { get; set; }
    public string WhatsAppMessageId { get; set; } = string.Empty; // wamid retornado pela Meta
    public string RecipientPhone { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public MessageStatus Status { get; set; }
    public DateTime SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? ReadAt { get; set; }

    // Rastreamento de conversão:
    // "o cliente pediu após ver a mensagem?"
    public bool Converted { get; set; }
    public DateTime? OrderPlacedAt { get; set; }  // preenchido quando um pedido entra
    public Guid? OrderId { get; set; }            // FK para o módulo de pedidos
}