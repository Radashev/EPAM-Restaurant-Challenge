using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Handlers.GuestSessions;
using APIHandler.Models.Dishes;
using APIHandler.Models.GuestSessions;
using APIHandler.Models.Orders;
using APIHandler.Services.GuestSessions;
using Moq;
using System.Text.Json;

namespace APIHandler.Tests.Handlers;

public class GetGroupSessionsHandlerTests
{
    private const string GroupReservationId = "group-123";
    private const string OwnerId = "owner-123";

    private readonly Mock<IGuestSessionService> _guestSessionService = new();
    private readonly Mock<ILambdaContext> _lambdaContext = new();
    private readonly Mock<ILambdaLogger> _logger = new();

    public GetGroupSessionsHandlerTests()
    {
        _lambdaContext.Setup(context => context.Logger).Returns(_logger.Object);
    }

    [Fact]
    public async Task HandleRequest_OwnerReceivesSerializedParticipantSnapshot()
    {
        _guestSessionService
            .Setup(service => service.GetGroupSessionsAsync(OwnerId, GroupReservationId))
            .ReturnsAsync(new GetAllSessionsResponse(
            [
                new Participant(
                    "session1",
                    ParticipantType.Guest,
                    "Andrii",
                    [
                        new OrderItemResponse
                        {
                            Dish = CreateDish("dish1", "Caesar Salad", 180m),
                            Quantity = 2
                        }
                    ],
                    360m,
                    true),
                new Participant(
                    "session2",
                    ParticipantType.Guest,
                    "Olena",
                    [
                        new OrderItemResponse
                        {
                            Dish = CreateDish("dish2", "Tom Yum", 220m),
                            Quantity = 1
                        }
                    ],
                    220m,
                    false)
            ],
            580m));

        var handler = new GetGroupSessionsHandler(_guestSessionService.Object);
        var response = await handler.HandleRequest(
            CreateRequest(GroupReservationId, new Dictionary<string, string> { ["sub"] = OwnerId }),
            _lambdaContext.Object);

        Assert.Equal(200, response.StatusCode);

        using JsonDocument document = JsonDocument.Parse(response.Body);
        JsonElement root = document.RootElement;
        JsonElement participants = root.GetProperty("participants");
        Assert.Equal(2, participants.GetArrayLength());

        JsonElement andrii = participants[0];
        Assert.Equal("session1", andrii.GetProperty("id").GetString());
        Assert.Equal("Guest", andrii.GetProperty("type").GetString());
        Assert.Equal("Andrii", andrii.GetProperty("name").GetString());
        Assert.Equal(360m, andrii.GetProperty("total").GetDecimal());
        Assert.True(andrii.GetProperty("isSubmitted").GetBoolean());
        JsonElement andriiItem = Assert.Single(andrii.GetProperty("items").EnumerateArray());
        JsonElement andriiDish = andriiItem.GetProperty("dish");
        Assert.Equal("dish1", andriiDish.GetProperty("id").GetString());
        Assert.Equal("Caesar Salad", andriiDish.GetProperty("name").GetString());
        Assert.Equal(180m, andriiDish.GetProperty("price").GetDecimal());
        Assert.Equal(2, andriiItem.GetProperty("quantity").GetInt32());

        JsonElement olena = participants[1];
        Assert.Equal("session2", olena.GetProperty("id").GetString());
        Assert.Equal("Guest", olena.GetProperty("type").GetString());
        Assert.Equal("Olena", olena.GetProperty("name").GetString());
        Assert.Equal(220m, olena.GetProperty("total").GetDecimal());
        Assert.False(olena.GetProperty("isSubmitted").GetBoolean());
        JsonElement olenaItem = Assert.Single(olena.GetProperty("items").EnumerateArray());
        JsonElement olenaDish = olenaItem.GetProperty("dish");
        Assert.Equal("dish2", olenaDish.GetProperty("id").GetString());
        Assert.Equal("Tom Yum", olenaDish.GetProperty("name").GetString());
        Assert.Equal(220m, olenaDish.GetProperty("price").GetDecimal());
        Assert.Equal(1, olenaItem.GetProperty("quantity").GetInt32());
        Assert.Equal(580m, root.GetProperty("total").GetDecimal());

        _guestSessionService.Verify(
            service => service.GetGroupSessionsAsync(OwnerId, GroupReservationId),
            Times.Once);
    }

    [Fact]
    public async Task HandleRequest_NonOwnerReceivesForbidden()
    {
        _guestSessionService
            .Setup(service => service.GetGroupSessionsAsync("non-owner", GroupReservationId))
            .ThrowsAsync(new ActionForbiddenException("Only the reservation owner can perform this action."));

        var response = await CreateHandler().HandleRequest(
            CreateRequest(GroupReservationId, new Dictionary<string, string> { ["sub"] = "non-owner" }),
            _lambdaContext.Object);

        Assert.Equal(403, response.StatusCode);
        _guestSessionService.Verify(
            service => service.GetGroupSessionsAsync("non-owner", GroupReservationId),
            Times.Once);
    }

    [Fact]
    public async Task HandleRequest_MissingSubjectReceivesUnauthorized()
    {
        var response = await CreateHandler().HandleRequest(
            CreateRequest(GroupReservationId, new Dictionary<string, string>()),
            _lambdaContext.Object);

        Assert.Equal(401, response.StatusCode);
        _guestSessionService.Verify(
            service => service.GetGroupSessionsAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleRequest_BlankSubjectReceivesUnauthorized(string subject)
    {
        var response = await CreateHandler().HandleRequest(
            CreateRequest(GroupReservationId, new Dictionary<string, string> { ["sub"] = subject }),
            _lambdaContext.Object);

        Assert.Equal(401, response.StatusCode);
        _guestSessionService.Verify(
            service => service.GetGroupSessionsAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleRequest_MissingGroupIdReceivesBadRequest()
    {
        var response = await CreateHandler().HandleRequest(
            CreateRequest(null, new Dictionary<string, string> { ["sub"] = OwnerId }),
            _lambdaContext.Object);

        Assert.Equal(400, response.StatusCode);
        _guestSessionService.Verify(
            service => service.GetGroupSessionsAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleRequest_BlankGroupIdReceivesBadRequest(string groupReservationId)
    {
        var response = await CreateHandler().HandleRequest(
            CreateRequest(groupReservationId, new Dictionary<string, string> { ["sub"] = OwnerId }),
            _lambdaContext.Object);

        Assert.Equal(400, response.StatusCode);
        _guestSessionService.Verify(
            service => service.GetGroupSessionsAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleRequest_NonexistentGroupReceivesNotFound()
    {
        _guestSessionService
            .Setup(service => service.GetGroupSessionsAsync(OwnerId, GroupReservationId))
            .ThrowsAsync(new GroupReservationNotFoundException());

        var response = await CreateHandler().HandleRequest(
            CreateRequest(GroupReservationId, new Dictionary<string, string> { ["sub"] = OwnerId }),
            _lambdaContext.Object);

        Assert.Equal(404, response.StatusCode);
    }

    private GetGroupSessionsHandler CreateHandler() =>
        new(_guestSessionService.Object);

    private static APIGatewayProxyRequest CreateRequest(
        string? groupReservationId,
        IDictionary<string, string> claims)
    {
        var pathParameters = new Dictionary<string, string>();
        if (groupReservationId is not null)
            pathParameters["groupReservationId"] = groupReservationId;

        return new APIGatewayProxyRequest
        {
            PathParameters = pathParameters,
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayCustomAuthorizerContext
                {
                    Claims = new Dictionary<string, string>(claims)
                }
            }
        };
    }

    private static DishResponse CreateDish(string id, string name, decimal price) =>
        new()
        {
            Id = id,
            Name = name,
            Description = string.Empty,
            DishType = string.Empty,
            Price = price,
            Weight = string.Empty,
            Calories = string.Empty,
            Proteins = string.Empty,
            Fats = string.Empty,
            Carbohydrates = string.Empty,
            Vitamins = string.Empty,
            ImageUrl = null,
            State = string.Empty
        };
}
