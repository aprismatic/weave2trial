using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using Aprismatic;
using weave2trial;
using Xunit;

namespace Tests.Protocols
{
    public class SSSProtocolTests
    {
        private static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();
        private static readonly Random Rnd = new();

        [Fact(DisplayName = "Shamir SS Protocol - Simple")]
        public void ShamirSecretSharingSimpleTest() {
            
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
                if (sssp is { State: SuccessState<ShamirSecretSharingProtocol.Result> ss })
                    break;
            }

            var accum = new List<ShamirShard>();
            foreach (var node in nodes.OrderBy(x => Globals.RND.Next()).Take(Globals.THRESHOLD)) {
                if (node.ActiveProtocols[sssp.ProtocolInstanceId].State is SuccessState<ShamirSecretSharingProtocol.Result> ssbi) {
                    accum.Add(ssbi.Result.MyShare);
                    Log.Info($"{node}'s secret share is: {ssbi.Result.MyShare}");
                }
                else {
                    Log.Error($"{node}'s state is {node.ActiveProtocols[sssp.ProtocolInstanceId].State}");
                }
            }

            Log.Info($"Recovered secret is {ShamirSecretSharing.RecoverSecret(accum)}");

            Assert.Equal(sssp, nodes[0].ActiveProtocols[sssp.ProtocolInstanceId]);
            Assert.Equal(secret, ShamirSecretSharing.RecoverSecret(accum));
        }
    }
}
