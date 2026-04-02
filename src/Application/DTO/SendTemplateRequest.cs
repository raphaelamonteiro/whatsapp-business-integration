public record SendTemplateRequest(
    string RecipientPhone,   // E.164: +5511999999999
    string TemplateName,
    List<string> Variables   // substituem {{1}}, {{2}}... no template
);