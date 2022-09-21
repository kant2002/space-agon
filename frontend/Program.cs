using Google.Protobuf;
using Grpc.Core;
using OpenMatch;
using System.Net.WebSockets;
using static OpenMatch.FrontendService;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseWebSockets();
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

app.MapGet("/", () => "CCO Frontend!");
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/matchmake/" || context.Request.Path == "/matchmake")
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            await AssignmentsOnConnection(webSocket);
        }
        else
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }
    else
    {
        await next(context);
    }
});

app.Run();

static async Task AssignmentsOnConnection(WebSocket webSocket)
{
    const string FrontendUrl = "http://open-match-frontend.open-match.svc.cluster.local:50504";

    using var channel = Grpc.Net.Client.GrpcChannel.ForAddress(FrontendUrl);
    var client = new FrontendServiceClient(channel);

    var searchFields = new SearchFields();
    // searchFields.Tags.Add(request.GameMode.ToString());
    var ticket = new Ticket();
    ticket.SearchFields = searchFields;

    CreateTicketRequest createTicketRequest = new CreateTicketRequest();
    createTicketRequest.Ticket = ticket;

    var response = await client.CreateTicketAsync(createTicketRequest);
    var ticketId = response.Id;
    
    Console.WriteLine($"Created ticket: {ticketId}");

    try
    {
        var buffer = new byte[1024 * 4];

        var watchedAssignments = client.WatchAssignments(new WatchAssignmentsRequest() { TicketId = ticketId });
        await foreach (var message in watchedAssignments.ResponseStream.ReadAllAsync())
        {
            Console.WriteLine($"Allocated assignment: {message.Assignment}");
            using (var ms = new CodedOutputStream(buffer))
            {
                // Assignment
                message.Assignment.WriteTo(ms);
                await webSocket.SendAsync(
                    new ArraySegment<byte>(buffer, 0, (int)ms.Position),
                    WebSocketMessageType.Binary,
                    endOfMessage: true,
                    CancellationToken.None);
            }
        }
    }
    finally
    {
        // Maybe we don't have to relete ticket which is assigned.
        // await client.DeleteTicketAsync(new DeleteTicketRequest() {  TicketId = ticketId });
    }
}
