using Grpc.Net.Client;
using OpenMatch;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System;
using grpc = global::Grpc.Core;
using static OpenMatch.MatchFunction;
using static OpenMatch.QueryService;

namespace MatchFunction.Services;

public struct PoolResult
{
    public string Error { get; set; }
    public List<Ticket> Tickets { get; set; }
    public string Name { get; set; }
}

public class MatchFunctionService : MatchFunctionBase
{
    private const string QueryUrl = "http://open-match-query.open-match.svc.cluster.local:50503";
    private const string MatchFunctionName = "multi";
    private const int MatchingCount = 2;
    private const int ApiTimeout = 5;
    private readonly ILogger<MatchFunctionService> logger;
    public MatchFunctionService(ILogger<MatchFunctionService> logger)
    {
        this.logger = logger;
    }

    public async override Task Run(RunRequest request, grpc::IServerStreamWriter<RunResponse> responseStream, grpc::ServerCallContext context)
    {
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        var poolMap = await GetPoolMapAsync(request);

        if (0 == poolMap.Count) {
            logger.LogTrace("No pools detected");
            return;
        }

        var matches = MakeMatches(request, poolMap);
        foreach (var match in matches) {
            logger.LogWarning($"Writing match {match.MatchId}");
            await responseStream.WriteAsync(new RunResponse { Proposal = match });
        }
    }

    private async Task<Dictionary<string, PoolResult>> GetPoolMapAsync(RunRequest request)
    {
        using var channel = GrpcChannel.ForAddress(QueryUrl);
        var client = new QueryServiceClient(channel);
        var poolMap = new Dictionary<string, PoolResult>();

        foreach (var pool in request.Profile.Pools)
        {
            var queryTicketRequest = new QueryTicketsRequest() { Pool = pool };
            using var stream = client.QueryTickets(queryTicketRequest);
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(ApiTimeout));
            var poolTickets = new List<Ticket>();

            while (await stream.ResponseStream.MoveNext(cts.Token))
            {
                poolTickets.AddRange(stream.ResponseStream.Current.Tickets);
            }

            if (0 == poolTickets.Count) {
                continue;
            }

            var result = new PoolResult();
            result.Name = pool.Name;
            result.Tickets = poolTickets.ToList();
            logger.LogInformation($"Pool {pool.Name} with tickets count {result.Tickets.Count}");

            poolMap.Add(pool.Name, result);
        }

        return poolMap;
    }

    private List<Match> MakeMatches(RunRequest request, Dictionary<string, PoolResult> poolMap)
    {
        var matches = new List<Match>();
        int matchIndex = 1;
        var time = DateTime.UtcNow.ToString("yyyy-MM-dd hh:mm:ss");
        while (true) {
            var insufficientTickets = false;
            var matchTickets = new List<Ticket>();

            foreach (var key in poolMap.Keys) {
                var pool = poolMap[key];
                var tickets = pool.Tickets.ToList();
                if (tickets.Count < MatchingCount) {
                    insufficientTickets = true;
                    break;
                }

                for (var i = 0; i < MatchingCount; i++) {
                    var ticket = tickets[i];
                    matchTickets.Add(ticket);
                    pool.Tickets.Remove(ticket);
                }
            }

            if (insufficientTickets) {
                break;
            }

            var match = new Match();
            var matchProfile = request.Profile.Name;
            match.MatchId = $"profile-{matchProfile}-{time}-{matchIndex}";
            match.MatchProfile = matchProfile;
            match.MatchFunction = MatchFunctionName;
            match.Tickets.AddRange(matchTickets);
            matches.Add(match);
            matchIndex++;
        }

        return matches;
    }
}
