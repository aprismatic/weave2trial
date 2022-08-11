using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using weave2trial;
using Xunit;

namespace Tests.Protocols;

public class Add2PolyProtocolTests
{
    private static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();
    private static readonly Random Rnd = new();

    public Add2PolyProtocolTests() => Router.Registry.Clear();

    [Fact(DisplayName = "Additive2Poly - Simple")]
    public void test() {
        var secret = new BigInteger(987654);

        var nodes = new List<Node>(Globals.TOTAL_NODES);
        nodes.Add(new Node("Initiator"));
        for (var i = 1; i < Globals.TOTAL_NODES; i++)
            nodes.Add(new Node());

        var lssp = LinearSecretSharingProtocol.CreateInstance(nodes[0], new ProtocolInstanceIdentity(), nodes[0].NodeId, null);
        var state = new LinearSecretSharingProtocol.InitiatorState(lssp, nodes.Select(x => x.NodeId), secret);
        nodes[0].ActivateProtocolWithState(state);

        while (true) {
            Log.Info(" ");
            foreach (var node in nodes)
                node.Tick();
            if (lssp is { State: SuccessState<LinearSecretSharingProtocol.Result> })
                break;
        }

        var accum = new List<LinearShard>();
        foreach (var node in nodes.OrderBy(x => Random.Shared.Next())) {
            if (node.ActiveProtocols[lssp.ProtocolInstanceId].State is SuccessState<LinearSecretSharingProtocol.Result> ssbi) {
                accum.Add(ssbi.Result.MyShare);
                Log.Info($"{node}'s secret share is: {ssbi.Result.MyShare}");
            }
            else {
                Log.Error($"{node}'s state is {node.ActiveProtocols[lssp.ProtocolInstanceId].State}");
            }
        }

        Log.Info($"Recovered secret is {LinearSecretSharing.RecoverSecret(accum)}");
        Assert.Equal(secret, LinearSecretSharing.RecoverSecret(accum));

        // -----
        Log.Info(" ");
        Log.Info("== 8< =====================================");
        Log.Info(" ");

        var a2pp = Additive2PolyProtocol.CreateInstance(nodes[0], new ProtocolInstanceIdentity(), nodes[0].NodeId, null);
        var state2 = new Additive2PolyProtocol.InitiatorState(a2pp, lssp.UniqueProtocolId, Globals.THRESHOLD);
        nodes[0].ActivateProtocolWithState(state2);

        var delay = 5;
        while (delay > 0) {
            Log.Info(" ");
            foreach (var node in nodes)
                node.Tick();
            if (nodes[0].ActiveProtocols[a2pp.ProtocolInstanceId] is { State: SuccessState<ShamirSecretSharingProtocol.Result> })
                delay--;
        }

        var accum2 = new List<ShamirShard>();
        foreach (var node in nodes) {
            if (node.ActiveProtocols[a2pp.ProtocolInstanceId].State is SuccessState<ShamirSecretSharingProtocol.Result> a2ppss) {
                accum2.Add(a2ppss.Result.MyShare);
                Log.Info($"{node}'s secret share is: {a2ppss.Result.MyShare}");
            }
            else {
                Log.Error($"{node}'s state is {node.ActiveProtocols[a2pp.ProtocolInstanceId].State}");
            }
        }

        var rec2 = ShamirSecretSharing.RecoverSecret(accum2);
        Log.Info($"Recovered secret is {rec2}");

        Assert.Equal(secret, rec2);
    }
}
