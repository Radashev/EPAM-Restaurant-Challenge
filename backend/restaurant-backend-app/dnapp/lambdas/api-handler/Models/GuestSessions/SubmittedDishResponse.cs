using System.Text.Json.Serialization;

namespace APIHandler.Models.GuestSessions;

public sealed class SubmittedDishResponse
{
    [JsonPropertyName("dishId")]
    public required string DishId { get; init; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; init; }
}
