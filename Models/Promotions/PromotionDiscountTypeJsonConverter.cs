using foxtrot_store_backend.Data.Entities;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace foxtrot_store_backend.Models.Promotions;

public sealed class PromotionDiscountTypeJsonConverter
    : JsonConverter<PromotionDiscountType?>
{
    public override PromotionDiscountType? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException(
                "DiscountType must be a string.");
        }

        var value = reader.GetString();

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException(
                "DiscountType cannot be empty.");
        }

        foreach (var name in Enum.GetNames<PromotionDiscountType>())
        {
            if (string.Equals(
                    name,
                    value,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Enum.Parse<PromotionDiscountType>(name);
            }
        }

        throw new JsonException(
            $"Invalid discount type: {value}.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        PromotionDiscountType? value,
        JsonSerializerOptions options)
    {
        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }

        if (!Enum.IsDefined(value.Value))
        {
            throw new JsonException(
                "Invalid discount type value.");
        }

        writer.WriteStringValue(value.Value.ToString());
    }
}