using weave2trial;
using Xunit;

namespace Tests.Protocols;

public class RequestSessionProtocolTests
{
    public RequestSessionProtocolTests() => Router.Registry.Clear();

    [Fact(DisplayName = "RequestSessionProtocol - Simple")]
    public void TestRequestSessionProtocol() {
        var authority = new Node("Authority");
        var initiator = new Node("Initiator");

        var rsp = RequestSessionProtocol.CreateInstance(initiator, new ProtocolInstanceIdentity(), initiator.NodeId, null);
        var state = new RequestSessionProtocol.InitiatorState(rsp, authority.NodeId);
        initiator.ActivateProtocolWithState(state);

        for (var i = 0; i < 10000; i++) { // MUST finish sooner than that
            Log.Info(" ");
            initiator.Tick();
            authority.Tick();
            if (rsp is { State: SuccessState<RequestSessionProtocol.Result> })
                break;
        }

        Assert.True(rsp is { State: SuccessState<RequestSessionProtocol.Result> });
    }
}
