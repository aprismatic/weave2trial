using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Xml;
using Aprismatic;
using weave2trial;
using Xunit;

namespace Tests.Protocols
{
    public class Poly2AddProtocolTests
    {
        private static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();
        private static readonly Random Rnd = new();

        public Poly2AddProtocolTests() => Router.Registry.Clear();

        [Fact(DisplayName = "Poly2Add SS Protocol - Simple")]
        public void LinearSecretSharingSimpleTest() {
            var secret = new BigInteger(987654);

            var nodes = new List<Node>(Globals.TOTAL_NODES);
            for (var i = 0; i < Globals.TOTAL_NODES; i++)
                nodes.Add(new Node());

            var sssp = ShamirSecretSharingProtocol.CreateInstance(nodes[0], new ProtocolInstanceIdentity(), nodes[0].NodeId, null);
            var state = new ShamirSecretSharingProtocol.InitiatorState(sssp, nodes.Select(x => x.NodeId), Globals.THRESHOLD, secret);
            nodes[0].ActivateProtocolWithState(state);

            while (true) {
                Log.Info(" ");
                foreach (var node in nodes)
                    node.Tick();
                if (sssp is { State: SuccessState<ShamirSecretSharingProtocol.Result> })
                    break;
            }

            Log.Info(" ");
            Log.Info($"Trying to recover the secret with {Globals.THRESHOLD} random nodes.");
            var accum = new List<ShamirShard>();
            foreach (var node in nodes.OrderBy(x => Random.Shared.Next()).Take(Globals.THRESHOLD)) {
                if (node.ActiveProtocols[sssp.ProtocolInstanceId].State is SuccessState<ShamirSecretSharingProtocol.Result> ssbi) {
                    accum.Add(ssbi.Result.MyShare);
                    Log.Info($"{node} secret share is: {ssbi.Result.MyShare}");
                }
                else {
                    Log.Error($"{node} state is {node.ActiveProtocols[sssp.ProtocolInstanceId].State}");
                }
            }

            var recoveredSecret = ShamirSecretSharing.RecoverSecret(accum);
            Log.Info($"Recovered secret is {recoveredSecret}");
            Assert.Equal(secret, recoveredSecret);

            // -----
            Log.Info(" ");
            Log.Info("== 8< == Starting Poly2Additive protocol == 8< ==");
            Log.Info(" ");

            var group = nodes.Skip(1).OrderBy(x => Random.Shared.Next()).Take(Globals.THRESHOLD - 1).Concat(new[] { nodes[0] }).ToList();

            var p2ap = Poly2AdditiveProtocol.CreateInstance(nodes[0], new ProtocolInstanceIdentity(), nodes[0].NodeId, null);
            var state2 = new Poly2AdditiveProtocol.InitiatorState(p2ap, sssp.UniqueProtocolId, group.Select(x => x.NodeId));
            nodes[0].ActivateProtocolWithState(state2);

            var delay = 5;
            while (delay > 0) {
                Log.Info(" ");
                foreach (var node in nodes)
                    node.Tick();
                if (nodes[0].ActiveProtocols[p2ap.ProtocolInstanceId] is
                    { State: SuccessState<LinearSecretSharingProtocol.Result> })
                    delay--;
            }

            var accum2 = new List<LinearShard>();
            foreach (var node in group) {
                if (node.ActiveProtocols[p2ap.ProtocolInstanceId].State is SuccessState<LinearSecretSharingProtocol.Result> p2apss) {
                    var curShare = p2apss.Result.MyShare;
                    Assert.Equal(curShare.S, new BigFraction(curShare.S.ToBigInteger())); // make sure shares are integer
                    accum2.Add(curShare);
                    Log.Info($"{node}'s secret share is: {p2apss.Result.MyShare}");
                }
                else {
                    Log.Error($"{node}'s state is {node.ActiveProtocols[p2ap.ProtocolInstanceId].State}");
                }
            }

            var rec2 = LinearSecretSharing.RecoverSecret(accum2);
            Log.Info($"Recovered secret is {rec2}");

            Assert.Equal(secret, rec2);
        }
    }
}
