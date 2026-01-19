using System;
using System.Globalization;
using Newtonsoft.Json;

namespace Product.Api.Serialization;

public sealed class DecimalStringJsonConverter : JsonConverter<decimal>
{
    private const string Format = "F6";

    public override void WriteJson(JsonWriter writer, decimal value, JsonSerializer serializer)
    {
        writer.WriteValue(value.ToString(Format, CultureInfo.InvariantCulture));
    }

    public override decimal ReadJson(
        JsonReader reader,
        Type objectType,
        decimal existingValue,
        bool hasExistingValue,
        JsonSerializer serializer
    )
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return default;
        }

        if (reader.TokenType == JsonToken.String)
        {
            var text = reader.Value?.ToString();
            if (string.IsNullOrWhiteSpace(text))
            {
                return default;
            }

            if (TryParseDecimal(text, out var parsed))
            {
                return parsed;
            }

            throw new JsonSerializationException($"Invalid decimal value: '{text}'.");
        }

        if (reader.TokenType == JsonToken.Float || reader.TokenType == JsonToken.Integer)
        {
            return Convert.ToDecimal(reader.Value, CultureInfo.InvariantCulture);
        }

        throw new JsonSerializationException(
            $"Unexpected token {reader.TokenType} when parsing decimal."
        );
    }

    internal static bool TryParseDecimal(string value, out decimal parsed)
    {
        return decimal.TryParse(
                value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out parsed
            )
            || decimal.TryParse(
                value,
                NumberStyles.Number,
                CultureInfo.GetCultureInfo("pt-BR"),
                out parsed
            );
    }
}

public sealed class NullableDecimalStringJsonConverter : JsonConverter<decimal?>
{
    public override void WriteJson(JsonWriter writer, decimal? value, JsonSerializer serializer)
    {
        if (!value.HasValue)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteValue(value.Value.ToString("F6", CultureInfo.InvariantCulture));
    }

    public override decimal? ReadJson(
        JsonReader reader,
        Type objectType,
        decimal? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer
    )
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonToken.String)
        {
            var text = reader.Value?.ToString();
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            if (DecimalStringJsonConverter.TryParseDecimal(text, out var parsed))
            {
                return parsed;
            }

            throw new JsonSerializationException($"Invalid decimal value: '{text}'.");
        }

        if (reader.TokenType == JsonToken.Float || reader.TokenType == JsonToken.Integer)
        {
            return Convert.ToDecimal(reader.Value, CultureInfo.InvariantCulture);
        }

        throw new JsonSerializationException(
            $"Unexpected token {reader.TokenType} when parsing decimal."
        );
    }
}
